using Orion.Tests.Helpers;

namespace Orion.Tests;

// Asserts the always-on security headers on a representative response (/health).
// HSTS is intentionally NOT covered here: NetEscapades only emits
// Strict-Transport-Security over HTTPS, and TestServer runs HTTP — that one is
// verified against the live endpoint instead.
public class SecurityHeadersTests : IClassFixture<OrionWebAppFactory>
{
    private readonly OrionWebAppFactory _factory;

    public SecurityHeadersTests(OrionWebAppFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "no-referrer")]
    [InlineData("X-XSS-Protection", "0")]
    public async Task Response_IncludesSecurityHeader(string name, string expected)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains(name), $"missing header: {name}");
        Assert.Equal(expected, response.Headers.GetValues(name).Single());
    }

    [Fact]
    public async Task Response_IncludesLockedDownContentSecurityPolicy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'none'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }
}
