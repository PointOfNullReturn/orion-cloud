using System.Net;
using System.Text.Json;
using NSubstitute;
using Orion.Api.Models;
using Orion.Api.Services;
using Orion.Tests.Helpers;

namespace Orion.Tests;

public class GeocodeEndpointTests : IClassFixture<OrionWebAppFactory>
{
    private readonly OrionWebAppFactory _factory;

    public GeocodeEndpointTests(OrionWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetGeocode_HappyPath_Returns200WithMappedCamelCaseResults()
    {
        var geocoding = StubGeocoding(
            new GeocodeResult("Seattle", "Washington", "United States", "US", 47.60621, -122.33207));
        var client = _factory.WithGeocoding(geocoding).CreateClient();

        var response = await client.GetAsync("/geocode?q=Seattle");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.EnumerateArray().Single();
        Assert.Equal("Seattle", first.GetProperty("name").GetString());
        Assert.Equal("Washington", first.GetProperty("region").GetString());
        Assert.Equal("United States", first.GetProperty("country").GetString());
        Assert.Equal("US", first.GetProperty("countryCode").GetString());
        Assert.Equal(47.60621, first.GetProperty("latitude").GetDouble());
        Assert.Equal(-122.33207, first.GetProperty("longitude").GetDouble());
    }

    [Fact]
    public async Task GetGeocode_NoMatches_Returns200WithEmptyArray()
    {
        var client = _factory.WithGeocoding(StubGeocoding()).CreateClient();

        var response = await client.GetAsync("/geocode?q=asdfghjkl");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", body.Trim());
    }

    [Theory]
    [InlineData("/geocode")] // missing q
    [InlineData("/geocode?q=")] // empty q
    [InlineData("/geocode?q=%20")] // whitespace q
    public async Task GetGeocode_BlankQuery_Returns400(string url)
    {
        var client = _factory.WithGeocoding(StubGeocoding()).CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetGeocode_ExceedingRateLimit_Returns429()
    {
        var client = _factory.WithGeocoding(StubGeocoding()).CreateClient();

        var first = await client.GetAsync("/geocode?q=Seattle");
        var second = await client.GetAsync("/geocode?q=Seattle");
        var third = await client.GetAsync("/geocode?q=Seattle");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    private static IGeocodingService StubGeocoding(params GeocodeResult[] results)
    {
        var service = Substitute.For<IGeocodingService>();
        service.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<GeocodeResult>)results);
        return service;
    }
}
