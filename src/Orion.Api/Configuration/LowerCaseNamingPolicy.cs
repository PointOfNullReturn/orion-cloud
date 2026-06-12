using System.Text.Json;

namespace Orion.Api.Configuration;

internal sealed class LowerCaseNamingPolicy : JsonNamingPolicy
{
    public static readonly LowerCaseNamingPolicy Instance = new();

    public override string ConvertName(string name) => name.ToLowerInvariant();
}
