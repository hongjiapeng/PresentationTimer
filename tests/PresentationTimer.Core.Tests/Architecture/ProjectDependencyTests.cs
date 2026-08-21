using System.Xml.Linq;

namespace PresentationTimer.Core.Tests.Architecture;

/// <summary>
/// Verifies the dependency direction of the domain project.
/// </summary>
[TestClass]
public sealed class ProjectDependencyTests
{
    /// <summary>
    /// Verifies that the Core project has no infrastructure project references.
    /// </summary>
    [TestMethod]
    public void CoreProject_HasNoInfrastructureProjectReferences()
    {
        // Arrange
        string repositoryRoot = FindRepositoryRoot();
        string coreProjectPath = Path.Combine(
            repositoryRoot,
            "src",
            "PresentationTimer.Core",
            "PresentationTimer.Core.csproj");
        XDocument project = XDocument.Load(coreProjectPath);

        // Act
        string[] projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(path => path is not null)
            .Cast<string>()
            .ToArray();

        // Assert
        Assert.IsEmpty(projectReferences);
    }

    /// <summary>
    /// Verifies Office interop remains isolated from Core and App view models.
    /// </summary>
    [TestMethod]
    public void OfficeInterop_IsReferencedOnlyByPowerPointInfrastructure()
    {
        // Arrange
        string repositoryRoot = FindRepositoryRoot();
        string coreProject = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "PresentationTimer.Core",
            "PresentationTimer.Core.csproj"));
        string[] viewModels = Directory.GetFiles(
            Path.Combine(repositoryRoot, "src", "PresentationTimer.App", "ViewModels"),
            "*.cs",
            SearchOption.AllDirectories);

        // Act
        bool coreReferencesOffice = coreProject.Contains("Office.Interop", StringComparison.Ordinal);
        bool viewModelReferencesOffice = viewModels.Any(path =>
            File.ReadAllText(path).Contains("Microsoft.Office", StringComparison.Ordinal));

        // Assert
        Assert.IsFalse(coreReferencesOffice);
        Assert.IsFalse(viewModelReferencesOffice);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PresentationTimer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate PresentationTimer.sln.");
    }
}
