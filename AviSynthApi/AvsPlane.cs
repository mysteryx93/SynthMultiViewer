namespace HanumanInstitute.AviSynthApi;

/// <summary>
/// Provides read-only access to an AviSynth image plane.
/// </summary>
public readonly struct AvsPlane
{
    internal AvsPlane(IntPtr pointer, int stride, int rowSize, int height)
    {
        Pointer = pointer;
        Stride = stride;
        RowSize = rowSize;
        Height = height;
    }

    /// <summary>
    /// Gets the first byte address.
    /// </summary>
    public IntPtr Pointer { get; }

    /// <summary>
    /// Gets the distance between rows.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Gets the meaningful bytes in each row.
    /// </summary>
    public int RowSize { get; }

    /// <summary>
    /// Gets the number of rows.
    /// </summary>
    public int Height { get; }
}
