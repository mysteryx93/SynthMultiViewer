using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Probes the VapourSynth and AviSynth libraries the process loader opens.
/// </summary>
public sealed class FrameworkDetectionService : IFrameworkDetectionService
{
    private readonly IScriptLanguageFactory? _languages;

    /// <summary>
    /// Creates a detector that probes the system libraries and refreshes script-assist catalogs.
    /// </summary>
    public FrameworkDetectionService(ISettingsProvider<AppSettingsData> settings, IScriptLanguageFactory? languages = null)
    {
        _languages = languages;
        Apply(settings.Value);
    }

    /// <inheritdoc />
    public FrameworkInstall VapourSynth { get; private set; } = new(false);

    /// <inheritdoc />
    public FrameworkInstall AviSynth { get; private set; } = new(false);

    /// <inheritdoc />
    public void Apply(AppSettingsData settings)
    {
        var vapourSynthFound = VsHelper.TryFindLibrary(out var vapourSynthPath);
        var vapourSynthUsable = VsHelper.TryEvaluate(out var vapourSynthError);
        string? vapourSynthVersion = null;
        string? vapourSynthDetail = null;
        if (vapourSynthFound && vapourSynthUsable)
        {
            VsHelper.TryReadVersion(out vapourSynthVersion, out vapourSynthDetail);
        }

        VapourSynth = CreateInstall(
            vapourSynthFound, vapourSynthUsable, vapourSynthPath,
            VsHelper.GetPluginDirectories(vapourSynthPath), vapourSynthError,
            vapourSynthVersion, vapourSynthDetail);

        var aviSynthFound = AvsScript.TryFindLibrary(out var aviSynthPath);
        var aviSynthUsable = AvsScript.TryEvaluate(out var aviSynthError);
        string? aviSynthVersion = null;
        string? aviSynthDetail = null;
        if (aviSynthFound && aviSynthUsable)
        {
            AvsScript.TryReadVersion(out aviSynthVersion, out aviSynthDetail);
        }

        AviSynth = CreateInstall(
            aviSynthFound, aviSynthUsable, aviSynthPath,
            AvsScript.GetPluginDirectories(aviSynthPath), aviSynthError,
            aviSynthVersion, aviSynthDetail);

        if (_languages == null)
        {
            return;
        }

        _languages.IsEnabled = settings.EnhanceEditorWithAutoComplete;
        _languages.Configure(ScriptLanguageFactory.VapourSynth, vapourSynthPath ?? "");
        _languages.Configure(ScriptLanguageFactory.AviSynth, aviSynthPath ?? "");
    }

    private static FrameworkInstall CreateInstall(
        bool found, bool usable, string? libraryPath,
        IReadOnlyList<string> pluginDirectories, string? error,
        string? version = null, string? versionDetail = null)
    {
        if (found && usable)
        {
            return new(
                FrameworkStatus.Detected, libraryPath, pluginDirectories,
                Version: version, VersionDetail: versionDetail);
        }

        if (found)
        {
            return new(
                FrameworkStatus.Error, libraryPath, pluginDirectories,
                FirstMessage(error, "The library could not run a script."));
        }

        return new(FrameworkStatus.NotFound, libraryPath, pluginDirectories);
    }

    private static string FirstMessage(string? error, string fallback) =>
        error.HasText() ? error : fallback;
}
