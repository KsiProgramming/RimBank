namespace RimBank.Postgresql.Tests;

// Roadmap 1.13: only a context's .PublishedLanguage gets a declared wire identity; every other message, even one that
// happens to have a MessageIdentity constant, keeps Wolverine's default name (ADR 0036). The positive case runs on the
// real Party events in PartyPublishedLanguageSchemaTests.
public sealed class PublishedLanguageMessageNamingTests
{
    [Fact]
    public void A_message_outside_a_published_language_has_no_declared_identity()
    {
        PublishedLanguageMessageNaming.DeclaredIdentity(typeof(NotPublished)).ShouldBeNull();
        new PublishedLanguageMessageNaming().TryDetermineName(typeof(NotPublished), out _).ShouldBeFalse();
    }

    [Fact]
    public void A_message_without_the_constant_has_no_declared_identity()
    {
        PublishedLanguageMessageNaming.DeclaredIdentity(typeof(string)).ShouldBeNull();
    }

    [Fact]
    public void Registering_twice_is_harmless()
    {
        PublishedLanguageMessageNaming.Register();
        PublishedLanguageMessageNaming.Register();

        Wolverine.Util.WolverineMessageNaming.ToMessageTypeName(typeof(NotPublished)).ShouldNotBe(NotPublished.MessageIdentity);
    }

    private sealed record NotPublished
    {
        public const string MessageIdentity = "not-published.v1";
    }
}
