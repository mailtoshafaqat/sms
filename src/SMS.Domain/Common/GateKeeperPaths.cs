namespace SMS.Domain.Common;

public static class GateKeeperPaths
{
    private static readonly HashSet<string> Allowed =
    [
        "/",
        "/attendance/gate",
        "/help"
    ];

    public static bool IsAllowed(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var normalized = path.TrimEnd('/');
        if (normalized.Length == 0)
        {
            normalized = "/";
        }

        return Allowed.Contains(normalized);
    }
}
