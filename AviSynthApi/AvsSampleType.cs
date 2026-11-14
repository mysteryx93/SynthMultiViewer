namespace HanumanInstitute.AviSynthApi;

/// <summary>
/// Identifies the storage of one interleaved AviSynth audio channel sample.
/// </summary>
public enum AvsSampleType
{
    /// <summary>
    /// No audio sample format is available.
    /// </summary>
    None = 0,
    /// <summary>
    /// Unsigned 8-bit PCM samples centered on 128.
    /// </summary>
    Int8 = 1,
    /// <summary>
    /// Signed 16-bit integer samples.
    /// </summary>
    Int16 = 2,
    /// <summary>
    /// Signed packed 24-bit integer samples.
    /// </summary>
    Int24 = 4,
    /// <summary>
    /// Signed 32-bit integer samples.
    /// </summary>
    Int32 = 8,
    /// <summary>
    /// IEEE 754 single-precision floating-point samples.
    /// </summary>
    Float = 16
}
