namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Presents diagnostic output from a failed encoding process.
/// </summary>
public partial class EncodeErrorViewModel : WorkspaceViewModel
{
    /// <summary>
    /// Creates the encoding error window.
    /// </summary>
    public EncodeErrorViewModel() => DisplayName = "Encoding error";

    /// <summary>
    /// Gets or sets the process output or startup error.
    /// </summary>
    [Reactive] public partial string Message { get; set; } = "";
}
