namespace RimBank.SharedKernel.Tests;

public sealed class DecisionTests
{
    [Fact]
    public void Accept_keeps_the_events_in_order()
    {
        var result = Decision.Accept("first", "second");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Accept_copies_the_events()
    {
        string[] events = ["first"];

        var result = Decision.Accept(events);
        events[0] = "changed";

        result.Value.ShouldBe(["first"]);
    }

    [Fact]
    public void Nothing_is_a_success_with_no_events()
    {
        var result = Decision.Nothing<string>();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public void Accept_rejects_a_null_array() =>
        Should.Throw<ArgumentNullException>(() => Decision.Accept<string>(null!));
}
