namespace SMS.Domain.Common;

public enum FaceMatchMode
{
    /// <summary>Same camera used for enroll and scan (local test).</summary>
    Local,

    /// <summary>Gate kiosk — allows different phones but rejects ambiguous matches.</summary>
    Gate
}

public readonly record struct FaceMatchCandidate(int Id, float Distance);

public enum FaceMatchFailureReason
{
    None,
    NoCandidates,
    AboveThreshold,
    Ambiguous
}

public static class FaceMatcher
{
    /// <summary>Same-device matching (enroll + scan on one camera). face-api default is ~0.6.</summary>
    public const float MatchThreshold = 0.6f;

    /// <summary>Gate kiosk — allows laptop enroll + phone scan when samples are good.</summary>
    public const float GateMatchThreshold = 0.82f;

    /// <summary>Reject only when two students are both plausible matches.</summary>
    public const float GateMatchMinMargin = 0.04f;

    /// <summary>Minimum gap between 1st and 2nd match in local test mode.</summary>
    public const float LocalMatchMinMargin = 0.05f;

    public static (float MaxDistance, float MinMargin) GetPolicy(FaceMatchMode mode) =>
        mode switch
        {
            FaceMatchMode.Local => (MatchThreshold, LocalMatchMinMargin),
            FaceMatchMode.Gate => (GateMatchThreshold, GateMatchMinMargin),
            _ => (GateMatchThreshold, GateMatchMinMargin)
        };

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

    public static float[] Normalize(IReadOnlyList<float> values)
    {
        var sum = 0f;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i] * values[i];
        }

        if (sum <= 1e-12f)
        {
            return values is float[] array ? array : values.ToArray();
        }

        var inv = 1f / MathF.Sqrt(sum);
        var normalized = new float[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            normalized[i] = values[i] * inv;
        }

        return normalized;
    }

    public static bool IsMatch(
        IReadOnlyList<float> left,
        IReadOnlyList<float> right,
        float threshold = MatchThreshold) =>
        Distance(Normalize(left), Normalize(right)) <= threshold;

    /// <summary>
    /// Picks the enrolled candidate with the lowest descriptor distance and rejects ambiguous ties.
    /// </summary>
    public static FaceMatchCandidate? FindBestMatch(
        IReadOnlyList<float> probe,
        IEnumerable<(int CandidateId, IReadOnlyList<float[]> DescriptorSets)> candidates,
        FaceMatchMode mode = FaceMatchMode.Gate) =>
        TryFindBestMatch(probe, candidates, mode).Match;

    public static (FaceMatchCandidate? Match, FaceMatchFailureReason Reason) TryFindBestMatch(
        IReadOnlyList<float> probe,
        IEnumerable<(int CandidateId, IReadOnlyList<float[]> DescriptorSets)> candidates,
        FaceMatchMode mode = FaceMatchMode.Gate)
    {
        var (maxDistance, minMargin) = GetPolicy(mode);
        return TryFindBestMatch(probe, candidates, maxDistance, minMargin);
    }

    public static FaceMatchCandidate? FindBestMatch(
        IReadOnlyList<float> probe,
        IEnumerable<(int CandidateId, IReadOnlyList<float[]> DescriptorSets)> candidates,
        float maxDistance,
        float minMargin) =>
        TryFindBestMatch(probe, candidates, maxDistance, minMargin).Match;

    public static (FaceMatchCandidate? Match, FaceMatchFailureReason Reason) TryFindBestMatch(
        IReadOnlyList<float> probe,
        IEnumerable<(int CandidateId, IReadOnlyList<float[]> DescriptorSets)> candidates,
        float maxDistance,
        float minMargin)
    {
        var normalizedProbe = Normalize(probe);
        var scores = new List<FaceMatchCandidate>();

        foreach (var (candidateId, descriptorSets) in candidates)
        {
            var bestForCandidate = float.MaxValue;
            foreach (var stored in descriptorSets)
            {
                if (stored.Length < 32)
                {
                    continue;
                }

                var distance = Distance(normalizedProbe, Normalize(stored));
                if (distance < bestForCandidate)
                {
                    bestForCandidate = distance;
                }
            }

            if (bestForCandidate < float.MaxValue)
            {
                scores.Add(new FaceMatchCandidate(candidateId, bestForCandidate));
            }
        }

        if (scores.Count == 0)
        {
            return (null, FaceMatchFailureReason.NoCandidates);
        }

        scores.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        var best = scores[0];
        if (best.Distance > maxDistance)
        {
            return (null, FaceMatchFailureReason.AboveThreshold);
        }

        if (scores.Count > 1)
        {
            var second = scores[1];
            if (second.Distance <= maxDistance && second.Distance - best.Distance < minMargin)
            {
                return (null, FaceMatchFailureReason.Ambiguous);
            }
        }

        return (best, FaceMatchFailureReason.None);
    }
}
