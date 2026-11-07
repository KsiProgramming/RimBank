using Microsoft.AspNetCore.Routing;
using RimBank.Party.PartyReferenceDataDirectory.Api.Registration;
using RimBank.Party.PartyReferenceDataDirectory.Api.Retrieval;

namespace RimBank.Party.PartyReferenceDataDirectory.Api;

/// <summary>The Party Reference Data Directory's HTTP endpoints, mapped by its host module (ADR 0014).</summary>
public static class PartyReferenceDataDirectoryApi
{
    /// <summary>Maps every implemented BIAN operation of the service domain (ADR 0024).</summary>
    /// <param name="endpoints">The host's endpoints.</param>
    /// <returns>The same endpoints.</returns>
    public static IEndpointRouteBuilder MapPartyReferenceDataDirectoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRegisterParty();
        endpoints.MapRetrievePartyReferenceDataDirectoryEntry();
        return endpoints;
    }
}
