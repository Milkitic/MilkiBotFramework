using System.Collections.Generic;

namespace MilkiBotFramework.GoCqHttp.Messaging;

public class RangeComparer : IComparer<(int index, int count, bool isRaw)>
{
    private RangeComparer()
    {
    }

    public static IComparer<(int index, int count, bool isRaw)> Instance { get; } = new RangeComparer();

    public int Compare((int index, int count, bool isRaw) x, (int index, int count, bool isRaw) y)
    {
        return Comparer<int>.Default.Compare(x.Item1, y.Item1);
    }
}