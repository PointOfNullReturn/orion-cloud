using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orion.Api.Services;

namespace Orion.Tests.Helpers;

public sealed class OrionWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:CacheTtlSeconds"] = "300",
                ["Weather:OpenMeteo:BaseUrl"] = "http://localhost/openmeteo/",
                ["Weather:OpenWeather:BaseUrl"] = "http://localhost/openweather/",
                ["Weather:OpenWeather:ApiKey"] = "test-key",
            });
        });
    }

    public WebApplicationFactory<Program> WithProviders(params IWeatherProvider[] providers) =>
        WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWeatherProvider>();
            foreach (var provider in providers)
            {
                services.AddSingleton(provider);
            }
        }));
}
