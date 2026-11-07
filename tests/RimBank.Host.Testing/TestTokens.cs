using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace RimBank.Host.Testing;

/// <summary>
/// Access tokens for in-process host tests, signed with an in-memory RSA key that the host is told to trust. The host
/// never fetches Keycloak's metadata in these tests (ADR 0019): <see cref="UseTestTokens"/> (or
/// <see cref="TrustTestTokens"/> with <see cref="Issuer"/> as <c>Authentication:Authority</c>) gives the JWT bearer a
/// fixed configuration.
/// </summary>
public static class TestTokens
{
    public const string Issuer = "https://keycloak.test/realms/rimbank";

    public const string Subject = "f81d4fae-7dec-11d0-a765-00a0c91e6bf6";

    public const string ClientId = "rimbank-dev-client";

    private static readonly RsaSecurityKey _trusted = NewKey("trusted");

    private static readonly RsaSecurityKey _untrusted = NewKey("untrusted");

    /// <summary>Makes the host trust tokens from <see cref="Issue"/> instead of fetching Keycloak's metadata.</summary>
    public static WebApplicationBuilder UseTestTokens(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Configuration[AuthenticationComposition.AuthorityKey] = Issuer;
        builder.Services.TrustTestTokens();
        return builder;
    }

    /// <summary>
    /// Makes the JWT bearer trust tokens from <see cref="Issue"/>, for a host built elsewhere (for example
    /// <c>WebApplicationFactory&lt;Program&gt;</c>). Also set <c>Authentication:Authority</c> to <see cref="Issuer"/>.
    /// </summary>
    public static IServiceCollection TrustTestTokens(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            configuration.SigningKeys.Add(_trusted);
            options.Authority = null;
            options.Configuration = configuration;
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            options.TokenValidationParameters.ValidIssuer = Issuer;
        });
        return services;
    }

    /// <summary>A token the host accepts, unless one of the arguments says otherwise.</summary>
    public static string Issue(
        string? subject = Subject,
        string? clientId = ClientId,
        string audience = AuthenticationComposition.Audience,
        DateTime? expires = null,
        bool trusted = true)
    {
        var claims = new List<Claim>();
        if (subject is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
        }

        if (clientId is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Azp, clientId));
        }

        var expiry = expires ?? DateTime.UtcNow.AddMinutes(5);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = expiry.AddMinutes(-10),
            IssuedAt = expiry.AddMinutes(-10),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(trusted ? _trusted : _untrusted, SecurityAlgorithms.RsaSha256),
        });
    }

    /// <summary>A client that sends a valid token with every request.</summary>
    public static HttpClient Authenticated(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Issue());
        return client;
    }

    /// <summary>A request with <paramref name="token"/> as its bearer token.</summary>
    public static HttpRequestMessage Get(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static RsaSecurityKey NewKey(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };
}
