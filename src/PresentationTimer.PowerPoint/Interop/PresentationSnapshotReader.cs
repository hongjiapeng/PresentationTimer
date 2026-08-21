using PresentationTimer.Core.Models;
using Ppt = Microsoft.Office.Interop.PowerPoint;

namespace PresentationTimer.PowerPoint.Interop;

internal static class PresentationSnapshotReader
{
    internal static PresentationSnapshot Read(Ppt.Application application)
    {
        using var scope = new ComObjectScope();
        Ppt.Presentations presentations = scope.Track(application.Presentations);
        if (presentations.Count == 0)
        {
            return new PresentationSnapshot(
                PresentationConnectionState.NoPresentation,
                null,
                null,
                string.Empty,
                null);
        }

        Ppt.SlideShowWindows windows = scope.Track(application.SlideShowWindows);
        if (windows.Count == 0)
        {
            return new PresentationSnapshot(
                PresentationConnectionState.NoSlideShow,
                null,
                null,
                string.Empty,
                null);
        }

        Ppt.SlideShowWindow window = scope.Track(windows[1]);
        Ppt.SlideShowView view = scope.Track(window.View);
        Ppt.Slide slide = scope.Track(view.Slide);
        Ppt.Presentation presentation = scope.Track(window.Presentation);
        Ppt.Slides slides = scope.Track(presentation.Slides);

        return new PresentationSnapshot(
            PresentationConnectionState.Running,
            slide.SlideIndex,
            slides.Count,
            SpeakerNotesReader.Read(slide, scope),
            null);
    }
}
