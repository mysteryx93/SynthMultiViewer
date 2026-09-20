namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Type identifiers used by the VapourSynth profile.
/// </summary>
public static class VapourSynthTypes
{
    /// <summary>The vapoursynth module.</summary>
    public static TypeRef Module { get; } = new("vs");

    /// <summary>The script core.</summary>
    public static TypeRef Core { get; } = new("core");

    /// <summary>A video clip.</summary>
    public static TypeRef VideoNode { get; } = new("vnode");

    /// <summary>An audio clip.</summary>
    public static TypeRef AudioNode { get; } = new("anode");

    /// <summary>A video format object.</summary>
    public static TypeRef Format { get; } = new("format");

    /// <summary>A video frame.</summary>
    public static TypeRef VideoFrame { get; } = new("vframe");

    /// <summary>An integer.</summary>
    public static TypeRef Int { get; } = new("int");

    /// <summary>A floating-point number.</summary>
    public static TypeRef Float { get; } = new("float");

    /// <summary>A boolean.</summary>
    public static TypeRef Bool { get; } = new("bool");

    /// <summary>A string.</summary>
    public static TypeRef String { get; } = new("string");

    /// <summary>Completion and hover label for a plugin namespace on core or a clip.</summary>
    internal const string PluginLabel = "plugin";

    /// <summary>A plugin namespace on the core.</summary>
    public static TypeRef Plugin(string ns) => new("plugin:" + ns);

    /// <summary>A plugin namespace bound to a node.</summary>
    public static TypeRef Bound(string ns) => Bound(ns, VideoNode);

    /// <summary>A plugin namespace bound to a video or audio node.</summary>
    public static TypeRef Bound(string ns, TypeRef node) =>
        new((node == AudioNode ? "bound-anode:" : "bound:") + ns);

    /// <summary>An imported Python script module.</summary>
    public static TypeRef Script(string module) => new("script:" + module);

    /// <summary>Gets the imported module id from a script type.</summary>
    public static string? ScriptOf(TypeRef type) =>
        type.Id.StartsWith("script:", StringComparison.Ordinal) ? type.Id["script:".Length..] : null;

    /// <summary>Gets the plugin namespace from a plugin or bound type.</summary>
    public static string? NamespaceOf(TypeRef type)
    {
        if (type.Id.StartsWith("plugin:", StringComparison.Ordinal))
        {
            return type.Id["plugin:".Length..];
        }

        if (type.Id.StartsWith("bound-anode:", StringComparison.Ordinal))
        {
            return type.Id["bound-anode:".Length..];
        }

        if (type.Id.StartsWith("bound:", StringComparison.Ordinal))
        {
            return type.Id["bound:".Length..];
        }

        return null;
    }

    /// <summary>Gets whether the type is a bound plugin.</summary>
    public static bool IsBound(TypeRef type) =>
        type.Id.StartsWith("bound:", StringComparison.Ordinal) ||
        type.Id.StartsWith("bound-anode:", StringComparison.Ordinal);

    /// <summary>Gets the node a bound plugin was taken from.</summary>
    public static TypeRef BoundNode(TypeRef type) =>
        type.Id.StartsWith("bound-anode:", StringComparison.Ordinal) ? AudioNode : VideoNode;

    /// <summary>Gets whether the type is a video or audio node.</summary>
    public static bool IsNode(TypeRef type) => type == VideoNode || type == AudioNode;

    /// <summary>Maps a catalog return string to a type.</summary>
    public static TypeRef FromReturn(string? returnType)
    {
        if (!returnType.HasValue() || returnType == "any")
        {
            return TypeRef.Unknown;
        }

        var parts = returnType.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 1)
        {
            return TypeRef.Unknown;
        }

