namespace PublicCloudDownloader.Tests.Architecture;

public sealed class AssemblyBoundaryTests
{
    [Fact]
    public void Core_does_not_reference_WPF_or_provider_assemblies()
    {
        var names = typeof(Core.AssemblyMarker).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("PresentationFramework", names);
        Assert.DoesNotContain(names, n => n!.StartsWith("PublicCloudDownloader.Providers.", StringComparison.Ordinal));
    }
}
