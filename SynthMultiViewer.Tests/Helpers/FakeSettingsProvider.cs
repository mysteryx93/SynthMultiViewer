
namespace HanumanInstitute.SynthMultiViewer.Tests;

/// <summary>
/// Keeps settings in memory and records saves for tests.
/// </summary>
internal sealed class FakeSettingsProvider : ISettingsProvider<AppSettingsData>
{
    public AppSettingsData Value { get; set; } = new();
    public int SaveCount { get; private set; }
#pragma warning disable 67
    public event EventHandler? Changed;
    public event EventHandler? Saving;
#pragma warning restore 67
    public AppSettingsData Load() => Value;
    public AppSettingsData Load(string path) => Value;
    public void Save()
    {
        Saving?.Invoke(this, EventArgs.Empty);
        SaveCount++;
    }
    public void Save(string path) => Save();
}
