using System.Collections.Generic;

namespace MilkiBotFramework.Plugining.Loading;

internal class PluginInfoIndexComparer : IComparer<PluginInfo>
{
    public int Compare(PluginInfo? x, PluginInfo? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (ReferenceEquals(null, y)) return 1;
        if (ReferenceEquals(null, x)) return -1;
        return x.Index.CompareTo(y.Index);
    }
}