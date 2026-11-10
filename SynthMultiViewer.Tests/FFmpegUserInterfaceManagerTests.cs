using HanumanInstitute.FFmpeg;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FFmpegUserInterfaceManagerTests : ViewerTestsBase
{
    [AvaloniaFact]
    public void AttachProcessWorker_ProcessStarted_OpensProgress()
    {
        var manager = new FFmpegUserInterfaceManager(Dialogs);
        var options = new ProcessOptionsEncoder(ProcessDisplayMode.Interface, "Encoding");
        var worker = new Mock<IProcessWorkerEncoder>();
        worker.As<IProcessWorker>().SetupGet(x => x.Options).Returns(options);
        manager.AttachProcessWorker(null, worker.Object, options);

        worker.Raise(x => x.ProcessStarted += null, worker.Object, new ProcessStartedEventArgs(worker.Object));

        Assert.IsType<EncodeProgressViewModel>(DialogsFake.LastShown);
    }

    [AvaloniaFact]
    public void AttachProcessWorker_ProcessFailed_ShowsOutput()
    {
        var manager = new FFmpegUserInterfaceManager(Dialogs);
        var options = new ProcessOptionsEncoder(ProcessDisplayMode.Interface, "Encoding");
        var worker = new Mock<IProcessWorkerEncoder>();
        worker.As<IProcessWorker>().SetupGet(x => x.Options).Returns(options);
        worker.SetupGet(x => x.Output).Returns("FFmpeg: invalid input");
        manager.AttachProcessWorker(null, worker.Object, options);
        worker.Raise(x => x.ProcessStarted += null, worker.Object, new ProcessStartedEventArgs(worker.Object));

        worker.Raise(x => x.ProcessCompleted += null, worker.Object, new ProcessCompletedEventArgs(CompletionStatus.Failed));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("FFmpeg: invalid input", Assert.IsType<EncodeErrorViewModel>(DialogsFake.LastShown).Message);
    }

    [AvaloniaFact]
    public void ReportStartFailure_StartedWorker_StopsProgress()
    {
        var manager = new FFmpegUserInterfaceManager(Dialogs);
        var options = new ProcessOptionsEncoder(ProcessDisplayMode.Interface, "Encoding");
        var worker = new Mock<IProcessWorkerEncoder>();
        worker.As<IProcessWorker>().SetupGet(x => x.Options).Returns(options);
        manager.AttachProcessWorker(null, worker.Object, options);
        worker.Raise(x => x.ProcessStarted += null, worker.Object, new ProcessStartedEventArgs(worker.Object));
        var progress = Assert.IsType<EncodeProgressViewModel>(DialogsFake.LastShown);

        manager.ReportStartFailure(worker.Object, "FFmpeg executable not found");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(progress.IsRunning);
        Assert.Equal("FFmpeg executable not found", Assert.IsType<EncodeErrorViewModel>(DialogsFake.LastShown).Message);
    }
}
