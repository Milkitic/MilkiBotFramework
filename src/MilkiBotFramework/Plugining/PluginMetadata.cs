namespace MilkiBotFramework.Plugining;

public class PluginMetadata
{
    public PluginMetadata(Guid guid, string name, string description, string version, string[] authors)
    {
        Guid = guid;
        Name = name;
        Description = description;
        Version = version;
        Authors = authors;
    }

    public Guid Guid { get; }
    public string Name { get; }
    public string Description { get; }
    public string Version { get; }
    public string[] Authors { get; }
}