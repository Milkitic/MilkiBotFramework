using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Platforms.Discord;

public static class DiscordSlashCommandHelper
{
    public static string NormalizeCommandName(string commandName)
    {
        return NormalizeToken(commandName, fallback: "cmd");
    }

    public static string GetSlashOptionName(CommandParameterInfo parameterInfo)
    {
        var sourceName = parameterInfo.IsArgument
            ? parameterInfo.ParameterName
            : parameterInfo.Name ?? parameterInfo.ParameterName;
        return NormalizeToken(sourceName, fallback: "param");
    }

    public static IReadOnlyList<DiscordSlashParameterInfo> GetSlashParameters(CommandInfo commandInfo)
    {
        if (commandInfo.ModelBindingInfo != null)
        {
            return commandInfo.ModelBindingInfo.ParameterInfos
                .Select(ToSlashParameter)
                .ToArray();
        }

        var methodParameters = commandInfo.MethodInfo.GetParameters();
        var slashParameters = new List<DiscordSlashParameterInfo>();

        for (var i = 0; i < commandInfo.ParameterInfos.Count; i++)
        {
            var parameterInfo = commandInfo.ParameterInfos[i];
            if (!parameterInfo.IsServiceArgument)
            {
                slashParameters.Add(ToSlashParameter(parameterInfo));
                continue;
            }

            if (parameterInfo.ParameterType == typeof(MessageContext))
            {
                continue;
            }

            if (i >= methodParameters.Length)
            {
                continue;
            }

            var modelParameters = BuildModelParameters(methodParameters[i].ParameterType);
            if (modelParameters.Count > 0)
            {
                slashParameters.AddRange(modelParameters);
            }
        }

        return slashParameters;
    }

    public static bool TryResolveCommandInfo(PluginCatalog pluginCatalog,
        string slashCommandName,
        out PluginInfo? pluginInfo,
        out CommandInfo? commandInfo)
    {
        foreach (var candidatePlugin in pluginCatalog.GetAllPlugins().OrderBy(k => k.Index))
        {
            foreach (var candidateCommand in candidatePlugin.Commands.Values.OrderBy(k => k.Command))
            {
                if (string.Equals(candidateCommand.Command, slashCommandName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeCommandName(candidateCommand.Command), slashCommandName,
                        StringComparison.Ordinal))
                {
                    pluginInfo = candidatePlugin;
                    commandInfo = candidateCommand;
                    return true;
                }
            }
        }

        pluginInfo = null;
        commandInfo = null;
        return false;
    }

    public static SlashCommandBuilder BuildSlashCommand(CommandInfo commandInfo)
    {
        var builder = new SlashCommandBuilder()
            .WithName(NormalizeCommandName(commandInfo.Command))
            .WithDescription(GetCommandDescription(commandInfo));
        builder.WithContextTypes(GetContextTypes(commandInfo.MessageType));

        if (commandInfo.Authority >= MessageAuthority.Admin)
        {
            builder.WithDefaultMemberPermissions(GuildPermission.Administrator);
        }

        foreach (var parameter in GetSlashParameters(commandInfo).Take(SlashCommandBuilder.MaxOptionsCount))
        {
            builder.AddOption(parameter.Name,
                GetOptionType(parameter.ParameterType),
                GetParameterDescription(parameter),
                parameter.Required);
        }

        return builder;
    }

    public static CommandLineResult BuildCommandLineResult(SocketSlashCommand slashCommand, PluginCatalog pluginCatalog)
    {
        var arguments = new List<ReadOnlyMemory<char>>();
        var options = new Dictionary<ReadOnlyMemory<char>, ReadOnlyMemory<char>?>();

        if (TryResolveCommandInfo(pluginCatalog, slashCommand.CommandName, out _, out var commandInfo))
        {
            var optionLookup = FlattenOptions(slashCommand.Data.Options)
                .GroupBy(k => k.Name, StringComparer.Ordinal)
                .ToDictionary(k => k.Key, k => k.First(), StringComparer.Ordinal);

            foreach (var slashParameter in GetSlashParameters(commandInfo!))
            {
                if (!optionLookup.TryGetValue(slashParameter.Name, out var slashOption))
                {
                    continue;
                }

                var rawValue = ConvertOptionValueToString(slashOption.Value);
                if (slashParameter.IsArgument)
                {
                    arguments.Add(rawValue.AsMemory());
                }
                else
                {
                    options.Add(slashParameter.SourceName.AsMemory(), rawValue.AsMemory());
                }
            }
        }

        var simpleArgument = string.Join(" ", arguments.Select(k => k.ToString()));
        return new CommandLineResult(CommandLineAuthority.Public,
            slashCommand.CommandName.AsMemory(),
            options,
            arguments,
            simpleArgument.AsMemory());
    }

    public static string BuildDisplayText(CommandLineResult commandLineResult, char commandFlag)
    {
        return commandFlag + commandLineResult.ToString();
    }

