using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.ApiVapourSynth.Tests;

public class VsHelperTests
{
    [Fact]
    public void BitBlt_PaddedRows_CopiesRowBytesOnly()
    {
        byte[] sourceRows = [1, 2, 3, 4, 99, 99, 5, 6, 7, 8, 99, 99];
        var destination = new byte[16];
        var source = Marshal.AllocHGlobal(sourceRows.Length);
        var target = Marshal.AllocHGlobal(destination.Length);
        Marshal.Copy(sourceRows, 0, source, sourceRows.Length);
        Marshal.Copy(destination, 0, target, destination.Length);
        try
        {
            VsHelper.BitBlt(target, 8, source, 6, 4, 2);
            Marshal.Copy(target, destination, 0, destination.Length);

            Assert.Equal([1, 2, 3, 4, 0, 0, 0, 0, 5, 6, 7, 8, 0, 0, 0, 0], destination);
        }
        finally
        {
            Marshal.FreeHGlobal(source);
            Marshal.FreeHGlobal(target);
        }
    }

    [Fact]
    public void GetApiVersion_Default_ReturnsApi4()
    {
        var version = VsHelper.GetApiVersion();

        Assert.Equal(4 << 16, version);
    }
}
