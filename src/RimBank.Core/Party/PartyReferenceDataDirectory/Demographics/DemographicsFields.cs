namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The BIAN names of the Demographics fields and parameters, used as the keys of field errors so HTTP and MCP name
/// them the same way.
/// </summary>
public static class DemographicsFields
{
    /// <summary>The BIAN parameter that names the entry's Demographics.</summary>
    public const string DemographicsId = "demographicsid";

    /// <summary>BIAN <c>Socio-EconomicClassification</c>.</summary>
    public const string SocioEconomicClassification = "Socio-EconomicClassification";

    /// <summary>BIAN <c>Employment.PartyReference</c>.</summary>
    public const string EmploymentPartyReference = "Employment.PartyReference";

    /// <summary>BIAN <c>Employment.InvolvementReference</c>.</summary>
    public const string EmploymentInvolvementReference = "Employment.InvolvementReference";

    /// <summary>BIAN <c>EmploymentHistory</c>.</summary>
    public const string EmploymentHistory = "EmploymentHistory";

    /// <summary>BIAN <c>EducationHistory</c>.</summary>
    public const string EducationHistory = "EducationHistory";

    /// <summary>BIAN <c>ServicingConstraints</c>.</summary>
    public const string ServicingConstraints = "ServicingConstraints";

    /// <summary>RimBank's extension field that says what an Exchange does (ADR 0028).</summary>
    public const string ExchangeActionType = "ExchangeActionType";
}
