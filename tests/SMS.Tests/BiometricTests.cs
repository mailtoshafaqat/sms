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

