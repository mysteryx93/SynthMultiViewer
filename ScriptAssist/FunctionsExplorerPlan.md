# Functions Explorer

A modeless function browser that is a **ScriptAssist feature** with a thin host window. Completion, hover, and this list share catalogs, bind, and module-export parse. The host does not walk plugins, parse scripts, or invent groups.

## Boundary

**ScriptAssist owns** language grouping, snapshot bind, browse projection, installed-Python package listing from **host-supplied roots**, module export parse (shared with `import`), namespace hover/hint text, and the refresh contract.

**The app owns** calling `VsCatalog.Read` / `AvsCatalog.Read`, plugin/site-package **directories**, plugin-folder disk, the modeless window, toolbar/shortcut, insert at caret, Go To a This-file header, and wiring `ScriptAssistService`.

**ApiVapourSynth / ApiAviSynth own** throwaway-core enumeration and extra native fields. They do not group or browse.

```
App window  →  factory.BrowseAsync / Refresh
                    │
                    ├─ CatalogCache.GetAsync      (ISymbolSource, once per config)
                    ├─ LanguageService snapshot     (same LRU as GetAsync)
                    ├─ VS: ScriptPackages.List      (browse only, not Analyze)
                    └─ ILanguage.Browse             (groups + insert text + offset)
```

`Analyze` / `GetAsync` stay caret-relative completion. They do **not** load unimported packages.

## Browse API

```csharp
public sealed record BrowseGroup(string Name, IReadOnlyList<BrowseFunction> Functions);
public sealed record BrowseFunction(
    string Name, string Signature, string InsertText,
    string? Import = null, int? Offset = null);
```

`ILanguage.Browse(catalog, bindings, text, token, documentPath, extraPackages)`

`ILanguageService.BrowseAsync(text, token, documentPath, extraPackages)` — `GetAsync` catalog, same snapshot as `Analyze`, then `Browse`. **Does not honor the assist gate.** Pass `ScriptPackages.List(roots, files, token)` as `extraPackages` so unimported VapourSynth packages can appear.

**VapourSynth groups:** native `core.<ns>.*` → group `<ns>`, insert `core.<ns>.Func()`; imported packages → group = alias, insert `alias.Func()`; unimported extras → group = import name, `Import` set, insert `module.Func()`; This file → buffer `def`s with a header `Offset`. Plugins and Python packages are siblings. Skip `vs` / `vapoursynth` script modules. Installed and imported packages both walk nested script modules.

**AviSynth groups:** Internal / Plugin / Autoload (`User` labeled Autoload) from `Symbol.Group`; This file from `BufferSymbols` (local headers have `Offset`; imported AVSI do not). Insert `Name()`.

This file is a snapshot at open, tab switch, language/path change, and Refresh — not on every keystroke.

## Host

Name: **Functions Explorer**. Modeless, Video Properties pattern. Toolbar toggle on editors only. Shortcut `Ctrl+E`. Viewer tabs close the window.

Search + two lists (groups | functions). Refresh = `factory.Refresh()` + clear the package inventory cache + `BrowseAsync`. Insert writes the canonical call at the caret, caret inside `()`, and a column-0 `import` via `VapourSynthImports.Plan` when `Import` is set. Go To moves to `Offset` only while the editor document version still matches the browse snapshot.

No live `Document.Changed` rebrowse. Filter in memory only; selection identity is insert text, import, signature, and offset.

## What does not change

- Unimported Python names do not appear in completion until the buffer imports them.
- Locals, keywords, `VideoNode` properties stay out of the explorer.
- No docking. No second `VsCatalog.Read` from the window.
- No `__all__` product, no class-member completion, no AviSynth per-DLL names.
