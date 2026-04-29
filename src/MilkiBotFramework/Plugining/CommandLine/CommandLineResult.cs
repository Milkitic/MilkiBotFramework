using System.Text;

namespace MilkiBotFramework.Plugining.CommandLine;

public sealed class CommandLineResult
{
    public CommandLineResult(CommandLineAuthority authority,
        ReadOnlyMemory<char>? command,
        Dictionary<ReadOnlyMemory<char>, ReadOnlyMemory<char>?> options,
        List<ReadOnlyMemory<char>> arguments,
        ReadOnlyMemory<char> simpleArgument)
    {
        Authority = authority;
        Command = command;
        Options = options;
        Arguments = arguments;
        SimpleArgument = simpleArgument;
    }

    public CommandLineAuthority Authority { get; }
    public ReadOnlyMemory<char>? Command { get; }
    public Dictionary<ReadOnlyMemory<char>, ReadOnlyMemory<char>?> Options { get; }
    public List<ReadOnlyMemory<char>> Arguments { get; }
    public ReadOnlyMemory<char> SimpleArgument { get; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Command != null)
        {
            sb.Append(GetArgumentString(Command) + " ");
        }

        if (Arguments is { Count: > 0 })
        {
            sb.Append(string.Join(" ", Arguments.Select(k => GetArgumentString(k))));
            sb.Append(' ');
        }

        if (Options is { Count: > 0 })
            sb.Append(string.Join(" ", Options
                .OrderBy(k => k.Key.ToString())
                .Select(k =>
                {
                    var option = GetOptionString(k.Key);
                    return k.Value == null
                        ? option
                        : $"{option} {GetArgumentString(k.Value)}";
                })
            ));

        if (sb.Length == 0)
            return "";

        if (sb[^1] == ' ')
            sb.Remove(sb.Length - 1, 1);
        return sb.ToString();
    }

    private static string GetArgumentString(ReadOnlyMemory<char>? k)
    {
        if (k == null) return "";
        return (k.Value.Span.Contains(' ') || k.Value.Span.Contains(':')) ? $"\"{k}\"" : k.Value.ToString();
    }

    private static string GetOptionString(ReadOnlyMemory<char> option)
    {
        var prefix = option.Length == 1 ? "-" : "--";
        return prefix + GetArgumentString(option);
    }
}