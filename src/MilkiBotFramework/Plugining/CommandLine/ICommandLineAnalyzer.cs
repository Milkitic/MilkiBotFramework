using System.Diagnostics.CodeAnalysis;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.CommandLine
{
    public interface ICommandLineAnalyzer
    {
        IParameterConverter DefaultParameterConverter { get; set; }

        bool TryAnalyze(string input,
            [NotNullWhen(true)] out CommandLineResult? result,
            out CommandLineException? exception);
    }
}
