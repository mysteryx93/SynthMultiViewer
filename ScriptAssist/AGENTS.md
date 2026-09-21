# ScriptAssist — implementation notes

Consumer setup and API examples: [README.md](README.md). These notes describe implementation constraints and regression expectations; they do not guarantee complete language support.

## Boundaries

- Keep one assembly. Never reference `ApiVapourSynth`, `ApiAviSynth`, or the app; never load a native core or execute scripts.
- Keep language behavior here, including type inference, `clip.` completion, `Symbol` mapping from dump DTOs, grouping, and browse. The host implements `IVapourSynthNativeCatalog` / `IAviSynthNativeCatalog`, `IScriptDirectory`, and `IIncludeSource`; it does not assemble language rules or map plugins in the app.
- Disk I/O is `IFileSystemService` in `Services/` (System.IO.Abstractions plus a few helpers). That folder is separable. Call `File` / `Directory` / `Path` only through that service when the member exists on `IFileSystem` / `IPath`. `ScriptFiles`, `ScriptPackages`, and include-path math take `IFileSystemService` / `IPath`. Tests use a Fake or Moq, not temp files. `System.IO` leftovers (`IOException`, `FileMode`, `SearchOption`) stay fully qualified. Do not remove `System.IO` from implicit usings.
- Collaborators are native dumps, plugin-folder disk, includes, and `IAssistSession`. Do not take `Func` / `delegate` catalogs, includes, paths, or enablement. Do not add `ITextFile`, a profile-list factory constructor, `IAssistGate`, or settable `AllowRequests` for tests. The factory takes natives and builds `VapourSynthSymbolSource` / `AviSynthSymbolSource`.
- Keep the consumer API centered on `ScriptLanguageFactory`, `GetAsync`, `BrowseAsync`, and `EditorAssist`. The factory owns the built-in language ids and `IsEnabled`.
- `Refresh()` always re-enumerates catalogs, including while `IsEnabled` is false. `Create` returns null while disabled; `EditorAssist` skips requests. `LanguageService` does not take an enablement gate. `BrowseAsync` still works while disabled. `GetAsync` / `Analyze` do not load unimported packages.
- One type per file; the file name matches the type.
- `LanguageService` and `CatalogCache` remain language-agnostic. Ranking belongs to `ILanguage.CompletionPriority`; do not special-case clip types in the engine. Member lists are alphabetical; named-argument completions keep a higher priority so they stay above the rest of the list. Do not demote plugin namespaces on `clip.`.
- Unless explicitly requested, exclude full parsers/type checkers, eval/GScript scopes, stdlib/numpy/`__all__`, array/list element types, map keys, imported Python class *methods*, nested FrameEval function environments, and `f.props`. A short Python prelude is bound: `os.path` string/bool helpers after `import os`, and builtins `str`/`int`/`float`/`len`/`hasattr`. Do not grow that into the standard library. Small static-analysis improvements remain appropriate. Type-name exports (Enum/Flag/ABC/Protocol) complete their class-body constants on `Name.` and list those choices as the type's hint (`Enum: A | B`). With no body constants, the hint is `Class bases: ...`, not a blank. A class with no local constructor, dataclass fields, or record fields is a type-name (`Class bases: ...` or `Class`), never a fake `Name()`. TypedDict/NamedTuple use body fields as constructor parameters.

## Structure

| Location | Namespace suffix after `HanumanInstitute.ScriptAssist` | Role |
| --- | --- | --- |
| Project root | none | Public services, factory, catalog and file helpers |
| `Services/` | `.Services` | `IFileSystemService` / `FileSystemService`; extractable |
| `Models/` | none | Public contracts; preserve namespace when moving files |
| `Utilities/` | none | Internal scanning and parsing helpers |
| `AviSynth/` | `.AviSynth` | Public language/parsers; internal binding and type walking |
| `VapourSynth/` | `.VapourSynth` | Public language/parsers; internal binding and type walking |
| `AvaloniaEdit/` | `.AvaloniaEdit` | Editor integration and popup presenters |

