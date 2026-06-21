namespace SMS.Domain.Common;

public static class FaceMatcher
{
    /// <summary>Same-device matching (enroll + scan on one camera).</summary>
    public const float MatchThreshold = 0.7f;

    /// <summary>Gate kiosk — allows laptop enroll + phone scan (slightly looser).</summary>
    public const float GateMatchThreshold = 0.82f;

    public static float Distance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count == 0 || left.Count != right.Count)
        {
            return float.MaxValue;
        }

        var sum = 0f;
        for (var i = 0; i < left.Count; i++)
        {
            var delta = left[i] - right[i];
            sum += delta * delta;
        }

        return MathF.Sqrt(sum);
    }

    public static bool IsMatch(IReadOnlyList<float> left, IReadOnlyList<float> right, float threshold = MatchThreshold) =>
        Distance(left, right) <= threshold;
}

