
namespace HanumanInstitute.SynthMultiViewer.Tests;

/// <summary>
/// Supplies predictable command-line, version, and clock values.
/// </summary>
internal sealed class FakeEnvironmentService(IReadOnlyList<string>? arguments = null) : IEnvironmentService
{
    public IReadOnlyList<string> CommandLineArguments { get; set; } = arguments ?? ["viewer"];
    public Version AppVersion { get; set; } = new(1, 2, 3);
    public string ApplicationDataPath { get; } = "/appdata";
    public DateTime Now { get; set; } = new(2026, 1, 15);
}
