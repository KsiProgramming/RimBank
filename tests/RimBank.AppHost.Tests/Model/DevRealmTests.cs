using System.Text.Json;

namespace RimBank.AppHost.Tests;

// The dev-only realm file (ADR 0019, ADR 0022), read as JSON without starting Keycloak.
public sealed class DevRealmTests
{
    private static readonly JsonElement _realm = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Realms", "rimbank-realm.json"))).RootElement;

    private static readonly string[] _operationScopes = ["party-reference-data-directory:read", "party-reference-data-directory:write"];

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

    // Roadmap 1.15: both dev clients get the Party scopes by default, so local flows may call every operation (ADR 0038).
    [Theory]
    [InlineData("rimbank-dev-client")]
    [InlineData("rimbank-mcp-client")]
    public void The_dev_clients_get_the_subject_and_every_operation_scope_by_default(string clientId)
    {
        Client(clientId).GetProperty("defaultClientScopes").EnumerateArray().Select(scope => scope.GetString())
            .ShouldBe(["basic", .. _operationScopes], ignoreOrder: true);
    }

    [Fact]
    public void Every_operation_scope_is_a_client_scope_written_into_the_token()
    {
        foreach (var name in _operationScopes)
        {
            var scope = ClientScope(name);
            scope.GetProperty("protocol").GetString().ShouldBe("openid-connect");
            scope.GetProperty("attributes").GetProperty("include.in.token.scope").GetString().ShouldBe("true", name);
        }
    }

    // A realm that lists its own client scopes gets none of Keycloak's built-in ones, so it declares basic for the
    // subject the actor needs (ADR 0019), and profile and email only so that clients may request them: they add no
    // personal data to the token.
    [Fact]
    public void The_realm_keeps_the_subject_in_tokens_and_adds_no_personal_data()
    {
        ClientScope("basic").GetProperty("protocolMappers").EnumerateArray().ShouldHaveSingleItem()
            .GetProperty("protocolMapper").GetString().ShouldBe("oidc-sub-mapper");
        ClientScope("profile").TryGetProperty("protocolMappers", out _).ShouldBeFalse();
        ClientScope("email").TryGetProperty("protocolMappers", out _).ShouldBeFalse();
    }

    private static JsonElement ClientScope(string name) =>
        _realm.GetProperty("clientScopes").EnumerateArray().Single(scope => scope.GetProperty("name").GetString() == name);

    private static JsonElement Client(string clientId) =>
        _realm.GetProperty("clients").EnumerateArray().Single(client => client.GetProperty("clientId").GetString() == clientId);
}
