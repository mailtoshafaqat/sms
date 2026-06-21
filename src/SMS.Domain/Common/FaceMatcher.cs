namespace SMS.Domain.Common;

public static class FaceMatcher
{
    public const float MatchThreshold = 0.7f;

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

    public static bool IsMatch(IReadOnlyList<float> left, IReadOnlyList<float> right) =>
        Distance(left, right) <= MatchThreshold;
}

