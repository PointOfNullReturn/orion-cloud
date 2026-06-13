using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Orion.Api.Configuration;
using Orion.Api.Models;
using Orion.Api.Services;
using Orion.Tests.Helpers;

namespace Orion.Tests;

public class WeatherAggregatorServiceTests
{
    private const double Lat = 47.6;
    private const double Lon = -122.3;

    [Fact]
    public async Task SingleProviderMode_CallsOnlyTheNamedProvider()
    {
        var meteo = StubProvider(Provider.OpenMeteo, WeatherResponses.Metric(temperature: 10));
        var openWeather = StubProvider(Provider.OpenWeather, WeatherResponses.Metric(temperature: 30));
        var aggregator = CreateAggregator(new[] { meteo, openWeather });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, Provider.OpenMeteo, default);

        Assert.NotNull(result);
        Assert.Equal(10, result.Temperature);
        await meteo.Received(1).GetCurrentWeatherAsync(Lat, Lon, Arg.Any<CancellationToken>());
        await openWeather.DidNotReceive().GetCurrentWeatherAsync(
            Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SingleProviderMode_UnknownProviderName_ReturnsNull()
    {
        var meteo = StubProvider(Provider.OpenMeteo, WeatherResponses.Metric());
        var aggregator = CreateAggregator(new[] { meteo });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, Provider.OpenWeather, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task SingleProviderMode_EchoesRequestLatLon()
    {
        var meteo = StubProvider(Provider.OpenMeteo,
            WeatherResponses.Metric(latitude: 0, longitude: 0));
        var aggregator = CreateAggregator(new[] { meteo });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, Provider.OpenMeteo, default);

        Assert.NotNull(result);
        Assert.Equal(Lat, result.Latitude);
        Assert.Equal(Lon, result.Longitude);
    }

    [Fact]
    public async Task AggregateMode_CallsAllProviders()
    {
        var meteo = StubProvider(Provider.OpenMeteo, WeatherResponses.Metric());
        var openWeather = StubProvider(Provider.OpenWeather, WeatherResponses.Metric());
        var aggregator = CreateAggregator(new[] { meteo, openWeather });

        await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        await meteo.Received(1).GetCurrentWeatherAsync(Lat, Lon, Arg.Any<CancellationToken>());
        await openWeather.Received(1).GetCurrentWeatherAsync(Lat, Lon, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AggregateMode_WhenOneProviderFails_ReturnsTheOtherResponse()
    {
        var failed = StubProvider(Provider.OpenMeteo, response: null);
        var success = StubProvider(Provider.OpenWeather,
            WeatherResponses.Metric(temperature: 25, providers: new[] { Provider.OpenWeather }));
        var aggregator = CreateAggregator(new[] { failed, success });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        Assert.NotNull(result);
        Assert.Equal(25, result.Temperature);
        Assert.Single(result.Providers);
        Assert.Equal(Provider.OpenWeather, result.Providers[0]);
    }

    [Fact]
    public async Task AggregateMode_WhenAllProvidersFail_ReturnsNull()
    {
        var p1 = StubProvider(Provider.OpenMeteo, response: null);
        var p2 = StubProvider(Provider.OpenWeather, response: null);
        var aggregator = CreateAggregator(new[] { p1, p2 });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task Merge_AveragesUniversalFields()
    {
        var p1 = StubProvider(Provider.OpenMeteo, WeatherResponses.Metric(
            temperature: 10, feelsLike: 8, humidity: 50, pressure: 1000,
            windSpeed: 4, cloudCover: 20));
        var p2 = StubProvider(Provider.OpenWeather, WeatherResponses.Metric(
            temperature: 20, feelsLike: 18, humidity: 70, pressure: 1020,
            windSpeed: 6, cloudCover: 60));
        var aggregator = CreateAggregator(new[] { p1, p2 });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        Assert.NotNull(result);
        Assert.Equal(15, result.Temperature);
        Assert.Equal(13, result.FeelsLike);
        Assert.Equal(60, result.Humidity);
        Assert.Equal(1010, result.Pressure);
        Assert.Equal(5, result.WindSpeed);
        Assert.Equal(40, result.CloudCover);
    }

    [Fact]
    public async Task Merge_WindDirection_VectorMeanHandlesWrap()
    {
        var p1 = StubProvider(Provider.OpenMeteo, WeatherResponses.Metric(windDirection: 359));
        var p2 = StubProvider(Provider.OpenWeather, WeatherResponses.Metric(windDirection: 1));
        var aggregator = CreateAggregator(new[] { p1, p2 });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        Assert.NotNull(result);
        Assert.Equal(0, result.WindDirection);
    }

    [Fact]
    public async Task Merge_SpecialtyFields_TakeFirstNonNullContributor()
    {
        var p1 = StubProvider(Provider.OpenMeteo, WeatherResponses.Metric(
            uvIndex: 7.5, precipitation: 2.5));
        var p2 = StubProvider(Provider.OpenWeather, WeatherResponses.Metric(
            visibility: 10000, summary: "scattered clouds", condition: "Clouds",
            city: "Seattle", country: "US"));
        var aggregator = CreateAggregator(new[] { p1, p2 });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        Assert.NotNull(result);
        Assert.Equal(7.5, result.UvIndex);
        Assert.Equal(2.5, result.Precipitation);
        Assert.Equal(10000, result.Visibility);
        Assert.Equal("scattered clouds", result.Summary);
        Assert.Equal("Clouds", result.Condition);
        Assert.Equal("Seattle", result.City);
        Assert.Equal("US", result.Country);
    }

    [Fact]
    public async Task Merge_Providers_IsUnionOfContributors()
    {
        var p1 = StubProvider(Provider.OpenMeteo,
            WeatherResponses.Metric(providers: new[] { Provider.OpenMeteo }));
        var p2 = StubProvider(Provider.OpenWeather,
            WeatherResponses.Metric(providers: new[] { Provider.OpenWeather }));
        var aggregator = CreateAggregator(new[] { p1, p2 });

        var result = await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        Assert.NotNull(result);
        Assert.Equal(2, result.Providers.Length);
        Assert.Contains(Provider.OpenMeteo, result.Providers);
        Assert.Contains(Provider.OpenWeather, result.Providers);
    }

    [Fact]
    public async Task Cache_SecondCallWithSameKey_DoesNotCallProviders()
    {
        var meteo = StubProvider(Provider.OpenMeteo, WeatherResponses.Metric(temperature: 10));
        var cache = new TestHybridCache();
        var aggregator = CreateAggregator(new[] { meteo }, cache);

        await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);
        await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        Assert.Equal(1, cache.FactoryCallCount);
        await meteo.Received(1).GetCurrentWeatherAsync(
            Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_FailureFromAllProviders_IsNotCached()
    {
        var p1 = StubProvider(Provider.OpenMeteo, response: null);
        var p2 = StubProvider(Provider.OpenWeather, response: null);
        var cache = new TestHybridCache();
        var aggregator = CreateAggregator(new[] { p1, p2 }, cache);

        var first = await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);
        var second = await aggregator.GetCurrentWeatherAsync(Lat, Lon, null, default);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, cache.FactoryCallCount);
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

    private static WeatherAggregatorService CreateAggregator(
        IEnumerable<IWeatherProvider> providers,
        HybridCache? cache = null)
    {
        var options = Options.Create(new WeatherOptions { CacheTtlSeconds = 300 });
        return new WeatherAggregatorService(
            providers,
            cache ?? new TestHybridCache(),
            options,
            NullLogger<WeatherAggregatorService>.Instance);
    }
}
