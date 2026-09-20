# ScriptAssist

Completion, call insight, and hover for **VapourSynth** and **AviSynth**, with an [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) integration and a headless API.

ScriptAssist analyzes text and supplied catalogs. It requires no Python runtime, never executes scripts, and never loads a native core itself.

Targets .NET 10. Uses `Avalonia.AvaloniaEdit` and `HanumanInstitute.Validators`. Add a project reference:

```xml
<ProjectReference Include="path/to/ScriptAssist/ScriptAssist.csproj" />
```

## Attach to an editor

Supply native catalog dumps and AviSynth plugin-folder disk (see [Catalogs](#catalogs)). Include sources are optional.

```csharp
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;

var factory = new ScriptLanguageFactory(vapoursynthNative, avisynthNative, avisynthAutoload);

var assist = new EditorAssist(editor, session);
assist.Attach();
```

`session` is an `IAssistSession` (current language service, path, enablement, refresh). Keep `assist` for the editor's lifetime and call `Dispose()` when finished.

`EditorAssistOptions.Hint` and `Hover` set wrap width, line count, and character cap (`AssistTipSize`). Hover defaults are wider than the completion side panel and also wrap call-insight headers. Function signatures omit the `core.ns.` prefix.

Shortcuts: **Ctrl+Space** completion, **Ctrl+Shift+Space** call insight, **Ctrl+Shift+R** catalog refresh, **Escape** dismiss.

## Use without an editor

```csharp
var service = factory.Create(ScriptLanguageFactory.VapourSynth)!;
Reply reply = await service.GetAsync(text, caret, cancellationToken, documentPath);
```

`Reply` contains completion items, call insight, and hover text. Discard it if the document, caret, language, or path changed while awaiting it. `Create` returns null while `IsEnabled` is false.

```csharp
var extra = await Task.Run(() => ScriptPackages.List(roots, files, cancellationToken), cancellationToken);
IReadOnlyList<BrowseGroup> groups = await factory.BrowseAsync(
    ScriptLanguageFactory.VapourSynth, text, cancellationToken, documentPath, extra);
```

`BrowseAsync` uses the same catalog and snapshot as `GetAsync`. It still runs while `IsEnabled` is false. Pass `extraPackages` from `ScriptPackages.List` so unimported VapourSynth packages can appear in the explorer; they do not appear in completion until the buffer imports them. `ScriptPackages.List` walks plugin and site-package roots on disk; UI hosts should run it off-thread and cache the result by those roots.

## Catalog lifecycle

Catalogs load on first request. Call `factory.Configure(language, catalogKey)` to prefetch or when native library/plugin settings change; choose a key representing those settings. `factory.Refresh()` forces enumeration again. Catalog callbacks run in the background; exceptions produce an empty catalog.

`factory.IsEnabled` defaults to true. When false, `EditorAssist` skips requests, `Configure` only remembers the key, and `Create` returns null. `Refresh()` still re-enumerates catalogs. After re-enabling, the next request uses the current catalog.

## Catalogs

The host copies native metadata (`VsCatalog.Read` / `AvsCatalog.Read`) into dump DTOs. The factory maps those to `Symbol` (`VapourSynthSymbolSource`, `AviSynthSymbolSource`). Do not map plugins in the app.

**VapourSynth:** use `core.<namespace>.<Function>`, one native argument descriptor per array entry, and the API4 return string. Multiple return keys remain untyped.

```csharp
new Symbol("core.std.Crop", ["clip:vnode", "left:int:opt"], ReturnType: "clip:vnode;");
```

**AviSynth:** use bare function names. `AviSynthParameters.Parse` (in `HanumanInstitute.ScriptAssist.AviSynth`) decodes the native parameter format:

```csharp
new Symbol("Crop", AviSynthParameters.Parse("c[left]i[top]i"));
```

## Includes

Pass optional `IIncludeSource` instances to follow external scripts. Each `Read` returns an `IncludeFile` with its resolved full path and text, or `null` when unavailable. The host owns search directories.

```csharp
var factory = new ScriptLanguageFactory(
    vapoursynthNative, avisynthNative, avisynthAutoload, pythonIncludes, avisynthIncludes);
```

`ScriptFiles` uses absolute paths directly; otherwise it tries paths beside `fromPath`, then the supplied directories, through `IFileSystemService`. Missing or unreadable candidates are skipped so later paths are tried. AviSynth uses the supplied filename, including its extension; Python tries `name.py`, `name.pyi`, `name/__init__.py`, and `name/__init__.pyi`. Leading-dot Python imports resolve relative to the importing file. Include plugin or site-packages directories in the host's roots as needed.

Pass the open document's path for sibling imports. Unsaved buffers can still use supplied search roots. Autoload AviSynth scripts are merged inside `AviSynthSymbolSource` via `AviSynthFunctions.Parse` and `UnionByName`.

Imported exports and failed lookups are cached on the language (bounded LRU). The current bind keeps its own import graph, and that document's working set is pinned so a later edit of the same file does not cascade-reread. `Invalidate`, factory `Refresh`, or `Configure` with a new catalog key still drop the cache. Files that fall out of the cache and the working set are read again.

## Scope

Type inference and import parsing are partial. Supported assistance includes core/node members, catalog signatures, local bindings, and imported script function headers. This is not a full Python or AviSynth interpreter.

Outside scope: stdlib/numpy analysis, `__all__`, imported Python class members, array/list element types, map keys, `f.props`, GScript/Eval scopes, and nested FrameEval function environments.

## Tests

Run from the repository root:

```bash
dotnet build ScriptAssist.Tests/ScriptAssist.Tests.csproj
dotnet ScriptAssist.Tests/bin/Debug/net10.0/ScriptAssist.Tests.dll
```

The tests use the xunit.v3 executable runner, rather than `dotnet test` / VSTest. Implementation notes: [AGENTS.md](AGENTS.md). Repository test contract: [../AGENTS.md](../AGENTS.md).
