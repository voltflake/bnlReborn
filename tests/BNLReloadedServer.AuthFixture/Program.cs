using System.Security.Claims;
using System.Security.Cryptography;
using BNLReloadedServer.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var assertions = 0;
using var rsa = RSA.Create(2048);
var validator = RebornGameTicketValidator.FromPublicKey(rsa.ExportSubjectPublicKeyInfoPem());
var ticket = CreateTicket(rsa, RebornGameTicketValidator.DefaultIssuer, RebornGameTicketValidator.DefaultAudience);
Assert(validator.TryValidateAndConsume(RebornGameTicketValidator.ProtocolId, ticket, out var identity, out _),
    "a correctly signed ticket is accepted");
Assert(identity?.SteamId == 76561198000000000UL && identity.DisplayName == "Reborn Tester",
    "signed identity fields are authoritative");
Assert(!validator.TryValidateAndConsume(RebornGameTicketValidator.ProtocolId, ticket, out _, out var replayError) &&
       replayError.Contains("already been used", StringComparison.Ordinal), "ticket replay is rejected");
var wrongIssuer = CreateTicket(rsa, "https://wrong.invalid", RebornGameTicketValidator.DefaultAudience);
Assert(!validator.TryValidateAndConsume(RebornGameTicketValidator.ProtocolId, wrongIssuer, out _, out _),
    "a ticket from another issuer is rejected");
Console.WriteLine($"Server Reborn authentication fixture passed ({assertions} assertions).");
return;

void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"Failed: {message}");
    assertions++;
}

static string CreateTicket(RSA signingKey, string issuer, string audience)
{
    var now = DateTime.UtcNow;
    var descriptor = new SecurityTokenDescriptor
    {
        Issuer = issuer,
        Audience = audience,
        Subject = new ClaimsIdentity([
            new Claim(JwtRegisteredClaimNames.Sub, "76561198000000000"),
            new Claim("name", "Reborn Tester"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        ]),
        IssuedAt = now,
        NotBefore = now.AddSeconds(-1),
        Expires = now.AddMinutes(1),
        SigningCredentials = new SigningCredentials(new RsaSecurityKey(signingKey), SecurityAlgorithms.RsaSha256)
    };
    return new JsonWebTokenHandler().CreateToken(descriptor);
}
