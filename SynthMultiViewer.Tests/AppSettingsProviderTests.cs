using System.IO.Abstractions.TestingHelpers;
using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class AppSettingsProviderTests
{
    private const string ConfigPath = "/config/settings.json";

    [Fact]
    public void Load_MissingFile_ReturnsDefaultLightTheme()
    {
        var provider = CreateProvider(new FakeFileSystemService());

        var settings = provider.Load();

        Assert.Equal(AppTheme.Light, settings.Theme);
    }

    [Fact]
    public void Save_RoundTrip_PersistsDarkTheme()
    {
        var files = new FakeFileSystemService();
        var provider = CreateProvider(files);
        provider.Value.Theme = AppTheme.Dark;

        provider.Save();
        var loaded = CreateProvider(files).Load();

        Assert.Equal(AppTheme.Dark, loaded.Theme);
    }

    [Fact]
    public void Save_RoundTrip_PersistsThreadCount()
    {
        var files = new FakeFileSystemService();
        var provider = CreateProvider(files);
        provider.Value.VapourSynthThreads = 8;

        provider.Save();
        var loaded = CreateProvider(files).Load();

        Assert.Equal(8, loaded.VapourSynthThreads);
    }

    [Fact]
    public void Save_RoundTrip_PersistsEnhanceEditorWithAutoComplete()
    {
        var files = new FakeFileSystemService();
        var provider = CreateProvider(files);
        provider.Value.EnhanceEditorWithAutoComplete = false;

        provider.Save();
        var loaded = CreateProvider(files).Load();

        Assert.False(loaded.EnhanceEditorWithAutoComplete);
    }

    [Fact]
    public void Save_RoundTrip_PersistsWindowBounds()
    {
        var files = new FakeFileSystemService();
        var provider = CreateProvider(files);
        provider.Value.Width = 1280;
        provider.Value.Height = 800;
        provider.Value.Maximized = true;

        provider.Save();
        var loaded = CreateProvider(files).Load();

        Assert.Equal(1280, loaded.Width);
        Assert.Equal(800, loaded.Height);
        Assert.True(loaded.Maximized);
    }

    [Fact]
    public void Save_RoundTrip_PersistsRecentFiles()
    {
        var files = new FakeFileSystemService();
        var provider = CreateProvider(files);
        provider.Value.RecentFiles = ["/tmp/a.vpy", "/tmp/b.avs"];

        provider.Save();
        var loaded = CreateProvider(files).Load();

        Assert.Equal(["/tmp/a.vpy", "/tmp/b.avs"], loaded.RecentFiles);
    }

    [Fact]
    public void Save_RoundTrip_PersistsCheckForUpdates()
    {
        var files = new FakeFileSystemService();
        var provider = CreateProvider(files);
        var lastCheck = new DateTime(2026, 1, 10, 8, 30, 0);
        provider.Value.CheckForUpdates = UpdateInterval.Monthly;
        provider.Value.LastCheckForUpdate = lastCheck;

        provider.Save();
        var loaded = CreateProvider(files).Load();

        Assert.Equal(UpdateInterval.Monthly, loaded.CheckForUpdates);
        Assert.Equal(lastCheck, loaded.LastCheckForUpdate);
    }

    [Fact]
    public void Load_MissingFile_ReturnsWeeklyUpdates()
    {
        var provider = CreateProvider(new FakeFileSystemService());

        var settings = provider.Load();

        Assert.Equal(UpdateInterval.Weekly, settings.CheckForUpdates);
        Assert.Null(settings.LastCheckForUpdate);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultSettings()
    {
        var files = new FakeFileSystemService(new Dictionary<string, MockFileData>
        {
            [ConfigPath] = "{ not json"
        });
        var provider = CreateProvider(files);

        var settings = provider.Load();

        Assert.Equal(AppTheme.Light, settings.Theme);
    }

    private static AppSettingsProvider CreateProvider(IFileSystemService files) =>
        new(new SerializationService(files), new FixedAppPath(ConfigPath));

    private sealed class FixedAppPath : IAppPathService
    {
        public FixedAppPath(string path) => ConfigFile = path;

        public string ConfigFile { get; }
    }
}
