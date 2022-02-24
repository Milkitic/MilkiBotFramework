using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.CommandLine;

[Obsolete]
public class StreamCommandLineAnalyzer : ICommandLineAnalyzer
{
    private static readonly char[] Keywords = { '/', '-' };
    private static readonly char[] Quotes = { '\"', '\'', '`' };

    public IParameterConverter DefaultParameterConverter { get; set; } = Loading.DefaultParameterConverter.Instance;

    public bool TryAnalyze(string input,
        [NotNullWhen(true)] out CommandLineResult? result,
        [NotNullWhen(false)] out CommandLineException? exception)
    {
        bool AddToSwitch(ICollection<ReadOnlyMemory<char>> list, ref string? s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            list.Add(s.AsMemory());
            s = null;
            return false;
        }

        var reader = new StringReader(input);
        var builder = new StringBuilder();
        int r = reader.Read();

        bool isReadingArgOrSwitch = false;

        var args = new Dictionary<ReadOnlyMemory<char>, ReadOnlyMemory<char>?>();
        var freeArgs = new List<ReadOnlyMemory<char>>();
        var switches = new List<ReadOnlyMemory<char>>();

        string? commandName = null;
        string? placeHolder = null;

        while (r != -1)
        {
            char c = (char)r;
            string value;

            if (Keywords.Contains(c)) // if the char is keyword
            {
                if (placeHolder != null) // placeHolder is switch
                {
                    isReadingArgOrSwitch = AddToSwitch(switches, ref placeHolder);
                }

                switch (c)
                {
                    case '-': // switch or arg
                        isReadingArgOrSwitch = true;
                        break;
                }

                r = reader.Read();
                continue;
            }
            else if (c == ' ') // skip the rest spaces
            {
                r = reader.Read();
                continue;
            }
            else
            {
                builder.Clear();
                if (Quotes.Contains(c))
                {
                    value = ReadUntilQuote(reader, builder, c);
                }
                else
                {
                    builder.Append(c);
                    value = ReadUntilSpaceOrEndOrCqCodeOrColon(reader, builder);
                }
                if (commandName == null)
                    commandName = value;
                else
                {
                    if (isReadingArgOrSwitch && placeHolder != null) // placeHolder is arg
                    {
                        args.Add(placeHolder.AsMemory(), value.AsMemory());
                        placeHolder = null;
                        isReadingArgOrSwitch = false;
                    }
                    else if (!isReadingArgOrSwitch) // is freeArg
                    {
                        freeArgs.Add(value.AsMemory());
                    }
                }
            }

            if (isReadingArgOrSwitch)
            {
                placeHolder = value;
            }

            r = reader.Read();
        }

        if (placeHolder != null) // placeHolder is switch
        {
            AddToSwitch(switches, ref placeHolder);
        }

        foreach (var @switch in switches)
        {
            args.Add(@switch, null);
        }

        result = new CommandLineResult(CommandLineAuthority.Public, commandName.AsMemory(), args, freeArgs, "".AsMemory());
        exception = null;
        return true;
    }


    private static string ReadUntilCharOrEnd(TextReader reader, StringBuilder builder, char ch)
    {
        return ReadUntilChars(reader, builder, new[] { ch, unchecked((char)-1) });
    }

    private static string ReadUntilChar(TextReader reader, StringBuilder builder, char ch)
    {
        return ReadUntilChars(reader, builder, new[] { ch });
    }

    private static string ReadUntilChars(TextReader reader, StringBuilder builder, char[] ch)
    {
        int r = reader.Peek();
        while (r != -1)
        {
            var c = (char)r;
            if (ch.Contains(c))
            {
                reader.Read();
                break;
            }

            reader.Read();
            builder.Append(c);
            r = reader.Peek();
        }

        if (r == -1 && !ch.Contains(unchecked((char)-1)))
            throw new ArgumentException();
        return builder.ToString();
    }

    private static string ReadUntilSpaceOrEndOrCqCodeOrColon(TextReader reader, StringBuilder builder)
    {
        return ReadUntilChars(reader, builder, new[] { ' ', unchecked((char)-1),/* '[', */':' });
    }

    private static string ReadUntilQuote(TextReader reader, StringBuilder builder, char quote)
    {
        return ReadUntilChar(reader, builder, quote);
    }
}