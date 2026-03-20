namespace LittleChat.Modules.Admin.API;

public sealed class AdminClaimOptions
{
    public const string SectionName = "AdminClaim";

    public string ClaimField { get; set; } = "groups";
    public string ClaimValues { get; set; } = "app-admin";

    public IReadOnlyList<string> ParsedClaimValues =>
        ClaimValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
