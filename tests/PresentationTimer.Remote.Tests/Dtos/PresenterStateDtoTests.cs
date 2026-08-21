using System.Collections.Immutable;
using System.Text.Json;
using PresentationTimer.Core.Models;
using PresentationTimer.Remote.Dtos;

namespace PresentationTimer.Remote.Tests.Dtos;

/// <summary>Verifies the explicit browser DTO allow-list.</summary>
[TestClass]
public sealed class PresenterStateDtoTests
{
    /// <summary>Verifies serialized presenter state contains only approved fields.</summary>
    [TestMethod]
    public void Serialize_WithSensitiveLookingInternalState_OmitsCredentialsAndDiagnostics()
    {
        // Arrange
        var state = new PresentationSessionState(
            17,
            DateTimeOffset.Parse("2026-08-21T12:00:00Z", null),
            new PresentationSnapshot(PresentationConnectionState.Running, 2, 8, "<b>plain notes</b>", "raw.error"),
            new TimerSnapshot(TimerRunState.Running, TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(-3)),
            new RemoteSessionPublicState(
                RemoteSessionStatus.Ready,
                ImmutableArray.Create(new Uri("http://192.0.2.10:1234")),
                new Uri("http://192.0.2.10:1234"),
                1,
                "remote.internal"));
        PresenterStateDto dto = PresenterStateDto.FromState(state);

        // Act
        string json = JsonSerializer.Serialize(dto, JsonSerializerOptions.Web);
        using JsonDocument document = JsonDocument.Parse(json);
        string[] names = document.RootElement.EnumerateObject()
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        // Assert
        var expectedNames = new List<string>
        {
            "currentSlideIndex",
            "isOvertime",
            "presentationStatus",
            "revision",
            "speakerNotes",
            "timerDisplaySeconds",
            "timerStatus",
            "totalSlides",
        };
        CollectionAssert.AreEqual(expectedNames, names);
        Assert.IsFalse(json.Contains("192.0.2.10", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("raw.error", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("<b>plain notes</b>", document.RootElement.GetProperty("speakerNotes").GetString());
    }
}
