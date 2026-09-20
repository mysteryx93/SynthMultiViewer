using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using HanumanInstitute.MediaSynthUI;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Commits editable combo text on Enter or click-away, and restores it on Escape.
/// </summary>
public static class ComboBoxCommit
{
    /// <summary>
    /// Defines whether the editable combo commits its explicit text binding when editing ends.
    /// </summary>
    public static readonly AttachedProperty<bool> EnabledProperty = AvaloniaProperty.RegisterAttached<ComboBox, bool>("Enabled", typeof(ComboBoxCommit));

    private static readonly ConditionalWeakTable<ComboBox, Subscription> Subscriptions = new();

    static ComboBoxCommit()
    {
        EnabledProperty.Changed.AddClassHandler<ComboBox>((combo, change) =>
        {
            if (Subscriptions.TryGetValue(combo, out var previous))
            {
                previous.Dispose();
                Subscriptions.Remove(combo);
            }
            if (change.NewValue is true)
            {
                Subscriptions.Add(combo, new(combo));
            }
        });
    }

    /// <summary>
    /// Gets whether editing commits the combo text binding.
    /// </summary>
    public static bool GetEnabled(ComboBox combo) => combo.GetValue(EnabledProperty);

    /// <summary>
    /// Sets whether editing commits the combo text binding.
    /// </summary>
    public static void SetEnabled(ComboBox combo, bool value) => combo.SetValue(EnabledProperty, value);

    private sealed class Subscription : IDisposable
    {
        private readonly ComboBox _combo;
        private readonly IDisposable _dropDown;
        private TextBox? _box;
        private Popup? _popup;
        private TopLevel? _root;
        private bool _suppressSelection;
        private bool _clearingSelection;
        private bool _dropDownWasOpen;

        public Subscription(ComboBox combo)
        {
            _combo = combo;
            combo.AttachedToVisualTree += OnAttached;
            combo.DetachedFromVisualTree += OnDetached;
            combo.TemplateApplied += OnTemplateApplied;
            combo.AddHandler(InputElement.PointerPressedEvent, OnDropDownButtonPressed, RoutingStrategies.Tunnel);
            combo.AddHandler(InputElement.PointerReleasedEvent, OnDropDownButtonReleased);
            _dropDown = combo.GetObservable(ComboBox.IsDropDownOpenProperty).Subscribe(OnDropDownOpen);
            Hook();
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => Hook();
        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Unhook();
        private void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            _popup = e.NameScope.Find<Popup>("PART_Popup");
            Hook(e.NameScope.Find<TextBox>("PART_EditableTextBox"));
        }

        private void Hook(TextBox? editor = null)
        {
            Unhook();
            _popup ??= _combo.GetTemplateDescendants().OfType<Popup>().FirstOrDefault(x => x.Name == "PART_Popup");
            _box = editor ?? _combo.GetVisualDescendants().OfType<TextBox>()
                .FirstOrDefault(x => x.Name == "PART_EditableTextBox");
            if (_box != null)
            {
                _box.LostFocus += OnLostFocus;
                _box.KeyDown += OnKeyDown;
                _box.PropertyChanged += OnEditorPropertyChanged;
            }
            _root = TopLevel.GetTopLevel(_combo);
            _root?.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed,
                RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        private void Unhook()
        {
            if (_box != null)
            {
                _box.LostFocus -= OnLostFocus;
                _box.KeyDown -= OnKeyDown;
                _box.PropertyChanged -= OnEditorPropertyChanged;
                _box = null;
            }
            _root?.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            _root = null;
        }

        private void OnDropDownButtonPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!IsDropDownButton(e.Source)) { return; }

            _suppressSelection = true;
            ClearEditorSelection();
        }

