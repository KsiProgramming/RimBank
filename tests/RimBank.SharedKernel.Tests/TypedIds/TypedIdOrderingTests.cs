using RimBank.SharedKernel.Testing;

namespace RimBank.SharedKernel.Tests.TypedIds;

public sealed class TypedIdOrderingTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 5, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Identifiers_sort_in_creation_order()
    {
        var time = new FixedTimeProvider(_start);
        var created = new List<SampleId>();
        for (var i = 0; i < 20; i++)
        {
            created.Add(SampleId.New(time));
            time.Advance(TimeSpan.FromMilliseconds(1));
        }

        var shuffled = created.OrderBy(_ => Random.Shared.Next()).ToList();

        shuffled.Order().ShouldBe(created);
    }

    [Fact]
    public void CompareTo_follows_creation_time()
    {
        var time = new FixedTimeProvider(_start);
        var earlier = SampleId.New(time);
        time.Advance(TimeSpan.FromSeconds(1));
        var later = SampleId.New(time);

        Compare(earlier, later).ShouldBeLessThan(0);
        Compare(later, earlier).ShouldBeGreaterThan(0);
        Compare(earlier, earlier).ShouldBe(0);
    }

    [Fact]
    public void Comparison_operators_follow_creation_time()
    {
        var time = new FixedTimeProvider(_start);
        var earlier = SampleId.New(time);
        time.Advance(TimeSpan.FromMilliseconds(1));
        var later = SampleId.New(time);

        (earlier < later).ShouldBeTrue();
        (earlier <= later).ShouldBeTrue();
        (later > earlier).ShouldBeTrue();
        (later >= earlier).ShouldBeTrue();
        (later < earlier).ShouldBeFalse();
    }

    [Fact]
    public void CreatedAt_returns_the_creation_instant_truncated_to_milliseconds()
    {
        var instant = _start.AddMilliseconds(456).AddTicks(7_890);

        var id = SampleId.New(new FixedTimeProvider(instant));

        TypedId.CreatedAt(id).ShouldBe(_start.AddMilliseconds(456));
    }

    private static int Compare<T>(T left, T right)
        where T : IComparable<T> => left.CompareTo(right);
}
