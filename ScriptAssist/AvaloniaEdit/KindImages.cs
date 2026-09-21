using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Loads the host glyph for a symbol or explorer-group kind. Missing assets stay null.
/// </summary>
public static class KindImages
{
    private static readonly Dictionary<string, IImage?> Cache = [];
    private static readonly Lock Gate = new();

    /// <summary>
    /// Returns the 16px kind glyph, or null when the host has not packaged it.
    /// </summary>
    public static IImage? Of(SymbolKind kind) => Cached(kind.ToString());

    /// <summary>
    /// Returns the left-list glyph: local, native plugin, script module, or internals.
    /// </summary>
    public static IImage? Of(BrowseGroupKind kind) => kind switch
    {
        BrowseGroupKind.Local => Of(SymbolKind.Local),
        BrowseGroupKind.Module or BrowseGroupKind.Internal => Of(SymbolKind.Namespace),
        BrowseGroupKind.Plugin => Cached(nameof(BrowseGroupKind.Plugin)),
        _ => null
    };

    private static IImage? Cached(string name)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(name, out var image))
            {
                return image;
            }

            image = Load(name);
            Cache[name] = image;
            return image;
        }
    }

    private static IImage? Load(string name)
    {
        try
        {
            var uri = new Uri($"avares://SynthMultiViewer/Assets/Icons/Script-{name}.webp");
            if (!AssetLoader.Exists(uri))
            {
                return null;
            }

            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
