using Xunit;

namespace HanumanInstitute.ApiVapourSynth.Tests;

public class VsPathResolverTests
{
    [Fact]
    public void ResolveScriptPath_RelativePath_ReturnsAbsolutePath()
    {
        var relativePath = Path.Combine("scripts", "example.vpy");

        var resolvedPath = VsPathResolver.ResolveScriptPath(relativePath);

        Assert.Equal(Path.GetFullPath(relativePath), resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_AbsolutePath_PreservesAbsolutePath()
    {
        var absolutePath = Path.GetFullPath(Path.Combine("scripts", "example.vpy"));

        var resolvedPath = VsPathResolver.ResolveScriptPath(absolutePath);

        Assert.Equal(absolutePath, resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        var act = () => VsPathResolver.ResolveScriptPath(path);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void GetBundledPluginDirectories_RootedLibrary_ReturnsSiblingPluginFolders()
    {
        var libraryPath = Path.Combine(Path.GetTempPath(), "vapoursynth", "libvsscript.so");
        var libDir = Path.GetDirectoryName(libraryPath)!;
        var parent = Directory.GetParent(libDir)!.FullName;

        var directories = VsPathResolver.GetBundledPluginDirectories(libraryPath);

        Assert.Equal(
            [
                Path.Combine(libDir, "vapoursynth", "plugins"),
                Path.Combine(libDir, "plugins"),
                Path.Combine(libDir, "core", "plugins"),
                Path.Combine(libDir, "vapoursynth"),
                Path.Combine(parent, "vapoursynth", "plugins"),
                Path.Combine(parent, "plugins"),
                Path.Combine(parent, "vapoursynth")
            ],
            directories);
    }

    [Fact]
    public void GetBundledPluginDirectories_RelativeLibrary_ReturnsEmpty()
    {
        const string libraryPath = "libvsscript.so";

        var directories = VsPathResolver.GetBundledPluginDirectories(libraryPath);

        Assert.Empty(directories);
    }

    [Fact]
    public void GetPythonModuleDirectories_AncestorTree_FindsSitePackages()
    {
        var root = Path.Combine(Path.GetTempPath(), "vs-python-" + Guid.NewGuid().ToString("N"));
        var site = Path.Combine(root, "lib", "python3.14", "site-packages");
        var package = Path.Combine(site, "vapoursynth");
        Directory.CreateDirectory(package);
        var libraryPath = Path.Combine(package, "libvapoursynth.so");
        try
        {
            var directories = VsPathResolver.GetPythonModuleDirectories(libraryPath);

            Assert.Contains(site, directories);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetPythonModuleDirectories_LibraryBesidePythonTree_FindsSitePackages()
    {
        var root = Path.Combine(Path.GetTempPath(), "vs-lib-" + Guid.NewGuid().ToString("N"));
        var lib = Path.Combine(root, "lib");
        var site = Path.Combine(lib, "python3.12", "site-packages");
        Directory.CreateDirectory(site);
        var libraryPath = Path.Combine(lib, "libvapoursynth-script.so");
        try
        {
            var directories = VsPathResolver.GetPythonModuleDirectories(libraryPath);

            Assert.Contains(site, directories);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ResolveExistingPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        var act = () => VsPathResolver.ResolveExistingPath(path);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void LibraryFileNames_CurrentOs_IsTheLoaderName()
    {
        var expected = OperatingSystem.IsWindows() ? "VSScript.dll"
            : OperatingSystem.IsMacOS() ? "libvapoursynth-script.dylib" : "libvapoursynth-script.so";

        var names = VsPathResolver.LibraryFileNames;

        Assert.Equal([expected], names);
    }

    [Fact]
    public void LibraryCandidates_ExistingFile_ReplacesBareName()
    {
        const string configured = "/usr/lib/python3.14/site-packages/vapoursynth/libvsscript.so";

        var candidates = VsPathResolver.LibraryCandidates(configured, configuredFileExists: true);

        Assert.Equal([configured], candidates);
    }

    [Fact]
    public void LibraryCandidates_MissingFile_UsesBareName()
    {
        const string configured = "/missing/libvsscript.so";

        var candidates = VsPathResolver.LibraryCandidates(configured, configuredFileExists: false);

        Assert.Equal(VsPathResolver.LibraryFileNames, candidates);
    }
}
