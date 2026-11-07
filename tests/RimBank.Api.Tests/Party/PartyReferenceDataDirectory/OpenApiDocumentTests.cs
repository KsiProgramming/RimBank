using System.Text.Json;
using Alba;

namespace RimBank.Party.PartyReferenceDataDirectory.Api.Tests;

// Roadmap 1.5 and tech-stack §1: in Development the host serves its OpenAPI document and the Scalar UI to anyone; in
// other environments neither exists.
public sealed class OpenApiDocumentTests(PostgresqlServer server)
{
    private const string Document = "/openapi/v1.json";
    private const string Register = "/PartyReferenceDataDirectory/Register";

    [Fact]
    public async Task In_Development_the_document_describes_Register_with_its_Idempotency_Key_header()
    {
        await using var host = await server.StartHostAsync("Development");

        var result = await host.Alba.Scenario(_ =>
        {
            _.Get.Url(Document);
            _.StatusCodeShouldBeOk();
        });

        var operation = (await result.ReadAsJsonAsync<JsonElement>()).GetProperty("paths").GetProperty(Register).GetProperty("post");
        operation.GetProperty("operationId").GetString().ShouldBe("PartyReferenceDataDirectoryEntry/Register");
        operation.GetProperty("responses").EnumerateObject().Select(response => response.Name)
            .ShouldBe(["200", "400", "401", "409", "500"], ignoreOrder: true);
        var header = operation.GetProperty("parameters").EnumerateArray()
            .Where(parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key")
            .ShouldHaveSingleItem();
        header.GetProperty("in").GetString().ShouldBe("header");
        header.GetProperty("required").GetBoolean().ShouldBeTrue();
        operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public async Task In_Development_the_Scalar_UI_is_served()
    {
        await using var host = await server.StartHostAsync("Development");

        // Scalar redirects /scalar to its page under /scalar/.
        await host.Alba.Scenario(_ =>
        {
            _.Get.Url("/scalar");
            _.StatusCodeShouldBe(302);
            _.Header("Location").SingleValueShouldEqual("scalar/");
        });

        var page = await host.Alba.Scenario(_ =>
        {
            _.Get.Url("/scalar/");
            _.StatusCodeShouldBeOk();
        });

        page.Context.Response.ContentType.ShouldNotBeNull().ShouldStartWith("text/html");
    }

    [Theory]
    [InlineData(Document)]
    [InlineData("/scalar")]
    public async Task Outside_Development_neither_exists(string path)
    {
        await using var host = await server.StartHostAsync();

        await host.Alba.Scenario(_ =>
        {
            _.Get.Url(path);
            _.WithRequestHeader("Authorization", "Bearer " + Host.Testing.TestTokens.Issue());
            _.StatusCodeShouldBe(404);
            _.ContentTypeShouldBe("application/problem+json");
        });
    }
}
