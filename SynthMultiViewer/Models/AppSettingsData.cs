namespace HanumanInstitute.SynthMultiViewer.Models;

/// <summary>
/// Contains the application settings.
/// </summary>
public class AppSettingsData
{
    /// <summary>
    /// Gets or sets the visual theme: Light or Dark.
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.Light;

    /// <summary>
    /// Gets or sets the restored main window width.
    /// </summary>
    public double Width { get; set; } = 800;

    /// <summary>
    /// Gets or sets the restored main window height.
    /// </summary>
    public double Height { get; set; } = 450;

    /// <summary>
    /// Gets or sets whether the main window is maximized.
    /// </summary>
    public bool Maximized { get; set; }

    /// <summary>
    /// Gets or sets the VapourSynth worker count when multi-threading is enabled; zero uses the processor count.
    /// </summary>
    public int VapourSynthThreads { get; set; }

    /// <summary>
    /// Gets or sets whether the script editor offers catalog-driven assistance.
    /// </summary>
    public bool EnhanceEditorWithAutoComplete { get; set; } = true;

    /// <summary>
    /// Gets or sets recently opened script paths, newest first.
    /// </summary>
    public List<string> RecentFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets when to automatically check for updates.
    /// </summary>
    public UpdateInterval CheckForUpdates { get; set; } = UpdateInterval.Weekly;

    /// <summary>
    /// Gets or sets the date of the last automatic check for updates.
    /// </summary>
    public DateTime? LastCheckForUpdate { get; set; }

    /// <summary>
    /// Gets or sets the FFmpeg executable. Empty uses ffmpeg on PATH.
    /// </summary>
    public string FFmpegPath { get; set; } = "";

    /// <summary>
    /// Gets or sets the encode argument presets.
    /// </summary>
    public List<EncodePreset> EncodePresets { get; set; } = [];
}
