namespace SyntaxCircus.Common.Tests;

public class PackageShapeTests
{
    [Fact]
    public void Package_declares_no_framework_reference()
    {
        var csproj = Path.Combine(FindRepoRoot(), "src", "SyntaxCircus.Common", "SyntaxCircus.Common.csproj");

        File.ReadAllText(csproj).ShouldNotContain("FrameworkReference");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }
}
