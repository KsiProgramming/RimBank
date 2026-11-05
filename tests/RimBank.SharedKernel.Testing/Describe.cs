using System.Text;

namespace RimBank.SharedKernel.Testing;

// Builds the business-readable failure messages of the specification helpers and laws.
internal static class Describe
{
    public static string Events<TEvent>(IEnumerable<TEvent> events)
    {
        var lines = events.Select(domainEvent => $"  {domainEvent}").ToArray();
        return lines.Length == 0 ? "  (no events)" : string.Join(Environment.NewLine, lines);
    }

    public static string Value(object? value) => value is null ? "  (none)" : $"  {value}";

    public static string Section(string title, string body) => $"{title}:{Environment.NewLine}{body}";

    // Records with a collection field compare it by reference, so two events can print the same and still be
    // unequal (ADR 0010). Say so instead of leaving the reader with two identical-looking lists.
    public static string? SameTextHint(string expected, string actual) =>
        expected == actual
            ? "Hint: expected and actual print the same but are not equal. A record field that is a collection compares by reference; give the record value equality (ADR 0010)."
            : null;

    public static string Join(IEnumerable<string?> sections)
    {
        var text = new StringBuilder();
        foreach (var section in sections.Where(section => section is not null))
        {
            if (text.Length > 0)
            {
                text.AppendLine();
            }

            text.Append(section);
        }

        return text.ToString();
    }
}
