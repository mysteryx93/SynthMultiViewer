using System.Globalization;
using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Writes one tab's script to a temporary file and deletes that file when the job ends.
/// </summary>
public sealed class ScriptJobFile : IDisposable
{
    private readonly IFileSystemService _files;
    private bool _deleted;

    /// <summary>
    /// Writes <paramref name="scriptText"/> beside a saved script, or into the system temp directory.
    /// A saved file is named <c>FileName_Guid</c> with the script extension.
    /// </summary>
    /// <param name="files">The file system used to create and remove the script.</param>
    /// <param name="scriptPath">The original script path, or null for an unsaved script.</param>
    /// <param name="scriptText">The script contents to write.</param>
    /// <param name="kind">The engine used to evaluate the script.</param>
    /// <param name="firstFrame">The zero-based first frame to process.</param>
    /// <param name="lastFrame">The inclusive last frame, or null for the rest of the clip.</param>
    public ScriptJobFile(IFileSystemService files, string? scriptPath, string scriptText, ScriptKind kind,
        int firstFrame, int? lastFrame)
    {
        _files = files.CheckNotNull();
        scriptText ??= "";
        kind.CheckEnumValid();
        var extension = Extension(files, scriptPath, kind);
        var directory = scriptPath.HasText()
            ? files.Path.GetDirectoryName(scriptPath)
            : null;
        if (!directory.HasText())
        {
            directory = files.Path.GetTempPath();
        }

        var baseName = scriptPath.HasText()
            ? files.Path.GetFileNameWithoutExtension(scriptPath)
            : "script";
        if (!baseName.HasText())
        {
            baseName = "script";
        }

        var guid = Guid.NewGuid().ToString("N")[..8];
        Path = files.Path.Combine(directory, baseName + "_" + guid + extension);
        WorkingDirectory = directory;
        files.EnsureDirectoryExists(Path);
        files.File.WriteAllText(Path, scriptText + RangeSuffix(kind, firstFrame, lastFrame));
    }

    /// <summary>
    /// Gets the temporary script path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the directory FFmpeg should use so relative sources resolve.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Deletes the temporary script.
    /// </summary>
    public void Dispose()
    {
        if (_deleted)
        {
            return;
        }

        _deleted = true;
        _files.DeleteFileSilent(Path);
    }

    private static string Extension(IFileSystemService files, string? scriptPath, ScriptKind kind)
    {
        if (!scriptPath.HasText())
        {
            return ScriptKindLookup.DefaultExtension(kind);
        }

        var extension = files.Path.GetExtension(scriptPath);
        return extension.HasText() ? extension : ScriptKindLookup.DefaultExtension(kind);
    }

    // Appends a trim only when the window asked for less than the whole clip.
    private static string RangeSuffix(ScriptKind kind, int firstFrame, int? lastFrame)
    {
        var first = Math.Max(0, firstFrame);
        if (first == 0 && !lastFrame.HasValue)
        {
            return "";
        }

        if (kind == ScriptKind.AviSynth)
        {
            var end = lastFrame.HasValue
                ? (-(long)lastFrame.Value + first - 1).ToString(CultureInfo.InvariantCulture)
                : "0";
            return "\nTrim(" + first.ToString(CultureInfo.InvariantCulture) + ", " + end + ")\n";
        }

        var sliceEnd = lastFrame.HasValue
            ? (lastFrame.Value + 1).ToString(CultureInfo.InvariantCulture)
            : "";
        return "\nimport vapoursynth as vs\n" +
               "_vs_job = vs.get_output(0).clip\n" +
               "_vs_job = _vs_job[" + first.ToString(CultureInfo.InvariantCulture) + ":" + sliceEnd + "]\n" +
               "_vs_job.set_output(0)\n";
    }
}
