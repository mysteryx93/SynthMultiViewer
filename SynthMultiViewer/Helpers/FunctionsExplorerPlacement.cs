namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Session size and screen position of the functions explorer window.
/// </summary>
public partial class FunctionsExplorerPlacement : ReactiveObject
{
    /// <summary>
    /// Gets or sets the restored window width.
    /// </summary>
    [Reactive]
    public partial double Width { get; set; } = 520;

    /// <summary>
    /// Gets or sets the restored window height.
    /// </summary>
    [Reactive]
    public partial double Height { get; set; } = 420;

    /// <summary>
    /// Gets or sets the restored screen X position. Null uses the first-show default.
    /// </summary>
    [Reactive]
    public partial int? Left { get; set; }

    /// <summary>
    /// Gets or sets the restored screen Y position. Null uses the first-show default.
    /// </summary>
    [Reactive]
    public partial int? Top { get; set; }
}
