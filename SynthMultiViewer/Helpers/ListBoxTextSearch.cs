using System.Collections;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HanumanInstitute.ScriptAssist;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// ComboBox-style type-ahead on a list: the same letter cycles matches.
/// </summary>
public static class ListBoxTextSearch
{
    /// <summary>
    /// Defines whether letter keys select the next item that starts with that letter.
    /// </summary>
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>("Enabled", typeof(ListBoxTextSearch));

    private static readonly ConditionalWeakTable<ListBox, Subscription> Subscriptions = new();

    static ListBoxTextSearch()
    {
        EnabledProperty.Changed.AddClassHandler<ListBox>((list, change) =>
        {
            if (Subscriptions.TryGetValue(list, out var previous))
            {
                previous.Dispose();
                Subscriptions.Remove(list);
            }

            if (change.NewValue is true)
            {
                Subscriptions.Add(list, new(list));
            }
        });
    }

    /// <summary>
    /// Gets whether type-ahead selection is enabled.
    /// </summary>
    public static bool GetEnabled(ListBox list) => list.GetValue(EnabledProperty);

    /// <summary>
    /// Sets whether type-ahead selection is enabled.
    /// </summary>
    public static void SetEnabled(ListBox list, bool value) => list.SetValue(EnabledProperty, value);

    private sealed class Subscription : IDisposable
    {
        private readonly ListBox _list;
        private string _term = "";
        private DispatcherTimer? _timer;

        private bool _fromKey;

        public Subscription(ListBox list)
        {
            _list = list;
            list.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, true);
            list.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, true);
            list.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel, true);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift) ||
                !TryCharacter(e.Key, out var typed))
            {
                return;
            }

            Type(typed);
            e.Handled = true;
            _fromKey = true;
        }

        private void OnKeyUp(object? sender, KeyEventArgs e) => _fromKey = false;

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (_fromKey)
            {
                e.Handled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(e.Text))
            {
                return;
            }

            Type(e.Text);
            e.Handled = true;
        }

        private void Type(string typed)
        {
            var cycle = _term.Length == 1 &&
                        typed.Length == 1 &&
                        string.Equals(_term, typed, StringComparison.OrdinalIgnoreCase);
            var prefix = cycle ? _term : typed;
            if (!cycle && _term.Length > 0 && AnyMatch(_term + typed))
            {
                prefix = _term + typed;
            }
            else if (!cycle && !AnyMatch(prefix))
            {
                _term = "";
                RestartTimer();
                return;
            }

            _term = prefix;
            SelectMatch(prefix, cycle);
            RestartTimer();
        }

        private bool SelectMatch(string prefix, bool cycle)
        {
            var items = CurrentItems();
            var count = items.Count;
            if (count == 0)
            {
                return false;
            }

            var start = cycle ? Math.Max(_list.SelectedIndex, -1) + 1 : 0;
            for (var n = 0; n < count; n++)
            {
                var index = (start + n) % count;
                if (ItemText(items[index]).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    _list.SelectedIndex = index;
                    _list.ScrollIntoView(index);
                    return true;
                }
            }

            return false;
        }

        private bool AnyMatch(string prefix)
        {
            var items = CurrentItems();
            for (var i = 0; i < items.Count; i++)
            {
                if (ItemText(items[i]).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private IList CurrentItems()
        {
            if (_list.ItemsSource is IList source)
            {
                return source;
            }

            return _list.Items;
        }

        private void RestartTimer()
        {
            _timer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            _timer.Stop();
            _timer.Start();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _term = "";
            _timer?.Stop();
        }

        public void Dispose()
        {
            _list.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            _list.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
            _list.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
            if (_timer != null)
            {
                _timer.Tick -= OnTick;
                _timer.Stop();
            }
        }
    }

    private static bool TryCharacter(Key key, out string text)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            text = key.ToString();
            return true;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            text = ((int)(key - Key.D0)).ToString();
            return true;
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            text = ((int)(key - Key.NumPad0)).ToString();
            return true;
        }

        text = "";
        return false;
    }

    private static string ItemText(object? item) =>
        item switch
        {
            BrowseGroup group => group.Name,
            BrowseFunction function => function.Name,
            FunctionHit hit => hit.Function.Name,
            _ => item?.ToString() ?? ""
        };
}
