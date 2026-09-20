using static HanumanInstitute.ScriptAssist.VapourSynth.VapourSynthClassRanges;
using static HanumanInstitute.ScriptAssist.VapourSynth.VapourSynthScopeLinks;

namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Collects module aliases, core aliases, assignments, and annotations.
/// </summary>
internal static class VapourSynthBinder
{
    /// <summary>
    /// Last assignment wins; seeded <c>vs</c>/<c>core</c> names are overwritten when rebound.
    /// </summary>
    public static DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, LexerOptions lexer,
        CancellationToken token, string? documentPath = null, IIncludeSource? read = null,
        IncludeCache? includes = null)
    {
        return Bind(PreparedDocument.Create(text, lexer, token), catalog, lexer, token, documentPath, read, includes);
    }

    public static DocumentBindings Bind(PreparedDocument prepared, IReadOnlyList<Symbol> catalog, LexerOptions lexer,
        CancellationToken token, string? documentPath = null, IIncludeSource? read = null,
        IncludeCache? includes = null)
    {
        var clean = prepared.Masked.Code;
        var quoted = prepared.Quoted.Code;
        var names = new Dictionary<string, TypeRef>(StringComparer.Ordinal)
        {
            ["vs"] = VapourSynthTypes.Module,
            ["vapoursynth"] = VapourSynthTypes.Module,
            ["core"] = VapourSynthTypes.Core
        };
        var scriptModules = new Dictionary<string, IReadOnlyList<Symbol>>(StringComparer.Ordinal);
        var modulesByPath = new Dictionary<string, SymbolList>(StringComparer.Ordinal);
        var buffer = new SymbolList();
        var index = VapourSynthCatalogIndex.Build(catalog);
        var statements = prepared.Statements;
        var scopes = FunctionScopes(clean, quoted, statements);
        LinkEnclosing(scopes);
        var innerAt = MapInnermost(statements, scopes);
        var classes = ClassRanges(clean, quoted, statements);
        var deferred = new List<int>();
        var visible = new VisibleCache();
        var cache = new IncludeSession(includes);

        for (var i = 0; i < statements.Count; i++)
        {
            if ((i & 63) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var span = statements[i];
            var inner = innerAt[i];
            if (PythonHeaders.IsDef(quoted, span.Start, span.End))
            {
                var self = inner;
                if (self == null || self.Start != span.Start)
                {
                    continue;
                }

                if (EnclosedByClass(self, classes))
                {
                    BindParameters(self.Parameters, self,
                        ForInfer(names, scriptModules, buffer, scopes, visible, self.Enclosing), index,
                        names, buffer, visible);
                    if (self.HeaderEnd < span.End)
                    {
                        deferred.Add(i);
                    }

                    continue;
                }

                if (self.Enclosing == null)
                {
                    BindDefHeader(quoted, span, self, buffer, names, scriptModules, scopes, index, classes, visible);
                    if (self.HeaderEnd < span.End)
                    {
                        deferred.Add(i);
                    }

                    continue;
                }

                deferred.Add(i);
                continue;
            }

            if (DirectlyInClass(span.Start, classes, inner))
            {
                continue;
            }

            if (inner != null && span.Start < inner.HeaderEnd)
            {
                continue;
            }

            if (inner != null && span.Start >= inner.HeaderEnd)
            {
                deferred.Add(i);
                continue;
            }

            ApplyBody(quoted, span, null, documentPath, read, scriptModules, modulesByPath, lexer, token, buffer,
                names, scopes, index, cache, visible);
        }

        foreach (var i in deferred)
        {
            token.ThrowIfCancellationRequested();
            var span = statements[i];
            var inner = innerAt[i];
            if (DirectlyInClass(span.Start, classes, inner) && !PythonHeaders.IsDef(quoted, span.Start, span.End))
            {
                continue;
            }

            if (PythonHeaders.IsDef(quoted, span.Start, span.End))
            {
                var self = inner;
                if (self != null && self.Start == span.Start && self.Enclosing != null &&
                    !EnclosedByClass(self, classes))
                {
                    BindDefHeader(quoted, span, self, buffer, names, scriptModules, scopes, index, classes, visible);
                }

                BindDefBody(quoted, span, self, names, scriptModules, buffer, scopes, index, visible, token);
                continue;
            }

            ApplyBody(quoted, span, inner, documentPath, read, scriptModules, modulesByPath, lexer, token, buffer,
                names, scopes, index, cache, visible);
        }

        names.Remove("");
        cache.Finish(documentPath);
        Freeze(scopes);
        FreezeModules(scriptModules);
        return Current(names, scriptModules, buffer.Freeze(), scopes);
    }

    private static void ApplyBody(string quoted, StatementScanner.Span span, BindingScope? scope,
        string? documentPath, IIncludeSource? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, SymbolList> modulesByPath, LexerOptions lexer, CancellationToken token,
        SymbolList buffer, Dictionary<string, TypeRef> names, IReadOnlyList<BindingScope> scopes,
        VapourSynthCatalogIndex index, IncludeSession includes, VisibleCache visible,
        SymbolList? exports = null)
    {
        var start = span.Start;
        var end = span.End;
        if (Keyword(quoted, start, end, "import"))
        {
            ApplyImport(quoted, AfterKeyword(quoted, start, end, "import"), end, scope, documentPath, read,
                scriptModules, modulesByPath, lexer, token, names, buffer, exports, includes, visible);
            return;
        }

        if (Keyword(quoted, start, end, "from"))
        {
            ApplyFrom(quoted, AfterKeyword(quoted, start, end, "from"), end, scope, documentPath, read, scriptModules,
                modulesByPath, lexer, token, buffer, names, includes, visible);
            return;
        }

        if (Keyword(quoted, start, end, "del"))
        {
            InvalidateNames(quoted, AfterKeyword(quoted, start, end, "del"), end, scope, names, buffer, visible);
            return;
        }

        if (Keyword(quoted, start, end, "for"))
        {
            InvalidateForTargets(quoted, start, end, scope, names, buffer, visible);
            return;
        }

        if (Keyword(quoted, start, end, "class"))
        {
            InvalidateClass(quoted, start, end, scope, names, buffer, visible);
            return;
        }

        if (Keyword(quoted, start, end, "with"))
        {
            BindWith(quoted, start, end, scope, names, scriptModules, buffer, scopes, index, visible, token);
            return;
        }

        if (Keyword(quoted, start, end, "except"))
        {
            InvalidateExceptAlias(quoted, start, end, scope, names, buffer, visible);
            return;
        }

        if (IsSkippedKeyword(quoted, start, end))
        {
            return;
        }

        TryAssign(quoted, start, end, scope, names, scriptModules, buffer, scopes, index, visible, token);
    }

    private static void ApplyImport(string quoted, int start, int end, BindingScope? scope, string? documentPath,
        IIncludeSource? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, SymbolList> modulesByPath, LexerOptions lexer, CancellationToken token,
        Dictionary<string, TypeRef> names, SymbolList buffer, SymbolList? exports, IncludeSession includes,
        VisibleCache? visible = null)
    {
        foreach (var part in ParameterNames.Split(quoted[start..end]))
        {
            var spec = part.Trim();
            if (spec.Length == 0)
            {
                continue;
            }

            var aliased = ParameterNames.TryAlias(spec, out var imported, out var alias);
            if (!aliased && imported.Contains('.', StringComparison.Ordinal))
            {
                alias = imported[..imported.IndexOf('.')];
            }

            if (imported.Length == 0 || alias.Length == 0)
            {
                continue;
            }

            BindImportedModule(imported, alias, aliased, scope, documentPath, read, scriptModules, modulesByPath,
                lexer, token, names, buffer, exports, includes, visible);
        }
    }

    private static void ApplyFrom(string quoted, int start, int end, BindingScope? scope, string? documentPath,
        IIncludeSource? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, SymbolList> modulesByPath, LexerOptions lexer, CancellationToken token,
        SymbolList buffer, Dictionary<string, TypeRef> names, IncludeSession includes,
        VisibleCache? visible = null)
    {
        var i = start;
        SkipWs(quoted, ref i, end);
        var specStart = i;
        while (i < end && (quoted[i] == '.' || BufferLexer.IsIdentifier(quoted[i])))
        {
            i++;
        }

        var imported = quoted[specStart..i].Trim();
        SkipWs(quoted, ref i, end);
        if (!Keyword(quoted, i, end, "import"))
        {
            return;
        }

        var list = FlattenImportList(quoted[AfterKeyword(quoted, i, end, "import")..end]);
        var target = scope == null ? buffer : ScopeSymbols(scope);
        var targetNames = scope == null ? names : ScopeNames(scope);
        if (imported is "vapoursynth" or "vs")
        {
            foreach (var (source, alias) in ImportNames(list))
            {
                if (source == "*")
                {
                    continue;
                }

                var type = source == "core"
                    ? VapourSynthTypes.Core
                    : VapourSynthTypes.FromAnnotation(source);
                if (!type.IsUnknown)
                {
                    SetName(alias, type, scope, names, buffer, visible);
                }
            }

            return;
        }

        ImportFrom(imported, list, documentPath, read, scriptModules, modulesByPath, lexer, token, target, targetNames,
            includes, visible, scope);
    }

    private static void BindImportedModule(string imported, string alias, bool explicitAlias, BindingScope? scope,
        string? documentPath, IIncludeSource? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, SymbolList> modulesByPath, LexerOptions lexer, CancellationToken token,
        Dictionary<string, TypeRef> names, SymbolList buffer, SymbolList? exports, IncludeSession includes,
        VisibleCache? visible = null)
    {
        if (imported is "vapoursynth" or "vs")
        {
            SetName(alias, VapourSynthTypes.Module, scope, names, buffer, visible);
            return;
        }

        var loaded = LoadModule(imported, documentPath, read, scriptModules, modulesByPath, lexer, token, includes);
        if (loaded == null)
        {
            return;
        }

        if (!explicitAlias && imported.Contains('.', StringComparison.Ordinal))
        {
            BindDotted(imported, loaded.Value, scope, documentPath, read, scriptModules, modulesByPath, lexer, token,
                names, buffer, exports, includes, visible);
            return;
        }

        var id = AdoptModuleId(alias, loaded.Value.Id, imported, scope, names, scriptModules, buffer, visible);
        ExportAlias(alias, VapourSynthTypes.Script(id), scope, exports);
    }

    private static void BindDotted(string imported, LoadedScript loaded, BindingScope? scope, string? documentPath,
        IIncludeSource? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, SymbolList> modulesByPath, LexerOptions lexer, CancellationToken token,
        Dictionary<string, TypeRef> names, SymbolList buffer, SymbolList? exports, IncludeSession includes,
        VisibleCache? visible = null)
    {
        var parts = imported.Split('.');
        if (parts.Length < 2)
        {
            return;
        }

        string? parentId = null;
        for (var i = 0; i < parts.Length; i++)
        {
            var prefix = string.Join('.', parts, 0, i + 1);
            string id;
            if (i == parts.Length - 1)
            {
                id = loaded.Id;
            }
            else
            {
                var prefixLoaded = LoadModule(prefix, documentPath, read, scriptModules, modulesByPath, lexer, token,
                    includes);
                id = prefixLoaded?.Id ?? NamespaceId(documentPath, prefix);
            }

            if (i == 0)
            {
                parentId = AdoptModuleId(parts[0], id, prefix, scope, names, scriptModules, buffer, visible);
                continue;
            }

            var members = MutableModule(scriptModules, parentId!);
            UpsertChild(members, scriptModules, parts[i], id);
            parentId = ChildId(members, parts[i], id);
        }

        ExportAlias(parts[0], VapourSynthTypes.Script(RootId(names, scope, parts[0],
            parentId ?? NamespaceId(documentPath, parts[0]))), scope, exports);
    }

    private static string RootId(Dictionary<string, TypeRef> names, BindingScope? scope, string alias, string fallback)
    {
        var table = scope == null ? names : ScopeNames(scope);
        return table.TryGetValue(alias, out var type)
            ? VapourSynthTypes.ScriptOf(type) ?? fallback
            : fallback;
    }

    private static string ChildId(IReadOnlyList<Symbol> members, string child, string fallback)
    {
        foreach (var symbol in members)
        {
            if (symbol.Name.Equals(child, StringComparison.Ordinal))
            {
                return VapourSynthTypes.ScriptOf(new(symbol.ReturnType ?? "")) ?? fallback;
            }
        }

        return fallback;
    }

    private static string AdoptModuleId(string alias, string id, string specifier, BindingScope? scope,
        Dictionary<string, TypeRef> names, Dictionary<string, IReadOnlyList<Symbol>> scriptModules, SymbolList buffer,
        VisibleCache? visible = null)
    {
        var table = scope == null ? names : ScopeNames(scope);
        if (table.TryGetValue(alias, out var existing))
        {
            var oldId = VapourSynthTypes.ScriptOf(existing);
            if (oldId != null && oldId != id)
            {
                if (IsPlaceholder(oldId) && !IsPlaceholder(id) &&
                    PlaceholderPackage(oldId).Equals(RootPackage(specifier), StringComparison.Ordinal))
                {
                    MergeModules(scriptModules, id, oldId);
                }
            }
            else if (oldId != null)
            {
                id = oldId;
            }
        }

        SetName(alias, VapourSynthTypes.Script(id), scope, names, buffer, visible);
        return id;
    }

    private static bool IsPlaceholder(string id) => id.Contains("::", StringComparison.Ordinal);

    private static string PlaceholderPackage(string id)
    {
        var separator = id.LastIndexOf("::", StringComparison.Ordinal);
        var prefix = separator < 0 ? id : id[(separator + 2)..];
        return RootPackage(prefix);
    }

    private static string RootPackage(string specifier)
    {
        var dot = specifier.IndexOf('.');
        return dot < 0 ? specifier : specifier[..dot];
    }

    private static string PreferId(string left, string right)
    {
        var leftSynthetic = left.Contains("::", StringComparison.Ordinal);
        var rightSynthetic = right.Contains("::", StringComparison.Ordinal);
        if (leftSynthetic && !rightSynthetic)
        {
            return right;
        }
        return left;
    }

    private static void MergeModules(Dictionary<string, IReadOnlyList<Symbol>> scriptModules, string into, string from)
    {
        var dest = MutableModule(scriptModules, into);
        if (!scriptModules.TryGetValue(from, out var src) || ReferenceEquals(src, dest))
        {
            return;
        }

        foreach (var symbol in src)
        {
            UpsertMember(dest, scriptModules, symbol);
        }

        scriptModules[from] = dest;
    }

    private static void UpsertChild(SymbolList members, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        string child, string childId)
    {
        UpsertMember(members, scriptModules,
            new(child, null, SymbolKind.Namespace, ReturnType: VapourSynthTypes.Script(childId).Id));
    }

    private static void UpsertMember(SymbolList members, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Symbol incoming)
    {
        if (members.TryGet(incoming.Name, out var existing))
        {
            var existingId = existing.ReturnType is { } existingReturn
                ? VapourSynthTypes.ScriptOf(new(existingReturn))
                : null;
            var incomingId = incoming.ReturnType is { } incomingReturn
                ? VapourSynthTypes.ScriptOf(new(incomingReturn))
                : null;
            if (existingId != null && incomingId != null && existingId != incomingId)
            {
                var keep = PreferId(existingId, incomingId);
                MergeModules(scriptModules, keep, keep == existingId ? incomingId : existingId);
                members.Replace(incoming with { ReturnType = VapourSynthTypes.Script(keep).Id });
                return;
            }
        }

        members.Replace(incoming);
    }

    private static string NamespaceId(string? documentPath, string prefix) =>
        (documentPath ?? "") + "::" + prefix;

    private static SymbolList MutableModule(Dictionary<string, IReadOnlyList<Symbol>> scriptModules, string id)
    {
        if (scriptModules.TryGetValue(id, out var existing) && existing is SymbolList list)
        {
            return list;
        }

        var created = new SymbolList();
        if (existing != null)
        {
            foreach (var symbol in existing)
            {
                created.Replace(symbol);
            }
        }

        scriptModules[id] = created;
        return created;
    }

    private static void FreezeModules(Dictionary<string, IReadOnlyList<Symbol>> scriptModules)
    {
        foreach (var key in scriptModules.Keys)
        {
            if (scriptModules[key] is SymbolList list)
            {
                scriptModules[key] = list.Freeze();
            }
        }
    }

    private static void ExportAlias(string alias, TypeRef type, BindingScope? scope, SymbolList? exports)
    {
        var symbol = new Symbol(alias, null, SymbolKind.Namespace, ReturnType: type.Id);
        if (scope != null)
        {
            ReplaceSymbol(ScopeSymbols(scope), symbol);
        }

        if (exports != null)
        {
            ReplaceSymbol(exports, symbol);
        }
    }

    private static void TryAssign(string quoted, int start, int end, BindingScope? scope,
        Dictionary<string, TypeRef> names, Dictionary<string, IReadOnlyList<Symbol>> scriptModules, SymbolList buffer,
        IReadOnlyList<BindingScope> scopes, VapourSynthCatalogIndex index, VisibleCache visible,
        CancellationToken token)
    {
        if (TryUnpackAssign(quoted, start, end, scope, names, buffer, visible))
        {
            return;
        }

        var eq = ParameterNames.TopLevelKeywordEquals(quoted, start, end);
        if (eq >= 0 && IsAugmentedAssign(quoted, start, eq))
        {
            return;
        }

        var limit = eq < 0 ? end : eq;
        if (!TryTargetSpan(quoted, start, limit, out var innerStart, out var innerEnd))
        {
            return;
        }

        var i = innerStart;
        if (!TryIdent(quoted, ref i, innerEnd, out var name))
        {
            return;
        }

        SkipWs(quoted, ref i, innerEnd);
        var annotation = "";
        if (i < innerEnd && quoted[i] == ':')
        {
            i++;
            var annStart = i;
            var depth = 0;
            while (i < innerEnd)
            {
                if ((i & 4095) == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                var c = quoted[i];
                if (c is '(' or '[' or '{')
                {
                    depth++;
                }
                else if (c is ')' or ']' or '}' && depth > 0)
                {
                    depth--;
                }
                else if (depth == 0 && ParameterNames.IsKeywordAssign(quoted, i))
                {
                    break;
                }

                i++;
            }

            annotation = quoted[annStart..i].Trim();
        }

        i = eq < 0 ? innerEnd : eq;
        SkipWs(quoted, ref i, end);
        var type = ResolveAnnotation(annotation, ForInfer(names, scriptModules, buffer, scopes, visible, scope));
        var targets = new List<string> { name };
        if (i < end && ParameterNames.IsKeywordAssign(quoted, i))
        {
            i++;
            SkipWs(quoted, ref i, end);
            while (true)
            {
                var saved = i;
                if (!TryIdent(quoted, ref i, end, out var next))
                {
                    i = saved;
                    break;
                }

                SkipWs(quoted, ref i, end);
                if (i >= end || !ParameterNames.IsKeywordAssign(quoted, i))
                {
                    i = saved;
                    break;
                }

                targets.Add(next);
                i++;
                SkipWs(quoted, ref i, end);
            }

            var rhs = quoted[i..end].Trim();
            var inferred = VapourSynthTypeWalker.Infer(rhs, ForInfer(names, scriptModules, buffer, scopes, visible, scope),
                index, token);
            if (inferred.IsRoot)
            {
                inferred = TypeRef.Unknown;
            }

            if (!inferred.IsUnknown)
            {
                type = inferred;
            }
        }
        else if (type.IsUnknown)
        {
            return;
        }

        foreach (var target in targets)
        {
            SetName(target, type, scope, names, buffer, visible);
        }
    }

    internal static string? ResolveAnnotationId(string annotation, DocumentBindings? bindings)
    {
        var type = ResolveAnnotation(annotation, bindings);
        return type.IsUnknown ? null : type.Id;
    }

    private static TypeRef ResolveAnnotation(string annotation, DocumentBindings? bindings)
    {
        var type = VapourSynthTypes.FromAnnotation(annotation);
        if (!type.IsUnknown)
        {
            return type;
        }

        var ident = VapourSynthTypes.AnnotationName(annotation);
        if (ident != null && bindings?.Names.TryGetValue(ident, out var aliased) == true)
        {
            return aliased;
        }

        return type;
    }

    private static void BindWith(string quoted, int start, int end, BindingScope? scope,
        Dictionary<string, TypeRef> names, Dictionary<string, IReadOnlyList<Symbol>> scriptModules, SymbolList buffer,
        IReadOnlyList<BindingScope> scopes, VapourSynthCatalogIndex index, VisibleCache visible,
        CancellationToken token)
    {
        var header = WithHeader(quoted, start, end);
        foreach (var part in ParameterNames.Split(header))
        {
            if (!ParameterNames.TryAlias(part, out var expression, out var alias) || !IsLocalName(alias))
            {
                continue;
            }

            var bindings = ForInfer(names, scriptModules, buffer, scopes, visible, scope);
            var type = VapourSynthTypeWalker.Infer(expression, bindings, index, token);
            if (type.IsRoot)
            {
                type = TypeRef.Unknown;
            }

            SetName(alias, type, scope, names, buffer, visible);
        }
    }

    private static string WithHeader(string quoted, int start, int end)
    {
        var i = AfterKeyword(quoted, start, end, "with");
        var depth = 0;
        var quote = '\0';
        for (var n = i; n < end; n++)
        {
            var c = quoted[n];
            if (quote != '\0')
            {
                if (c == '\\' && n + 1 < end)
                {
                    n++;
                    continue;
                }

                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
            else if (c == ':' && depth == 0)
            {
                end = n;
                break;
            }
        }

        return ExpressionParts.UnwrapParentheses(quoted[i..end]).Trim();
    }

    private static bool IsLocalName(string name)
    {
        if (name.Length == 0 || !BufferLexer.IsIdentifier(name[0]) || char.IsDigit(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!BufferLexer.IsIdentifier(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void InvalidateNames(string quoted, int start, int end, BindingScope? scope,
        Dictionary<string, TypeRef> names, SymbolList buffer, VisibleCache? visible = null)
    {
        foreach (var name in BindingTargets(quoted, start, end, null))
        {
            SetName(name, TypeRef.Unknown, scope, names, buffer, visible);
        }
    }

    private static void InvalidateForTargets(string quoted, int start, int end, BindingScope? scope,
        Dictionary<string, TypeRef> names, SymbolList buffer, VisibleCache? visible = null)
    {
        foreach (var name in BindingTargets(quoted, AfterKeyword(quoted, start, end, "for"), end, "in"))
        {
            SetName(name, TypeRef.Unknown, scope, names, buffer, visible);
        }
    }

    private static void InvalidateExceptAlias(string quoted, int start, int end, BindingScope? scope,
        Dictionary<string, TypeRef> names, SymbolList buffer, VisibleCache? visible)
    {
        var i = AfterKeyword(quoted, start, end, "except");
        if (i < end && quoted[i] == '*')
        {
            i++;
        }

        while (i < end)
        {
            SkipWs(quoted, ref i, end);
            if (i >= end || quoted[i] == ':')
            {
                return;
            }

            if (Keyword(quoted, i, end, "as"))
            {
                i = AfterKeyword(quoted, i, end, "as");
                if (TryIdent(quoted, ref i, end, out var name))
                {
                    SetName(name, TypeRef.Unknown, scope, names, buffer, visible);
                }

                return;
            }

            if (quoted[i] is '(' or '[')
            {
                var close = quoted[i] == '(' ? ')' : ']';
                i = SkipBalanced(quoted, i, end, quoted[i], close);
                continue;
            }

            if (!TryIdent(quoted, ref i, end, out _))
            {
                i++;
            }
        }
    }

    private static bool TryUnpackAssign(string quoted, int start, int end, BindingScope? scope,
        Dictionary<string, TypeRef> names, SymbolList buffer, VisibleCache visible)
    {
        var eq = ParameterNames.TopLevelKeywordEquals(quoted, start, end);
        if (eq < 0)
        {
            return false;
        }

        var i = start;
        SkipWs(quoted, ref i, eq);
        if (i >= eq)
        {
            return false;
        }

        if (IsAugmentedAssign(quoted, start, eq))
        {
            return false;
        }

        if (!TryTargetSpan(quoted, start, eq, out var innerStart, out var innerEnd))
        {
            return false;
        }

        i = innerStart;
        var unpack = i < innerEnd && quoted[i] is '[' or '*';
        if (!unpack)
        {
            if (!TryIdent(quoted, ref i, innerEnd, out _))
            {
                return false;
            }

            SkipWs(quoted, ref i, innerEnd);
            unpack = i < innerEnd && quoted[i] is ',' or '*';
        }

        if (!unpack)
        {
            return false;
        }

        foreach (var name in BindingTargets(quoted, start, eq, null))
        {
            SetName(name, TypeRef.Unknown, scope, names, buffer, visible);
        }

        return true;
    }

    private static bool IsAugmentedAssign(string quoted, int start, int eq) =>
        eq > start && quoted[eq - 1] is '*' or '+' or '-' or '/' or '%' or '&' or '|' or '^' or '@' or ':';

    private static bool TryTargetSpan(string quoted, int start, int limit, out int innerStart, out int innerEnd)
    {
        innerStart = start;
        innerEnd = limit;
        SkipWs(quoted, ref innerStart, innerEnd);
        while (innerEnd > innerStart && char.IsWhiteSpace(quoted[innerEnd - 1]))
        {
            innerEnd--;
        }

        while (innerStart < innerEnd && quoted[innerStart] == '(')
        {
            var close = SkipBalanced(quoted, innerStart, innerEnd, '(', ')');
            if (close != innerEnd)
            {
                break;
            }

            innerStart++;
            innerEnd--;
            SkipWs(quoted, ref innerStart, innerEnd);
            while (innerEnd > innerStart && char.IsWhiteSpace(quoted[innerEnd - 1]))
            {
                innerEnd--;
            }
        }

        return innerStart < innerEnd;
    }

    private static IEnumerable<string> BindingTargets(string quoted, int start, int end, string? stop) =>
        VapourSynthBindingTargets.Names(quoted, start, end, stop);

    private static int SkipBalanced(string quoted, int start, int end, char open, char close)
    {
        var depth = 1;
        var i = start + 1;
        while (i < end && depth > 0)
        {
            if (quoted[i] is '"' or '\'')
            {
                SkipString(quoted, ref i, end);
                continue;
            }

            if (quoted[i] == open)
            {
                depth++;
            }
            else if (quoted[i] == close)
            {
                depth--;
            }

            i++;
        }

        return i;
    }

    private static void SkipString(string quoted, ref int i, int end)
    {
        var quote = quoted[i++];
        while (i < end)
        {
            if (quoted[i] == '\\' && i + 1 < end)
            {
                i += 2;
                continue;
            }

            if (quoted[i++] == quote)
            {
                return;
            }
        }
    }

    private static void InvalidateClass(string quoted, int start, int end, BindingScope? scope,
        Dictionary<string, TypeRef> names, SymbolList buffer, VisibleCache? visible = null)
    {
        var i = AfterKeyword(quoted, start, end, "class");
        if (TryIdent(quoted, ref i, end, out var name))
        {
            SetName(name, TypeRef.Unknown, scope, names, buffer, visible);
        }
    }

    private static void SetName(string name, TypeRef type, BindingScope? scope, Dictionary<string, TypeRef> names,
        SymbolList? buffer = null, VisibleCache? visible = null)
    {
        if (scope != null)
        {
            ScopeNames(scope)[name] = type;
            RemoveSymbol(ScopeSymbols(scope), name);
            visible?.NoteName(scope, name, type);
            return;
        }

        names[name] = type;
        if (buffer != null)
        {
            RemoveSymbol(buffer, name);
        }

        visible?.NoteName(null, name, type);
    }

    private static void BindFunction(Symbol symbol, BindingScope? scope, SymbolList buffer,
        Dictionary<string, TypeRef> names, VisibleCache? visible = null)
    {
        if (scope != null)
        {
            ScopeNames(scope).Remove(symbol.Name);
            ReplaceSymbol(ScopeSymbols(scope), symbol);
            visible?.NoteFunction(scope, symbol);
            return;
        }

        names.Remove(symbol.Name);
        ReplaceSymbol(buffer, symbol);
        visible?.NoteFunction(null, symbol);
    }

    private static void ReplaceSymbol(SymbolList target, Symbol symbol) =>
        target.Replace(symbol);

    private static void RemoveSymbol(SymbolList target, string name) =>
        target.Remove(name);

    private static void Freeze(IReadOnlyList<BindingScope> scopes)
    {
        foreach (var scope in scopes)
        {
            if (scope.Symbols is SymbolList list)
            {
                scope.Symbols = list.Freeze();
            }
        }
    }

    private static void BindDefHeader(string quoted, StatementScanner.Span span, BindingScope self,
        SymbolList buffer, Dictionary<string, TypeRef> names, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        IReadOnlyList<BindingScope> scopes, VapourSynthCatalogIndex index,
        IReadOnlyList<(int Start, int End)> classes, VisibleCache visible)
    {
        if (self.Start != span.Start)
        {
            return;
        }

        var header = ForInfer(names, scriptModules, buffer, scopes, visible, self.Enclosing);
        var bound = new List<(string Name, TypeRef Type)>();
        foreach (var parameter in self.Parameters)
        {
            var name = ParameterNames.LocalName(parameter);
            if (name == null)
            {
                continue;
            }

            var type = ParameterNames.OfPython(parameter) == null
                ? TypeRef.Unknown
                : ParameterType(parameter, name, header, index);
            bound.Add((name, type));
        }

        var returnType = self.Async || self.ParenClose < 0
            ? null
            : VapourSynthFunctions.ReturnId(quoted, self.ParenClose, header);
        foreach (var (name, type) in bound)
        {
            SetName(name, type, self, names, buffer, visible);
        }

        if (EnclosedByClass(self, classes))
        {
            return;
        }

        var parameters = self.Parameters as string[] ?? [..self.Parameters];
        BindFunction(new(self.Name, parameters, ReturnType: returnType, Offset: self.Start), self.Enclosing,
            buffer, names, visible);
    }

    private static void BindDefBody(string quoted, StatementScanner.Span span, BindingScope? self,
        Dictionary<string, TypeRef> names, Dictionary<string, IReadOnlyList<Symbol>> scriptModules, SymbolList buffer,
        IReadOnlyList<BindingScope> scopes, VapourSynthCatalogIndex index, VisibleCache visible,
        CancellationToken token)
    {
        if (self == null || self.Start != span.Start || self.HeaderEnd >= span.End)
        {
            return;
        }

        var i = self.HeaderEnd;
        SkipWs(quoted, ref i, span.End);
        if (i < span.End)
        {
            TryAssign(quoted, i, span.End, self, names, scriptModules, buffer, scopes, index, visible, token);
        }
    }

    private static List<BindingScope> FunctionScopes(string clean, string quoted,
        IReadOnlyList<StatementScanner.Span> statements)
    {
        var scopes = new List<BindingScope>();
        for (var i = 0; i < statements.Count; i++)
        {
            var span = statements[i];
            if (!PythonHeaders.TryDef(quoted, span.Start, span.End, out var name, out var open, out var async))
            {
                continue;
            }

            var close = FunctionHeaders.MatchingClose(clean, open, span.End);
            if (close < 0)
            {
                close = -1;
            }

            var listEnd = close < 0 ? span.End : close;
            if (listEnd <= open)
            {
                continue;
            }

            var parameters = ParameterNames.Split(quoted[(open + 1)..listEnd]);
            var headerEnd = close < 0 ? span.End : HeaderColon(clean, close);
            var end = BlockEnd(clean, statements, i, span, headerEnd);
            if (headerEnd > end)
            {
                headerEnd = end;
            }

            scopes.Add(new()
            {
                Start = span.Start,
                End = end,
                Name = name,
                HeaderEnd = headerEnd,
                ParenClose = close,
                Async = async,
                Names = new Dictionary<string, TypeRef>(StringComparer.Ordinal),
                Parameters = parameters,
                Symbols = new SymbolList()
            });
        }

        return scopes;
    }

    private static void BindParameters(IReadOnlyList<string> parameters, BindingScope scope,
        DocumentBindings bindings, VapourSynthCatalogIndex index, Dictionary<string, TypeRef> names,
        SymbolList buffer, VisibleCache visible)
    {
        foreach (var parameter in parameters)
        {
            var name = ParameterNames.LocalName(parameter);
            if (name == null)
            {
                continue;
            }

            var type = ParameterNames.OfPython(parameter) == null
                ? TypeRef.Unknown
                : ParameterType(parameter, name, bindings, index);
            SetName(name, type, scope, names, buffer, visible);
        }
    }

    private static TypeRef ParameterType(string parameter, string name, DocumentBindings bindings,
        VapourSynthCatalogIndex index)
    {
        var text = parameter.Trim();
        var eq = ParameterNames.KeywordEqualsIndex(text);
        var colon = text.IndexOf(':');
        var type = TypeRef.Unknown;
        if (colon > 0 && (eq < 0 || colon < eq))
        {
            var annotation = (eq < 0 ? text[(colon + 1)..] : text[(colon + 1)..eq]).Trim();
            type = ResolveAnnotation(annotation, bindings);
        }

        if (type.IsUnknown && eq >= 0)
        {
            type = VapourSynthTypeWalker.Infer(text[(eq + 1)..].Trim(), bindings, index);
        }

        if (type.IsUnknown && name == "clip")
        {
            type = VapourSynthTypes.VideoNode;
        }

        return type;
    }

    private static DocumentBindings ForInfer(Dictionary<string, TypeRef> names,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, SymbolList buffer,
        IReadOnlyList<BindingScope> scopes, VisibleCache visible, BindingScope? inner)
    {
        if (inner == null)
        {
            visible.Reset();
            return Current(names, scriptModules, buffer, scopes);
        }

        visible.Ensure(inner, names, buffer);
        return Current(visible.Names!, scriptModules, visible.Symbols(buffer), scopes);
    }

    private static void ImportFrom(string imported, string list, string? documentPath, IIncludeSource? read,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, Dictionary<string, SymbolList> modulesByPath,
        LexerOptions lexer, CancellationToken token, SymbolList target, Dictionary<string, TypeRef>? names,
        IncludeSession includes, VisibleCache? visible = null, BindingScope? scope = null)
    {
        list = FlattenImportList(list);
        var script = LoadModule(imported, documentPath, read, scriptModules, modulesByPath, lexer, token, includes);
        foreach (var (source, alias) in ImportNames(list))
        {
            if (source == "*")
            {
                if (script == null)
                {
                    continue;
                }

                if (target.Owns(script.Value.Members))
                {
                    continue;
                }

                foreach (var symbol in script.Value.Members)
                {
                    if (symbol.Name.StartsWith('_'))
                    {
                        continue;
                    }

                    Export(symbol, target, names, visible, scope);
                }

                continue;
            }

            var match = script?.Members.FirstOrDefault(x => x.Name.Equals(source, StringComparison.Ordinal));
            if (match != null)
            {
                Export(match.Name == alias ? match : match with { Name = alias }, target, names, visible, scope);
                continue;
            }

            var nested = LoadModule(JoinImport(imported, source), documentPath, read, scriptModules, modulesByPath,
                lexer, token, includes);
            BindImported(nested, alias, target, names, visible, scope);
        }
    }

    private static string JoinImport(string imported, string source)
    {
        if (imported.Length == 0)
        {
            return source;
        }

        return imported.All(static c => c == '.') ? imported + source : imported + "." + source;
    }

    private static void BindImported(LoadedScript? script, string alias, SymbolList target,
        Dictionary<string, TypeRef>? names, VisibleCache? visible = null, BindingScope? scope = null)
    {
        if (script == null)
        {
            return;
        }

        Export(new(alias, null, SymbolKind.Namespace, ReturnType: VapourSynthTypes.Script(script.Value.Id).Id),
            target, names, visible, scope);
    }

    private static void Export(Symbol symbol, SymbolList target, Dictionary<string, TypeRef>? names,
        VisibleCache? visible = null, BindingScope? scope = null)
    {
        ReplaceSymbol(target, symbol);
        var script = symbol.ReturnType != null ? VapourSynthTypes.ScriptOf(new(symbol.ReturnType)) : null;
        if (script != null && names != null)
        {
            var type = VapourSynthTypes.Script(script);
            names[symbol.Name] = type;
            visible?.NoteName(scope, symbol.Name, type);
            return;
        }

        names?.Remove(symbol.Name);
        visible?.NoteFunction(scope, symbol);
    }

    private static IEnumerable<(string Source, string Alias)> ImportNames(string list)
    {
        foreach (var part in ParameterNames.Split(FlattenImportList(list)))
        {
            var piece = part.Trim();
            if (piece.Length == 0)
            {
                continue;
            }

            if (piece == "*")
            {
                yield return ("*", "*");
                continue;
            }

            ParameterNames.TryAlias(piece, out var source, out var alias);
            if (alias.Length > 0 && source.Length > 0)
            {
                yield return (source, alias);
            }
        }
    }

    /// <summary>
    /// Parses column-0 exports of <paramref name="specifier"/> without binding them into a document.
    /// </summary>
    internal static LoadedScript? LoadPackage(string specifier, string? documentPath, IIncludeSource? read,
        LexerOptions lexer, CancellationToken token, IncludeCache? includes) =>
        LoadPackage(specifier, documentPath, read, lexer, token, new IncludeSession(includes));

    internal static LoadedScript? LoadPackage(string specifier, string? documentPath, IIncludeSource? read,
        LexerOptions lexer, CancellationToken token, IncludeSession includes)
    {
        var scriptModules = new Dictionary<string, IReadOnlyList<Symbol>>(StringComparer.Ordinal);
        var modulesByPath = new Dictionary<string, SymbolList>(StringComparer.Ordinal);
        return LoadModule(specifier, documentPath, read, scriptModules, modulesByPath, lexer, token, includes);
    }

    private static LoadedScript? LoadModule(string imported, string? documentPath, IIncludeSource? read,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, Dictionary<string, SymbolList> modulesByPath,
        LexerOptions lexer, CancellationToken token, IncludeSession includes)
    {
        if (imported is "vapoursynth" or "vs" || read == null)
        {
            return null;
        }

        string? text = null;
        if (includes.TryPath(imported, documentPath, out var path))
        {
            if (path == null)
            {
                return null;
            }
        }
        else
        {
            if (!includes.CanImport())
            {
                return null;
            }

            var file = read.Read(imported, documentPath);
            path = file?.Path;
            text = file?.Text;
            includes.SetPath(imported, documentPath, path);
            if (file == null)
            {
                return null;
            }
        }

        if (path == null)
        {
            return null;
        }

        if (modulesByPath.TryGetValue(path, out var existing))
        {
            scriptModules[path] = existing;
            return new LoadedScript(path, existing, scriptModules);
        }

        if (includes.TryMembers(path, out var cached) &&
            ModuleComplete(path, includes, token))
        {
            if (!RestoreModule(path, cached, scriptModules, modulesByPath, includes, 0) ||
                !scriptModules.TryGetValue(path, out var restored))
            {
                return null;
            }

            return new LoadedScript(path, restored, scriptModules);
        }

        if (!includes.TryDepth())
        {
            return null;
        }

        try
        {
            if (text == null)
            {
                if (!includes.CanImport())
                {
                    return null;
                }

                var file = read.Read(imported, documentPath);
                if (file == null)
                {
                    return null;
                }

                path = file.Value.Path;
                text = file.Value.Text;
            }

            if (!includes.TryImport())
            {
                return null;
            }

            var members = new SymbolList();
            scriptModules[path] = members;
            modulesByPath[path] = members;
            FillModule(text, path, members, read, scriptModules, modulesByPath, lexer, token, includes);
            if (!includes.Limited)
            {
                includes.SetMembers(path, members);
            }

            return new LoadedScript(path, members, scriptModules);
        }
        finally
        {
            includes.LeaveDepth();
        }
    }

    private static bool RestoreModule(string path, IReadOnlyList<Symbol> members,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, Dictionary<string, SymbolList> modulesByPath,
        IncludeSession includes, int depth)
    {
        if (modulesByPath.ContainsKey(path))
        {
            return true;
        }

        if (depth >= IncludeCache.ImportDepthLimit)
        {
            return false;
        }

        var copy = new SymbolList();
        foreach (var symbol in members)
        {
            copy.Replace(symbol);
        }

        scriptModules[path] = copy;
        modulesByPath[path] = copy;
        foreach (var symbol in copy)
        {
            var nested = symbol.ReturnType != null
                ? VapourSynthTypes.ScriptOf(new(symbol.ReturnType))
                : null;
            if (nested == null || nested == path)
            {
                continue;
            }

            if (includes.TryMembers(nested, out var child))
            {
                RestoreModule(nested, child, scriptModules, modulesByPath, includes, depth + 1);
            }
        }

        return true;
    }

    private static bool ModuleComplete(string path, IncludeSession includes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (includes.Complete.Contains(path))
        {
            return true;
        }

        var walking = new HashSet<string>(StringComparer.Ordinal);
        if (!WalkModule(path, includes, walking, token, 0))
        {
            return false;
        }

        foreach (var item in walking)
        {
            includes.Complete.Add(item);
        }

        return true;
    }

    private static bool WalkModule(string path, IncludeSession includes, HashSet<string> walking,
        CancellationToken token, int depth)
    {
        token.ThrowIfCancellationRequested();
        if (depth >= IncludeCache.ImportDepthLimit)
        {
            return false;
        }

        if (includes.Complete.Contains(path) || !walking.Add(path))
        {
            return true;
        }

        if (!includes.TryMembers(path, out var members))
        {
            return false;
        }

        foreach (var symbol in members)
        {
            var nested = symbol.ReturnType != null
                ? VapourSynthTypes.ScriptOf(new(symbol.ReturnType))
                : null;
            if (nested == null || nested == path)
            {
                continue;
            }

            if (!WalkModule(nested, includes, walking, token, depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    private static void FillModule(string text, string path, SymbolList members, IIncludeSource? read,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, Dictionary<string, SymbolList> modulesByPath,
        LexerOptions lexer, CancellationToken token, IncludeSession includes)
    {
        var prepared = PreparedDocument.Create(text, lexer, token);
        var clean = prepared.Masked.Code;
        var quoted = prepared.Quoted.Code;
        var statements = prepared.Statements;
        var scopes = FunctionScopes(clean, quoted, statements);
        var classes = ClassRanges(clean, quoted, statements);
        var dummy = new Dictionary<string, TypeRef>(StringComparer.Ordinal)
        {
            ["vs"] = VapourSynthTypes.Module,
            ["vapoursynth"] = VapourSynthTypes.Module,
            ["core"] = VapourSynthTypes.Core
        };
        var index = VapourSynthCatalogIndex.Build([]);
        var visible = new VisibleCache();
        LinkEnclosing(scopes);
        var innerAt = MapInnermost(statements, scopes);
        for (var i = 0; i < statements.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var span = statements[i];
            var inner = innerAt[i];
            if (DirectlyInClass(span.Start, classes, inner))
            {
                continue;
            }

            if (inner != null && span.Start != inner.Start)
            {
                continue;
            }

            if (PythonHeaders.IsDef(quoted, span.Start, span.End))
            {
                if (inner == null || IndentAt(clean, span.Start) != 0)
                {
                    continue;
                }

                if (!PythonHeaders.TryDef(quoted, span.Start, span.End, out _, out _, out var async))
                {
                    continue;
                }

                var parameters = inner.Parameters as string[] ?? [..inner.Parameters];
                var returnType = async || inner.ParenClose < 0
                    ? null
                    : VapourSynthFunctions.ReturnId(quoted, inner.ParenClose,
                        Current(dummy, scriptModules, members, scopes));
                ReplaceSymbol(members, new(inner.Name, parameters, ReturnType: returnType));
                continue;
            }

            if (Keyword(quoted, span.Start, span.End, "class") && IndentAt(clean, span.Start) == 0)
            {
                var at = AfterKeyword(quoted, span.Start, span.End, "class");
                if (TryIdent(quoted, ref at, span.End, out var name))
                {
                    ReplaceSymbol(members, new(name, null));
                }

                continue;
            }

            ApplyBody(quoted, span, null, path, read, scriptModules, modulesByPath, lexer, token, members, dummy,
                scopes, index, includes, visible, members);
        }
    }

    private static string FlattenImportList(string list)
    {
        var text = list.Trim();
        if (text.StartsWith('('))
        {
            var close = text.LastIndexOf(')');
            if (close > 0)
            {
                text = text[1..close];
            }
        }

        return text;
    }

    internal static bool Keyword(string text, int start, int end, string word)
    {
        if (end - start < word.Length)
        {
            return false;
        }

        if (!text.AsSpan(start, word.Length).Equals(word, StringComparison.Ordinal))
        {
            return false;
        }

        var after = start + word.Length;
        return after == end || !BufferLexer.IsIdentifier(text[after]);
    }

    private static bool IsSkippedKeyword(string text, int start, int end) =>
        Keyword(text, start, end, "def") || Keyword(text, start, end, "class") ||
        Keyword(text, start, end, "if") || Keyword(text, start, end, "elif") ||
        Keyword(text, start, end, "else") || Keyword(text, start, end, "for") ||
        Keyword(text, start, end, "while") || Keyword(text, start, end, "try") ||
        Keyword(text, start, end, "except") || Keyword(text, start, end, "finally") ||
        Keyword(text, start, end, "with") || Keyword(text, start, end, "return") ||
        Keyword(text, start, end, "pass") || Keyword(text, start, end, "raise") ||
        Keyword(text, start, end, "assert") || Keyword(text, start, end, "yield") ||
        Keyword(text, start, end, "async") || Keyword(text, start, end, "lambda") ||
        Keyword(text, start, end, "global") || Keyword(text, start, end, "del");

    internal static int AfterKeyword(string text, int start, int end, string word)
    {
        var i = start + word.Length;
        SkipWs(text, ref i, end);
        return i;
    }

    internal static void SkipWs(string text, ref int i, int end)
    {
        while (i < end && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
    }

    internal static bool TryIdent(string text, ref int i, int end, out string name)
    {
        var start = i;
        if (i >= end || !BufferLexer.IsIdentifier(text[i]) || char.IsDigit(text[i]))
        {
            name = "";
            return false;
        }

        i++;
        while (i < end && BufferLexer.IsIdentifier(text[i]))
        {
            i++;
        }

        name = text[start..i];
        return true;
    }

    private static Dictionary<string, TypeRef> ScopeNames(BindingScope scope) =>
        (Dictionary<string, TypeRef>)scope.Names;

    private static SymbolList ScopeSymbols(BindingScope scope) => (SymbolList)scope.Symbols;

    private static DocumentBindings Current(IReadOnlyDictionary<string, TypeRef> names,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, IReadOnlyList<Symbol> buffer,
        IReadOnlyList<BindingScope>? scopes = null) =>
        new()
        {
            Names = names,
            ScriptModules = scriptModules,
            BufferSymbols = buffer,
            Scopes = scopes ?? []
        };

    internal readonly record struct LoadedScript(
        string Id,
        IReadOnlyList<Symbol> Members,
        IReadOnlyDictionary<string, IReadOnlyList<Symbol>> Modules);
}
