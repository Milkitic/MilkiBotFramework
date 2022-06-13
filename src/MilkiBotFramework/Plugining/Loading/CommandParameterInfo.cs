using System.Reflection;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugining.Loading;

public sealed class CommandParameterInfo
{
    public MessageAuthority Authority { get; internal set; }
    public string? Name { get; internal set; }
    public string ParameterName { get; internal init; } = null!;
    public Type ParameterType { get; internal init; } = null!;
    public PropertyInfo PropertyInfo { get; internal init; } = null!;

    public char? Abbr { get; internal set; }
    public bool IsArgument { get; internal set; }

    public IParameterConverter ValueConverter { get; internal set; } = DefaultParameterConverter.Instance;

    public object? DefaultValue { get; internal set; }
    public string? Description { get; internal set; }
    public bool IsServiceArgument { get; internal set; }
}