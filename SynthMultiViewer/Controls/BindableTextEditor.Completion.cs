using System.Reflection;
using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Search;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using Splat;

namespace HanumanInstitute.SynthMultiViewer.Controls;

public partial class BindableTextEditor : IAssistSession
{
    /// <summary>
    /// Defines the script language used for completion.
    /// </summary>
    public static readonly StyledProperty<ScriptKind> ScriptKindProperty =
        AvaloniaProperty.Register<BindableTextEditor, ScriptKind>(nameof(ScriptKind));

    /// <summary>
    /// Gets or sets the script language.
    /// </summary>
    public ScriptKind ScriptKind
    {
        get => GetValue(ScriptKindProperty);
        set => SetValue(ScriptKindProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional factory; the locator is used when this is null.
    /// </summary>
    public IScriptLanguageFactory? LanguageFactory { get; set; }

    /// <summary>
    /// Gets the currently displayed result, or null after dismissal.
    /// </summary>
    public Reply? DisplayedReply => _assist?.DisplayedReply;

    internal CompletionWindow? Completion => _assist?.Completion;

    internal OverloadInsightWindow? Insight => _assist?.Insight;

    private EditorAssist? _assist;
    private bool _searchUninstalled;
    private static readonly FieldInfo? SearchPanelField =
        typeof(TextEditor).GetField("searchPanel", BindingFlags.Instance | BindingFlags.NonPublic);

    private void InitializeCompletion()
    {
        _assist = new(this, this);
        _assist.Attach();
        AttachedToVisualTree += (_, _) =>
        {
            _assist.Attach();
            RestoreSearch();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _assist.Detach();
            SearchPanel?.Uninstall();
            _searchUninstalled = true;
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ScriptKindProperty || e.Property == DocumentProperty)
            {
                _assist.Dismiss();
            }
        };
    }

    private IScriptLanguageFactory? ResolveFactory() =>
        LanguageFactory ?? Locator.Current.GetService<IScriptLanguageFactory>();

    /// <inheritdoc />
    public ILanguageService? ResolveService()
    {
        var factory = ResolveFactory();
        if (factory == null || !factory.IsEnabled)
        {
            return null;
        }

        return factory.Create(ScriptKind.ToString());
    }

    /// <inheritdoc />
    public bool AssistanceEnabled => ResolveFactory()?.IsEnabled != false;

    /// <inheritdoc />
    public string? DocumentPath => (DataContext as IEditorViewModel)?.FileName;

    /// <inheritdoc />
    public void RefreshCatalogs() => ResolveFactory()?.Refresh();

    /// <summary>
    /// Cancels pending requests and closes all editor assistance popups.
    /// </summary>
    public void DismissCompletion() => _assist?.Dismiss();

    /// <summary>
    /// Requests completion for the current snapshot and discards replies after any editor state change.
    /// </summary>
    public Task RequestCompletionAsync(bool showCompletion = true, TimeSpan? delay = null) =>
        _assist?.RequestAsync(showCompletion, delay) ?? Task.CompletedTask;

    private void RestoreSearch()
    {
        if (!_searchUninstalled)
        {
            return;
        }

        var panel = SearchPanel.Install(this);
        SearchPanelField?.SetValue(this, panel);
        _searchUninstalled = false;
    }
}
