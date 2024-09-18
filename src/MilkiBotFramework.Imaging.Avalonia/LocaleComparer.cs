using System.Collections.Generic;

namespace MilkiBotFramework.Imaging.Avalonia;

public class LocaleComparer : IComparer<string>
{
    private LocaleComparer()
    {
    }

    public static LocaleComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (x == "en-US" && y == "en-US") return 0;
        if (x == "en-US") return -1;
        if (y == "en-US") return 1;
        return 0;
    }
}