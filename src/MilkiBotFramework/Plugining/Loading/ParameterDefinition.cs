using System;

namespace MilkiBotFramework.Plugining.Loading;

public sealed class ParameterDefinition
{
    public string Name { get; internal set; }
    public string ParameterName { get; internal set; }
    public Type ParameterType { get; internal set; }

    public char? Abbr { get; internal set; }
    public bool IsArgument { get; internal set; }

    public IParameterConverter ValueConverter { get; internal set; } = DefaultConverter.Instance;

    public object? DefaultValue { get; internal set; }
    public string? Description { get; internal set; }
    public bool IsServiceArgument { get; set; }
}