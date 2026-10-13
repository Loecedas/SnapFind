namespace SnapFind.Plugin.Abstractions;

public sealed class PluginManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public string Author { get; set; } = "Third Party";
    public int ApiVersion { get; set; } = 1;
    public string Entry { get; set; } = "";
    public string? EntryType { get; set; }
    public string? Sha256 { get; set; }
    public bool HasHotkey { get; set; }
    public string? DefaultHotkey { get; set; }
    public List<string> Features { get; set; } = new();
    public List<string> FeaturesEn { get; set; } = new();
}

public static class PluginApi
{
    public const int Current = 1;
}
