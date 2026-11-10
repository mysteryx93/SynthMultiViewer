using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class AppUpdateServiceTests : ViewerTestsBase
{

    [Theory]
    [InlineData(UpdateInterval.Never, null, false)]
    [InlineData(UpdateInterval.Daily, null, true)]
    [InlineData(UpdateInterval.Daily, 0.5, false)]
    [InlineData(UpdateInterval.Daily, 1.5, true)]
    [InlineData(UpdateInterval.Biweekly, 3.0, false)]
    [InlineData(UpdateInterval.Biweekly, 4.0, true)]
    [InlineData(UpdateInterval.Weekly, 6.0, false)]
    [InlineData(UpdateInterval.Weekly, 8.0, true)]
    [InlineData(UpdateInterval.Bimonthly, 14.0, false)]
    [InlineData(UpdateInterval.Bimonthly, 16.0, true)]
    [InlineData(UpdateInterval.Monthly, 29.0, false)]
    [InlineData(UpdateInterval.Monthly, 31.0, true)]
    public async Task CheckForUpdatesAsync_Interval_QueriesWhenDue(
        UpdateInterval interval, double? daysAgo, bool shouldQuery)
    {
        var now = new DateTime(2026, 1, 15);
        var settings = new FakeSettingsProvider
        {
            Value =
            {
                CheckForUpdates = interval,
                LastCheckForUpdate = daysAgo is { } days ? now.AddDays(-days) : null
            }
        };
        var versions = new Mock<IAppVersionClient>();
        versions.Setup(x => x.QueryVersionAsync()).ReturnsAsync(new AppVersionInfo(new Version(1, 2, 3)));
        var service = CreateService(settings, versions, now: now);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        versions.Verify(x => x.QueryVersionAsync(), Times.Exactly(shouldQuery ? 1 : 0));
        if (shouldQuery)
        {
            Assert.Equal(now, settings.Value.LastCheckForUpdate);
            Assert.Equal(1, settings.SaveCount);
        }
        else
        {
            Assert.Equal(0, settings.SaveCount);
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_QueryFailed_DoesNotSaveLastCheck()
    {
        var settings = new FakeSettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new Mock<IAppVersionClient>();
        var service = CreateService(settings, versions);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        versions.Verify(x => x.QueryVersionAsync(), Times.Once);
        Assert.Null(settings.Value.LastCheckForUpdate);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SameVersion_DoesNotPrompt()
    {
        var dialogs = new FakeDialogManager();
        var settings = new FakeSettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new Mock<IAppVersionClient>();
        versions.Setup(x => x.QueryVersionAsync()).ReturnsAsync(new AppVersionInfo(new Version(1, 2, 3)));
        var process = new Mock<IProcessService>();
        var service = CreateService(settings, versions, dialogs, process);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        Assert.Equal(0, dialogs.FrameworkDialogCount);
        process.Verify(x => x.OpenBrowserUrl(It.IsAny<string>()), Times.Never);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NewerVersionAccepted_OpensDownloadUrl()
    {
        var dialogs = new FakeDialogManager();
        dialogs.QueueFrameworkResult(true);
        var settings = new FakeSettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new Mock<IAppVersionClient>();
        versions.Setup(x => x.QueryVersionAsync()).ReturnsAsync(new AppVersionInfo(new Version(2, 0, 0), "https://example/download"));
        var process = new Mock<IProcessService>();
        var service = CreateService(settings, versions, dialogs, process);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        var prompt = Assert.IsType<MessageBoxSettings>(dialogs.LastFrameworkSettings);
        Assert.Equal("Update Available!", prompt.Title);
        Assert.Equal(MessageBoxButton.YesNo, prompt.Button);
        process.Verify(x => x.OpenBrowserUrl("https://example/download"), Times.Once);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NewerVersionAcceptedWithoutUrl_OpensReleases()
    {
        var dialogs = new FakeDialogManager();
        dialogs.QueueFrameworkResult(true);
        var settings = new FakeSettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new Mock<IAppVersionClient>();
        versions.Setup(x => x.QueryVersionAsync()).ReturnsAsync(new AppVersionInfo(new Version(2, 0, 0)));
        var process = new Mock<IProcessService>();
        var service = CreateService(settings, versions, dialogs, process);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        process.Verify(x => x.OpenBrowserUrl(AppUpdateService.ReleasesUrl), Times.Once);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NewerVersionDeclined_DoesNotOpenUrl()
    {
        var dialogs = new FakeDialogManager();
        dialogs.QueueFrameworkResult(false);
        var settings = new FakeSettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new Mock<IAppVersionClient>();
        versions.Setup(x => x.QueryVersionAsync()).ReturnsAsync(new AppVersionInfo(new Version(2, 0, 0)));
        var process = new Mock<IProcessService>();
        var service = CreateService(settings, versions, dialogs, process);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        Assert.Equal(1, dialogs.FrameworkDialogCount);
        process.Verify(x => x.OpenBrowserUrl(It.IsAny<string>()), Times.Never);
        Assert.Equal(1, settings.SaveCount);
    }

    private AppUpdateService CreateService(
        FakeSettingsProvider settings,
        Mock<IAppVersionClient> versions,
        FakeDialogManager? dialogs = null,
        Mock<IProcessService>? process = null,
        DateTime? now = null)
    {
        var environment = new FakeEnvironmentService { Now = now ?? new DateTime(2026, 1, 15) };
        return new(
            settings,
            new HanumanInstitute.MvvmDialogs.Avalonia.DialogService(dialogs ?? new FakeDialogManager()),
            environment,
            versions.Object,
            process?.Object ?? Mock.Of<IProcessService>());
    }
}
