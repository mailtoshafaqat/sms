using SMS.Domain.Enums;

namespace SMS.Domain.Common;

public static class BiometricTypeRules
{
    public static bool Supports(BiometricType deviceType, BiometricType scanType) =>
        deviceType == BiometricType.Both ||
        deviceType == scanType;

    public static string GetDisplayName(BiometricType type) => type switch
    {
        BiometricType.Fingerprint => "Fingerprint",
        BiometricType.Face => "Face",
        BiometricType.Card => "Card",
        BiometricType.Both => "Both",
        _ => type.ToString()
    };
}

