namespace RimBank.Architecture.Tests;

// business-folder-names on disk (mission principle 1, tech-stack §3.4, ADR 0026): a context's namespaces no longer show
// its folders, so the folders themselves are checked. Build output (bin, obj) and the architecture fixtures, which
// hold bad examples on purpose, are left out.
public sealed class BusinessFolderNamesOnDiskTests
{
    private static readonly string[] _topFolders = ["src", "tests"];

    [Fact]
    public void No_source_or_test_folder_has_a_technical_name()
    {
        var root = RepositoryRoot();

        var folders = _topFolders
            .SelectMany(top => Directory.EnumerateDirectories(Path.Combine(root, top), "*", SearchOption.AllDirectories))
            .Select(folder => Path.GetRelativePath(root, folder).Replace('\\', '/'));

        ForbiddenFolders(folders).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("src/RimBank.Core/Party/PartyReferenceDataDirectory/Services")]
    [InlineData("src/RimBank.Api/Party/PartyReferenceDataDirectory/Dtos/Requests")]
    [InlineData("tests/RimBank.Core.Tests/Helpers")]
    public void A_technical_folder_name_is_found(string folder) =>
        ForbiddenFolders([folder]).ShouldHaveSingleItem().ShouldBe(folder);

    [Theory]
    [InlineData("src/RimBank.Core/Party/PartyReferenceDataDirectory/Registration")]
    [InlineData("src/RimBank.Api/bin/Debug/Services")]
    [InlineData("tests/RimBank.Architecture.Fixtures/Bad/Services")]
    public void Business_folders_build_output_and_fixtures_pass(string folder) =>
        ForbiddenFolders([folder]).ShouldBeEmpty();

    /// <summary>The folders, relative to the repository root with '/' separators, whose path holds a forbidden name.</summary>
    public static IReadOnlyList<string> ForbiddenFolders(IEnumerable<string> folders) =>
        [.. folders
            .Select(folder => (Folder: folder, Segments: folder.Split('/')))
            .Where(folder => !folder.Segments.Any(segment => segment is "bin" or "obj" or "RimBank.Architecture.Fixtures"))
            .Where(folder => folder.Segments.Any(segment => ArchitectureRules.ForbiddenFolderNames.Contains(segment)))
            .Select(folder => folder.Folder)];

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "RimBank.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("RimBank.slnx was not found above the test's output folder.");
    }
}
