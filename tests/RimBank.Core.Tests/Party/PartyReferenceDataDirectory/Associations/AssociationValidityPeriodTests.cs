namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// BIAN AssociationValidFromToDate (roadmap 1.11, ADR 0033): ISO 8601 bounds kept in UTC, from not after to.
public sealed class AssociationValidityPeriodTests
{
    [Fact]
    public void A_date_is_midnight_utc() =>
        AssociationValidityPeriod.TryCreate(new("2024-01-01", null)).Value
            .ShouldBe(new AssociationValidityPeriod(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), null));

    [Fact]
    public void A_date_time_with_an_offset_is_kept_in_utc() =>
        AssociationValidityPeriod.TryCreate(new(null, "2025-12-31T23:30:00+01:00")).Value
            .ShouldBe(new AssociationValidityPeriod(null, new DateTimeOffset(2025, 12, 31, 22, 30, 0, TimeSpan.Zero)));

    [Fact]
    public void A_date_time_without_an_offset_is_utc() =>
        AssociationValidityPeriod.TryCreate(new("2024-03-04T05:06:07", null)).Value.From
            .ShouldBe(new DateTimeOffset(2024, 3, 4, 5, 6, 7, TimeSpan.Zero));

    [Fact]
    public void Both_bounds_can_be_the_same_instant() =>
        AssociationValidityPeriod.TryCreate(new("2024-01-01", "2024-01-01T00:00:00Z")).IsSuccess.ShouldBeTrue();

    [Fact]
    public void An_end_before_the_start_is_invalid_and_names_the_end()
    {
        var error = AssociationValidityPeriod.TryCreate(new("2025-01-01", "2024-12-31")).Error;

        error.Code.ShouldBe("party.association-invalid");
        error.FieldErrors.Keys.ShouldBe(["ToDateTime"]);
    }

    [Theory]
    [InlineData("01/01/2024", null, "FromDateTime")]
    [InlineData(null, "2024-13-01", "ToDateTime")]
    [InlineData(null, null, "FromDateTime")]
    [InlineData(" ", "", "FromDateTime")]
    public void A_bound_that_is_not_iso_8601_or_no_bound_at_all_is_invalid(string? from, string? to, string field) =>
        AssociationValidityPeriod.TryCreate(new(from, to)).Error.FieldErrors.Keys.ShouldBe([field]);
}
