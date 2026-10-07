using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Reports how a native script engine probe completed.
/// </summary>
public enum FrameworkStatus
{
    /// <summary>
    /// No library was found.
    /// </summary>
    NotFound,

    /// <summary>
    /// A compatible library was loaded.
    /// </summary>
    Detected,

    /// <summary>
    /// A library was found but could not run a script.
    /// </summary>
    Error
}

/// <summary>
/// Reports whether a native script engine was found and which paths were detected.
/// </summary>
public sealed record FrameworkInstall(
    FrameworkStatus Status,
    string? LibraryPath = null,
    IReadOnlyList<string>? PluginDirectories = null,
    string? Message = null,
    string? Version = null,
    string? VersionDetail = null)
{
    /// <summary>
    /// Creates an install report from a found/not-found probe.
    /// </summary>
    public FrameworkInstall(bool found, string? libraryPath = null, IReadOnlyList<string>? pluginDirectories = null)
        : this(found ? FrameworkStatus.Detected : FrameworkStatus.NotFound, libraryPath, pluginDirectories)
    {
    }

    /// <summary>
    /// Gets whether a compatible library was loaded.
    /// </summary>
    public bool Found => Status == FrameworkStatus.Detected;
}

/// <summary>
/// Detects installed VapourSynth and AviSynth libraries.
/// </summary>
public interface IFrameworkDetectionService
{
    /// <summary>
    /// Gets whether VapourSynth can be loaded.
    /// </summary>
    FrameworkInstall VapourSynth { get; }

    /// <summary>
    /// Gets whether AviSynth can be loaded.
    /// </summary>
    FrameworkInstall AviSynth { get; }

    /// <summary>
    /// Probes the libraries the process loader opens and refreshes script-assist catalogs.
    /// </summary>
    void Apply(AppSettingsData settings);
}
