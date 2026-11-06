namespace RimBank.SharedKernel.Actors.Tests;

// Roadmap 0.13: every state change is traceable to an actor (mission quality bar "Auditability", ADR 0019).
public sealed class ActorTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("f81d4fae-7dec-11d0-a765-00a0c91e6bf6")]
    [InlineData("service-account-rimbank-dev-client")]
    [InlineData("auth0|5f7c8ec7c33c6c004bbafe82")]
    public void TryCreate_accepts_a_visible_ascii_subject_exactly_as_sent(string subject)
    {
        var actor = Actor.TryCreate(subject, clientId: null);

        actor.IsSuccess.ShouldBeTrue();
        actor.Value.Subject.ShouldBe(subject);
        actor.Value.ClientId.ShouldBeNull();
        actor.Value.IsSystem.ShouldBeFalse();
    }

    [Fact]
    public void TryCreate_keeps_the_client_the_caller_used()
    {
        var actor = Actor.TryCreate("f81d4fae-7dec-11d0-a765-00a0c91e6bf6", "rimbank-dev-client");

        actor.Value.ClientId.ShouldBe("rimbank-dev-client");
    }

    [Fact]
    public void TryCreate_accepts_a_subject_of_the_maximum_length() =>
        Actor.TryCreate(new string('s', Actor.MaxLength), null).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("two words")]
    [InlineData("tab\tsubject")]
    [InlineData("sujet-é")]
    public void TryCreate_rejects_a_subject_that_is_not_visible_ascii(string? subject)
    {
        var actor = Actor.TryCreate(subject, null);

        actor.IsFailure.ShouldBeTrue();
        actor.Error.ShouldBe(Actor.Invalid);
        actor.Error.Code.ShouldBe(Actor.InvalidCode);
        actor.Error.Kind.ShouldBe(ErrorKind.Unauthorized);
    }

    [Fact]
    public void TryCreate_rejects_a_subject_that_is_too_long() =>
        Actor.TryCreate(new string('s', Actor.MaxLength + 1), null).Error.ShouldBe(Actor.Invalid);

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    public void TryCreate_rejects_a_client_that_is_not_visible_ascii(string clientId) =>
        Actor.TryCreate("subject", clientId).Error.ShouldBe(Actor.Invalid);

    [Fact]
    public void System_is_the_actor_of_messages_no_caller_sent()
    {
        Actor.System.IsSystem.ShouldBeTrue();
        Actor.System.ClientId.ShouldBeNull();
    }

    [Fact]
    public void No_caller_subject_can_be_the_system_subject() =>
        Actor.TryCreate(Actor.System.Subject, null).IsFailure.ShouldBeTrue();

    [Fact]
    public void Actors_are_equal_by_value()
    {
        Actor.TryCreate("subject", "client").Value.ShouldBe(Actor.TryCreate("subject", "client").Value);
        Actor.TryCreate("subject", "client").Value.ShouldNotBe(Actor.TryCreate("subject", "other").Value);
    }

    [Fact]
    public void Default_is_not_an_actor() => default(Actor).IsDefault.ShouldBeTrue();
}
