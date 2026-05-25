namespace Intellinode.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Intellinode";
    public string Audience { get; set; } = "IntellinodeClients";
    public string SigningKey { get; set; } = "ReplaceWithAtLeast32CharacterSecretKey!";
    public int AgentTokenMinutes { get; set; } = 60;
    public int AdminTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;
}
