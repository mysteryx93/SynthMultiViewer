using System.Diagnostics;
using HanumanInstitute.AviSynthApi;
using HanumanInstitute.VapourSynthApi;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Reads source frames without rendering them and reports cumulative throughput.
/// </summary>
public class ScriptBenchmark
{
    /// <summary>
    /// A cumulative count and processing time for a benchmark run.
    /// </summary>
    /// <param name="Frames">The number of frames processed.</param>
    /// <param name="Total">The number of frames selected for processing.</param>
    /// <param name="Elapsed">The time spent processing frames, excluding script evaluation.</param>
    public readonly record struct Sample(int Frames, int Total, TimeSpan Elapsed);

    /// <summary>
    /// Evaluates a script and processes its selected frames until completion or cancellation.
    /// </summary>
    /// <param name="text">The script text to evaluate.</param>
    /// <param name="path">The source path used to resolve relative paths, or null for an unsaved script.</param>
    /// <param name="kind">The script engine to use.</param>
    /// <param name="start">The zero-based first frame.</param>
    /// <param name="length">The maximum number of frames, or null to process the rest of the clip.</param>
    /// <param name="threads">The VapourSynth worker and concurrent request count; AviSynth uses one.</param>
    /// <param name="progress">Receives periodic cumulative samples during processing.</param>
    /// <param name="cancellation">Stops new requests and lets active frame requests finish.</param>
    /// <returns>The final cumulative sample, including any frames completed before cancellation.</returns>
    public Sample Run(string text, string? path, ScriptKind kind, int start, int? length, int threads, IProgress<Sample> progress, CancellationToken cancellation)
    {
        if (kind == ScriptKind.AviSynth)
        {
            using var script = AvsScript.LoadScript(text, path, convertForDisplay: false);
            return ReadFrames(start, length, script.SourceVideoInfo.FrameCount, 1, progress, cancellation,
                index => { using var frame = script.GetFrame(index, convertForDisplay: false); });
        }

        using var vs = VsScript.LoadScript(text, path, convertForDisplay: false);
        using var output = vs.GetOutput(0);
        output.SetThreadCount(Math.Max(1, threads));
        return ReadFrames(start, length, output.VideoInfo.NumFrames, Math.Max(1, threads), progress, cancellation,
            index => { using var frame = output.GetFrame(index); });
    }

    // Completes all in-flight requests before the source and its environment are released.
    private static Sample ReadFrames(int start, int? length, int frameCount, int threads, IProgress<Sample> progress, CancellationToken cancellation, Action<int> read)
    {
        if (start >= frameCount)
        {
            throw new InvalidOperationException("Start frame is outside the clip.");
        }

        var total = (int)Math.Min((long)frameCount - start, length ?? int.MaxValue);
        var clock = Stopwatch.StartNew();
        var completed = 0;
        long lastReport = 0;
        try
        {
            Parallel.For(start, start + total, new ParallelOptions
            {
                MaxDegreeOfParallelism = threads,
                CancellationToken = cancellation
            }, index =>
            {
                read(index);
                var count = Interlocked.Increment(ref completed);
                var elapsed = clock.ElapsedMilliseconds;
                var previous = Interlocked.Read(ref lastReport);
                if (elapsed - previous >= 250 && Interlocked.CompareExchange(ref lastReport, elapsed, previous) == previous)
                {
                    progress.Report(new Sample(count, total, clock.Elapsed));
                }
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }

        clock.Stop();
        return new Sample(completed, total, clock.Elapsed);
    }
}
