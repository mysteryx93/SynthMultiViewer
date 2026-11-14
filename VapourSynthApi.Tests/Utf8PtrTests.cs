using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace HanumanInstitute.VapourSynthApi.Tests;

public class Utf8PtrTests
{
    [Fact]
    public void Constructor_String_CopiesNullTerminatedUtf8()
    {
        const string text = "vapoursynth";
        using var pointer = new Utf8Ptr(text);
        var expected = Encoding.UTF8.GetBytes(text + '\0');
        var actual = new byte[expected.Length];

        Marshal.Copy(pointer.ptr, actual, 0, actual.Length);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FromUtf8Ptr_ZeroPointer_ReturnsNull()
    {
        var pointer = IntPtr.Zero;

        var text = Utf8Ptr.FromUtf8Ptr(pointer);

        Assert.Null(text);
    }

    [Fact]
    public void FromUtf8Ptr_MaxLength_StopsBeforeTerminator()
    {
        using var pointer = new Utf8Ptr("abcdef");

        var text = Utf8Ptr.FromUtf8Ptr(pointer.ptr, 3);

        Assert.Equal("abc", text);
    }

    [Fact]
    public void Dispose_ReleasedPointer_IsZero()
    {
        var pointer = new Utf8Ptr("frame");

        pointer.Dispose();

        Assert.Equal(IntPtr.Zero, pointer.ptr);
    }
}
