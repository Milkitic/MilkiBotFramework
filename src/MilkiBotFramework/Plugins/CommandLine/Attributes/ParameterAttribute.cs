using System;

namespace MilkiBotFramework.Plugins.CommandLine.Attributes;

public abstract class ParameterAttribute : Attribute
{
    private Type? _converter;
    public object? DefaultValue { get; set; }

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

            if (value.GetInterface(nameof(IValueConverter)) == null)
                throw new InvalidOperationException();
            _converter = value;
        }
    }
}