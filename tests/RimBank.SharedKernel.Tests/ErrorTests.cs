namespace RimBank.SharedKernel.Tests;

public sealed class ErrorTests
{
    private const string Code = "party.party-not-found";
    private const string Message = "No party with this identifier.";

    public static TheoryData<Func<Error>, ErrorKind> FactoriesByKind => new()
    {
        { () => Error.Validation(Code, Message), ErrorKind.Validation },
        { () => Error.NotFound(Code, Message), ErrorKind.NotFound },
        { () => Error.Conflict(Code, Message), ErrorKind.Conflict },
        { () => Error.BusinessRule(Code, Message), ErrorKind.BusinessRule },
        { () => Error.Unauthorized(Code, Message), ErrorKind.Unauthorized },
        { () => Error.Forbidden(Code, Message), ErrorKind.Forbidden },
        { () => Error.Unexpected(Code, Message), ErrorKind.Unexpected },
    };

    [Theory]
    [MemberData(nameof(FactoriesByKind))]
    public void Each_factory_sets_its_kind_code_and_message(Func<Error> create, ErrorKind expectedKind)
    {
        var error = create();

        error.Kind.ShouldBe(expectedKind);
        error.Code.ShouldBe(Code);
        error.Message.ShouldBe(Message);
        error.FieldErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Errors_with_the_same_code_message_and_kind_are_equal()
    {
        Error.NotFound(Code, Message).ShouldBe(Error.NotFound(Code, Message));
        Error.NotFound(Code, Message).GetHashCode().ShouldBe(Error.NotFound(Code, Message).GetHashCode());
    }

    [Fact]
    public void Errors_with_a_different_kind_are_not_equal() =>
        Error.NotFound(Code, Message).ShouldNotBe(Error.Conflict(Code, Message));

    [Fact]
    public void Validation_error_keeps_its_field_errors()
    {
        var error = Error.Validation(
            "shared.validation-failed",
            "The request is not valid.",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["legalName"] = ["Legal name is required."],
                ["birthDate"] = ["Birth date must be in the past.", "Birth date is required."],
            });

        error.FieldErrors.Count.ShouldBe(2);
        error.FieldErrors["legalName"].ShouldBe(["Legal name is required."]);
        error.FieldErrors["birthDate"].ShouldBe(["Birth date must be in the past.", "Birth date is required."]);
    }

    [Fact]
    public void Field_errors_are_copied_so_later_changes_to_the_input_do_not_leak_in()
    {
        var messages = new List<string> { "Legal name is required." };
        var input = new Dictionary<string, IReadOnlyList<string>> { ["legalName"] = messages };

        var error = Error.Validation("shared.validation-failed", "The request is not valid.", input);
        input["other"] = ["Added later."];
        messages.Add("Added later.");

        error.FieldErrors.Keys.ShouldBe(["legalName"]);
        error.FieldErrors["legalName"].ShouldBe(["Legal name is required."]);
    }

    [Fact]
    public void Field_errors_take_part_in_equality_regardless_of_order()
    {
        Error Create(params (string Field, string[] Messages)[] fields) => Error.Validation(
            "shared.validation-failed",
            "The request is not valid.",
            fields.ToDictionary(f => f.Field, IReadOnlyList<string> (f) => f.Messages));

        var first = Create(("a", ["x"]), ("b", ["y"]));
        var sameInOtherOrder = Create(("b", ["y"]), ("a", ["x"]));
        var differentMessage = Create(("a", ["x"]), ("b", ["z"]));

        first.ShouldBe(sameInOtherOrder);
        first.GetHashCode().ShouldBe(sameInOtherOrder.GetHashCode());
        first.ShouldNotBe(differentMessage);
        first.ShouldNotBe(Error.Validation("shared.validation-failed", "The request is not valid."));
    }

    [Theory]
    [InlineData("party.party-not-found")]
    [InlineData("shared.validation-failed")]
    [InlineData("currentaccount.insufficient-funds")]
    [InlineData("payments2.limit-3-exceeded")]
    [InlineData("a.b")]
    public void Codes_following_the_convention_are_accepted(string code) =>
        Error.Conflict(code, Message).Code.ShouldBe(code);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("party")]
    [InlineData("Party.NotFound")]
    [InlineData("party.party_not_found")]
    [InlineData("party.-not-found")]
    [InlineData("party.not-found-")]
    [InlineData("party.not--found")]
    [InlineData("party.party.not-found")]
    [InlineData(".not-found")]
    [InlineData("party.")]
    [InlineData("1party.not-found")]
    [InlineData("party.not found")]
    public void Codes_breaking_the_convention_are_rejected(string code)
    {
        var exception = Should.Throw<ArgumentException>(() => Error.NotFound(code, Message));

        exception.ParamName.ShouldBe("code");
        exception.Message.ShouldContain("<context>.<error-name>");
    }

    [Fact]
    public void A_null_code_is_rejected() =>
        Should.Throw<ArgumentNullException>(() => Error.NotFound(null!, Message));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_message_is_rejected(string message) =>
        Should.Throw<ArgumentException>(() => Error.NotFound(Code, message)).ParamName.ShouldBe("message");

    [Fact]
    public void A_field_error_without_messages_is_rejected() =>
        Should.Throw<ArgumentException>(() => Error.Validation(
            "shared.validation-failed",
            "The request is not valid.",
            new Dictionary<string, IReadOnlyList<string>> { ["legalName"] = [] }))
            .ParamName.ShouldBe("fieldErrors");

    [Fact]
    public void A_field_error_with_an_empty_field_name_is_rejected() =>
        Should.Throw<ArgumentException>(() => Error.Validation(
            "shared.validation-failed",
            "The request is not valid.",
            new Dictionary<string, IReadOnlyList<string>> { [" "] = ["Required."] }))
            .ParamName.ShouldBe("fieldErrors");

    [Fact]
    public void ToString_shows_kind_code_and_message() =>
        Error.NotFound(Code, Message).ToString().ShouldBe($"NotFound {Code}: {Message}");
}
