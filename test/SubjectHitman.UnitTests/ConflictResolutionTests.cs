using SubjectHitman.Api.Domain;
using SubjectHitman.Domain.Entities;

namespace SubjectHitman.UnitTests;

public class ConflictResolutionTests
{
    private static readonly Guid SubjectA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SubjectB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Older = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newer = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MoreMatches_Wins()
    {
        var winner = SubjectIdentificationService.ResolveWinner(
            new Dictionary<Guid, List<SearchKeyType>>
            {
                [SubjectA] = [SearchKeyType.K5],
                [SubjectB] = [SearchKeyType.K5, SearchKeyType.K6],
            },
            new Dictionary<Guid, DateTimeOffset> { [SubjectA] = Older, [SubjectB] = Newer });

        Assert.Equal(SubjectB, winner);
    }

    [Fact]
    public void EqualMatches_StrongerKey_Wins()
    {
        var winner = SubjectIdentificationService.ResolveWinner(
            new Dictionary<Guid, List<SearchKeyType>>
            {
                [SubjectA] = [SearchKeyType.K2, SearchKeyType.K5],
                [SubjectB] = [SearchKeyType.K1, SearchKeyType.K6],
            },
            new Dictionary<Guid, DateTimeOffset> { [SubjectA] = Older, [SubjectB] = Newer });

        // B matched K1 which is stronger than A's best K2.
        Assert.Equal(SubjectB, winner);
    }

    [Fact]
    public void EqualMatchesAndStrength_OlderRecord_Wins()
    {
        var winner = SubjectIdentificationService.ResolveWinner(
            new Dictionary<Guid, List<SearchKeyType>>
            {
                [SubjectA] = [SearchKeyType.K1, SearchKeyType.K4],
                [SubjectB] = [SearchKeyType.K1, SearchKeyType.K4],
            },
            new Dictionary<Guid, DateTimeOffset> { [SubjectA] = Newer, [SubjectB] = Older });

        Assert.Equal(SubjectB, winner);
    }

    [Fact]
    public void SingleCandidate_Wins()
    {
        var winner = SubjectIdentificationService.ResolveWinner(
            new Dictionary<Guid, List<SearchKeyType>> { [SubjectA] = [SearchKeyType.K6] },
            new Dictionary<Guid, DateTimeOffset> { [SubjectA] = Older });

        Assert.Equal(SubjectA, winner);
    }
}
