using FsCheck;
using FsCheck.Fluent;
using RimBank.SharedKernel.Testing;
using LedgerLaws = RimBank.SharedKernel.Testing.DeciderLaws<
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedger,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerCommand,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerEvent>;
using LedgerSpecification = RimBank.SharedKernel.Testing.DeciderSpecification<
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedger,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerCommand,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerEvent>;
using LedgerViewSpecification = RimBank.SharedKernel.Testing.ProjectionSpecification<
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerViewProjection,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerView,
    RimBank.SharedKernel.DecidersAndProjections.Tests.SampleLedgerEvent>;

namespace RimBank.SharedKernel.DecidersAndProjections.Tests;

// The helpers must fail when they should, with a message that names what was expected and what happened.
public sealed class SpecificationHelperTests
{
    private static readonly Currency _eur = Currency.From("EUR");
    private static readonly Money _tenEuros = Money.Of(10.00m, _eur);

    [Fact]
    public void Then_fails_on_different_events()
    {
        var failure = Should.Throw<ShouldAssertException>(() =>
            LedgerSpecification.Given(new SampleLedgerOpened(_eur))
                .When(new PostToSampleLedger(_tenEuros))
                .Then(new SampleLedgerPosted(Money.Of(9.99m, _eur))));

        failure.Message.ShouldContain("Given:");
        failure.Message.ShouldContain("SampleLedgerOpened { Currency = EUR }");
        failure.Message.ShouldContain("When:");
        failure.Message.ShouldContain("Expected events:");
        failure.Message.ShouldContain("SampleLedgerPosted { Amount = 9.99 EUR }");
        failure.Message.ShouldContain("Actual events:");
        failure.Message.ShouldContain("SampleLedgerPosted { Amount = 10.00 EUR }");
    }

    [Fact]
    public void Then_fails_on_missing_events() =>
        Should.Throw<ShouldAssertException>(() =>
                LedgerSpecification.Given(new SampleLedgerOpened(_eur))
                    .When(new CloseSampleLedger())
                    .Then())
            .Message.ShouldContain("(no events)");

    [Fact]
    public void Then_fails_when_the_command_is_rejected() =>
        Should.Throw<ShouldAssertException>(() =>
                LedgerSpecification.Given()
                    .When(new PostToSampleLedger(_tenEuros))
                    .Then(new SampleLedgerPosted(_tenEuros)))
            .Message.ShouldContain($"But the command was rejected with:{Environment.NewLine}  Conflict {SampleLedger.NotOpenedCode}");

    [Fact]
    public void ThenFails_fails_when_the_command_is_accepted() =>
        Should.Throw<ShouldAssertException>(() =>
                LedgerSpecification.Given()
                    .When(new OpenSampleLedger(_eur))
                    .ThenFails(ErrorKind.Conflict, SampleLedger.AlreadyOpenedCode))
            .Message.ShouldContain($"But it was accepted with events:{Environment.NewLine}  SampleLedgerOpened");

    [Fact]
    public void ThenFails_fails_on_another_error_code() =>
        Should.Throw<ShouldAssertException>(() =>
                LedgerSpecification.Given(new SampleLedgerOpened(_eur), new SampleLedgerClosed())
                    .When(new PostToSampleLedger(_tenEuros))
                    .ThenFails(ErrorKind.Conflict, SampleLedger.NotOpenedCode))
            .Message.ShouldContain($"But it was rejected with:{Environment.NewLine}  Conflict {SampleLedger.ClosedCode}");

    [Fact]
    public void ThenFails_fails_on_another_error_kind() =>
        Should.Throw<ShouldAssertException>(() =>
                LedgerSpecification.Given(new SampleLedgerOpened(_eur))
                    .When(new PostToSampleLedger(Money.Of(1m, Currency.From("USD"))))
                    .ThenFails(ErrorKind.BusinessRule, Money.CurrencyMismatchCode))
            .Message.ShouldContain($"Validation {Money.CurrencyMismatchCode}");

