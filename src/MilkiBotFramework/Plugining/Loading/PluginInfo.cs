namespace MilkiBotFramework.Plugining.Loading;

public sealed class PluginInfo
{
    public PluginInfo(PluginMetadata metadata,
        Type type,
        Type baseType,
        PluginLifetime lifetime,
        IReadOnlyDictionary<string, CommandInfo> commands,
        int index)
    {
        Metadata = metadata;
        Type = type;
        BaseType = baseType;
        Lifetime = lifetime;
        Commands = commands;
        Index = index;
    }

    public bool InitializationFailed { get; internal set; }
    public PluginMetadata Metadata { get; }
    public Type Type { get; }
    public Type BaseType { get; }
    public PluginLifetime Lifetime { get; }
    public IReadOnlyDictionary<string, CommandInfo> Commands { get; }
    public int Index { get; }
}