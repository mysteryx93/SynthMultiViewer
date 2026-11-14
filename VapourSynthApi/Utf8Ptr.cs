using System.Runtime.InteropServices;
using System.Text;

namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Owns a null-terminated UTF-8 allocation that is released by its finalizer.
/// </summary>
public sealed class Utf8Ptr : IDisposable
{
    /// <summary>
    /// The unmanaged string address, or zero for a null string.
    /// </summary>
    public IntPtr ptr;

    private Utf8Ptr() { }

    /// <summary>
    /// Copies a managed string to a null-terminated UTF-8 allocation.
    /// </summary>
    public Utf8Ptr(string? s)
    {
        if (s == null) { return; }

        var bytes = Encoding.UTF8.GetByteCount(s);
        var buffer = new byte[bytes + 1];
        Encoding.UTF8.GetBytes(s, 0, s.Length, buffer, 0);
        ptr = Marshal.AllocCoTaskMem(bytes + 1);
        Marshal.Copy(buffer, 0, ptr, bytes + 1);
    }

    /// <summary>
    /// Releases the unmanaged UTF-8 allocation.
    /// </summary>
    ~Utf8Ptr() => Dispose();

    /// <summary>
    /// Releases the unmanaged UTF-8 allocation.
    /// </summary>
    public void Dispose()
    {
        if (ptr == IntPtr.Zero) { return; }

        Marshal.FreeCoTaskMem(ptr);
        ptr = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Reads a null-terminated UTF-8 string, returning null for a zero address.
    /// </summary>
    public static string? FromUtf8Ptr(IntPtr pointer) => FromUtf8Ptr(pointer, -1);

    /// <summary>
    /// Reads at most maxLength UTF-8 bytes; a negative limit reads to the terminator.
    /// </summary>
    public static string? FromUtf8Ptr(IntPtr pointer, int maxLength)
    {
        if (pointer == IntPtr.Zero) { return null; }

        var length = 0;
        while ((maxLength < 0 || length < maxLength) && Marshal.ReadByte(pointer, length) != 0)
        {
            length++;
        }

        var data = new byte[length];
        Marshal.Copy(pointer, data, 0, length);
        return Encoding.UTF8.GetString(data);
    }
}
