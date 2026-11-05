namespace RimBank.SharedKernel.Tests;

public sealed class SharedKernelAssemblyTests
{
    [Fact]
    public void Shared_kernel_assembly_loads() =>
        typeof(SharedKernelAssemblyMarker).Assembly.GetName().Name.ShouldBe("RimBank.SharedKernel");
}
