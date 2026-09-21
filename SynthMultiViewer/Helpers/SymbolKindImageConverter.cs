using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Maps a browse or completion kind to the host glyph.
/// </summary>
public sealed class SymbolKindImageConverter : IValueConverter
{
    /// <summary>
    /// Gets the shared converter instance.
    /// </summary>
    public static SymbolKindImageConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            SymbolKind kind => KindImages.Of(kind),
            BrowseGroupKind kind => KindImages.Of(kind),
            _ => null
        };

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
