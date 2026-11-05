namespace RimBank.Architecture.Tests;

internal static class Namespaces
{
    /// <summary><paramref name="namespaceName"/> is <paramref name="root"/> or one of its child namespaces.</summary>
    public static bool IsWithin(string namespaceName, string root) =>
        namespaceName == root || namespaceName.StartsWith(root + ".", StringComparison.Ordinal);
}
