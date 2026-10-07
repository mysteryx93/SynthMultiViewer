namespace HanumanInstitute.ApiAviSynth;

/// <summary>
/// Reads autoload metadata without evaluating editor text.
/// </summary>
public static class AvsCatalog
{
    /// <summary>
    /// Enumerates available functions and copies their metadata before releasing the native environment.
    /// </summary>
    public static IReadOnlyList<AvsFilterInfo> Read()
    {
        using var native = AvsNative.Load();
        var env = native.CreateEnvironment();
        if (env == IntPtr.Zero)
        {
            throw new AvsException("Could not create catalog environment.");
        }
        try
        {
            if (native.FunctionExists(env, "AutoloadPlugins"))
            {
                var value = native.Invoke(env, "AutoloadPlugins");
                try
                {
                    if (value.Type == (short)'e')
                    {
                        throw new AvsException(value.GetString() ?? "Autoload failed.");
                    }
                }
                finally
                {
                    native.ReleaseValue(value);
                }
            }
            var result = new List<AvsFilterInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var category in new[] { "InternalFunctions", "PluginFunctions", "UserFunctions" })
            {
                var names = native.ReadStringVariable(env, "$" + category + "$");
                foreach (var name in (names ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!seen.Add(name))
                    {
                        continue;
                    }

                    result.Add(new(name, ReadParameters(native, env, category, name), category));
                }
            }
            return result;
        }
        finally
        {
            native.DeleteEnvironment(env);
        }
    }

    /// <summary>
    /// AviSynth+ always writes <c>$Plugin!Name!Param$</c>; classic builds used the list prefix instead.
    /// </summary>
    private static string? ReadParameters(AvsNative native, IntPtr environment, string category, string name)
    {
        var prefix = category == "PluginFunctions" ? "Plugin" : category;
        return native.ReadStringVariable(environment, "$Plugin!" + name + "!Param$")
            ?? native.ReadStringVariable(environment, "$" + prefix + "!" + name + "!Param$");
    }
}

/// <summary>
/// Metadata copied from an autoload environment.
/// </summary>
public sealed record AvsFilterInfo(string Name, string? Arguments, string Category);
