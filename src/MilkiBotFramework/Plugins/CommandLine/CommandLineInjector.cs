using System;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugins.CommandLine;

public class CommandLineInjector
{
    private readonly ICommandLineAnalyzer _commandLineAnalyzer;

    public CommandLineInjector(ICommandLineAnalyzer commandLineAnalyzer)
    {
        _commandLineAnalyzer = commandLineAnalyzer;
    }

    public void InjectProperties<T>(string input, T obj)
    {
        var success = _commandLineAnalyzer.TryAnalyze(input, out var result, out var ex);
        if (!success) throw ex!;
        InjectProperties(result!, obj);
    }

    public void InjectProperties<T>(CommandLineResult result, T obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
    }

    public MessageResponseContext InjectMethod<T>(CommandLineResult result, T obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        var type = obj.GetType();
        throw new NotImplementedException();
    }
}