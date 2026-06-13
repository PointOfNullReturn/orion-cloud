using Orion.Api.Models;
using Orion.Api.Services;

namespace Orion.Tests;

public class UnitConverterTests
{
    [Fact]
    public void ToImperial_SetsUnitsToImperial()
    {
        var result = UnitConverter.ToImperial(Metric());
        Assert.Equal(Units.Imperial, result.Units);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(100, 212)]
    [InlineData(-40, -40)]
    public void ToImperial_ConvertsTemperatureCelsiusToFahrenheit(double celsius, double expectedF)
    {
        var result = UnitConverter.ToImperial(Metric(temperature: celsius));
        Assert.Equal(expectedF, result.Temperature, precision: 4);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(20, 68)]
    public void ToImperial_ConvertsFeelsLikeCelsiusToFahrenheit(double celsius, double expectedF)
    {
        var result = UnitConverter.ToImperial(Metric(feelsLike: celsius));
        Assert.Equal(expectedF, result.FeelsLike, precision: 4);
    }

    [Theory]
    [InlineData(1013.25, 29.9213)]
    [InlineData(1000.0, 29.5300)]
    public void ToImperial_ConvertsPressureHectopascalsToInchesOfMercury(double hpa, double expectedInHg)
    {
        var result = UnitConverter.ToImperial(Metric(pressure: hpa));
        Assert.Equal(expectedInHg, result.Pressure, precision: 4);
    }

    [Theory]
    [InlineData(1.0, 2.236936)]
    [InlineData(10.0, 22.36936)]
    public void ToImperial_ConvertsWindSpeedMetersPerSecondToMilesPerHour(double mps, double expectedMph)
    {
        var result = UnitConverter.ToImperial(Metric(windSpeed: mps));
        Assert.Equal(expectedMph, result.WindSpeed, precision: 4);
    }

    [Theory]
    [InlineData(25.4, 1.0)]
    [InlineData(0.0, 0.0)]
    public void ToImperial_ConvertsPrecipitationMillimetersToInches(double mm, double expectedIn)
    {
        var result = UnitConverter.ToImperial(Metric(precipitation: mm));
        Assert.NotNull(result.Precipitation);
        Assert.Equal(expectedIn, result.Precipitation.Value, precision: 4);
    }

    [Fact]
    public void ToImperial_NullPrecipitation_StaysNull()
    {
        var result = UnitConverter.ToImperial(Metric(precipitation: null));
        Assert.Null(result.Precipitation);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1000, 3281)]
    public void ToImperial_ConvertsVisibilityMetersToFeet(int meters, int expectedFt)
    {
        var result = UnitConverter.ToImperial(Metric(visibility: meters));
        Assert.Equal(expectedFt, result.Visibility);
    }

    [Fact]
    public void ToImperial_NullVisibility_StaysNull()
    {
        var result = UnitConverter.ToImperial(Metric(visibility: null));
        Assert.Null(result.Visibility);
    }

    [Fact]
    public void ToImperial_UnitInvariantFields_PassThrough()
    {
        var metric = Metric();
        var imperial = UnitConverter.ToImperial(metric);

        Assert.Equal(metric.Latitude, imperial.Latitude);
        Assert.Equal(metric.Longitude, imperial.Longitude);
        Assert.Equal(metric.Timestamp, imperial.Timestamp);
        Assert.Equal(metric.Providers, imperial.Providers);
        Assert.Equal(metric.Humidity, imperial.Humidity);
        Assert.Equal(metric.WindDirection, imperial.WindDirection);
        Assert.Equal(metric.CloudCover, imperial.CloudCover);
        Assert.Equal(metric.UvIndex, imperial.UvIndex);
        Assert.Equal(metric.Summary, imperial.Summary);
        Assert.Equal(metric.Condition, imperial.Condition);
        Assert.Equal(metric.Sunrise, imperial.Sunrise);
        Assert.Equal(metric.Sunset, imperial.Sunset);
        Assert.Equal(metric.City, imperial.City);
        Assert.Equal(metric.Country, imperial.Country);
    }

    private static WeatherResponse Metric(
        double temperature = 20,
        double feelsLike = 18,
        double pressure = 1013,
        double windSpeed = 5,
        double? precipitation = null,
        int? visibility = null) =>
        new WeatherResponse(
            Latitude: 47.6,
            Longitude: -122.3,
            Units: Units.Metric,
            Timestamp: DateTimeOffset.UnixEpoch,
            Providers: new[] { Provider.OpenMeteo, Provider.OpenWeather },
            Temperature: temperature,
            FeelsLike: feelsLike,
            Humidity: 60,
            Pressure: pressure,
            WindSpeed: windSpeed,
            WindDirection: 270,
            CloudCover: 40,
            UvIndex: 5.0,
            Precipitation: precipitation,
            Visibility: visibility,
            Summary: "scattered clouds",
            Condition: "Clouds",
            Sunrise: DateTimeOffset.UnixEpoch,
            Sunset: DateTimeOffset.UnixEpoch,
            City: "Seattle",
            Country: "US"
        );
}
