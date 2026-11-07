using System.Text.Json;

namespace RimBank.AppHost.Tests;

// The dev-only realm file (ADR 0019, ADR 0022), read as JSON without starting Keycloak.
public sealed class DevRealmTests
{
    private static readonly JsonElement _realm = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Realms", "rimbank-realm.json"))).RootElement;

    [Fact]
    public void The_dev_mcp_client_is_public_with_pkce_and_loopback_redirects_only()
    {
        var client = Client("rimbank-mcp-client");

        client.GetProperty("publicClient").GetBoolean().ShouldBeTrue();
        client.GetProperty("standardFlowEnabled").GetBoolean().ShouldBeTrue();
        client.GetProperty("implicitFlowEnabled").GetBoolean().ShouldBeFalse();
        client.GetProperty("directAccessGrantsEnabled").GetBoolean().ShouldBeFalse();
        client.GetProperty("serviceAccountsEnabled").GetBoolean().ShouldBeFalse();
        client.TryGetProperty("secret", out _).ShouldBeFalse();
        client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString().ShouldBe("S256");
        client.GetProperty("redirectUris").EnumerateArray().Select(uri => new Uri(uri.GetString()!.TrimEnd('*')))
            .ShouldAllBe(uri => uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp);
    }

    [Fact]
    public void The_dev_mcp_client_gets_tokens_for_the_rimbank_api_audience()
    {
        Client("rimbank-mcp-client").GetProperty("protocolMappers").EnumerateArray()
            .ShouldContain(mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-audience-mapper"
                && mapper.GetProperty("config").GetProperty("included.client.audience").GetString() == "rimbank-api");
    }

    [Fact]
    public void The_dev_user_is_marked_local_only_and_needs_no_first_login_actions()
    {
        var user = _realm.GetProperty("users").EnumerateArray().ShouldHaveSingleItem();

        user.GetProperty("username").GetString().ShouldBe("rimbank-developer");
        user.GetProperty("email").GetString().ShouldEndWith(".invalid");
        var password = user.GetProperty("credentials").EnumerateArray().ShouldHaveSingleItem();
        password.GetProperty("value").GetString().ShouldEndWith("local-only");
        password.GetProperty("temporary").GetBoolean().ShouldBeFalse();
    }

    private static JsonElement Client(string clientId) =>
        _realm.GetProperty("clients").EnumerateArray().Single(client => client.GetProperty("clientId").GetString() == clientId);
}
