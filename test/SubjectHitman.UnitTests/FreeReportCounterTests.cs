using SubjectHitman.Domain.Counting;

namespace SubjectHitman.UnitTests;

public class FreeReportCounterTests
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private static int Collapse(params DateTimeOffset[] timestamps)
        => FreeReportCounter.CollapseByCooldown(timestamps, Cooldown);

    [Fact]
    public void EmptyList_ReturnsZero() => Assert.Equal(0, Collapse());

    [Fact]
    public void SingleReport_ReturnsOne() => Assert.Equal(1, Collapse(T0));

    [Fact]
    public void ReportsWithinCooldownOfFirst_CountAsOne()
        => Assert.Equal(1, Collapse(T0, T0.AddHours(10), T0.AddHours(22)));

    [Fact]
    public void ReportBeyondCooldownOfGroupStart_OpensNewGroup()
        // Spec § 6.2 example: 10:00, 20:00, 32:00 -> {10:00, 20:00} + {32:00} = 2.
        => Assert.Equal(2, Collapse(T0, T0.AddHours(10), T0.AddHours(22), T0.AddHours(30)));

    [Fact]
    public void ChainFromFirst_NotSlidingWindow()
        // 0h, 20h, 40h: 20h is within 24h of 0h (same group), 40h is beyond 24h of 0h
        // (new group even though it is within 24h of 20h) -> 2 groups.
        => Assert.Equal(2, Collapse(T0, T0.AddHours(20), T0.AddHours(40)));

    [Fact]
    public void ExactCooldownBoundary_SameGroup()
        // Difference of exactly the cooldown does NOT open a new group (strict '>').
        => Assert.Equal(1, Collapse(T0, T0.Add(Cooldown)));

    [Fact]
    public void JustOverCooldownBoundary_NewGroup()
        => Assert.Equal(2, Collapse(T0, T0.Add(Cooldown).AddTicks(1)));

    [Fact]
    public void ManyGroups_CountedCorrectly()
        => Assert.Equal(3, Collapse(T0, T0.AddDays(2), T0.AddDays(4)));
}
