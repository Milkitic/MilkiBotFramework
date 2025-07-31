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

                // 解析选项名称（支持 --option、-o 和 -abc 格式）
                var parsedOptions = ParseOptionName(currentWord);
                
                // 处理解析出的选项
                foreach (var optionName in parsedOptions)
                {
                    if (isEnd || parsedOptions.Count > 1) // 如果是结尾或者是组合选项，直接添加为开关选项
                        options.Add(optionName, null);
                    else
                        currentOption = optionName; // 单个选项可能有值
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

    /// <summary>
    /// 解析选项名称，支持双横杠（--option）、单横杠单字符（-o）和单横杠多字符组合（-abc）格式
    /// </summary>
    /// <param name="optionWord">包含选项标识的完整单词</param>
    /// <returns>解析后的选项名称列表</returns>
    protected virtual List<ReadOnlyMemory<char>> ParseOptionName(ReadOnlyMemory<char> optionWord)
    {
        var result = new List<ReadOnlyMemory<char>>();
        
        if (optionWord.Length > 2 && optionWord.Span[1] == '-')
        {
            // 双横杠选项 --option (完整名称)
            result.Add(optionWord[2..]);
        }
        else if (optionWord.Length == 2)
        {
            // 单横杠单字符选项 -o (简写形式)
            result.Add(optionWord[1..]);
        }
        else if (optionWord.Length > 2)
        {
            // 单横杠多字符选项 -abc (组合简写形式，每个字符都是一个独立的bool选项)
            var chars = optionWord[1..];
            for (int i = 0; i < chars.Length; i++)
            {
                result.Add(chars.Slice(i, 1));
            }
        }
        else
        {
            // 单横杠选项 - (无效格式)
            result.Add(optionWord[1..]);
        }
        
        return result;
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