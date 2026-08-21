using System.Xml.Linq;

namespace PresentationTimer.Remote.Tests.Architecture;

/// <summary>
/// Verifies the dependency direction of the remote infrastructure project.
/// </summary>
[TestClass]
public sealed class ProjectDependencyTests
{
    /// <summary>
    /// Verifies that the Remote project references the shared Core contracts.
    /// </summary>
    [TestMethod]
    public void RemoteProject_ReferencesCoreContract()
    {
        // Arrange
        string repositoryRoot = FindRepositoryRoot();
        string remoteProjectPath = Path.Combine(
            repositoryRoot,
            "src",
            "PresentationTimer.Remote",
            "PresentationTimer.Remote.csproj");
        XDocument project = XDocument.Load(remoteProjectPath);

        // Act
        string[] projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(path => path is not null)
            .Cast<string>()
            .ToArray();

        // Assert
        Assert.HasCount(1, projectReferences);
        Assert.EndsWith(
            @"PresentationTimer.Core\PresentationTimer.Core.csproj",
            projectReferences[0],
            StringComparison.OrdinalIgnoreCase);
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
