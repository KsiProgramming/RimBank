using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using RimBank.SharedKernel;

namespace RimBank.Problems.Tests;

// The Problem Details shape of tech-stack §5 (ADR 0015).
public sealed class ErrorProblemDetailsTests
{
    private static readonly Error _notFound = Error.NotFound("party.party-not-found", "No party with this identifier.");

    [Fact]
    public void An_error_becomes_problem_details_with_its_urn_status_and_code()
    {
        var problem = ErrorProblemDetails.Create(_notFound, "/PartyReferenceDataDirectory/42/Retrieve");

        problem.Type.ShouldBe("urn:rimbank:problem:party:party-not-found");
        problem.Title.ShouldBe("Not Found");
        problem.Status.ShouldBe(404);
        problem.Detail.ShouldBe("No party with this identifier.");
        problem.Instance.ShouldBe("/PartyReferenceDataDirectory/42/Retrieve");
        problem.Extensions[ProblemExtensions.ErrorCode].ShouldBe("party.party-not-found");
    }

    [Fact]
    public void The_instance_is_left_to_the_host_when_not_given()
    {
        ErrorProblemDetails.Create(_notFound).Instance.ShouldBeNull();
    }

    [Fact]
    public void The_title_is_the_reason_phrase_of_the_status()
    {
        var problem = ErrorProblemDetails.Create(Error.BusinessRule("currentaccount.insufficient-funds", "The balance does not cover the payment."));

        problem.Status.ShouldBe(422);
        problem.Title.ShouldBe("Unprocessable Entity");
    }

    [Fact]
    public void A_validation_error_lists_its_field_errors()
    {
        var error = Error.Validation(
            "party.invalid-party-name",
            "The party name is not valid.",
            new Dictionary<string, IReadOnlyList<string>> { ["name"] = ["The name is required.", "The name is too short."] });

        var problem = ErrorProblemDetails.Create(error).ShouldBeOfType<HttpValidationProblemDetails>();

        problem.Status.ShouldBe(400);
        problem.Errors.ShouldContainKey("name");
        problem.Errors["name"].ShouldBe(["The name is required.", "The name is too short."]);
        problem.Extensions[ProblemExtensions.ErrorCode].ShouldBe("party.invalid-party-name");
    }

    [Fact]
    public void A_validation_error_without_field_errors_has_no_errors_member()
    {
        var problem = ErrorProblemDetails.Create(Error.Validation("shared.validation-failed", "The request is not valid."));

        problem.ShouldNotBeOfType<HttpValidationProblemDetails>();
        problem.Status.ShouldBe(400);
        problem.Extensions.ShouldNotContainKey(ProblemExtensions.Errors);
    }

    [Fact]
    public void Other_kinds_never_carry_errors()
    {
        ErrorProblemDetails.Create(_notFound).ShouldNotBeOfType<HttpValidationProblemDetails>();
    }

    [Fact]
    public void The_http_result_carries_the_same_problem_details()
    {
        var result = _notFound.ToHttpResult().ShouldBeOfType<ProblemHttpResult>();

        result.StatusCode.ShouldBe(404);
        result.ContentType.ShouldBe("application/problem+json");
        result.ProblemDetails.Type.ShouldBe("urn:rimbank:problem:party:party-not-found");
    }

    [Fact]
    public void The_shared_codes_follow_the_convention()
    {
        ErrorProblemDetails.Create(SharedProblems.NotFound).Type.ShouldBe("urn:rimbank:problem:shared:not-found");
        ErrorProblemDetails.Create(SharedProblems.Unexpected).Type.ShouldBe("urn:rimbank:problem:shared:unexpected");
        ErrorProblemDetails.Create(SharedProblems.Unexpected).Status.ShouldBe(500);
    }

    [Fact]
    public void The_authentication_codes_are_a_401_and_a_403()
    {
        var unauthenticated = ErrorProblemDetails.Create(SharedProblems.Unauthenticated);
        var forbidden = ErrorProblemDetails.Create(SharedProblems.Forbidden);

        unauthenticated.Type.ShouldBe("urn:rimbank:problem:shared:unauthenticated");
        unauthenticated.Status.ShouldBe(401);
        unauthenticated.Title.ShouldBe("Unauthorized");
        unauthenticated.Extensions[ProblemExtensions.ErrorCode].ShouldBe("shared.unauthenticated");
        forbidden.Type.ShouldBe("urn:rimbank:problem:shared:forbidden");
        forbidden.Status.ShouldBe(403);
        forbidden.Title.ShouldBe("Forbidden");
        forbidden.Extensions[ProblemExtensions.ErrorCode].ShouldBe("shared.forbidden");
    }
}
