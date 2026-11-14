using System.Runtime.InteropServices;

namespace HanumanInstitute.AviSynthApi;

[StructLayout(LayoutKind.Sequential)]
internal struct AvsValue
{
    public short Type;
    public short ArraySize;
    public IntPtr Value;

    public string? GetString() => Value == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(Value);
}
