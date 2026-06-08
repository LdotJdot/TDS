using Avalonia;
using TDS.ScreenShot.Core.Annotations;

namespace TDS.ScreenShot.UI.Models;

public enum EditOutcome
{
    Cancelled,
    Saved,
    Copied,
    Confirmed,
}

public sealed record EditResult
{
    public required EditOutcome Outcome { get; init; }
    /// <summary>Selection rectangle in the captured bitmap's pixel space. <c>null</c> when cancelled.</summary>
    public Rect? Selection { get; init; }
    /// <summary>Annotations drawn on the image. Empty when cancelled.</summary>
    public IReadOnlyList<Annotation> Annotations { get; init; } = Array.Empty<Annotation>();
    /// <summary>Final rendered bitmap (size = selection size). <c>null</c> when cancelled.</summary>
    public Avalonia.Media.Imaging.Bitmap? Result { get; init; }
    /// <summary>PNG bytes of <see cref="Result"/>. <c>null</c> when cancelled.</summary>
    public byte[]? PngBytes { get; init; }
    /// <summary>File path the user saved to (only when <see cref="EditOutcome.Saved"/>).</summary>
    public string? SavedPath { get; init; }
}