using System;
using System.Threading.Tasks;
using Avalonia;

namespace MilkiBotFramework.Imaging.Avalonia;

public static class AvaloniaOptions
{
    // ReSharper disable once InconsistentNaming
    public static Func<TaskCompletionSource, Application>? GetApplicationFunc;
    public static Func<AppBuilder, AppBuilder>? CustomConfigureFunc;

    public static AppBuilder CustomConfigure(this AppBuilder builder)
    {
        return CustomConfigureFunc == null ? builder : CustomConfigureFunc(builder);
    }
}