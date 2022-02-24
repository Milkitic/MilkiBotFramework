using System.Collections.Generic;

namespace MilkiBotFramework.Plugining.Loading;

internal class PluginDefinitionIndexComparer : IComparer<PluginDefinition>
{
    public int Compare(PluginDefinition? x, PluginDefinition? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (ReferenceEquals(null, y)) return 1;
        if (ReferenceEquals(null, x)) return -1;
        return x.Index.CompareTo(y.Index);
    }
}