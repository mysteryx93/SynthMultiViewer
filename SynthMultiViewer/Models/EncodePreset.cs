namespace HanumanInstitute.SynthMultiViewer.Models;

/// <summary>
/// A named FFmpeg argument string for the encode window.
/// </summary>
public class EncodePreset
{
    /// <summary>
    /// Gets the argument strings offered when settings have no saved presets.
    /// They are display until Save, Delete, or Reset writes them.
    /// </summary>
    public static IReadOnlyList<EncodePreset> Examples { get; } =
    [
        new() { Name = "8-bit H.264", Arguments = "-c:v libx264 -crf 18" },
        new() { Name = "10-bit H.265", Arguments = "-c:v libx265 -pix_fmt yuv420p10le -crf 20" },
        new() { Name = "AV1", Arguments = "-c:v libsvtav1 -crf 25" },
        new() { Name = "Audio omitted", Arguments = "-an" }
    ];

    /// <summary>
    /// Gets or sets the name shown in the preset list.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the FFmpeg arguments between the input and the destination.
    /// </summary>
    public string Arguments { get; set; } = "";

    /// <summary>
    /// Returns the saved presets, or the examples when none are saved.
    /// The list shows this catalog. It does not become the catalog.
    /// </summary>
    /// <param name="stored">The presets kept in settings.</param>
    public static IReadOnlyList<EncodePreset> StoredOrExamples(IReadOnlyList<EncodePreset> stored) =>
        stored.Count > 0 ? stored : Examples;

    /// <summary>
    /// Returns a separate preset with the same name and arguments.
    /// The list holds the copy. Settings keep their own instances.
    /// </summary>
    public EncodePreset Copy() => new() { Name = Name, Arguments = Arguments };
}
