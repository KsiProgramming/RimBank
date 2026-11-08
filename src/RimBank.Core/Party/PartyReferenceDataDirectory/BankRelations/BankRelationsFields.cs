namespace RimBank.Party.PartyReferenceDataDirectory;

/// <summary>
/// The BIAN names of the BankRelations fields and parameters, used as the keys of field errors so HTTP and MCP name them
/// the same way (roadmap 1.12, ADR 0035).
/// </summary>
public static class BankRelationsFields
{
    /// <summary>The BIAN parameter that names the entry's BankRelations.</summary>
    public const string BankRelationsId = "bankrelationsid";

    /// <summary>BIAN <c>BankRelationType</c>.</summary>
    public const string BankRelationType = "BankRelationType";

    /// <summary>BIAN <c>BusinessUnitEmployeeReference</c>, an <c>Involvedparty</c> object.</summary>
    public const string BusinessUnitEmployeeReference = "BusinessUnitEmployeeReference";

    /// <summary>BIAN <c>BusinessUnitEmployeeReference.PartyReference</c>: the employee's or unit's directory entry id.</summary>
    public const string BusinessUnitEmployeePartyReference = BusinessUnitEmployeeReference + "." + PartyReferenceFields.PartyReferencePart;

    /// <summary>BIAN <c>BankRelationReference</c>, a <c>PartyRelationship</c> object.</summary>
    public const string BankRelationReference = "BankRelationReference";

    /// <summary>BIAN <c>BankRelationReference.RelationshipStartDate</c>, a <c>Datetime</c> object.</summary>
    public const string RelationshipStartDate = BankRelationReference + ".RelationshipStartDate";

    /// <summary>BIAN <c>BankRelationReference.RelationshipEndDate</c>, a <c>Datetime</c> object.</summary>
    public const string RelationshipEndDate = BankRelationReference + ".RelationshipEndDate";

    /// <summary>BIAN <c>BankRelationReference.RelationshipLifecycleStatus</c>, an ISO 20022 <c>Status</c> object.</summary>
    public const string RelationshipLifecycleStatus = BankRelationReference + ".RelationshipLifecycleStatus";

    /// <summary>The part of a BIAN <c>Datetime</c> object RimBank records.</summary>
    public const string DateTimeContentPart = "DateTimeContent";

    /// <summary>BIAN <c>BankRelationReference.RelationshipStartDate.DateTimeContent</c>.</summary>
    public const string RelationshipStartDateContent = RelationshipStartDate + "." + DateTimeContentPart;

    /// <summary>BIAN <c>BankRelationReference.RelationshipEndDate.DateTimeContent</c>.</summary>
    public const string RelationshipEndDateContent = RelationshipEndDate + "." + DateTimeContentPart;

    /// <summary>BIAN <c>BankRelationReference.RelationshipLifecycleStatus.StatusDescription</c>.</summary>
    public const string RelationshipLifecycleStatusDescription =
        RelationshipLifecycleStatus + "." + PartyReferenceFields.StatusDescriptionPart;
}
