namespace HanumanInstitute.VapourSynthApi;

/// <summary>
/// Copies API4 plugin metadata from a throwaway core using the playback library.
/// </summary>
public static class VsCatalog
{
    /// <summary>
    /// Enumerates available functions and copies their metadata before releasing the native environment.
    /// </summary>
    public static IReadOnlyList<VsFilterInfo> Read()
    {
        var api = VsCoreApi.Load(VsScriptApi.Load().CoreApi);
        var core = api.CreateCore(0);
        if (core == IntPtr.Zero)
        {
            throw new VsException("Could not create catalog core.");
        }

        try
        {
            var result = new List<VsFilterInfo>();
            for (var plugin = api.GetNextPlugin(IntPtr.Zero, core); plugin != IntPtr.Zero; plugin = api.GetNextPlugin(plugin, core))
            {
                var space = api.PluginNamespace(plugin);
                var pluginName = api.PluginName(plugin);
                for (var function = api.GetNextPluginFunction(IntPtr.Zero, plugin); function != IntPtr.Zero;
                     function = api.GetNextPluginFunction(function, plugin))
                {
                    result.Add(new(space, api.PluginFunctionName(function), api.PluginFunctionArguments(function),
                        api.PluginFunctionReturnType(function), pluginName));
                }
            }

            return result;
        }
        finally
        {
            api.FreeCore(core);
        }
    }
}
