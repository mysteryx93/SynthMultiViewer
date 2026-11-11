using System.Text.Json.Serialization;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Source-generated JSON metadata for settings files and the app-version API.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppSettingsData))]
[JsonSerializable(typeof(AppTheme))]
[JsonSerializable(typeof(UpdateInterval))]
[JsonSerializable(typeof(AppVersionInfo))]
[JsonSerializable(typeof(EncodePreset))]
[JsonSerializable(typeof(List<EncodePreset>))]
internal partial class AppJsonContext : JsonSerializerContext;