        var part = parts[0];
        var colon = part.IndexOf(':');
        var type = colon < 0 ? part : part[(colon + 1)..];
        type = type.Replace("[]", "", StringComparison.Ordinal).Replace(":opt", "", StringComparison.Ordinal)
            .Replace(":empty", "", StringComparison.Ordinal);
        return type switch
        {
            "vnode" or "VideoNode" => VideoNode,
            "anode" or "AudioNode" => AudioNode,
            "int" => Int,
            "float" => Float,
            "bool" => Bool,
            "data" or "string" or "str" => String,
            "vframe" or "VideoFrame" => VideoFrame,
            "format" or "VideoFormat" or "Format" => Format,
            "core" or "Core" => Core,
            "func" => new("func"),
            _ => TypeRef.Unknown
        };
    }

    /// <summary>Display name for hover and completion hints; null when there is nothing to show.</summary>
    public static string? Display(TypeRef type)
    {
        if (type.IsUnknown || type.IsRoot)
        {
            return null;
        }

        if (type == VideoNode)
        {
            return "VideoNode";
        }

        if (type == AudioNode)
        {
            return "AudioNode";
        }

        if (type == Core)
        {
            return "Core";
        }

        if (type == Module)
        {
            return "vapoursynth";
        }

        if (type == Format)
        {
            return "VideoFormat";
        }

        if (type == VideoFrame)
        {
            return "VideoFrame";
        }

        if (type == Int)
        {
            return "int";
        }

        if (type == Float)
        {
            return "float";
        }

        if (type == Bool)
        {
            return "bool";
        }

        if (type == String)
        {
            return "str";
        }

        var ns = NamespaceOf(type);
        if (ns != null)
        {
            return PluginLabel;
        }

        var script = ScriptOf(type);
        if (script != null)
        {
            return "module " + script;
        }

        var function = FunctionSymbol(type);
        if (function != null)
        {
            var name = function.Name;
            var dot = name.LastIndexOf('.');
            return dot >= 0 ? name[(dot + 1)..] : name;
        }

        return type.Id;
    }

    /// <summary>Display name for a catalog return string such as <c>vnode</c> or <c>Fraction</c>.</summary>
    public static string? DisplayReturn(string? returnType)
    {
        if (!returnType.HasValue())
        {
            return null;
        }

        var type = returnType.Trim();
        var array = type.EndsWith("[]", StringComparison.Ordinal);
        if (array)
        {
            type = type[..^2];
        }

        var mapped = FromReturn(type);
        if (mapped.IsUnknown && type.IndexOf('|') < 0 && type.IndexOf('=') < 0)
        {
            var dot = type.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < type.Length)
            {
                mapped = FromReturn(type[(dot + 1)..].Trim());
            }
        }

        var display = !mapped.IsUnknown ? Display(mapped)
            : type == "format" ? "VideoFormat" : type;
        if (!display.HasValue())
        {
            return null;
        }

        return array ? display + "[]" : display;
    }

    /// <summary>
    /// Hover/insight type for a native or Python parameter. <c>format:int</c> is a
    /// VideoFormat id; <c>float[]</c> stays an array (BlankClip <c>color</c>).
    /// </summary>
    internal static string? DisplayType(string? name, string? typeKey)
    {
        if (!typeKey.HasValue())
        {
            return null;
        }

        if (name == "format" && typeKey == "int")
        {
            return "VideoFormat";
        }

        return DisplayReturn(typeKey);
    }

    /// <summary>Rewrites a native parameter string for display without changing the name.</summary>
    internal static string DisplayParameter(string parameter)
    {
        var text = parameter.Trim();
        var colon = text.IndexOf(':');
        if (colon <= 0 || colon + 1 >= text.Length)
        {
            return parameter;
        }

        var name = text[..colon];
        var rest = text[(colon + 1)..];
        var extra = rest.IndexOf(':');
        var type = (extra < 0 ? rest : rest[..extra]).Trim();
        var flags = extra < 0 ? "" : rest[extra..];
        var display = DisplayType(name, type);
        if (!display.HasValue() || display == type)
        {
            return parameter;
        }

        return name + ":" + display + flags;
    }

    /// <summary>Copy of <paramref name="symbol"/> with display parameter and return types.</summary>
    internal static Symbol ForDisplay(Symbol symbol)
    {
        string[]? mapped = null;
        if (symbol.Parameters != null)
        {
            for (var i = 0; i < symbol.Parameters.Length; i++)
            {
                var pretty = DisplayParameter(symbol.Parameters[i]);
                if (pretty == symbol.Parameters[i])
                {
                    continue;
                }

                mapped ??= [..symbol.Parameters];
                mapped[i] = pretty;
            }
        }

        var ret = symbol.ReturnType;
        var displayRet = DisplayReturn(ret);
        if (displayRet.HasValue() && displayRet != ret)
        {
            ret = displayRet;
        }

        if (mapped == null && ret == symbol.ReturnType)
        {
            return symbol;
        }

        return symbol with { Parameters = mapped ?? symbol.Parameters, ReturnType = ret };
    }

    private const char Field = '\x1e';
    private const char Param = '\x1f';
    private const string FunctionPrefix = "fn:";
    private const string BoundFunctionPrefix = "fn-bound:";

    /// <summary>A snapshot of a resolved plugin, host, or local function not yet called.</summary>
    internal static TypeRef Function(Symbol symbol, bool bound = false)
    {
        var payload = string.Concat(bound ? BoundFunctionPrefix : FunctionPrefix, symbol.Name, Field,
            symbol.ReturnType ?? "");
        if (symbol.Parameters == null)
        {
            return new(payload);
        }

        return new(string.Concat(payload, Field, string.Join(Param, symbol.Parameters)));
    }

    /// <summary>Gets the function snapshot stored by <see cref="Function"/>.</summary>
    internal static Symbol? FunctionSymbol(TypeRef type)
    {
        var id = type.Id;
        var prefix = id.StartsWith(BoundFunctionPrefix, StringComparison.Ordinal) ? BoundFunctionPrefix
            : id.StartsWith(FunctionPrefix, StringComparison.Ordinal) ? FunctionPrefix : null;
        if (prefix == null)
        {
            return null;
        }

        var payload = id[prefix.Length..];
        var first = payload.IndexOf(Field);
        if (first < 0)
        {
            return null;
        }

        var name = payload[..first];
        var second = payload.IndexOf(Field, first + 1);
        if (second < 0)
        {
            var unknownReturn = payload[(first + 1)..];
            return new(name, null, ReturnType: unknownReturn.Length == 0 ? null : unknownReturn);
        }

        var returnType = payload[(first + 1)..second];
        var joined = payload[(second + 1)..];
        var parameters = joined.Length == 0 ? Array.Empty<string>() : joined.Split(Param);
        return new(name, parameters, ReturnType: returnType.Length == 0 ? null : returnType);
    }

    /// <summary>Gets whether a function alias was taken from a bound plugin.</summary>
    internal static bool IsBoundFunction(TypeRef type) =>
        type.Id.StartsWith(BoundFunctionPrefix, StringComparison.Ordinal);

    /// <summary>Maps a Python annotation or last identifier to a type.</summary>
    public static TypeRef FromAnnotation(string? annotation)
    {
        if (!annotation.HasValue())
        {
            return TypeRef.Unknown;
        }

        return AnnotationName(annotation) switch
        {
            "VideoNode" => VideoNode,
            "AudioNode" => AudioNode,
            "VideoFrame" => VideoFrame,
            "VideoFormat" or "Format" => Format,
            "Core" => Core,
            "int" => Int,
            "float" => Float,
            "bool" => Bool,
            "str" or "string" => String,
            _ => TypeRef.Unknown
        };
    }

    /// <summary>Last identifier of a Python annotation after unwrapping quotes, <c>Optional</c>, and <c>| None</c>.</summary>
    internal static string? AnnotationName(string? annotation)
    {
        if (!annotation.HasValue())
        {
            return null;
        }

        var text = UnwrapAnnotation(annotation.Trim());
        if (text == null)
        {
            return null;
        }

        var last = text.LastIndexOf('.');
        if (last >= 0)
        {
            text = text[(last + 1)..];
        }

        return text.Length == 0 ? null : text;
    }

    private static string? UnwrapAnnotation(string text)
    {
        for (var n = 0; n < 4; n++)
        {
            text = StripQuotes(text.Trim());
            var union = UnionMember(text);
            if (union == null)
            {
                return null;
            }

            if (union != text)
            {
                text = union;
                continue;
            }

            var inner = OptionalInner(text);
            if (inner == null)
            {
                return text;
            }

            text = inner;
        }

        return text;
    }

    private static string StripQuotes(string text)
    {
        if (text.Length >= 2 && text[0] is '"' or '\'' && text[^1] == text[0])
        {
            return text[1..^1].Trim();
        }

        return text;
    }

    private static string? UnionMember(string text)
    {
        if (text.IndexOf('|') < 0)
        {
            return text;
        }

        string? kept = null;
        var start = 0;
        var depth = 0;
        var pipes = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : '|';
            if (c is '[' or '(')
            {
                depth++;
            }
            else if (c is ']' or ')' && depth > 0)
            {
                depth--;
            }
            else if (c == '|' && depth == 0)
            {
                pipes++;
                var part = text[start..i].Trim();
                start = i + 1;
                if (part.Length == 0 || part == "None")
                {
                    continue;
                }

                if (kept != null)
                {
                    return null;
                }

                kept = part;
            }
        }

        return pipes == 0 ? text : kept;
    }

    private static string? OptionalInner(string text)
    {
        const string optional = "Optional[";
        var start = text.StartsWith(optional, StringComparison.Ordinal) ? 0
            : text.EndsWith(']') ? text.LastIndexOf('.' + optional, StringComparison.Ordinal)
            : -1;
        if (start < 0)
        {
            return null;
        }

        if (start > 0)
        {
            start++;
        }

        if (start + optional.Length >= text.Length || text[^1] != ']')
        {
            return null;
        }

        return text[(start + optional.Length)..^1].Trim();
    }
}
