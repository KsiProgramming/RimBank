using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using static RimBank.Party.PartyReferenceDataDirectory.Tests.BankRelationsExamples;

namespace RimBank.Party.PartyReferenceDataDirectory.Tests;

// The partial-update law of the BankRelations (roadmap 1.12): an omitted change keeps the current value, a given one sets
// or clears it, and applying the same changes twice is the same as once.
public sealed class BankRelationsPartialUpdateProperties
{
    private static readonly Gen<BankRelationsDetails> _details =
        from type in Gen.Elements<BankRelationType?>([null, .. Enum.GetValues<BankRelationType>().Select(type => (BankRelationType?)type)])
        from reference in Gen.Elements<PartyReferenceDataDirectoryEntryId?>(null, ManagerId, BranchId)
        from start in Gen.Elements<DateTimeOffset?>(null, StartDate, EndDate)
        from end in Gen.Elements<DateTimeOffset?>(null, StartDate, EndDate)
        from status in Gen.Elements<BankRelationLifecycleStatus?>([null, .. Enum.GetValues<BankRelationLifecycleStatus>().Select(status => (BankRelationLifecycleStatus?)status)])
        select new BankRelationsDetails(type, reference, start, end, status);

    // Each part is omitted (false) or sent with the target's value (which may clear it).
    private static readonly Gen<BankRelationsChanges> _changes =
        from target in _details
        from sent in Gen.ArrayOf(Gen.Elements(false, true), 5)
        let full = BankRelationsChanges.To(target)
        select new BankRelationsChanges(
            sent[0] ? full.Type : null,
            sent[1] ? full.BusinessUnitEmployee : null,
            sent[2] ? full.StartDate : null,
            sent[3] ? full.EndDate : null,
            sent[4] ? full.LifecycleStatus : null);

    private static readonly (Func<BankRelationsDetails, object?> Value, Func<BankRelationsChanges, (bool Sent, object? Value)> Change)[] _parts =
    [
        (d => d.Type, c => (c.Type is not null, c.Type?.Value)),
        (d => d.BusinessUnitEmployee, c => (c.BusinessUnitEmployee is not null, c.BusinessUnitEmployee?.Value)),
        (d => d.StartDate, c => (c.StartDate is not null, c.StartDate?.Value)),
        (d => d.EndDate, c => (c.EndDate is not null, c.EndDate?.Value)),
        (d => d.LifecycleStatus, c => (c.LifecycleStatus is not null, c.LifecycleStatus?.Value)),
    ];

    [Property]
    public Property No_change_keeps_any_bank_relations() =>
        Prop.ForAll(_details.ToArbitrary(), details => details.Apply(BankRelationsChanges.None) == details);

    [Property]
    public Property The_changes_to_some_details_give_those_details_whatever_the_current_ones() =>
        Prop.ForAll(_details.ToArbitrary(), _details.ToArbitrary(), (current, target) => current.Apply(BankRelationsChanges.To(target)) == target);

    [Property]
    public Property An_omitted_part_keeps_its_value_and_a_sent_one_takes_the_change() =>
        Prop.ForAll(
            _details.ToArbitrary(),
            _changes.ToArbitrary(),
            (current, changes) =>
            {
                var updated = current.Apply(changes);
                return _parts.All(part => part.Change(changes) switch
                {
                    (false, _) => Equals(part.Value(updated), part.Value(current)),
                    (true, var value) => Equals(part.Value(updated), value),
                });
            });

    [Property]
    public Property Applying_the_same_changes_twice_is_applying_them_once() =>
        Prop.ForAll(
            _details.ToArbitrary(),
            _changes.ToArbitrary(),
            (current, changes) => current.Apply(changes).Apply(changes) == current.Apply(changes));
}
