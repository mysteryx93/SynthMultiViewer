using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Moq;

namespace HanumanInstitute.ScriptAssist.Tests;

internal static class AssistHarness
{
    internal static readonly IReadOnlyList<Symbol> Vs =
    [
        new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"], ReturnType: "clip:vnode;"),
        new("core.std.BlankClip",
            ["clip:vnode:opt", "width:int:opt", "height:int:opt", "format:int:opt", "color:float[]:opt"],
            ReturnType: "clip:vnode;"),
        new("core.rife.RIFE", ["clip:vnode", "model:int:opt"], ReturnType: "clip:vnode;"),
        new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"], ReturnType: "clip:vnode;"),
        new("core.svp1.Super", ["clip:vnode"], ReturnType: "clip:vnode;clip:vnode;"),
        new("core.std.AudioTrim", ["clip:anode", "first:int:opt"], ReturnType: "clip:anode;")
    ];

    internal static ISymbolSource Symbols(IReadOnlyList<Symbol> symbols)
    {
        var source = new Mock<ISymbolSource>();
        source.Setup(s => s.Enumerate()).Returns(symbols);
        return source.Object;
    }

    internal static IVapourSynthNativeCatalog VsNative(params VapourSynthFunction[] functions)
    {
        var native = new Mock<IVapourSynthNativeCatalog>();
        native.Setup(n => n.Read()).Returns(functions);
        return native.Object;
    }

    internal static IAviSynthNativeCatalog AvsNative(params AviSynthFilter[] filters)
    {
        var native = new Mock<IAviSynthNativeCatalog>();
        native.Setup(n => n.Read()).Returns(filters);
        return native.Object;
    }

    internal static IScriptDirectory NoScripts()
    {
        var folders = new Mock<IScriptDirectory>();
        folders.Setup(d => d.Roots()).Returns([]);
        folders.Setup(d => d.Files(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>())).Returns([]);
        folders.Setup(d => d.TryRead(It.IsAny<string>())).Returns((string?)null);
        return folders.Object;
    }

    internal static ScriptLanguageFactory Languages(
        IVapourSynthNativeCatalog? vapoursynth = null,
        IAviSynthNativeCatalog? avisynth = null,
        IIncludeSource? vapoursynthIncludes = null,
        IIncludeSource? avisynthIncludes = null,
        IScriptDirectory? autoload = null) =>
        new(vapoursynth ?? VsNative(), avisynth ?? AvsNative(), autoload ?? NoScripts(),
            vapoursynthIncludes, avisynthIncludes);

    internal static CatalogCache Catalog(params Symbol[] symbols) => new(Symbols(symbols));

    internal static IIncludeSource Includes(Func<string, string?, IncludeFile?> read)
    {
        var source = new Mock<IIncludeSource>();
        source.Setup(s => s.Read(It.IsAny<string>(), It.IsAny<string?>())).Returns(read);
        return source.Object;
    }

    internal static LanguageService VsService(IIncludeSource? read = null) =>
        new(new VapourSynthLanguage(read), Catalog());

    internal static LanguageService AvsService(IIncludeSource? read = null) =>
        new(new AviSynthLanguage(read), Catalog());

    internal static IIncludeSource HavsReader(string? text) =>
        FilesReader(text == null ? [] : new Dictionary<string, string> { ["havsfunc"] = text });

    internal static IIncludeSource FilesReader(IReadOnlyDictionary<string, string> files) =>
        Includes((specifier, _) => files.TryGetValue(specifier, out var text)
            ? new IncludeFile("/plugins/" + specifier.TrimStart('.') + ".py", text)
            : null);
}

internal sealed class CountingLanguage(ILanguage inner, ManualResetEventSlim? started = null,
    ManualResetEventSlim? proceed = null) : ILanguage, IPreparedLanguage, IContextHover
{
    public int Binds;

    public LexerOptions Lexer => inner.Lexer;
    public StringComparison Comparison => inner.Comparison;
    public IReadOnlyList<Symbol> Keywords => inner.Keywords;

    public DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, CancellationToken token,
        string? documentPath = null)
    {
        Interlocked.Increment(ref Binds);
        started?.Set();
        proceed?.Wait();
        return inner.Bind(text, catalog, token, documentPath);
    }

    DocumentBindings IPreparedLanguage.Bind(PreparedDocument prepared, IReadOnlyList<Symbol> catalog,
        CancellationToken token, string? documentPath)
    {
        Interlocked.Increment(ref Binds);
        started?.Set();
        proceed?.Wait();
        return inner is IPreparedLanguage preparedLanguage
            ? preparedLanguage.Bind(prepared, catalog, token, documentPath)
            : inner.Bind(prepared.Masked.Code, catalog, token, documentPath);
    }

    public TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog) =>
        inner.TypeOf(segments, bindings, catalog);

    public Thread? LastMembersThread { get; set; }

    public IReadOnlyList<Symbol> Members(TypeRef type, IReadOnlyList<Symbol> catalog, DocumentBindings bindings)
    {
        LastMembersThread = Thread.CurrentThread;
        return inner.Members(type, catalog, bindings);
    }

    public CallResolution? ResolveCall(IReadOnlyList<PathSegment> callee, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog) =>
        inner.ResolveCall(callee, bindings, catalog);

    public HoverInfo? Hover(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog) =>
        inner.Hover(code, path, bindings, catalog);

    HoverInfo? IContextHover.Hover(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog,
        HoverContext? context) =>
        inner is IContextHover hover
            ? hover.Hover(code, path, bindings, catalog, context)
            : inner.Hover(code, path, bindings, catalog);

    public double CompletionPriority(Symbol symbol, TypeRef receiver) => inner.CompletionPriority(symbol, receiver);

    public string? ParameterName(string parameter) => inner.ParameterName(parameter);
}
