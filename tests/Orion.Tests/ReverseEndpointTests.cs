using System.Net;
using System.Text.Json;
using NSubstitute;
using Orion.Api.Models;
using Orion.Api.Services;
using Orion.Tests.Helpers;

namespace Orion.Tests;

public class ReverseEndpointTests : IClassFixture<OrionWebAppFactory>
{
    private readonly OrionWebAppFactory _factory;

    public ReverseEndpointTests(OrionWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetReverse_HappyPath_Returns200WithMappedCamelCaseResult()
    {
        var svc = StubReverse(
            new GeocodeResult("Seattle", "Washington", "United States of America (the)", "US", 47.6, -122.3));
        var client = _factory.WithReverseGeocoding(svc).CreateClient();

        var response = await client.GetAsync("/reverse?lat=47.6&lon=-122.3");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("Seattle", root.GetProperty("name").GetString());
        Assert.Equal("Washington", root.GetProperty("region").GetString());
        Assert.Equal("US", root.GetProperty("countryCode").GetString());
    }

    [Fact]
    public async Task GetReverse_NoPlaceFound_Returns404()
    {
        var client = _factory.WithReverseGeocoding(StubReverse(null)).CreateClient();

        var response = await client.GetAsync("/reverse?lat=47.6&lon=-122.3");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    public async Task GetReverse_OutOfRangeCoords_Returns400(double lat, double lon)
    {
        var client = _factory.WithReverseGeocoding(StubReverse(null)).CreateClient();

        var response = await client.GetAsync(FormattableString.Invariant($"/reverse?lat={lat}&lon={lon}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetReverse_ExceedingRateLimit_Returns429()
    {
        var svc = StubReverse(
            new GeocodeResult("Seattle", "Washington", "United States", "US", 47.6, -122.3));
        var client = _factory.WithReverseGeocoding(svc).CreateClient();

        var first = await client.GetAsync("/reverse?lat=47.6&lon=-122.3");
        var second = await client.GetAsync("/reverse?lat=47.6&lon=-122.3");
        var third = await client.GetAsync("/reverse?lat=47.6&lon=-122.3");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    private static IReverseGeocodingService StubReverse(GeocodeResult? result)
    {
        var svc = Substitute.For<IReverseGeocodingService>();
        svc.ReverseAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(result);
        return svc;
    }
}
