using System;
using System.Reflection;

namespace MilkiBotFramework.Plugining.Loading;

public sealed class CommandParameterInfo
{
    public string Name { get; internal set; }
    public string ParameterName { get; internal set; }
    public Type ParameterType { get; internal set; }
    public PropertyInfo? PropertyInfo { get; internal set; }

    public char? Abbr { get; internal set; }
    public bool IsArgument { get; internal set; }

    public IParameterConverter ValueConverter { get; internal set; } = DefaultParameterConverter.Instance;

    public object? DefaultValue { get; internal set; }
    public string? Description { get; internal set; }
    public bool IsServiceArgument { get; internal set; }
}