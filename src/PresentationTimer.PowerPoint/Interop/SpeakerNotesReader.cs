using System.Runtime.InteropServices;
using PresentationTimer.PowerPoint.Notes;
using Ppt = Microsoft.Office.Interop.PowerPoint;

namespace PresentationTimer.PowerPoint.Interop;

internal static class SpeakerNotesReader
{
    internal static string Read(Ppt.Slide slide, ComObjectScope scope)
    {
        var noteBodies = new List<string?>();
        Ppt.SlideRange notesPage = scope.Track(slide.NotesPage);
        Ppt.Shapes shapes = scope.Track(notesPage.Shapes);
        for (int index = 1; index <= shapes.Count; index++)
        {
            Ppt.Shape shape = scope.Track(shapes[index]);
            Ppt.PlaceholderFormat placeholder;
            try
            {
                placeholder = scope.Track(shape.PlaceholderFormat);
            }
            catch (COMException)
            {
                continue;
            }

            if (placeholder.Type is not Ppt.PpPlaceholderType.ppPlaceholderBody and
                not Ppt.PpPlaceholderType.ppPlaceholderVerticalBody)
            {
                continue;
            }

            Ppt.TextFrame textFrame = scope.Track(shape.TextFrame);
            Ppt.TextRange textRange = scope.Track(textFrame.TextRange);
            noteBodies.Add(textRange.Text);
        }

        return SpeakerNotesNormalizer.Normalize(noteBodies);
    }
}
