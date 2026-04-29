using System.Diagnostics.CodeAnalysis;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.CommandLine;

public class CommandLineAnalyzer : ICommandLineAnalyzer
{
    private readonly BotOptions _botOptions;
    public IParameterConverter DefaultParameterConverter { get; set; } = Loading.DefaultParameterConverter.Instance;

    private static readonly HashSet<char> OptionFlags = new() { '-' };
    private static readonly HashSet<char> QuoteFlags = new() { '\"', '\'', '`' };
    private static readonly HashSet<char> SplitterFlags = new() { ' ' };

    public CommandLineAnalyzer(BotOptions botOptions)
    {
        _botOptions = botOptions;
    }

    public virtual bool TryAnalyze(string input,
        [NotNullWhen(true)] out CommandLineResult? result,
        out CommandLineException? exception)
    {
        var commandFlag = GetCommandFlag();

        var memory = input.AsMemory().Trim();
        if (memory.Length <= 1 || memory.Span[0] != commandFlag)
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
        bool optionsEnded = false;

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

        if (currentQuote != null)
        {
            result = null;
            exception = new CommandLineException("Unclosed quote in command line.");
            return false;
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

        if (currentOption != null)
        {
            try
            {
                AddOption(currentOption.Value, null);
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
            var containsOptionFlag = !optionsEnded && OptionFlags.Contains(currentWord.Span[0]);
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
                {
                    AddOption(currentOption.Value, null);
                    currentOption = null;
                }

                if (currentWord.Span.SequenceEqual("--"))
                {
                    optionsEnded = true;
                    return;
                }

                var optionName = ParseOptionName(currentWord);
                if (optionName.Length == 0)
                    throw new CommandLineException("Option name cannot be empty.");

                if (isEnd || IsShortOptionGroup(currentWord))
                {
                    AddOption(optionName, null);
                }
                else
                {
                    currentOption = optionName; // Single short or long options may have a value.
                }
            }
            else if (!containsOptionFlag && command == null)
            {
                if (currentWord.Span is "root")
                    authority = CommandLineAuthority.Root;
                else if (currentWord.Span is "sudo")
                    authority = CommandLineAuthority.Admin;
                else
                    command = currentWord;
            }
            else if (currentOption != null) // Option value
            {
                AddOption(currentOption.Value, currentWord);
                currentOption = null;
            }
            else // Argument
            {
                arguments.Add(currentWord);
                simpleArgStart ??= index;
            }
        }

        void AddOption(ReadOnlyMemory<char> name, ReadOnlyMemory<char>? value)
        {
            if (options.Keys.Any(k => k.Span.SequenceEqual(name.Span)))
                throw new CommandLineException($"Duplicate option: {name}");

            options.Add(name, value);
        }
    }

    /// <summary>
    /// 解析选项名称，支持双横杠长选项（--option）和单横杠短选项（-o）。
    /// 单横杠多字符（-abc）先保留为一个候选项，由绑定阶段根据命令参数定义决定是否拆分。
    /// </summary>
    /// <param name="optionWord">包含选项标识的完整单词</param>
    /// <returns>解析后的选项名称</returns>
    protected virtual ReadOnlyMemory<char> ParseOptionName(ReadOnlyMemory<char> optionWord)
    {
        if (optionWord.Length > 2 && optionWord.Span[1] == '-')
        {
            return optionWord[2..];
        }

        return optionWord[1..];
    }

    private static bool IsShortOptionGroup(ReadOnlyMemory<char> optionWord)
    {
        return optionWord.Length > 2 && optionWord.Span[1] != '-';
    }

    protected virtual char GetCommandFlag()
    {
        return _botOptions.CommandFlag;
    }

    private static bool IsNumber(char c)
    {
        var i = (int)c;
        return i is >= 48 and <= 57;
    }
}