namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// A short Python prelude for VapourSynth scripts: <c>os.path</c> string helpers and a few builtins.
/// Not the standard library.
/// </summary>
internal static class VapourSynthPythonPrelude
{
    public static TypeRef Os { get; } = new("py:os");

    public static TypeRef OsPath { get; } = new("py:os.path");

    public static IReadOnlyList<Symbol> Builtins { get; } =
    [
        new("str", ["object"], ReturnType: "str"),
        new("int", ["x"], ReturnType: "int"),
        new("float", ["x"], ReturnType: "float"),
        new("len", ["obj"], ReturnType: "int"),
        new("hasattr", ["obj", "name:str"], ReturnType: "bool")
    ];

    public static IReadOnlyList<Symbol> OsMembers { get; } =
    [
        new("path", null, SymbolKind.Namespace, ReturnType: OsPath.Id),
        new("getcwd", [], ReturnType: "str")
    ];

    public static IReadOnlyList<Symbol> OsPathMembers { get; } =
    [
        new("join", ["a:str", "b:str"], ReturnType: "str"),
        new("dirname", ["path:str"], ReturnType: "str"),
        new("basename", ["path:str"], ReturnType: "str"),
        new("abspath", ["path:str"], ReturnType: "str"),
        new("expanduser", ["path:str"], ReturnType: "str"),
        new("normpath", ["path:str"], ReturnType: "str"),
        new("realpath", ["path:str"], ReturnType: "str"),
        new("exists", ["path:str"], ReturnType: "bool"),
        new("isfile", ["path:str"], ReturnType: "bool"),
        new("isdir", ["path:str"], ReturnType: "bool"),
        new("isabs", ["path:str"], ReturnType: "bool")
    ];

    public static bool TryModule(string imported, bool explicitAlias, out TypeRef type)
    {
        if (imported == "os")
        {
            type = Os;
            return true;
        }

        if (imported == "os.path")
        {
            type = explicitAlias ? OsPath : Os;
            return true;
        }

        type = default;
        return false;
    }

    public static bool TryMember(string imported, string name, out Symbol symbol)
    {
        var members = imported switch
        {
            "os" => OsMembers,
            "os.path" => OsPathMembers,
            _ => null
        };
        if (members != null)
        {
            foreach (var member in members)
            {
                if (member.Name.Equals(name, StringComparison.Ordinal))
                {
                    symbol = member;
                    return true;
                }
            }
        }

        symbol = null!;
        return false;
    }

    public static IReadOnlyList<Symbol>? Members(TypeRef type)
    {
        if (type == Os)
        {
            return OsMembers;
        }

        if (type == OsPath)
        {
            return OsPathMembers;
        }

        return null;
    }
}
