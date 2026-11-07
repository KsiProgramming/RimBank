using System.Reflection;
using RimBank.Postgresql.FakeContexts.Ledger.Tests;
using RimBank.Postgresql.FakeContexts.Registry.Tests;

namespace RimBank.Postgresql.Tests;

// The fake cores stand in for real ones, so they must hold to the same rule (specs/tech-stack.md §4): handlers,
// commands, queries, messages and ports never mention Marten or Wolverine. The architecture tests enforce it on src/.
public sealed class FakeCoreTests
{
    private static readonly Type[] _coreTypes =
    [
        typeof(OpenFakeLedgerHandler), typeof(PostFakeAmountHandler), typeof(FakeAmountNotedHandler), typeof(RetrieveFakeLedgerHandler),
        typeof(OpenFakeLedger), typeof(PostFakeAmount), typeof(FakeAmountNoted), typeof(RetrieveFakeLedger), typeof(FakeLedgerAcknowledged),
        typeof(IFakeLedgerJournal), typeof(IFakeLedgerEnquiry),
        typeof(RegisterFakeEntryHandler), typeof(RegisterFakeEntry), typeof(FakeEntryAcknowledged), typeof(IFakeRegistryBook),
    ];

    [Fact]
    public void The_fake_cores_reference_neither_Marten_nor_Wolverine()
    {
        var offending = _coreTypes
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .SelectMany(ReferencedTypes)
                .Where(IsInfrastructure)
                .Select(referenced => $"{type.Name} → {referenced.FullName}"))
            .ToList();

        offending.ShouldBeEmpty();
    }

    private static IEnumerable<Type> ReferencedTypes(MemberInfo member) => member switch
    {
        MethodBase method => method.GetParameters().Select(parameter => parameter.ParameterType)
            .Concat(method is MethodInfo info ? [info.ReturnType] : []),
        PropertyInfo property => [property.PropertyType],
        FieldInfo field => [field.FieldType],
        _ => [],
    };

    private static bool IsInfrastructure(Type type) =>
        type.Assembly.GetName().Name is { } assembly
            && (assembly.StartsWith("Marten", StringComparison.Ordinal) || assembly.StartsWith("Wolverine", StringComparison.Ordinal))
        || (type.IsGenericType && type.GetGenericArguments().Any(IsInfrastructure));
}
