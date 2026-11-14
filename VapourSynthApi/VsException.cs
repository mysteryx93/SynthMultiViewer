namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Reports an error returned by VapourSynth.
/// </summary>
public class VsException : Exception
{
    /// <summary>
    /// Creates an exception with the native error message.
    /// </summary>
    public VsException(string msg) : base(msg)
    {
    }
}
