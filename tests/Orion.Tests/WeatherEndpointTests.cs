using System.Net;
using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orion.Api.Models;
using Orion.Api.Services;
using Orion.Tests.Helpers;

namespace Orion.Tests;

public class WeatherEndpointTests : IClassFixture<OrionWebAppFactory>
{
    private readonly OrionWebAppFactory _factory;

    public WeatherEndpointTests(OrionWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWeather_HappyPath_Returns200WithBothProvidersAndLowercaseEnums()
    {
        var meteo = StubProvider(Provider.OpenMeteo,
            WeatherResponses.Metric(temperature: 10, providers: new[] { Provider.OpenMeteo }));
        var openWeather = StubProvider(Provider.OpenWeather,
            WeatherResponses.Metric(temperature: 20, providers: new[] { Provider.OpenWeather }));
        var client = _factory.WithProviders(meteo, openWeather).CreateClient();

        var response = await client.GetAsync("/weather?lat=47.6&lon=-122.3");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openmeteo\"", body);
        Assert.Contains("\"openweather\"", body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal(15, root.GetProperty("temperature").GetDouble());
        Assert.Equal("metric", root.GetProperty("units").GetString());

        var providers = root.GetProperty("providers").EnumerateArray()
            .Select(e => e.GetString())
            .ToArray();
        Assert.Equal(2, providers.Length);
        Assert.Contains("openmeteo", providers);
        Assert.Contains("openweather", providers);
    }

    [Fact]
    public async Task GetWeather_AllProvidersReturnNull_Returns502()
    {
        var meteo = StubProvider(Provider.OpenMeteo, response: null);
        var openWeather = StubProvider(Provider.OpenWeather, response: null);
        var client = _factory.WithProviders(meteo, openWeather).CreateClient();

        var response = await client.GetAsync("/weather?lat=47.6&lon=-122.3");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWeather_ExceedingRateLimit_Returns429()
    {
        var meteo = StubProvider(Provider.OpenMeteo,
            WeatherResponses.Metric(temperature: 10, providers: new[] { Provider.OpenMeteo }));
        var client = _factory.WithProviders(meteo).CreateClient();

        var first = await client.GetAsync("/weather?lat=47.6&lon=-122.3");
        var second = await client.GetAsync("/weather?lat=47.6&lon=-122.3");
        var third = await client.GetAsync("/weather?lat=47.6&lon=-122.3");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task GetHealth_NotAffectedByWeatherRateLimit()
    {
        var meteo = StubProvider(Provider.OpenMeteo,
            WeatherResponses.Metric(temperature: 10, providers: new[] { Provider.OpenMeteo }));
        var client = _factory.WithProviders(meteo).CreateClient();

        // Exhaust /weather's per-IP limit.
        await client.GetAsync("/weather?lat=47.6&lon=-122.3");
        await client.GetAsync("/weather?lat=47.6&lon=-122.3");
        var limited = await client.GetAsync("/weather?lat=47.6&lon=-122.3");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        // /health must remain reachable so Container Apps probes don't fail.
        for (int i = 0; i < 3; i++)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(0.0, -181.0)]
    public async Task GetWeather_OutOfRangeCoords_Returns400(double lat, double lon)
    {
        var client = _factory.WithProviders().CreateClient();

        var response = await client.GetAsync(FormattableString.Invariant($"/weather?lat={lat}&lon={lon}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("NaN", "0")]
    [InlineData("Infinity", "0")]
    [InlineData("-Infinity", "0")]
    [InlineData("0", "NaN")]
    [InlineData("0", "Infinity")]
    public async Task GetWeather_NonFiniteCoords_Returns400(string lat, string lon)
    {
        var client = _factory.WithProviders().CreateClient();

        var response = await client.GetAsync($"/weather?lat={lat}&lon={lon}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWeather_AllowedOrigin_EchoesAccessControlAllowOriginHeader()
    {
        var meteo = StubProvider(Provider.OpenMeteo,
            WeatherResponses.Metric(temperature: 10, providers: new[] { Provider.OpenMeteo }));
        var client = _factory.WithProviders(meteo).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/weather?lat=47.6&lon=-122.3");
        request.Headers.Add("Origin", "http://localhost:5173");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task GetWeather_DisallowedOrigin_OmitsAccessControlAllowOriginHeader()
    {
        var meteo = StubProvider(Provider.OpenMeteo,
            WeatherResponses.Metric(temperature: 10, providers: new[] { Provider.OpenMeteo }));
        var client = _factory.WithProviders(meteo).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/weather?lat=47.6&lon=-122.3");
        request.Headers.Add("Origin", "https://evil.example.com");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task GetWeather_ProviderThrows_Returns500ProblemDetailsWithoutLeakingException()
    {
        var meteo = StubProvider(Provider.OpenMeteo,
            WeatherResponses.Metric(temperature: 10, providers: new[] { Provider.OpenMeteo }));
        var throwing = Substitute.For<IWeatherProvider>();
        throwing.Name.Returns(Provider.OpenWeather);
        throwing
            .GetCurrentWeatherAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("kaboom-secret-detail"));
        var client = _factory.WithProviders(meteo, throwing).CreateClient();

        var response = await client.GetAsync("/weather?lat=47.6&lon=-122.3");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal(500, doc.RootElement.GetProperty("status").GetInt32());

        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("kaboom-secret-detail", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    private static IWeatherProvider StubProvider(Provider name, WeatherResponse? response)
    {
        var provider = Substitute.For<IWeatherProvider>();
        provider.Name.Returns(name);
        provider.GetCurrentWeatherAsync(
            Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(response);
        return provider;
    }
}
