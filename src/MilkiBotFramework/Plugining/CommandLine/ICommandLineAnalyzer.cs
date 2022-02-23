using System.Diagnostics.CodeAnalysis;

namespace MilkiBotFramework.Plugining.CommandLine
{
    public interface ICommandLineAnalyzer
    {
        bool TryAnalyze(string input,
            [NotNullWhen(true)] out CommandLineResult? result,
            out CommandLineException? exception);
    }
}
