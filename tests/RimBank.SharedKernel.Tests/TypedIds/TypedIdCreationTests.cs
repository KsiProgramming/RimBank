namespace RimBank.SharedKernel.Tests.TypedIds;

// Creation rules for every identifier are in TypedIdContractTests (see SampleIdContractTests).
// These tests cover the TypedId helpers directly.
public sealed class TypedIdCreationTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 5, 9, 30, 15, 123, TimeSpan.Zero);

    [Fact]
    public void IsValid_is_true_only_for_non_empty_version_7_values()
    {
        TypedId.IsValid(Guid.CreateVersion7(_now)).ShouldBeTrue();
        TypedId.IsValid(Guid.Empty).ShouldBeFalse();
        TypedId.IsValid(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void EnsureValid_returns_a_valid_value_unchanged()
    {
        var value = Guid.CreateVersion7(_now);

        TypedId.EnsureValid(value).ShouldBe(value);
    }

    [Fact]
    public void EnsureValid_explains_the_rule()
    {
        var exception = Should.Throw<ArgumentException>(() => TypedId.EnsureValid(Guid.NewGuid()));

        exception.Message.ShouldContain("version 7");
    }

    [Fact]
    public void NewGuid_requires_a_time_provider() =>
        Should.Throw<ArgumentNullException>(() => TypedId.NewGuid(null!));

    [Fact]
    public void The_default_identifier_is_empty() =>
        default(SampleId).Value.ShouldBe(Guid.Empty);

    [Fact]
    public void The_invalid_identifier_error_is_in_business_language()
    {
        var error = SampleId.TryCreate(Guid.Empty).Error;

        error.ShouldBe(Error.Validation("shared.invalid-identifier", "The value is not a valid identifier."));
    }
}
