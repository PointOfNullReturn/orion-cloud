namespace Orion.Api.Configuration;

public class ReverseGeocodingOptions
{
    public const string SectionName = "ReverseGeocoding";

    public string BaseUrl { get; set; } = string.Empty;
}
