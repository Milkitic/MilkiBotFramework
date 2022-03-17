using System.Diagnostics.CodeAnalysis;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.CommandLine;

public class CommandLineAnalyzer : ICommandLineAnalyzer
{
    public IParameterConverter DefaultParameterConverter { get; set; } = Loading.DefaultParameterConverter.Instance;

    protected const char CommandFlag = '/';
    private static readonly HashSet<char> OptionFlags = new() { '-' };
    private static readonly HashSet<char> QuoteFlags = new() { '\"', '\'', '`' };
    private static readonly HashSet<char> SplitterFlags = new() { ' ' };

    public virtual bool TryAnalyze(string input,
        [NotNullWhen(true)] out CommandLineResult? result,
        out CommandLineException? exception)
    {
        var memory = input.AsMemory().Trim();
        if (memory.Length <= 1 || memory.Span[0] != CommandFlag)
        {
            result = null;
            exception = null;
            return false;
        }

        memory = memory[1..];

        int index = 0;
        int? simpleArgStart = null;
        int? simpleArgEnd = null;
        int count = 0;

        var authority = CommandLineAuthority.Public;
        ReadOnlyMemory<char>? command = null;
        char? currentQuote = null;

        var options = new Dictionary<ReadOnlyMemory<char>, ReadOnlyMemory<char>?>();
        var arguments = new List<ReadOnlyMemory<char>>();

        ReadOnlyMemory<char>? currentOption = null;

        foreach (var c in memory.Span)
        {
            if (currentQuote == null && SplitterFlags.Contains(c) ||
                c == currentQuote)
            {
                currentQuote = null;
                if (count > 0)
                {
                    var currentWord = memory.Slice(index, count);
                    try
                    {
                        AddOperation(currentWord);
                    }
                    catch (CommandLineException ex)
                    {
                        exception = ex;
                        result = null;
                        return false;
                    }
                }

                index += count + 1;
                count = 0;
            }
            else if (currentQuote == null && QuoteFlags.Contains(c))
            {
                currentQuote = c;
                index += count + 1;
                count = 0;
            }
            else
            {
                count++;
            }
        }

        if (count > 0)
        {
            var currentWord = memory.Slice(index, count);
            try
            {
                AddOperation(currentWord, true);
            }
            catch (CommandLineException ex)
            {
                exception = ex;
                result = null;
                return false;
            }
        }

        var simpleArgs = simpleArgStart != null
            ? simpleArgEnd == null
                ? memory[simpleArgStart.Value..].TrimEnd()
                : memory.Slice(simpleArgStart.Value, simpleArgEnd.Value - simpleArgStart.Value).TrimEnd()
            : string.Empty.AsMemory();

        result = new CommandLineResult(authority,
            command,
            options,
            arguments,
            simpleArgs);
        exception = null;
        return command != null;

        void AddOperation(ReadOnlyMemory<char> currentWord, bool isEnd = false)
        {
            var containsOptionFlag = OptionFlags.Contains(currentWord.Span[0]);
            if (containsOptionFlag &&
                currentWord.Length > 1 && !IsNumber(currentWord.Span[1])) // Option key
            {
                if (simpleArgStart.HasValue && simpleArgEnd == null)
                {
                    simpleArgEnd = index - 1;
                }

                if (command == null)
                    throw new CommandLineException("Command should be declared before any options.");

                if (currentOption != null) // Previous is a switch
                    options.Add(currentOption.Value, null);

                if (isEnd)
                    options.Add(currentWord[1..], null);
                else
                    currentOption = currentWord[1..];
            }
            else if (!containsOptionFlag && command == null)
            {
                if (currentWord.Span.SequenceEqual("root"))
                    authority = CommandLineAuthority.Root;
                else if (currentWord.Span.SequenceEqual("sudo"))
                    authority = CommandLineAuthority.Admin;
                else
                    command = currentWord;
            }
            else if (currentOption != null) // Option value
            {
                options.Add(currentOption.Value, currentWord);
                currentOption = null;
            }
            else // Argument
            {
                arguments.Add(currentWord);
                simpleArgStart ??= index;
            }
        }
    }

    private static bool IsNumber(char c)
    {
        var i = (int)c;
        return i is >= 48 and <= 57;
    }
}