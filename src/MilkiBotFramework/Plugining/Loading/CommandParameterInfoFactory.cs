using System.ComponentModel;
using System.Reflection;
using MilkiBotFramework.Plugining.Attributes;

namespace MilkiBotFramework.Plugining.Loading;

internal static class CommandParameterInfoFactory
{
    public static CommandParameterInfo CreateForMethodParameter(ParameterInfo parameter,
        IParameterConverter defaultConverter)
    {
        var parameterInfo = new CommandParameterInfo
        {
            ParameterName = parameter.Name!,
            ParameterType = parameter.ParameterType,
        };

        var hasBindingMetadata = ApplyAttributes(parameter.GetCustomAttributes(false), parameterInfo, defaultConverter);
        if (hasBindingMetadata &&
            parameterInfo.DefaultValue == DBNull.Value &&
            parameter.HasDefaultValue)
        {
            parameterInfo.DefaultValue = parameter.DefaultValue;
        }

        if (!hasBindingMetadata)
        {
            parameterInfo.IsServiceArgument = true;
        }

        return parameterInfo;
    }

    public static CommandParameterInfo? CreateForModelProperty(PropertyInfo property,
        IParameterConverter defaultConverter)
    {
        var parameterInfo = new CommandParameterInfo
        {
            ParameterName = property.Name,
            ParameterType = property.PropertyType,
            PropertyInfo = property
        };

        var hasBindingMetadata = ApplyAttributes(property.GetCustomAttributes(false), parameterInfo, defaultConverter);
        return hasBindingMetadata ? parameterInfo : null;
    }

    private static bool ApplyAttributes(object[] attrs,
        CommandParameterInfo parameterInfo,
        IParameterConverter defaultConverter)
    {
        var hasBindingMetadata = false;
        foreach (var attr in attrs)
        {
            switch (attr)
            {
                case OptionAttribute optionAttribute:
                    parameterInfo.Authority = optionAttribute.Authority;
                    parameterInfo.Abbr = optionAttribute.Abbreviate == '\0' ? null : optionAttribute.Abbreviate;
                    parameterInfo.DefaultValue = optionAttribute.DefaultValue;
                    parameterInfo.Name = optionAttribute.Name;
                    parameterInfo.ValueConverter = CreateParameterConverter(optionAttribute.Converter, defaultConverter);
                    hasBindingMetadata = true;
                    break;
                case ArgumentAttribute argumentAttribute:
                    parameterInfo.Authority = argumentAttribute.Authority;
                    parameterInfo.DefaultValue = argumentAttribute.DefaultValue;
                    parameterInfo.ValueConverter = CreateParameterConverter(argumentAttribute.Converter, defaultConverter);
                    parameterInfo.IsArgument = true;
                    hasBindingMetadata = true;
                    break;
                case DescriptionAttribute descriptionAttribute:
                    parameterInfo.Description = descriptionAttribute.Description;
                    break;
            }
        }

        return hasBindingMetadata;
    }

    private static IParameterConverter CreateParameterConverter(Type? converterType,
        IParameterConverter defaultConverter)
    {
        if (converterType == null)
        {
            return defaultConverter;
        }

        return (IParameterConverter)Activator.CreateInstance(converterType)!;
    }
}