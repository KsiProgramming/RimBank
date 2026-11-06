using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace RimBank.Host.Security;

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
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

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

    private static bool IsHttps(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
