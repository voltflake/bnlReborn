using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BNLReloadedServer.Authentication;

public sealed record RebornGameIdentity(ulong SteamId, string DisplayName, string TicketId);

public sealed class RebornGameTicketValidator
{
    public const string ProtocolId = "bnl-reborn-v1";
    public const string DefaultIssuer = "https://auth.blocknload.cc";
    public const string DefaultAudience = "bnl-reborn-game";

    private readonly JsonWebTokenHandler _handler = new();
    private readonly TokenValidationParameters? _parameters;
    private readonly ConcurrentDictionary<string, long> _consumedTickets = new(StringComparer.Ordinal);
    private int _validationCount;

    public static RebornGameTicketValidator Shared { get; } = CreateFromEnvironment();
    public string? ConfigurationError { get; }

    private RebornGameTicketValidator(TokenValidationParameters? parameters, string? configurationError)
    {
        _parameters = parameters;
        ConfigurationError = configurationError;
    }

    public static RebornGameTicketValidator FromPublicKey(
        string publicKeyPem,
        string issuer = DefaultIssuer,
        string audience = DefaultAudience)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        return new RebornGameTicketValidator(CreateParameters(rsa, issuer, audience), null);
    }

    private static RebornGameTicketValidator CreateFromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable("BNL_REBORN_AUTH_PUBLIC_KEY_PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return new RebornGameTicketValidator(null,
                "BNL_REBORN_AUTH_PUBLIC_KEY_PATH is not configured; Reborn logins will be rejected.");
        }
        try
        {
            var issuer = Environment.GetEnvironmentVariable("BNL_REBORN_AUTH_ISSUER") ?? DefaultIssuer;
            var audience = Environment.GetEnvironmentVariable("BNL_REBORN_AUTH_AUDIENCE") ?? DefaultAudience;
            return FromPublicKey(File.ReadAllText(path), issuer, audience);
        }
        catch (Exception exception)
        {
            return new RebornGameTicketValidator(null,
                $"Could not load the Reborn authentication public key: {exception.Message}");
        }
    }

    private static TokenValidationParameters CreateParameters(RSA rsa, string issuer, string audience) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new RsaSecurityKey(rsa),
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        ClockSkew = TimeSpan.FromSeconds(15)
    };

    public bool TryValidateAndConsume(
        string protocolId,
        string ticket,
        out RebornGameIdentity? identity,
        out string error)
    {
        identity = null;
        error = "Authentication failed.";
        if (!string.Equals(protocolId, ProtocolId, StringComparison.Ordinal))
        {
            error = "This client does not support the current BNL Reborn login protocol.";
            return false;
        }
        if (_parameters is null)
        {
            error = "BNL Reborn authentication is unavailable on this server.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 8192)
        {
            return false;
        }

        try
        {
            var validation = _handler.ValidateTokenAsync(ticket, _parameters).GetAwaiter().GetResult();
            if (!validation.IsValid || validation.ClaimsIdentity is null)
            {
                return false;
            }
            var claims = validation.ClaimsIdentity;
            var subject = claims.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var ticketId = claims.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var displayName = claims.FindFirst("name")?.Value;
            var expiresValue = claims.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            if (subject is null || subject.Length != 17 ||
                !ulong.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var steamId) ||
                string.IsNullOrWhiteSpace(ticketId) || ticketId.Length > 128 ||
                !long.TryParse(expiresValue, NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAt))
            {
                return false;
            }
            CleanupConsumedTickets();
            if (!_consumedTickets.TryAdd(ticketId, expiresAt))
            {
                error = "This login ticket has already been used.";
                return false;
            }
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? $"Player-{subject[^6..]}"
                : displayName.Trim();
            if (displayName.Length > 32)
            {
                displayName = displayName[..32];
            }
            identity = new RebornGameIdentity(steamId, displayName, ticketId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CleanupConsumedTickets()
    {
        if (Interlocked.Increment(ref _validationCount) % 256 != 0)
        {
            return;
        }
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var item in _consumedTickets)
        {
            if (item.Value < now)
            {
                _consumedTickets.TryRemove(item.Key, out _);
            }
        }
    }
}
