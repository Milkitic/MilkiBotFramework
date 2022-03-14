namespace MilkiBotFramework.Plugining.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class PluginIdentifierAttribute : Attribute
{
    public PluginIdentifierAttribute(string guid, string? name = null)
    {
        Guid = guid;
        Name = name;
    }

    public string? Scope { get; init; }
    public string Guid { get; }
    public string? Name { get; }
    public string? Authors { get; init; }

    /// <summary>
    /// 插件优先级，越小则优先级越高
    /// </summary>
    public int Index { get; init; }

    public bool AllowDisable { get; init; } = true;
}