        private void OnDropDownButtonReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_combo.IsDropDownOpen)
            {
                _suppressSelection = false;
            }
        }

        private void OnDropDownOpen(bool open)
        {
            if (open)
            {
                _dropDownWasOpen = true;
                _suppressSelection = true;
                ClearEditorSelection();
                return;
            }

            if (!_dropDownWasOpen) { return; }

            _dropDownWasOpen = false;
            _suppressSelection = true;
            ClearEditorSelection();
            // Closing returns focus to the combo, which SelectAlls the editor.
            Dispatcher.UIThread.Post(EndDropDownSelectionSuppress, DispatcherPriority.Input);
        }

        private void EndDropDownSelectionSuppress()
        {
            ClearEditorSelection();
            if (!_combo.IsDropDownOpen)
            {
                _suppressSelection = false;
            }
        }

        private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_suppressSelection &&
                (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty))
            {
                ClearEditorSelection();
            }
        }

        private void ClearEditorSelection()
        {
            if (_box == null || _clearingSelection) { return; }

            var length = _box.Text?.Length ?? 0;
            if (_box.SelectionStart == _box.SelectionEnd && _box.CaretIndex == length) { return; }

            _clearingSelection = true;
            try
            {
                _box.ClearSelection();
                _box.CaretIndex = length;
            }
            finally
            {
                _clearingSelection = false;
            }
        }

        private static bool IsDropDownButton(object? source)
        {
            for (var visual = source as Visual; visual != null; visual = visual.GetVisualParent())
            {
                if (visual is ComboBox) { return false; }
                if (visual is Control { Name: "DropDownOverlay" or "DropDownGlyph" }) { return true; }
            }

            return false;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_combo.IsKeyboardFocusWithin || e.Source is not Visual source ||
                source == _combo || _combo.IsVisualAncestorOf(source)) { return; }

            // Popup events can route through the owner window even though the popup
            // content is outside the combo's visual subtree (including overlay popups).
            if (_popup?.Child is { } child && (source == child || child.IsVisualAncestorOf(source))) { return; }

            Commit();
            _combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            FocusWorkspace();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (_combo.IsDropDownOpen || e.Key is not (Key.Enter or Key.Escape)) { return; }

            if (e.Key == Key.Enter)
            {
                Commit();
            }
            else
            {
                RestoreText();
            }
            FocusWorkspace();
            e.Handled = true;
        }

        private void FocusWorkspace()
        {
            if (_root is not Visual root)
            {
                _root?.FocusManager?.Focus(null);
                return;
            }

            foreach (var zoom in root.GetVisualDescendants().OfType<ZoomViewer>())
            {
                if (zoom.IsEffectivelyVisible && zoom.Focus())
                {
                    return;
                }
            }

            foreach (var editor in root.GetVisualDescendants().OfType<TextEditor>())
            {
                if (editor.IsEffectivelyVisible)
                {
                    editor.TextArea.Focus();
                    return;
                }
            }

            _root.FocusManager?.Focus(null);
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            // Selection transfers focus between the editor and popup while their text
            // is still synchronizing. Commit only after focus has left the whole combo.
            Dispatcher.UIThread.Post(() =>
            {
                if (!_combo.IsDropDownOpen && !_combo.IsKeyboardFocusWithin) { Commit(); }
            }, DispatcherPriority.Input);
        }

        private void Commit()
        {
            if (_box == null) { return; }
            _combo.SetCurrentValue(ComboBox.TextProperty, _box.Text);
            var expression = BindingOperations.GetBindingExpressionBase(_combo, ComboBox.TextProperty);
            expression?.UpdateSource();
            RestoreText();
        }

        private void RestoreText()
        {
            BindingOperations.GetBindingExpressionBase(_combo, ComboBox.TextProperty)?.UpdateTarget();
            // The template's two-way binding may still hold a local edit after normalization.
            _box?.SetCurrentValue(TextBox.TextProperty, _combo.Text);
        }

        public void Dispose()
        {
            Unhook();
            _dropDown.Dispose();
            _combo.RemoveHandler(InputElement.PointerPressedEvent, OnDropDownButtonPressed);
            _combo.RemoveHandler(InputElement.PointerReleasedEvent, OnDropDownButtonReleased);
            _combo.AttachedToVisualTree -= OnAttached;
            _combo.DetachedFromVisualTree -= OnDetached;
            _combo.TemplateApplied -= OnTemplateApplied;
        }
    }
}
