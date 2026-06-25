using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using NetEscapades.AspNetCore.SecurityHeaders;
using Orion.Api.Configuration;
using Orion.Api.Models;
using Orion.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Drop the "Server: Kestrel" banner — Kestrel writes it after the middleware
// pipeline, so the security-headers RemoveServerHeader() can't reach it; the
// only reliable way to suppress it is at the Kestrel level.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

builder.Services.Configure<WeatherOptions>(
    builder.Configuration.GetSection(WeatherOptions.SectionName));

builder.Services.Configure<GeocodingOptions>(
    builder.Configuration.GetSection(GeocodingOptions.SectionName));

builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection(CorsOptions.SectionName));

builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<IOptions<CorsOptions>>((aspCors, myCors) =>
    {
        var policy = new Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder()
            .WithOrigins(myCors.Value.AllowedOrigins)
            .WithMethods("GET")
            .SetPreflightMaxAge(TimeSpan.FromHours(1))
            .Build();
        aspCors.AddPolicy("OrionFrontend", policy);
    });

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Container Apps ingress is the only path to the container, so trust X-Forwarded-* from any upstream.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("weather-per-ip", httpContext =>
    {
        var rl = httpContext.RequestServices
            .GetRequiredService<IOptions<WeatherOptions>>().Value.RateLimit;
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = rl.PermitLimit,
            Window = TimeSpan.FromSeconds(rl.WindowSeconds),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(LowerCaseNamingPolicy.Instance));
});

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromSeconds(300)
    };
});

builder.Services.AddTransient<OpenWeatherApiKeyHandler>();

builder.Services.AddHttpClient<OpenMeteoProvider>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<WeatherOptions>>().Value;
    client.BaseAddress = new Uri(opts.OpenMeteo.BaseUrl);
});

builder.Services.AddHttpClient<OpenWeatherProvider>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<WeatherOptions>>().Value;
    client.BaseAddress = new Uri(opts.OpenWeather.BaseUrl);
})
.AddHttpMessageHandler<OpenWeatherApiKeyHandler>()
.RemoveAllLoggers();

builder.Services.AddHttpClient<IGeocodingService, GeocodingService>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<GeocodingOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
});

builder.Services.AddTransient<IWeatherProvider>(sp => sp.GetRequiredService<OpenMeteoProvider>());
builder.Services.AddTransient<IWeatherProvider>(sp => sp.GetRequiredService<OpenWeatherProvider>());

builder.Services.AddScoped<IWeatherAggregatorService, WeatherAggregatorService>();

var app = builder.Build();

// First in the pipeline so every response — including errors, 429s, and CORS
// preflights — carries the headers. CSP is locked all the way down because this
// is a JSON API that serves no markup; HSTS only emits over HTTPS.
var securityHeaders = new HeaderPolicyCollection()
    .AddFrameOptionsDeny()
    .AddContentTypeOptionsNoSniff()
    .AddStrictTransportSecurityMaxAgeIncludeSubDomains(maxAgeInSeconds: 60 * 60 * 24 * 365)
    .AddReferrerPolicyNoReferrer()
    .AddXssProtectionDisabled()
    .AddContentSecurityPolicy(csp =>
    {
        csp.AddDefaultSrc().None();
        csp.AddFrameAncestors().None();
    });

app.UseSecurityHeaders(securityHeaders);
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("OrionFrontend");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

app.MapGet("/weather", async (
    double lat,
    double lon,
    string? units,
    string? provider,
    IWeatherAggregatorService aggregator,
    CancellationToken cancellationToken) =>
{
    if (!double.IsFinite(lat) || lat < -90 || lat > 90)
    {
        return Results.Problem(
            title: "Invalid query parameter",
            detail: $"Invalid lat value: '{lat}'. Expected a number between -90 and 90.",
            statusCode: 400);
    }

    if (!double.IsFinite(lon) || lon < -180 || lon > 180)
    {
        return Results.Problem(
            title: "Invalid query parameter",
            detail: $"Invalid lon value: '{lon}'. Expected a number between -180 and 180.",
            statusCode: 400);
    }

    var unitsValue = Units.Metric;
    if (!string.IsNullOrEmpty(units) &&
        !Enum.TryParse(units, ignoreCase: true, out unitsValue))
    {
        return Results.Problem(
            title: "Invalid query parameter",
            detail: $"Invalid units value: '{units}'. Expected one of: metric, imperial.",
            statusCode: 400);
    }

    Provider? providerValue = null;
    if (!string.IsNullOrEmpty(provider))
    {
        if (!Enum.TryParse<Provider>(provider, ignoreCase: true, out var parsed))
        {
            return Results.Problem(
                title: "Invalid query parameter",
                detail: $"Invalid provider value: '{provider}'. Expected one of: openmeteo, openweather.",
                statusCode: 400);
        }
        providerValue = parsed;
    }

    var response = await aggregator.GetCurrentWeatherAsync(lat, lon, providerValue, cancellationToken);
    if (response is null)
    {
        return Results.Problem(
            title: "Upstream weather providers unavailable",
            statusCode: 502);
    }

    return Results.Ok(unitsValue == Units.Imperial ? UnitConverter.ToImperial(response) : response);
}).RequireRateLimiting("weather-per-ip");

app.MapGet("/geocode", async (
    string? q,
    int? count,
    IGeocodingService geocoding,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.Problem(
            title: "Invalid query parameter",
            detail: "Query parameter 'q' is required.",
            statusCode: 400);
    }

    var resultCount = Math.Clamp(count ?? 5, 1, 5);
    var results = await geocoding.SearchAsync(q, resultCount, cancellationToken);
    return Results.Ok(results);
}).RequireRateLimiting("weather-per-ip");

app.Run();

public partial class Program;
