using System.Collections.ObjectModel;

namespace MilkiBotFramework.Plugining.Loading;

public sealed class PluginInfo
{
    public PluginInfo(PluginMetadata metadata,
        Type type,
        Type baseType,
        PluginLifetime lifetime,
        ReadOnlyDictionary<string, CommandInfo> commands,
        int index,
        string pluginHome,
        bool allowDisable)
    {
        Metadata = metadata;
        Type = type;
        BaseType = baseType;
        Lifetime = lifetime;
        Commands = commands;
        Index = index;
        PluginHome = pluginHome;
        AllowDisable = allowDisable;
        if (baseType == StaticTypes.BasicPlugin || baseType == StaticTypes.BasicPlugin_)
            PluginType = PluginType.Basic;
        else if (baseType == StaticTypes.ServicePlugin)
            PluginType = PluginType.Service;
        else
            PluginType = PluginType.Unspecified;
    }


    public bool InitializationFailed { get; internal set; }
    public PluginMetadata Metadata { get; }
    public Type Type { get; }
    public Type BaseType { get; }
    public PluginLifetime Lifetime { get; }
    public ReadOnlyDictionary<string, CommandInfo> Commands { get; }
    public int Index { get; }
    public string PluginHome { get; }
    public bool AllowDisable { get; }
    public PluginType PluginType { get; }
}