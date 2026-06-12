using Orion.Api.Models;

namespace Orion.Api.Services;

public static class UnitConverter
{
    public static WeatherResponse ToImperial(WeatherResponse metric) =>
        metric with
        {
            Units = Units.Imperial,
            Temperature = CelsiusToFahrenheit(metric.Temperature),
            FeelsLike = CelsiusToFahrenheit(metric.FeelsLike),
            Pressure = HectopascalsToInchesOfMercury(metric.Pressure),
            WindSpeed = MetersPerSecondToMilesPerHour(metric.WindSpeed),
            Precipitation = metric.Precipitation is { } p ? MillimetersToInches(p) : null,
            Visibility = metric.Visibility is { } v ? MetersToFeet(v) : null,
        };

    private static double CelsiusToFahrenheit(double c) => c * 9.0 / 5.0 + 32.0;
    private static double HectopascalsToInchesOfMercury(double hpa) => hpa / 33.8639;
    private static double MetersPerSecondToMilesPerHour(double ms) => ms * 2.236936;
    private static double MillimetersToInches(double mm) => mm / 25.4;
    private static int MetersToFeet(int m) => (int)Math.Round(m * 3.28084);
}
