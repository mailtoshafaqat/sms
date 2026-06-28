using SMS.Domain.Common;
using SMS.Domain.Enums;
using Xunit;

namespace SMS.Tests;

public class BiometricTypeRulesTests
{
    [Theory]
    [InlineData(BiometricType.Both, BiometricType.Face, true)]
    [InlineData(BiometricType.Both, BiometricType.Fingerprint, true)]
    [InlineData(BiometricType.Face, BiometricType.Face, true)]
    [InlineData(BiometricType.Face, BiometricType.Fingerprint, false)]
    public void Supports_ReturnsExpectedResult(BiometricType deviceType, BiometricType scanType, bool expected) =>
        Assert.Equal(expected, BiometricTypeRules.Supports(deviceType, scanType));
}

public class FaceMatcherTests
{
    [Fact]
    public void IsMatch_ReturnsTrue_ForIdenticalDescriptors()
    {
        var descriptor = Enumerable.Range(1, 128).Select(i => (float)i / 128f).ToArray();
        Assert.True(FaceMatcher.IsMatch(descriptor, descriptor));
    }

    [Fact]
    public void IsMatch_ReturnsFalse_ForDifferentDescriptors()
    {
        var left = Enumerable.Range(1, 128).Select(i => (float)i / 128f).ToArray();
        var right = Enumerable.Range(1, 128).Select(i => (float)(129 - i) / 128f).ToArray();
        Assert.False(FaceMatcher.IsMatch(left, right));
    }

    [Fact]
    public void FindBestMatch_RejectsMatch_WhenSecondPlaceIsTooClose()
    {
        var anchor = UnitVector(1);
        var probe = Perturb(anchor, 0.04f);
        var first = Perturb(anchor, 0.05f);
        var second = Perturb(anchor, 0.052f);

        var result = FaceMatcher.FindBestMatch(
            probe,
            [
                (1, new[] { first }),
                (2, new[] { second })
            ],
            FaceMatchMode.Gate);

        Assert.Null(result);
    }

    [Fact]
    public void FindBestMatch_AcceptsMatch_WhenSecondIsFarEvenWithSmallMargin()
    {
        var anchor = UnitVector(2);
        var probe = Perturb(anchor, 0.03f);
        var enrolled = Perturb(anchor, 0.04f);
        var far = UnitVector(50);

        var result = FaceMatcher.FindBestMatch(
            probe,
            [
                (1, new[] { enrolled }),
                (2, new[] { far })
            ],
            FaceMatchMode.Gate);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Id);
    }

    [Fact]
    public void FindBestMatch_ReturnsBestCandidate_WhenMarginIsClear()
    {
        var anchor = UnitVector(3);
        var probe = Perturb(anchor, 0.02f);
        var enrolled = Perturb(anchor, 0.03f);
        var other = UnitVector(99);

        var result = FaceMatcher.FindBestMatch(
            probe,
            [
                (11, new[] { enrolled }),
                (22, new[] { other })
            ],
            FaceMatchMode.Gate);

        Assert.NotNull(result);
        Assert.Equal(11, result.Value.Id);
        Assert.True(result.Value.Distance <= FaceMatcher.GateMatchThreshold);
    }

    [Fact]
    public void FindBestMatch_RejectsMatch_AboveGateThreshold()
    {
        var probe = UnitVector(4);
        var enrolled = Perturb(probe, 0.2f);

        var result = FaceMatcher.FindBestMatch(
            probe,
            [(5, new[] { enrolled })],
            FaceMatchMode.Gate);

        Assert.Null(result);
    }

    private static float[] UnitVector(int seed)
    {
        var values = Enumerable.Range(0, 128).Select(i => (float)Math.Sin(i + seed)).ToArray();
        return FaceMatcher.Normalize(values);
    }

    private static float[] Perturb(IReadOnlyList<float> source, float amount)
    {
        var values = source.Select((value, index) => value + (index % 2 == 0 ? amount : -amount)).ToArray();
        return FaceMatcher.Normalize(values);
    }
}

public class WeeklyOffRulesTests
{
    [Theory]
    [InlineData(WeeklyOffDays.Sunday, DayOfWeek.Sunday, true)]
    [InlineData(WeeklyOffDays.Sunday, DayOfWeek.Monday, false)]
    [InlineData(WeeklyOffDays.Saturday | WeeklyOffDays.Sunday, DayOfWeek.Saturday, true)]
    [InlineData(WeeklyOffDays.Saturday | WeeklyOffDays.Sunday, DayOfWeek.Friday, false)]
    public void IsOffDay_ReturnsExpectedResult(WeeklyOffDays offDays, DayOfWeek day, bool expected) =>
        Assert.Equal(expected, WeeklyOffRules.IsOffDay(day, offDays));
}
