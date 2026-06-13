using System.Net;
using System.Text.Json;
using NSubstitute;
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
