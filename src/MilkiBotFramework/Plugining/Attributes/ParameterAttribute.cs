using System;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.Attributes;

public abstract class ParameterAttribute : Attribute
{
    private Type? _converter;
    public object? DefaultValue { get; set; } = DBNull.Value;

    public Type? Converter
    {
        get => _converter;
        set
        {
            if (value == null)
            {
                _converter = null;
                return;
            }

            if (value.GetInterface(nameof(IParameterConverter)) == null)
                throw new InvalidOperationException();
            _converter = value;
        }
    }
}