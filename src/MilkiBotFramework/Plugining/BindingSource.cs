using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining;

public sealed class BindingSource
{
    public BindingSource(CommandInfo commandInfo,
        CommandParameterInfo? parameterInfo)
    {
        CommandInfo = commandInfo;
        ParameterInfo = parameterInfo;
    }

    public CommandInfo CommandInfo { get; }
    public CommandParameterInfo? ParameterInfo { get; }
}