    private static IReadOnlyList<DiscordSlashParameterInfo> BuildModelParameters(Type modelType)
    {
        var result = new List<DiscordSlashParameterInfo>();
        var properties = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(k => k.SetMethod is { IsPublic: true });
        foreach (var property in properties)
        {
            var optionAttribute = property.GetCustomAttribute<OptionAttribute>();
            var argumentAttribute = property.GetCustomAttribute<ArgumentAttribute>();
            if (optionAttribute == null && argumentAttribute == null)
            {
                continue;
            }

            var isArgument = argumentAttribute != null;
            var sourceName = isArgument
                ? property.Name
                : optionAttribute!.Name;
            var defaultValue = optionAttribute?.DefaultValue ?? argumentAttribute?.DefaultValue ?? DBNull.Value;
            var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;

            result.Add(new DiscordSlashParameterInfo(
                NormalizeToken(sourceName, fallback: property.Name.ToLowerInvariant()),
                sourceName,
                isArgument,
                property.PropertyType,
                defaultValue == DBNull.Value,
                description));
        }

        return result;
    }

    private static DiscordSlashParameterInfo ToSlashParameter(CommandParameterInfo parameterInfo)
    {
        var sourceName = parameterInfo.IsArgument
            ? parameterInfo.ParameterName
            : parameterInfo.Name ?? parameterInfo.ParameterName;
        return new DiscordSlashParameterInfo(
            GetSlashOptionName(parameterInfo),
            sourceName,
            parameterInfo.IsArgument,
            parameterInfo.ParameterType,
            parameterInfo.DefaultValue == DBNull.Value,
            parameterInfo.Description);
    }

    private static IEnumerable<SocketSlashCommandDataOption> FlattenOptions(
        IReadOnlyCollection<SocketSlashCommandDataOption> options)
    {
        foreach (var option in options)
        {
            if (option.Options is { Count: > 0 })
            {
                foreach (var nested in FlattenOptions(option.Options))
                {
                    yield return nested;
                }

                continue;
            }

            yield return option;
        }
    }

    private static ApplicationCommandOptionType GetOptionType(Type parameterType)
    {
        var effectiveType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        if (effectiveType == typeof(bool))
        {
            return ApplicationCommandOptionType.Boolean;
        }

        if (effectiveType.IsEnum)
        {
            return ApplicationCommandOptionType.String;
        }

        if (effectiveType == typeof(byte) ||
            effectiveType == typeof(sbyte) ||
            effectiveType == typeof(short) ||
            effectiveType == typeof(ushort) ||
            effectiveType == typeof(int) ||
            effectiveType == typeof(uint) ||
            effectiveType == typeof(long) ||
            effectiveType == typeof(ulong))
        {
            return ApplicationCommandOptionType.Integer;
        }

        if (effectiveType == typeof(float) ||
            effectiveType == typeof(double) ||
            effectiveType == typeof(decimal))
        {
            return ApplicationCommandOptionType.Number;
        }

        return ApplicationCommandOptionType.String;
    }

    private static string ConvertOptionValueToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            IUser user => user.Id.ToString(CultureInfo.InvariantCulture),
            IRole role => role.Id.ToString(CultureInfo.InvariantCulture),
            IChannel channel => channel.Id.ToString(CultureInfo.InvariantCulture),
            IAttachment attachment => attachment.Url,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string GetCommandDescription(CommandInfo commandInfo)
    {
        return TrimDescription(string.IsNullOrWhiteSpace(commandInfo.Description)
            ? $"执行 {commandInfo.Command} 命令"
            : commandInfo.Description);
    }

    private static string GetParameterDescription(DiscordSlashParameterInfo parameterInfo)
    {
        return TrimDescription(string.IsNullOrWhiteSpace(parameterInfo.Description)
            ? $"参数 {parameterInfo.Name}"
            : parameterInfo.Description!);
    }

    private static string TrimDescription(string value)
    {
        return value.Length <= SlashCommandBuilder.MaxDescriptionLength
            ? value
            : value[..SlashCommandBuilder.MaxDescriptionLength];
    }

    private static string NormalizeToken(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var output = new List<char>(value.Length * 2);
        var previousWasSeparator = false;

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (char.IsUpper(ch) && output.Count > 0 && !previousWasSeparator)
                {
                    output.Add('-');
                }

                output.Add(char.ToLowerInvariant(ch));
                previousWasSeparator = false;
                continue;
            }

            if (output.Count == 0 || previousWasSeparator)
            {
                continue;
            }

            output.Add('-');
            previousWasSeparator = true;
        }

        while (output.Count > 0 && output[^1] == '-')
        {
            output.RemoveAt(output.Count - 1);
        }

        var normalized = output.Count == 0 ? fallback : new string(output.ToArray());
        if (normalized.Length > SlashCommandBuilder.MaxNameLength)
        {
            normalized = normalized[..SlashCommandBuilder.MaxNameLength];
        }

        return normalized;
    }

    private static InteractionContextType[] GetContextTypes(MilkiBotFramework.Messaging.MessageType messageType)
    {
        var result = new List<InteractionContextType>();
        if (messageType.HasFlag(MilkiBotFramework.Messaging.MessageType.Channel))
        {
            result.Add(InteractionContextType.Guild);
        }

        if (messageType.HasFlag(MilkiBotFramework.Messaging.MessageType.Private))
        {
            result.Add(InteractionContextType.BotDm);
            result.Add(InteractionContextType.PrivateChannel);
        }

        return result.Count == 0
            ? [InteractionContextType.Guild]
            : result.ToArray();
    }
}

public sealed record DiscordSlashParameterInfo(
    string Name,
    string SourceName,
    bool IsArgument,
    Type ParameterType,
    bool Required,
    string? Description);
