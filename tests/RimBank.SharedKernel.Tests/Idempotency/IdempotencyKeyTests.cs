using System.Text.Json;

namespace RimBank.SharedKernel.Tests;

// Roadmap 0.12: every command carries an idempotency key (specs/tech-stack.md §8.1, ADR 0018).
public sealed class IdempotencyKeyTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("0193a5b2-7c4e-7d10-9f3a-1b2c3d4e5f60")]
    [InlineData("order:42/retry#1")]
    [InlineData("!~")]
    public void TryCreate_accepts_visible_ascii_exactly_as_sent(string value)
    {
        var result = IdempotencyKey.TryCreate(value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(value);
        result.Value.ToString().ShouldBe(value);
    }

    [Fact]
    public void TryCreate_accepts_a_key_of_the_maximum_length() =>
        IdempotencyKey.TryCreate(new string('k', IdempotencyKey.MaxLength)).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" key")]
    [InlineData("key ")]
    [InlineData("two words")]
    [InlineData("tab\tkey")]
    [InlineData("line\nkey")]
    [InlineData("clé")]
    [InlineData("ключ")]
    public void TryCreate_rejects_anything_that_is_not_visible_ascii(string value)
    {
        var result = IdempotencyKey.TryCreate(value);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IdempotencyKey.Invalid);
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("shared.idempotency-key-invalid");
    }

    [Fact]
    public void TryCreate_rejects_a_key_longer_than_the_maximum() =>
        IdempotencyKey.TryCreate(new string('k', IdempotencyKey.MaxLength + 1)).Error.ShouldBe(IdempotencyKey.Invalid);

    [Fact]
    public void TryCreate_reports_a_missing_key_separately()
    {
        var result = IdempotencyKey.TryCreate(null);

        result.Error.ShouldBe(IdempotencyKey.Missing);
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("shared.idempotency-key-missing");
    }

    [Fact]
    public void The_conflicts_are_stable_conflict_errors()
    {
        IdempotencyKey.Reused.Kind.ShouldBe(ErrorKind.Conflict);
        IdempotencyKey.Reused.Code.ShouldBe("shared.idempotency-key-reused");
        IdempotencyKey.InUse.Kind.ShouldBe(ErrorKind.Conflict);
        IdempotencyKey.InUse.Code.ShouldBe("shared.idempotency-key-in-use");
    }

    [Fact]
    public void From_throws_for_an_invalid_key_because_it_is_a_programmer_error() =>
        Should.Throw<ArgumentException>(() => IdempotencyKey.From("not a key"));

    [Fact]
    public void Keys_are_equal_by_value_and_case_sensitive()
    {
        IdempotencyKey.From("Key-1").ShouldBe(IdempotencyKey.From("Key-1"));
        IdempotencyKey.From("Key-1").ShouldNotBe(IdempotencyKey.From("key-1"));
    }

    [Fact]
    public void Default_is_not_a_key()
    {
        default(IdempotencyKey).IsDefault.ShouldBeTrue();
        default(IdempotencyKey).ToString().ShouldBeEmpty();
        IdempotencyKey.From("k").IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void Json_is_a_plain_string_that_round_trips()
    {
        var key = IdempotencyKey.From("0193a5b2-7c4e");

        var json = JsonSerializer.Serialize(key);

        json.ShouldBe("\"0193a5b2-7c4e\"");
        JsonSerializer.Deserialize<IdempotencyKey>(json).ShouldBe(key);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"two words\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void Json_rejects_a_value_that_is_not_a_key(string json) =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<IdempotencyKey>(json));

    [Fact]
    public void Json_refuses_to_write_the_default_key() =>
        Should.Throw<JsonException>(() => JsonSerializer.Serialize(default(IdempotencyKey)));
}
