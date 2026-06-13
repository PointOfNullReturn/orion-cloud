using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Orion.Api.Configuration;
using Orion.Api.Models;
using Orion.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.Configure<WeatherOptions>(
    builder.Configuration.GetSection(WeatherOptions.SectionName));

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

builder.Services.AddTransient<IWeatherProvider>(sp => sp.GetRequiredService<OpenMeteoProvider>());
builder.Services.AddTransient<IWeatherProvider>(sp => sp.GetRequiredService<OpenWeatherProvider>());

builder.Services.AddScoped<IWeatherAggregatorService, WeatherAggregatorService>();

var app = builder.Build();

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
});

app.Run();

public partial class Program;
