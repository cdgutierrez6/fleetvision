using System.Text.RegularExpressions;

namespace FleetVision.PredictiveMaintenance.Domain.ValueObjects;

public enum OBD2Severity { Unknown, Warning, Critical }

public sealed record OBD2Code(string Code)
{
    private static readonly Regex ValidPattern =
        new(@"^[PCBU][0-9]{4}$", RegexOptions.Compiled);

    private static readonly HashSet<string> CriticalCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "P0300", "P0301", "P0302", "P0303", "P0304", "P0305", "P0306", "P0307", "P0308",
        "P0420", "P0430",
        "P0562",
        "U0100", "U0101", "U0121",
    };

    public OBD2Severity Severity =>
        string.IsNullOrWhiteSpace(Code) ? OBD2Severity.Unknown
        : CriticalCodes.Contains(Code)  ? OBD2Severity.Critical
        : OBD2Severity.Warning;

    public bool IsCritical => Severity == OBD2Severity.Critical;

    public static OBD2Code? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var normalized = raw.Trim().ToUpperInvariant();
        return ValidPattern.IsMatch(normalized) ? new OBD2Code(normalized) : null;
    }
}
