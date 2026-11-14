namespace HanumanInstitute.AviSynthApi;

/// <summary>
/// Reports an error returned by AviSynth.
/// </summary>
public sealed class AvsException : Exception
{
    /// <summary>
    /// Initializes an exception with the AviSynth error message.
    /// </summary>
    public AvsException(string message) : base(message)
    {
    }
}
