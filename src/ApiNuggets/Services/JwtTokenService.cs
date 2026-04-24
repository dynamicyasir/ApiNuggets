using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiNuggets.Services;

/// <inheritdoc />
internal sealed class JwtTokenService : IJwtTokenService
{
    private readonly ApiNuggetsOptions _options;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtTokenService(IOptions<ApiNuggetsOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateToken(
        string subject,
        IEnumerable<string>? roles = null,
        IEnumerable<Claim>? additionalClaims = null)
    {
        if (!_options.Jwt.Enable)
        {
            throw new InvalidOperationException(
                "JWT is not enabled. Set ApiNuggets:Jwt:Enable = true in configuration.");
        }

        if (string.IsNullOrWhiteSpace(_options.Jwt.Key))
        {
            throw new InvalidOperationException(
                "ApiNuggets:Jwt:Key is required when JWT is enabled.");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

        if (roles is not null)
        {
            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        if (additionalClaims is not null)
        {
            claims.AddRange(additionalClaims);
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Jwt.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Jwt.Issuer,
            audience: _options.Jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.Jwt.ExpiryMinutes),
            signingCredentials: credentials);

        return _handler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_options.Jwt.Key))
        {
            return null;
        }

        var validationParameters = BuildValidationParameters(_options.Jwt);

        try
        {
            var principal = _handler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal static TokenValidationParameters BuildValidationParameters(JwtOptions jwt)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
        };
    }
}
