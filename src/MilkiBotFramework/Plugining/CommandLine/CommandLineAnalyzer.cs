using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MilkiBotFramework.Plugining.CommandLine;

public class CommandLineAnalyzer : ICommandLineAnalyzer
{
    private const char CommandFlag = '/';
    private static readonly HashSet<char> OptionFlags = new() { '-' };
    private static readonly HashSet<char> QuoteFlags = new() { '\"', '\'', '`' };
    private static readonly HashSet<char> SplitterFlags = new() { ' ', ':' };

    public bool TryAnalyze(string input,
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
        int? argStartIndex = null;
        int count = 0;

        CommandLineAuthority authority = CommandLineAuthority.Public;
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

        result = new CommandLineResult
        {
            Authority = authority,
            Command = command,
            Arguments = arguments,
            Options = options,
            SimpleArgument = argStartIndex != null ? memory[argStartIndex.Value..].Trim() : string.Empty.AsMemory()
        };
        exception = null;
        return command != null;

        void AddOperation(ReadOnlyMemory<char> currentWord, bool isEnd = false)
        {
            if (OptionFlags.Contains(currentWord.Span[0])) // Option key
            {
                if (command == null)
                    throw new CommandLineException("Command should be declared before any options.");

                if (currentOption != null) // Previous is a switch
                    options.Add(currentOption.Value, null);

                if (isEnd)
                    options.Add(currentWord[1..], null);
                else
                    currentOption = currentWord[1..];
            }
            else if (command == null)
            {
                if (currentWord.Span.SequenceEqual("root"))
                    authority = CommandLineAuthority.Root;
                else if (currentWord.Span.SequenceEqual("sudo"))
                    authority = CommandLineAuthority.Admin;
                else
                {
                    command = currentWord;
                    argStartIndex = currentWord.Length;
                }
            }
            else if (currentOption != null) // Option value
            {
                options.Add(currentOption.Value, currentWord);
                currentOption = null;
            }
            else // Argument
            {
                arguments.Add(currentWord);
            }
        }
    }
}