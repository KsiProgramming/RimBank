using RimBank.SharedKernel.Testing;

namespace RimBank.SharedKernel.Tests.TypedIds;

public sealed class TypedIdCreationTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 5, 9, 30, 15, 123, TimeSpan.Zero);

    [Fact]
    public void New_creates_a_version_7_value_stamped_with_the_provider_time()
    {
        var id = SampleId.New(new FixedTimeProvider(_now));

        id.Value.ShouldNotBe(Guid.Empty);
        id.Value.Version.ShouldBe(7);
        TypedId.CreatedAt(id).ShouldBe(_now);
    }

    [Fact]
    public void Two_new_identifiers_at_the_same_instant_differ()
    {
        var time = new FixedTimeProvider(_now);

        SampleId.New(time).ShouldNotBe(SampleId.New(time));
    }

    [Fact]
    public void From_keeps_a_valid_value()
    {
        var value = Guid.CreateVersion7(_now);

        SampleId.From(value).Value.ShouldBe(value);
    }

    [Fact]
    public void From_rejects_the_empty_guid() =>
        Should.Throw<ArgumentException>(() => SampleId.From(Guid.Empty));

    [Fact]
    public void From_rejects_a_version_4_guid() =>
        Should.Throw<ArgumentException>(() => SampleId.From(Guid.NewGuid()));

    [Fact]
    public void TryCreate_accepts_a_version_7_guid()
    {
        var value = Guid.CreateVersion7(_now);

        var result = SampleId.TryCreate(value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(value);
    }

    [Fact]
    public void TryCreate_rejects_the_empty_guid_with_a_validation_error() =>
        ShouldBeInvalidIdentifier(SampleId.TryCreate(Guid.Empty));

    [Fact]
    public void TryCreate_rejects_a_version_4_guid_with_a_validation_error() =>
        ShouldBeInvalidIdentifier(SampleId.TryCreate(Guid.NewGuid()));

    [Fact]
    public void Identifiers_with_the_same_value_are_equal()
    {
        var value = Guid.CreateVersion7(_now);

        SampleId.From(value).ShouldBe(SampleId.From(value));
        SampleId.From(value).GetHashCode().ShouldBe(SampleId.From(value).GetHashCode());
    }

    [Fact]
    public void The_default_identifier_is_empty_and_not_valid()
    {
        var unset = default(SampleId);

        unset.Value.ShouldBe(Guid.Empty);
        ShouldBeInvalidIdentifier(SampleId.TryCreate(unset.Value));
    }

    [Fact]
    public void IsValid_is_true_only_for_non_empty_version_7_values()
    {
        TypedId.IsValid(Guid.CreateVersion7(_now)).ShouldBeTrue();
        TypedId.IsValid(Guid.Empty).ShouldBeFalse();
        TypedId.IsValid(Guid.NewGuid()).ShouldBeFalse();
    }

    private static void ShouldBeInvalidIdentifier(Result<SampleId> result)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("shared.invalid-identifier");
    }
}
