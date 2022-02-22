using System.Diagnostics.CodeAnalysis;

namespace MilkiBotFramework.Plugins.CommandLine
{
    public interface ICommandLineAnalyzer
    {
        bool TryAnalyze(string input,
            [NotNullWhen(true)] out CommandLineResult? result,
            [NotNullWhen(false)] out CommandLineException? exception);
    }
}
