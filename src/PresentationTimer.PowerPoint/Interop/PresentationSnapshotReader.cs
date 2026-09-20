using System.Runtime.InteropServices;
using PresentationTimer.Core.Models;
using Ppt = Microsoft.Office.Interop.PowerPoint;

namespace PresentationTimer.PowerPoint.Interop;

internal static class PresentationSnapshotReader
{
    // PowerPoint exposes a SlideShowWindow before its SlideShowView has a current
    // slide, for example while starting a show or changing windows. This is a
    // normal transient state, not a disconnected COM server.
    private const int NoSlideCurrentlyInView = unchecked((int)0x80048240);

    internal static bool IsNoSlideCurrentlyInView(COMException exception) =>
        exception.HResult == NoSlideCurrentlyInView;

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
        Ppt.Slide slide;
        try
        {
            slide = scope.Track(view.Slide);
        }
        catch (COMException exception) when (IsNoSlideCurrentlyInView(exception))
        {
            return new PresentationSnapshot(
                PresentationConnectionState.NoSlideShow,
                null,
                null,
                string.Empty,
                null);
        }

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
