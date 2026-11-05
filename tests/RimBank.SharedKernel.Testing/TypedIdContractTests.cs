using System.Text.Json;

namespace RimBank.SharedKernel.Testing;

/// <summary>
/// The rules every typed identifier must follow (ADR 0008). Inherit it once per identifier:
/// <code>public sealed class PartyReferenceDataDirectoryEntryIdContractTests
///     : TypedIdContractTests&lt;PartyReferenceDataDirectoryEntryId&gt;;</code>
/// </summary>
/// <typeparam name="TId">The identifier under test.</typeparam>
public abstract class TypedIdContractTests<TId>
    where TId : struct, ITypedId<TId>
{
    private static readonly DateTimeOffset _now = new(2026, 11, 5, 9, 30, 15, 123, TimeSpan.Zero);

    private static readonly JsonSerializerOptions _json = new() { Converters = { new TypedIdJsonConverterFactory() } };

    [Fact]
    public void New_creates_a_version_7_value_stamped_with_the_provider_time()
    {
        var id = TId.New(new FixedTimeProvider(_now));

        id.Value.ShouldNotBe(Guid.Empty);
        id.Value.Version.ShouldBe(7);
        TypedId.CreatedAt(id).ShouldBe(_now);
    }

    [Fact]
    public void New_creates_a_different_value_each_time()
    {
        var time = new FixedTimeProvider(_now);

        TId.New(time).ShouldNotBe(TId.New(time));
    }

    [Fact]
    public void From_keeps_a_valid_value()
    {
        var value = Guid.CreateVersion7(_now);

        TId.From(value).Value.ShouldBe(value);
    }

    [Fact]
    public void From_rejects_the_empty_guid_and_other_versions()
    {
        Should.Throw<ArgumentException>(() => TId.From(Guid.Empty));
        Should.Throw<ArgumentException>(() => TId.From(Guid.NewGuid()));
    }

    [Fact]
    public void TryCreate_accepts_a_version_7_value()
    {
        var value = Guid.CreateVersion7(_now);

        TId.TryCreate(value).Value.Value.ShouldBe(value);
        TId.TryCreate(value.ToString()).Value.Value.ShouldBe(value);
    }

    [Fact]
    public void TryCreate_rejects_invalid_values_with_a_validation_error()
    {
        ShouldBeInvalidIdentifier(TId.TryCreate(Guid.Empty));
        ShouldBeInvalidIdentifier(TId.TryCreate(Guid.NewGuid()));
        ShouldBeInvalidIdentifier(TId.TryCreate(default(TId).Value));
        ShouldBeInvalidIdentifier(TId.TryCreate((string?)null));
        ShouldBeInvalidIdentifier(TId.TryCreate("not-an-identifier"));
        ShouldBeInvalidIdentifier(TId.TryCreate(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void Identifiers_with_the_same_value_are_equal()
    {
        var value = Guid.CreateVersion7(_now);

        TId.From(value).ShouldBe(TId.From(value));
        (TId.From(value) == TId.From(value)).ShouldBeTrue();
        TId.From(value).GetHashCode().ShouldBe(TId.From(value).GetHashCode());
    }

    [Fact]
    public void ToString_is_the_lower_case_D_format()
    {
        var value = Guid.CreateVersion7(_now);

        TId.From(value).ToString().ShouldBe(value.ToString("D"));
    }

    [Fact]
    public void Parse_round_trips_the_text_form()
    {
        var id = TId.New(new FixedTimeProvider(_now));

        TId.Parse(id.ToString()!, null).ShouldBe(id);
        TId.TryParse(id.ToString(), null, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(id);
    }

    [Fact]
    public void Identifiers_order_by_creation_time()
    {
        var time = new FixedTimeProvider(_now);
        var earlier = TId.New(time);
        time.Advance(TimeSpan.FromMilliseconds(1));
        var later = TId.New(time);

        earlier.CompareTo(later).ShouldBeLessThan(0);
        (earlier < later).ShouldBeTrue();
        (earlier <= later).ShouldBeTrue();
        (later > earlier).ShouldBeTrue();
        (later >= earlier).ShouldBeTrue();
        (later < earlier).ShouldBeFalse();
        new[] { later, earlier }.Order().ShouldBe([earlier, later]);
    }

    [Fact]
    public void Json_round_trips_as_a_plain_string()
    {
        var id = TId.New(new FixedTimeProvider(_now));

        var json = JsonSerializer.Serialize(id, _json);

        json.ShouldBe($"\"{id.Value:D}\"");
        JsonSerializer.Deserialize<TId>(json, _json).ShouldBe(id);
    }

    private static void ShouldBeInvalidIdentifier(Result<TId> result)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe(TypedId.InvalidIdentifierCode);
    }
}
