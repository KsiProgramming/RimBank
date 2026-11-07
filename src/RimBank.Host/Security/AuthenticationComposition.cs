using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace RimBank.Host;

/// <summary>
/// Authentication of every request to the host: JWT bearer tokens issued by Keycloak, and deny by default
/// (specs/tech-stack.md §1, §10, ADR 0019).
/// </summary>
/// <remarks>
/// <para>
/// The fallback policy requires an authenticated user, so every endpoint a context maps is protected without opting
/// in, and so is a request that matches no endpoint. Only the health probes of <c>RimBank.ServiceDefaults</c> are
/// open to anonymous callers.
/// </para>
/// <para>
/// A missing or invalid token gets the bodiless 401 of the JWT bearer handler (with <c>WWW-Authenticate: Bearer</c>),
/// and a refused policy a bodiless 403; the status-code pages of the Problem Details baseline turn them into
/// <c>shared.unauthenticated</c> and <c>shared.forbidden</c> (ADR 0015).
/// </para>
/// <para>
/// The MCP endpoint is challenged by the MCP authentication scheme instead (<see cref="McpPolicy"/>): its 401 says
/// <c>WWW-Authenticate: Bearer resource_metadata="…"</c>, pointing to the RFC 9728 protected-resource metadata that
/// names the Keycloak realm, so an MCP client can find where to sign in (ADR 0022).
/// </para>
/// </remarks>
public static class AuthenticationComposition
{
    /// <summary>The Keycloak resource name in the AppHost, resolved through service discovery.</summary>
    public const string KeycloakResource = "keycloak";

    /// <summary>The Keycloak realm that issues RimBank's access tokens.</summary>
    public const string Realm = "rimbank";

    /// <summary>The audience every access token must be issued for.</summary>
    public const string Audience = "rimbank-api";

    /// <summary>The claim that identifies the caller: the OpenID Connect subject.</summary>
    public const string SubjectClaim = "sub";

    /// <summary>The claim that identifies the client application the caller used (authorized party).</summary>
    public const string ClientClaim = "azp";

    /// <summary>
    /// The configuration key of the realm's address, for example <c>https://login.example.com/realms/rimbank</c>.
    /// Required outside Development, where it must use HTTPS; in Development the AppHost's Keycloak is found through
    /// service discovery when it is not set.
    /// </summary>
    public const string AuthorityKey = "Authentication:Authority";

    /// <summary>
    /// The configuration key of the authorization server MCP clients are sent to (RFC 9728 <c>authorization_servers</c>),
    /// when it differs from <see cref="AuthorityKey"/>: in Development the AppHost sets it to the Keycloak address that
    /// clients outside the AppHost can reach. Defaults to <see cref="AuthorityKey"/>; outside Development it must use
    /// HTTPS (ADR 0022).
    /// </summary>
    public const string AuthorizationServerKey = "Mcp:AuthorizationServer";

    /// <summary>
    /// The policy of the MCP endpoint: an authenticated caller, challenged by the MCP authentication scheme so that a
    /// 401 tells the client where to find the protected-resource metadata (ADR 0022).
    /// </summary>
    public const string McpPolicy = "Mcp";

    /// <summary>
    /// Adds JWT bearer authentication against the Keycloak realm <see cref="Realm"/> for the audience
    /// <see cref="Audience"/>, and the fallback policy that requires an authenticated user.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// Outside Development, <see cref="AuthorityKey"/> is not set to an HTTPS address: the host refuses to start rather
    /// than fetch signing keys over an unprotected connection.
    /// </exception>
    public static WebApplicationBuilder AddRimBankAuthentication(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var development = builder.Environment.IsDevelopment();
        var authority = builder.Configuration[AuthorityKey];
        if (!development && !IsHttps(authority))
        {
            throw new InvalidOperationException(
                $"Set {AuthorityKey} to the HTTPS address of the '{Realm}' realm, for example https://login.example.com/realms/{Realm}. " +
                "Outside Development, RimBank only trusts signing keys fetched over HTTPS (ADR 0019).");
        }

        var authorizationServers = AuthorizationServers(builder.Configuration, development);

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddKeycloakJwtBearer(KeycloakResource, Realm, options =>
            {
                options.Audience = Audience;
                if (!string.IsNullOrEmpty(authority))
                {
                    options.Authority = authority;
                }

                // Claims keep their JWT names ("sub", not a SOAP claim URI), and the caller's name is its subject.
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = SubjectClaim;

                // The local Keycloak of the AppHost is found through service discovery and serves HTTP; every other
                // environment fetches its metadata over HTTPS from the configured authority.
                options.RequireHttpsMetadata = !development;
            })
            // MCP authorization (RFC 9728): validates through the JWT bearer above, and its challenge points MCP clients
            // to the protected-resource metadata, which it serves itself. The resource is the requested /mcp address.
            .AddMcp(options => options.ResourceMetadata = new ProtectedResourceMetadata
            {
                ResourceName = McpComposition.ServerName,
                AuthorizationServers = authorizationServers,
                BearerMethodsSupported = ["header"],
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(McpPolicy, policy => policy
                .AddAuthenticationSchemes(McpAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        return builder;
    }

    /// <summary>
    /// Adds authentication, authorization and the actor relay to the pipeline, after the Problem Details baseline and
    /// before any endpoint.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseRimBankAuthentication(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<ActorRelay>();

        return app;
    }

    // The realm MCP clients sign in to. Outside Development it is required and uses HTTPS. In Development the AppHost
    // sets it; a host started on its own without it (it then finds Keycloak through service discovery, which a client
    // cannot use) announces no authorization server.
    private static string[] AuthorizationServers(ConfigurationManager configuration, bool development)
    {
        var configured = configuration[AuthorizationServerKey];
        var address = string.IsNullOrEmpty(configured) ? configuration[AuthorityKey] : configured;
        if (development && string.IsNullOrEmpty(address))
        {
            return [];
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var server) || (!development && server.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Set {AuthorizationServerKey} (or {AuthorityKey}) to the address of the '{Realm}' realm that MCP clients sign in to, " +
                $"for example https://login.example.com/realms/{Realm}. Outside Development it must use HTTPS (ADR 0022).");
        }

        return [server.OriginalString];
    }

    private static bool IsHttps(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
