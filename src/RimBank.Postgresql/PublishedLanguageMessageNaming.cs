using System.Reflection;
using Wolverine.Util;

namespace RimBank.Postgresql;

/// <summary>
/// Gives every integration event of a context's <c>.PublishedLanguage</c> its stable wire identity: the value of its
/// <c>public const string MessageIdentity</c> (ADR 0036). The published language stays BCL only, so it carries the
/// identity as a constant rather than a Wolverine attribute, and renaming or moving the CLR type never changes the
/// contract. Every other message keeps Wolverine's default name.
/// </summary>
public sealed class PublishedLanguageMessageNaming : IMessageTypeNaming
{
    /// <summary>The namespace suffix of a context's published language (specs/tech-stack.md §3).</summary>
    public const string PublishedLanguageSuffix = ".PublishedLanguage";

    /// <summary>The name of the constant that holds a published event's message identity.</summary>
    public const string MessageIdentityField = "MessageIdentity";

    private static int _registered;

    /// <summary>Adds this naming ahead of Wolverine's own, once per process: Wolverine's naming is static.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            WolverineMessageNaming.InsertFirst<PublishedLanguageMessageNaming>();
        }
    }

    /// <summary>The message identity a published event declares, or <see langword="null"/> for any other type.</summary>
    /// <param name="messageType">The message type.</param>
    /// <returns>The declared identity, if any.</returns>
    public static string? DeclaredIdentity(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return messageType.Namespace?.EndsWith(PublishedLanguageSuffix, StringComparison.Ordinal) == true
            && messageType.GetField(MessageIdentityField, BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } field
            && field.GetRawConstantValue() is string { Length: > 0 } identity
                ? identity
                : null;
    }

    /// <inheritdoc />
    public bool TryDetermineName(Type messageType, out string messageTypeName)
    {
        messageTypeName = DeclaredIdentity(messageType)!;
        return messageTypeName is not null;
    }
}