    [Fact]
    public void ThenFails_returns_the_error() =>
        LedgerSpecification.Given()
            .When(new CloseSampleLedger())
            .ThenFails(ErrorKind.Conflict, SampleLedger.NotOpenedCode)
            .Message.ShouldBe("The ledger has not been opened.");

    [Fact]
    public void Then_hints_when_events_print_the_same_but_are_not_equal() =>
        Should.Throw<ShouldAssertException>(() =>
                DeciderSpecification<TaggedNote, TagNote, NoteTagged>.Given()
                    .When(new TagNote(["urgent"]))
                    .Then(new NoteTagged(["urgent"])))
            .Message.ShouldContain("print the same but are not equal");

    [Fact]
    public void Then_view_fails_on_a_different_view()
    {
        var failure = Should.Throw<ShouldAssertException>(() =>
            LedgerViewSpecification.Given(new SampleLedgerOpened(_eur), new SampleLedgerPosted(_tenEuros))
                .Then(new SampleLedgerView(_tenEuros, 2, false)));

        failure.Message.ShouldContain("Expected view:");
        failure.Message.ShouldContain("Postings = 2");
        failure.Message.ShouldContain("Actual view:");
        failure.Message.ShouldContain("Postings = 1");
    }

    [Fact]
    public void Then_view_fails_when_there_is_no_view() =>
        Should.Throw<ShouldAssertException>(() =>
                LedgerViewSpecification.Given().Then(new SampleLedgerView(Money.Zero(_eur), 0, false)))
            .Message.ShouldContain($"Actual view:{Environment.NewLine}  (none)");

    [Fact]
    public void ThenNoView_fails_when_a_view_exists() =>
        Should.Throw<ShouldAssertException>(() =>
                LedgerViewSpecification.Given(new SampleLedgerOpened(_eur)).ThenNoView())
            .Message.ShouldContain("Expected no view:");

    [Fact]
    public void The_agreement_law_catches_a_projection_that_ignores_postings() =>
        Should.Throw<Exception>(() =>
                Check.QuickThrowOnFailure(
                    LedgerLaws.ProjectionAgreesWithState<ProjectionIgnoringPostings, SampleLedgerView>(
                        Gen.Elements<SampleLedgerCommand>(new OpenSampleLedger(_eur), new PostToSampleLedger(_tenEuros)),
                        SampleLedgerLawsProperties.ExpectedView)))
            .Message.ShouldContain("Projected view:");

    [Fact]
    public void The_evolve_law_catches_an_event_evolve_cannot_apply() =>
        Should.Throw<Exception>(() =>
                Check.QuickThrowOnFailure(
                    DeciderLaws<TaggedNote, TagNote, NoteTagged>.DecidedEventsEvolve(Gen.Constant(new TagNote(["broken"])))))
            .Message.ShouldContain("Evolve threw for the decided event");

    // The sample ledger's projection with the Posted case removed: a deliberately broken read side.
    private sealed class ProjectionIgnoringPostings : IProjection<SampleLedgerView, SampleLedgerEvent>
    {
        private ProjectionIgnoringPostings()
        {
        }

        public static SampleLedgerView? Project(SampleLedgerView? view, SampleLedgerEvent domainEvent) => domainEvent switch
        {
            SampleLedgerPosted => view,
            _ => SampleLedgerViewProjection.Project(view, domainEvent),
        };
    }
}

// An aggregate whose event holds an array (compared by reference) and whose Evolve rejects the tag "broken":
// it exists only to prove the helpers' hint and the evolve law.
public sealed record TagNote(string[] Tags);

public sealed record NoteTagged(string[] Tags);

public sealed record TaggedNote : IDecider<TaggedNote, TagNote, NoteTagged>
{
    public static TaggedNote Initial { get; } = new();

    public static Result<IReadOnlyList<NoteTagged>> Decide(TagNote command, TaggedNote state) =>
        Decision.Accept(new NoteTagged(command.Tags));

    public static TaggedNote Evolve(TaggedNote state, NoteTagged domainEvent) =>
        domainEvent.Tags.Contains("broken") ? throw new InvalidOperationException("Cannot apply a broken tag.") : state;
}
