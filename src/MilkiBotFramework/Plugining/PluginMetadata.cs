namespace MilkiBotFramework.Plugining;

public class PluginMetadata
{
    public PluginMetadata(Guid guid, string name, string description, string authors, string scope)
    {
        Guid = guid;
        Name = name;
        Description = description;
        Authors = authors;
        Scope = scope;
    }

    public Guid Guid { get; }
    public string Name { get; }
    public string Description { get; }
    public string Authors { get; }
    public string Scope { get; }
}