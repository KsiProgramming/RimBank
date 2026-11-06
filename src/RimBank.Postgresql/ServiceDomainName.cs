using System.Text;
using System.Text.RegularExpressions;

namespace RimBank.Postgresql;

/// <summary>
/// A BIAN service domain name in PascalCase (for example <c>PartyReferenceDataDirectory</c>), and the database names
/// derived from it by convention (ADR 0016): schema <c>party_reference_data_directory</c>, login role
/// <c>rimbank_party_reference_data_directory</c>, connection string <c>ConnectionStrings:PartyReferenceDataDirectory</c>
/// health check <c>postgresql-party-reference-data-directory</c> and local queue <c>party-reference-data-directory</c>.
/// </summary>
public sealed partial record ServiceDomainName
{
    /// <summary>The prefix of every context's database role, so RimBank's roles are easy to tell apart on a shared server.</summary>
    public const string RolePrefix = "rimbank_";

    // PostgreSQL truncates identifiers longer than NAMEDATALEN - 1 bytes; a truncated role would be another role.
    private const int MaxIdentifierLength = 63;

    private ServiceDomainName(string value)
    {
        Value = value;
        SchemaName = Words(value, '_');
        RoleName = RolePrefix + SchemaName;
        QueueName = Words(value, '-');
        HealthCheckName = "postgresql-" + QueueName;
    }

    /// <summary>The service domain name in PascalCase, also the connection string name.</summary>
    public string Value { get; }

    /// <summary>The context's own PostgreSQL schema, for its events and its documents.</summary>
    public string SchemaName { get; }

    /// <summary>The context's own login role. It owns <see cref="SchemaName"/> and is given nothing else.</summary>
    public string RoleName { get; }

    /// <summary>The name of the connection string the context's store uses (<c>ConnectionStrings:&lt;ServiceDomain&gt;</c>).</summary>
    public string ConnectionStringName => Value;

    /// <summary>The name of the context's durable local Wolverine queue, in kebab case (ADR 0017).</summary>
    public string QueueName { get; }

    /// <summary>The name of the store's health check, tagged <c>ready</c>.</summary>
    public string HealthCheckName { get; }

    /// <summary>Reads a service domain name.</summary>
    /// <param name="value">PascalCase ASCII letters only, starting with an upper-case letter, short enough for the role name to fit PostgreSQL's 63-byte identifier limit.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentException">The value is not a PascalCase service domain name.</exception>
    public static ServiceDomainName Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!PascalCase().IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a service domain name: use the BIAN name in PascalCase, ASCII letters only (for example PartyReferenceDataDirectory).",
                nameof(value));
        }

        var name = new ServiceDomainName(value);
        if (name.RoleName.Length > MaxIdentifierLength)
        {
            throw new ArgumentException(
                $"'{value}' is too long: its role '{name.RoleName}' exceeds PostgreSQL's {MaxIdentifierLength}-character identifier limit.",
                nameof(value));
        }

        return name;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string Words(string pascalCase, char separator)
    {
        var words = new StringBuilder(pascalCase.Length + 8);
        foreach (var character in pascalCase)
        {
            if (char.IsUpper(character) && words.Length > 0)
            {
                words.Append(separator);
            }

            words.Append(char.ToLowerInvariant(character));
        }

        return words.ToString();
    }

    [GeneratedRegex("^[A-Z][A-Za-z]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PascalCase();
}