- Do not add an `Engine/` folder or share language global usings. Import language namespaces at composition points only.
- Static scanners, splitters, type tables, regex patterns, parsers, and binders are intentional. Do not turn them into services or `Symbol` extension methods.
- Prefer `IReadOnlyList` for stored lists and `IEnumerable` for streamed paths. Existing `Symbol.Parameters` is an array.
- Keep `LanguageService.Analyze` internal. Tests have `InternalsVisibleTo`; consumers use `GetAsync`.
- Use the existing Validators dependency. `HasValue()` means non-null/non-empty, not non-whitespace; preserve whitespace checks and ordinary `Length == 0` checks.

## Analysis and source handling

`GetAsync` awaits the catalog, then analyzes off-thread, including cache hits. A snapshot masks the document and binds names, scopes, and imports. Languages overlay buffer symbols over native catalog entries when resolving members, calls, and hover. Requests overlay scopes at the caret, read the expression, and produce completion, insight, and hover. Inside comments/strings, assistance is suppressed; function headers suppress call insight.

The snapshot key is **text + document path + catalog reference**, not editor version. `LanguageService` retains a small LRU of snapshots (multiple documents, evicting older revisions of the same path) with a retained-byte cap that includes imported modules, scopes, and parameter strings. Concurrent requests for the same key share one in-flight bind; every caller awaits that task through its own token, and a build with no remaining waiters is cancelled. Invalidation drops cached snapshots and the in-flight lookup so the next request cannot observe a stale bind. Binding performs additional scans; do not assume total analysis is linear. There is no incremental parser. Honor cancellation in potentially long scans.

Include cache lifecycle is documented in README; parsed exports and failed path lookups are LRU-bounded, unpinned entries also have a byte cap, the current document's working set is pinned, and `Invalidate` still clears everything. Import expansion is depth-limited. Do not assume includes are rebound on every document edit. Snapshot invalidation clears the language include cache in the same generation transition. Cached snapshot sizes follow live binding views.

