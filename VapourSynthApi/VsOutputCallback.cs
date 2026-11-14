namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Receives completed frames in request order.
/// </summary>
public delegate void VsOutputCallback(VsFrame frame, int index, string errorMsg);
