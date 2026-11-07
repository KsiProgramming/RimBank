using RimBank.SharedKernel.Testing;

namespace RimBank.SharedKernel.Tests;

// The usage sample from ADR 0008, kept compiling and passing here.
public sealed class TypedIdUsageSampleTests
{
    private sealed record RegisterSample(SampleId Id, string Name);

    private sealed record SampleRegistered(SampleId Id, string Name);

    // Pure decision: it receives the identifier, it never creates one.
    private static Result<SampleRegistered> Decide(RegisterSample command) =>
        new SampleRegistered(command.Id, command.Name);

    // Command handler: creates the identifier from the injected clock, then decides.
    private static Result<SampleRegistered> Handle(string name, TimeProvider time) =>
        Decide(new RegisterSample(SampleId.New(time), name));

    // Query side: untrusted route or tool input becomes a typed identifier or a Validation error (400 from 0.9).
    private static Result<string> Retrieve(string? routeValue) =>
        SampleId.TryCreate(routeValue).Map(id => $"sample {id}");

    [Fact]
    public void A_handler_creates_the_identifier_before_deciding()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero));

        var registered = Handle("First sample", time).Value;

        TypedId.CreatedAt(registered.Id).ShouldBe(time.GetUtcNow());
    }

    [Fact]
    public void Untrusted_input_becomes_a_validation_error()
    {
        var result = Retrieve("not-a-guid");

        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("shared.invalid-identifier");
    }

    [Fact]
    public void Valid_input_becomes_a_typed_identifier() =>
        Retrieve("019a5a3e-4f2b-7c3d-8e9f-0a1b2c3d4e5f").Value.ShouldBe("sample 019a5a3e-4f2b-7c3d-8e9f-0a1b2c3d4e5f");
}
