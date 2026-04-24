using System.Security.Claims;

namespace ApiNuggets.Services;

/// <summary>
/// Issues and validates JWT bearer tokens using the signing key configured
/// under <c>ApiNuggets:Jwt:Key</c>.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed JWT for the supplied subject, with optional roles
    /// and any additional claims.
    /// </summary>
    string GenerateToken(string subject, IEnumerable<string>? roles = null, IEnumerable<Claim>? additionalClaims = null);

    /// <summary>
    /// Validates a token. Returns the principal on success, or <c>null</c>
    /// if the token is malformed, expired, or signed with the wrong key.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
}
