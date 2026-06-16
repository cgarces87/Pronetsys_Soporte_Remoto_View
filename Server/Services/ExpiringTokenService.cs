using Pronetsys.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace Pronetsys.Server.Services;

public interface IExpiringTokenService
{
    string GetToken(DateTimeOffset expiration, string organizationId = "");
    bool TryGetExpiration(string secret, out DateTimeOffset tokenExpiration);
    bool TryGetOrganizationId(string secret, out string organizationId);
}

public class ExpiringTokenService : IExpiringTokenService
{
    private static readonly MemoryCache _tokenCache = new(new MemoryCacheOptions());

    private record TokenInfo(DateTimeOffset Expiration, string OrganizationId);

    public string GetToken(DateTimeOffset expiration, string organizationId = "")
    {
        var secret = RandomGenerator.GenerateString(36);
        _tokenCache.Set(secret, new TokenInfo(expiration, organizationId), expiration);
        return secret;
    }

    public bool TryGetExpiration(string secret, out DateTimeOffset tokenExpiration)
    {
        if (_tokenCache.TryGetValue(secret, out TokenInfo? info) && info is not null)
        {
            tokenExpiration = info.Expiration;
            return true;
        }
        tokenExpiration = default;
        return false;
    }

    public bool TryGetOrganizationId(string secret, out string organizationId)
    {
        if (_tokenCache.TryGetValue(secret, out TokenInfo? info) && info is not null)
        {
            organizationId = info.OrganizationId;
            return true;
        }
        organizationId = string.Empty;
        return false;
    }
}
