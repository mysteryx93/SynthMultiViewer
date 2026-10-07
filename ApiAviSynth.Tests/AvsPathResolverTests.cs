using Xunit;

namespace HanumanInstitute.ApiAviSynth.Tests;

public class AvsPathResolverTests
{
    [Fact]
    public void ResolveScriptPath_RelativePath_ReturnsAbsolutePath()
    {
        var relativePath = Path.Combine("scripts", "example.avs");

        var resolvedPath = AvsPathResolver.ResolveScriptPath(relativePath);

        Assert.Equal(Path.GetFullPath(relativePath), resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_AbsolutePath_PreservesAbsolutePath()
    {
        var absolutePath = Path.GetFullPath(Path.Combine("scripts", "example.avs"));

        var resolvedPath = AvsPathResolver.ResolveScriptPath(absolutePath);

        Assert.Equal(absolutePath, resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        var act = () => AvsPathResolver.ResolveScriptPath(path);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void GetBundledPluginDirectories_RootedLibrary_ReturnsSiblingPluginFolders()
    {
        var libraryPath = Path.Combine(Path.GetTempPath(), "avisynth", "libavisynth.so");
        var libDir = Path.GetDirectoryName(libraryPath)!;
        var parent = Directory.GetParent(libDir)!.FullName;

        var directories = AvsPathResolver.GetBundledPluginDirectories(libraryPath);

        Assert.Equal(
            [
                Path.Combine(libDir, "plugins+"),
                Path.Combine(libDir, "plugins"),
                Path.Combine(libDir, "avisynth"),
                Path.Combine(parent, "plugins+"),
                Path.Combine(parent, "plugins"),
                Path.Combine(parent, "avisynth")
            ],
            directories);
    }

    [Fact]
    public void ResolveExistingPath_FileOnDisk_ReturnsAbsolutePath()
    {
        var file = Path.GetTempFileName();
        try
        {
            var resolved = AvsPathResolver.ResolveExistingPath(file);

            Assert.Equal(Path.GetFullPath(file), resolved);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveExistingPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        var act = () => AvsPathResolver.ResolveExistingPath(path);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void LibraryFileNames_CurrentOs_IsTheLoaderName()
    {
        var expected = OperatingSystem.IsWindows() ? "avisynth.dll"
            : OperatingSystem.IsMacOS() ? "libavisynth.dylib" : "libavisynth.so";

        var names = AvsPathResolver.LibraryFileNames;

        Assert.Equal([expected], names);
    }
}
