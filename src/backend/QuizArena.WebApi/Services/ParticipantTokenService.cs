using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Options;
using JwtRegisteredClaimNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;

namespace QuizArena.WebApi.Services;

public sealed class ParticipantTokenService(IOptions<JwtOptions> jwtOptions) : IParticipantTokenService
{
    private const string ParticipantAudience = "quizarena-participant";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(5);

    private static readonly JsonWebTokenHandler Handler = new();

    private SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(jwtOptions.Value.Secret));

    public string GenerateToken(string roomCode, Guid participantId, Guid? userId)
    {
        var options = jwtOptions.Value;

        var claims = new List<Claim>
        {
            new("room_code", roomCode),
            new("participant_id", participantId.ToString())
        };

        if (userId is not null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = ParticipantAudience,
            Expires = DateTime.UtcNow.Add(TokenLifetime),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(claims)
        };

        return Handler.CreateToken(descriptor);
    }

    public async Task<ParticipantTokenValidationResult?> ValidateAsync(
        string token, string roomCode, Guid participantId, CancellationToken ct = default)
    {
        var options = jwtOptions.Value;

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,

            ValidateAudience = true,
            ValidAudience = ParticipantAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        TokenValidationResult result;

        try
        {
            result = await Handler.ValidateTokenAsync(token, validationParameters);
        }
        catch
        {
            return null;
        }

        if (!result.IsValid || result.ClaimsIdentity is null)
            return null;

        var claims = result.ClaimsIdentity;

        var tokenRoomCode = claims.FindFirst("room_code")?.Value;
        var tokenParticipantId = claims.FindFirst("participant_id")?.Value;

        if (tokenRoomCode != roomCode || tokenParticipantId != participantId.ToString())
            return null;

        var sub = claims.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var userId = Guid.TryParse(sub, out var parsedUserId) ? parsedUserId : (Guid?)null;

        return new ParticipantTokenValidationResult(userId);
    }
}
