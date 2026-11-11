using System.Reactive.Linq;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Asks for a preset name and can delete or reset the saved rows.
/// Returns the name when accepted. The encode window stores the arguments.
/// </summary>
public partial class SavePresetViewModel : WorkspaceViewModel, IModalDialogViewModel
{
    private readonly ISettingsProvider<AppSettingsData> _settings;
    private readonly IDialogService _dialogs;
    private bool _refreshing;

    /// <summary>
    /// Creates the save dialog over the stored presets.
    /// </summary>
    /// <param name="settings">The provider that stores named argument presets.</param>
    /// <param name="dialogs">The service that asks before resetting the list.</param>
    public SavePresetViewModel(ISettingsProvider<AppSettingsData> settings, IDialogService dialogs)
    {
        _settings = settings.CheckNotNull();
        _dialogs = dialogs.CheckNotNull();
        DisplayName = "Save Preset";
        this.WhenAnyValue(x => x.Presets.SelectedValue).Subscribe(CopySelectedName);
    }

    /// <summary>
    /// Gets true after acceptance, or null while unconfirmed.
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Gets the named preset choices.
    /// The rows reflect settings. Settings are not read from this list.
    /// </summary>
    public ListItemCollectionView<EncodePreset?> Presets { get; } = new();

    /// <summary>
    /// Gets or sets the name written when the dialog is accepted.
    /// </summary>
    [Reactive]
    public partial string PresetName { get; set; } = "";

    /// <summary>
    /// Fills the list from saved presets, or the examples when none are saved.
    /// </summary>
    /// <param name="name">The name shown in the box. Empty when no preset is selected.</param>
    public SavePresetViewModel Load(string name)
    {
        PresetName = name;
        Show(EncodePreset.StoredOrExamples(_settings.Value.EncodePresets));
        return this;
    }

    /// <summary>
    /// Accepts the name and requests that the dialog close.
    /// </summary>
    public RxCommandVoid Ok => field ??= ReactiveCommand.Create(OkImpl,
        this.WhenAnyValue(x => x.PresetName).Select(name => name.Trim().HasText()));

    /// <summary>
    /// Removes the selected preset from settings and refreshes the list.
    /// </summary>
    public RxCommandVoid Delete => field ??= ReactiveCommand.Create(DeleteImpl,
        this.WhenAnyValue(x => x.Presets.SelectedValue).Select(preset => preset?.Name.HasText() == true));

    /// <summary>
    /// Asks, then replaces the saved presets with the built-in examples.
    /// </summary>
    public RxCommandVoid Reset => field ??= ReactiveCommand.CreateFromTask(ResetAsync);

    // Copies a chosen row into the name box. A list refresh does not.
    private void CopySelectedName(EncodePreset? preset)
    {
        if (_refreshing || preset == null || !preset.Name.HasText())
        {
            return;
        }

        PresetName = preset.Name;
    }

    private void OkImpl()
    {
        PresetName = PresetName.Trim();
        DialogResult = true;
        CloseView();
    }

    // Drops the selected name from settings. An emptied list shows the examples again.
    private void DeleteImpl()
    {
        var name = Presets.SelectedValue?.Name;
        if (!name.HasText())
        {
            return;
        }

        _settings.Value.EncodePresets = EncodePreset.StoredOrExamples(_settings.Value.EncodePresets)
            .Where(preset => preset.Name != name)
            .Select(preset => preset.Copy())
            .ToList();
        _settings.Save();
        Show(EncodePreset.StoredOrExamples(_settings.Value.EncodePresets));
    }

    // Replaces settings with the examples after the user confirms, then shows that list.
    private async Task ResetAsync()
    {
        var confirmed = await _dialogs.ShowMessageBoxAsync(this, "Replace the saved presets with the defaults?", "Reset Presets", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmed == true)
        {
            _settings.Value.EncodePresets = EncodePreset.Examples.Select(preset => preset.Copy()).ToList();
            _settings.Save();
            Show(_settings.Value.EncodePresets);
        }
    }

    // Shows the named presets and highlights the name in the box when it is in the list.
    private void Show(IReadOnlyList<EncodePreset> source)
    {
        _refreshing = true;
        Presets.Clear();
        Presets.AddRange(source
            .Where(preset => preset.Name.HasText())
            .Select(preset => new ListItem<EncodePreset?>(preset.Copy(), preset.Name)));
        Presets.CurrentItem = Presets.FirstOrDefault(item => item.Value?.Name == PresetName);
        _refreshing = false;
    }
}
