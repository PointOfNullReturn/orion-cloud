using Microsoft.Extensions.Options;
using Orion.Api.Configuration;

namespace Orion.Api.Services;

internal sealed class OpenWeatherApiKeyHandler : DelegatingHandler
{
    private readonly string _apiKey;

    public OpenWeatherApiKeyHandler(IOptions<WeatherOptions> options)
    {
        _apiKey = options.Value.OpenWeather.ApiKey;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || request.RequestUri is null)
        {
            return base.SendAsync(request, cancellationToken);
        }

        var builder = new UriBuilder(request.RequestUri);
        var existing = builder.Query.TrimStart('?');
        var append = $"appid={Uri.EscapeDataString(_apiKey)}";
        builder.Query = string.IsNullOrEmpty(existing) ? append : $"{existing}&{append}";
        request.RequestUri = builder.Uri;

        return base.SendAsync(request, cancellationToken);
    }
}