- Preserve UTF-16 offsets when masking comments/strings or joining lines; retain newlines when masking.
- `TypeRef.Root` means an empty completion path, never a stored value type. Empty assignment expressions and tuples must not become Root.
- Unwrap matching outer parentheses before call/ternary inference. `(core.std.BlankClip())` should retain its node type.
- AviSynth preprocessing order is **join continuations, then mask**. `BufferLexer.Mask` joins when `BackslashLineContinuations` is set; `AviSynthPatterns.Clean` delegates to that and must not join again. A leading `\` can join a statement even inside a later triple-quoted string. Keep replacements the same length.
- AviSynth recognizes hash comments, `/* */`, `/[ ]/`, `[* *]`, triple quotes, and doubled quotes. Preserve the correct block closer for each opener.
- VapourSynth recognizes hash comments, single/double/triple quotes, and backslash escapes. Literal inference needs original RHS text, with comments removed and offsets aligned.

## Completion and hover

- Locals/properties hover as a type only. Suppress text identical to the identifier.
- A named argument `name=` belongs to its call, even when unresolved; never fall through to a same-named local.
- Function parameter names are silent in headers. Preserve header suppression at an unclosed EOF header (`HeaderEnd == End`). In `clip: vs.VideoNode`, `vs` may hover but `VideoNode` must not.
- Completion hints and hover share `Symbol.Tip`: function signature, or the type only for locals, properties, and plugin namespaces (`plugin` on `core.std` and `clip.std`). Named-argument hover is the parameter type; those completions insert `name=` and have no side hint. Function `Signature` uses `DisplayName` (last dotted segment), so `core.std.BlankClip` shows `BlankClip(...)`. Catalog `Name` stays fully qualified. Wrap and truncation come from `EditorAssistOptions.Hint` and `Hover` (`AssistTipSize`); do not hard-code widths in presenters. Short tips size to content (MaxWidth, not Width). Hover defaults are wider than the completion side panel and also wrap call-insight headers (`OverloadProvider.CurrentHeader` is a wrapping block, not a one-line string). Fluent's tooltip chrome is 320px (`ToolTipContentMaxWidth`); hover must set MaxWidth on a `ToolTip` instance so that cap does not wrap signatures.
- VS display names come from `VapourSynthTypes.Display` / `DisplayReturn`: use `VideoNode`, not `vnode`, for local hints. Keep `[]` on parameter types (`color:float[]` is not `float`). A parameter named `format` with native type `int` displays as `VideoFormat` (BlankClip/`resize` format ids, and `clip.format`). Format constants (`vs.YUV420P8`) stay int. `VideoNode`/`AudioNode` on `vs` remain namespaces. `|` in a Python parameter is a union (`int | None` is optional int). Display unwraps `| None` and maps each remaining part; mixed unions stay joined with `|`.
- `[` may commit a completion but must not trigger member completion. Insight within brackets belongs to the enclosing call.

## Language rules

### AviSynth

- Compare identifiers ordinal-ignore-case. `last` is always a clip. Unknown called names default to clip except entries in `AviSynthInternals`; clip property syntax also uses that return table.
- Preserve explicit and implicit-first-clip call handling on one catalog signature. Bound `last.Crop` / `clip.Crop` skip the first clip; root `Crop(10,` and `BlankClip(length=100,` do the same when the first argument is not a clip (numbers, strings, and `name=` are not clips). Do not present implicit last as a second overload. Native `$InternalFunctions$` lists the same filter many times with one Param$ string; insight keeps one copy of each distinct signature.
- Bind function parameters/assignments in `BindingScope`; keep `last`, top-level assignments, and `global x =` in script names. Imported AVSI headers contribute signatures, never their parameter locals.
- A header without `{` ends before the next function, or at EOF if none follows. Retain incomplete parameter-list spans.
- Preserve `Default(x, y)` inference, clip preference in ternaries, and current last-assignment-wins behavior. GScript/Eval are not scopes.
- Header type words such as `string "Preset"` are not function calls; `String(5)` still is. Mask comments before looking for ternary delimiters.

### VapourSynth

- Compare identifiers ordinally. Native catalogs alone supply `core.ns.Func`; do not expose those functions at statement root.
- Offer members only for a known receiver. `clip = core.std.BlankClip()` enables node properties and bound plugin namespaces; unknown receivers stay silent. Call return types do not depend on argument types: `core.ffms2.Source(src)` is still a node when `src` is untyped.
- Dotted assignments must not create locals. Preserve node types through supported `+`, `*`, slice expressions, and Python `a if c else y` when the branches agree or one is unknown. Mixed known types stay unknown. Last assignment at or before the caret wins: hovering an earlier binding keeps that type after a later rebind; a later caret follows the later assignment. Unknown RHS assignments do not overwrite a known name during bind.
- `FromReturn` ignores empty semicolon parts; two or more return keys stay unknown. Use realistic `clip:vnode;` return strings in tests. Node arrays currently collapse to the node type.
- Function scopes follow logical statements and the statement’s initial indentation, including indented methods/nested defs; imported-module exports use column-zero defs and classes. Blank/comment/masked-string lines must not end a body. Outer scopes overlay before inner scopes. Name lookup, aliases, and function symbols share that overlay; a later assignment shadows a function of the same name.
- Infer parameters from supported annotations/defaults, then the single naming convention `clip` → VideoNode. Do not guess `Input`, `src`, or `self`. Annotations map the last identifier through the type table (`VideoNode`, `AudioNode`, `VideoFrame`, `VideoFormat`/`Format`, `Core`, primitives). Quoted forms, `Optional[T]`, and `T | None` unwrap; mixed unions stay unknown. Aliases such as `from vapoursynth import VideoNode as Node` resolve through bound names.
- Imported `.py` definitions enter the importing document through `import`/`from`; do not expose every discovered file at root. Failed imports must not overwrite names with Unknown. A `script:id` member is a module, not a function call.
- Declare `Formats`/`Families` before `ModuleMembers` in `VapourSynthHostTypes` to avoid static initialization failures. Fraction members (`clip.fps.`) remain unsupported.
- `VapourSynthImports.Plan` is the public insert API. It scans shebang, encoding, module docstring, and header `import`/`from` once. Inline comments are not part of the specifier. An unclosed string or import in the prologue does not move the insert offset into that construct. `import module` is present only while the root name still refers to that path; `import pkg.sub as other` and a later non-module binding of `pkg` are not present. A later assignment, `del`, `def`, `class`, `for`, `with`, or unpacking of the qualifier cannot be repaired by a header import, so `Needed` is false. Inserted text uses the document newline (`\n`, `\r\n`, or `\r`), or `Environment.NewLine` when the document has none. Planner shadowing uses the same binding-target walk as the binder.

## Catalogs, includes, and editor lifecycle

- `ISymbolSource.Enumerate` runs in the background via `CatalogCache`. `SetKey` only remembers configuration; `Refresh` starts enumeration; cancelling `GetAsync` cancels the wait, not native work. A first failure or a new configuration key yields an empty catalog. A forced refresh of the same key that throws keeps the last successful catalog. The cache retains the current task until configuration changes or refresh is forced.
- `IsEnabled` is a factory bool. Disabled `Configure` stores the key, `Refresh` still enumerates, and `Create` returns null. A service already obtained still analyzes; the editor does not call it while the factory is disabled.
- `IIncludeSource.Read(specifier, fromPath)` returns full resolved path + text, or null. The host owns search roots; `ScriptFiles` tries candidates through `IFileSystemService` and returns null for missing/unreadable paths so lookup can continue. Autoload AviSynth scripts are read through `IScriptDirectory.TryRead`. `ScriptPackages.List` inventories installed Python import names from those roots for browse only.
- `ILanguage.Browse` groups native plugins, imported aliases, This file, and (VS) installed packages not already in `ScriptModules`. Column-0 `class Name` in an imported or listed package is an insertable export; nested methods stay out. Constructor insight reads that class body's `def __init__` at the class indent, dropping a leading `self`/`cls`. Skip PEP 695 type parameters after the class name so the body still binds. A `@dataclass` with no `__init__` uses that class body's fields. TypedDict/NamedTuple use those fields as constructor parameters and insert `module.Name()`. Enum/Flag/ABC/Protocol bases are names, not calls: insert `module.Name`. Class-body constants complete on `Name.` and are the type's tooltip as `A | B` choices allowed for a parameter of that type. A type-name hint that equals the name is omitted (no empty tooltip). Error classes are constructors: local `__init__` when present, otherwise `Name()`. No local constructor, dataclass, or type-name base is `Name()` (not unknown). Do not walk bases. Skip `vs` / `vapoursynth` script modules. Each unimported package gets its own import work budget so one graph cannot drop later groups. AviSynth `User` is labeled Autoload. Function names are sorted ordinal-ignore-case. VS groups are This file then ordinal; AVS buckets stay This file / Internal / Plugin / Autoload. Unimported VS packages set `BrowseFunction.Import` and insert `module.Name()`; the host adds a column-0 `import module` when missing. `from module import` does not bind the module name.
- Use absolute include paths directly; otherwise search beside the importing file, then host roots. Python tries `name.py`, `name.pyi`, `name/__init__.py`, and `name/__init__.pyi`. Leading dots are relative to that file's directory, never a rooted path produced by replacing dots. Track resolved paths to prevent cycles. Include LRU is byte-capped for unpinned entries; small files are not count-capped. Working-set pins skip entries above the unpinned byte cap and a larger pin ceiling.
- Pass `documentPath` through `GetAsync`/`Bind`. Unsaved buffers lack a sibling directory but can use configured roots. Python site-packages belongs in script search roots, never native autoload directories.
- `EditorAssist` owns attach/detach, cancellation, debounce, and stale-result rejection; presenters own popups. It takes `IAssistSession`, not `Func` catalogs or path callbacks. Keep parser internals out of wrap-size options; exposing `Reply` is sufficient. No Splat registration helper.

Reference host: `ScriptAssistService` passes native dump adapters, `AviSynthPluginDirectory`, and include sources into the factory. `FrameworkDetectionService` configures keys after native setup; the factory must not call `VsHelper.SetDllPath`. `EnhanceEditorWithAutoComplete` is the enablement setting; `BindableTextEditor` implements `IAssistSession` and maps `ScriptKind` to factory ids.

## Analysis notes

- VS bindings scan logical statements (brackets, `;`, `\` continuations) so assignments keep original RHS spans. Column-0 `def` symbols, return annotations, and annotation-only names use the existing type table. Imports follow source order and function scope; module identity is the resolved path.
- Parameter names are language-specific (`OfPython` / `OfAviSynth`). Bound node completion filters video vs audio first arguments. `UnionByName` keeps native overload groups. Argument completion reuses the resolved call frame and skips `*`/`/` separators.
- `ILanguageService.Invalidate` increments a generation and drops snapshots independently of catalog identity; in-flight analysis must not publish a stale snapshot. Factory `Refresh` invalidates every profile. `Configure` with a new catalog key also invalidates include state, including keys stored while disabled. `Create` returns null while disabled.
- Expression reading walks consecutive `()` / `[]` suffixes. AviSynth `BackslashLineContinuations` joins before mask; Python joins `\` continuations outside strings and comments. Unterminated single-line strings recover at the next newline. CRLF is one newline.

## Validation

Run from the repository root; these are xunit.v3 executables on .NET 10, not VSTest projects:

```bash
dotnet build ScriptAssist.Tests/ScriptAssist.Tests.csproj
dotnet ScriptAssist.Tests/bin/Debug/net10.0/ScriptAssist.Tests.dll
```

- Prefer `LanguageService.Analyze` tests for language rules. Suite shape, placement, and UI-test rules: repository [AGENTS.md](../AGENTS.md). Unqualified native names lock once on hover; insight wrap locks wrap/`MaxWidth` only. Do not add `Symbol.DisplayName`/`Signature` facts for the same string. Run Avalonia editor tests in `SynthMultiViewer.Tests/EditorCompletionTests` with `-parallel none`; the headless dispatcher is not thread-safe.
- User-facing coverage is primary: completion items, call insight, and hover as `Analyze` returns them. If a name can be completed, called, and hovered, lock the surfaces that can disagree (list vs insight vs hover). Engine and parser tests do not substitute for that. Put new editor-visible behavior in `User/`; ad-hoc lexer/parser edges stay in the language files.

| Folder | Class | Role |
| --- | --- | --- |
| `User/` | `VapourSynthAssistTests` | VS complete / insight / hover contracts |
| `User/` | `AviSynthAssistTests` | AVS complete / insight / hover contracts |
| `Engine/` | `ExpressionReaderTests` | Caret expression, joins, grouping |
| `Engine/` | `IncludeCacheTests` | Include LRU, pinned working sets |
| `Engine/` | `LanguageServiceTests` | Snapshots, inflight share, invalidate, cancel |
| `AviSynth/` | `AviSynthLanguageTests` | Bind, `last`, headers, types, AVS lexer |
| `VapourSynth/` | `VapourSynthLanguageTests` | Bind, scopes, types, members, VS lexer |
| `Imports/` | `ScriptImportTests` | Import graphs, packages, cycles |
| `Imports/` | `ScriptIncludesTests` | Parameter parse, include path search |
| `Calls/` | `CallInsightTests` | Named-arg mapping, separators, recovery |
| `Host/` | `ScriptLanguageFactoryTests` | Factory, catalogs, enablement |
| `Host/` | `BrowseTests` | Explorer groups, This file, unimported packages |
| `Host/` | `ScriptPackagesTests` | Installed Python package inventory |
| `Host/` | `CompletionDataTests` | Presenters, wrap, overload UI |

- Test inside and after function scopes, header silence, and `vs` annotation hover. Include comments, multiline/incomplete input, and realistic native signatures.
- Prefer representative installed/sibling scripts: xClean trailing `\`, FrameRateConverter leading `\` and `[** *]`/triple quotes, Shader, and havsfunc. Import graphs use a `Read` local function at the end of Prepare, wrapped as `Includes(Read)` for `IIncludeSource`.
