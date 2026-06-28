using SMS.Application.Common;
using Xunit;

namespace SMS.Tests;

public class NotificationMessageBuilderTests
{
    [Fact]
    public void BuildAbsentMessage_UsesDefaultWhenTemplateEmpty()
    {
        var message = NotificationMessageBuilder.BuildAbsentMessage(
            null,
            "Demo School",
            "Ali Raza",
            new DateOnly(2026, 6, 21),
            "Class 9",
            "A");

        Assert.Contains("Demo School", message);
        Assert.Contains("Ali Raza", message);
        Assert.Contains("ABSENT", message);
        Assert.Contains("Class 9-A", message);
    }

    [Fact]
    public void BuildAbsentMessage_AppliesCustomTemplate()
    {
        var message = NotificationMessageBuilder.BuildAbsentMessage(
            "Dear parent, {StudentName} missed school on {Date} ({ClassSection}). - {SchoolName}",
            "Green Valley",
            "Hassan",
            new DateOnly(2026, 6, 21),
            "Class 1",
            "Red");

        Assert.Equal("Dear parent, Hassan missed school on 21 Jun 2026 (Class 1-Red). - Green Valley", message);
    }

    [Fact]
    public void BuildLateMessage_AppliesCustomTemplate()
    {
        var message = NotificationMessageBuilder.BuildLateMessage(
            "{SchoolName}: {StudentName} is late ({Date}) at {Time}.",
            "Demo School",
            "Ali",
            new DateOnly(2026, 6, 21),
            new TimeOnly(8, 45));

        Assert.Equal("Demo School: Ali is late (21 Jun 2026) at 08:45.", message);
    }

    [Fact]
    public void BuildCheckInMessage_UsesDefaultTemplate()
    {
        var message = NotificationMessageBuilder.BuildCheckInMessage(
            null,
            "Demo School",
            "Ali",
            new DateOnly(2026, 6, 21),
            "Class 9",
            "A",
            new TimeOnly(7, 55));

        Assert.Contains("checked IN", message);
        Assert.Contains("07:55", message);
    }
}
