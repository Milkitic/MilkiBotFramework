using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.CommandLine;

public class CommandLineInjector
{
    private static readonly Type _messageContextType = typeof(MessageContext);

    private readonly ICommandLineAnalyzer _commandLineAnalyzer;

    public CommandLineInjector(ICommandLineAnalyzer commandLineAnalyzer)
    {
        _commandLineAnalyzer = commandLineAnalyzer;
    }

    public async Task InjectParameters(string input,
        PluginCommandDefinition commandDefinition,
        PluginBase obj,
        MessageContext messageContext,
        IServiceProvider serviceProvider)
    {
        var success = _commandLineAnalyzer.TryAnalyze(input, out var result, out var ex);
        if (!success) throw ex!;
        await InjectParametersAndRunAsync(result!, commandDefinition, obj, messageContext, serviceProvider);
    }

    public async Task InjectParametersAndRunAsync(CommandLineResult commandLineResult,
        PluginCommandDefinition commandDefinition,
        PluginBase obj,
        MessageContext messageContext,
        IServiceProvider serviceProvider)
    {
        var parameterDefinitions = commandDefinition.ParameterDefinitions;
        var parameterCount = parameterDefinitions.Count;

        var parameters = parameterCount == 0
            ? Array.Empty<object?>()
            : new object?[parameterCount];

        for (var i = 0; i < parameters.Length; i++)
        {
            parameters[i] = DBNull.Value;
        }

        var options = commandLineResult.Options
            .ToDictionary(k => k.Key.ToString(), k => k.Value);

        bool modelBind = false;
        bool paramBind = false;
        int argIndex = 0;
        for (var i = 0; i < parameterCount; i++)
        {
            var paramDef = parameterDefinitions[i];

            if (paramDef.IsServiceArgument)
            {
                if (paramDef.ParameterType == _messageContextType)
                {
                    parameters[i] = messageContext;
                    continue;
                }

                var result = serviceProvider.GetService(paramDef.ParameterType);
                if (result == null)
                {
                    if (modelBind)
                        throw new ArgumentException(
                            $"Could not resolve type {paramDef.ParameterType}. Only one model binding declaration is supported.");
                    if (paramBind)
                        throw new ArgumentException(
                            $"Could not resolve type {paramDef.ParameterType}. Combination of model binding and parameter binding is not supported.");
                    modelBind = true;

                    // model binding
                    var model = GetBindingModel(paramDef.ParameterType, commandDefinition, options, commandLineResult.Arguments);
                    parameters[i] = model;
                }
                else
                {
                    parameters[i] = result;
                }
            }
            else
            {
                if (modelBind)
                    throw new ArgumentException(
                        $"Could not resolve type {paramDef.ParameterType}. Combination of model binding and parameter binding is not supported.");
                paramBind = true;

                // parameter binding
                if (paramDef.IsArgument)
                {
                    var argValue = GetArgumentValue(commandLineResult.Arguments, paramDef, ref argIndex);
                    parameters[i] = argValue;
                }
                else
                {
                    var optionValue = GetOptionValue(options, paramDef);
                    parameters[i] = optionValue;
                }
            }
        }

        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var method = commandDefinition.MethodInfo/* obj.GetType().GetMethod(commandDefinition.MethodInfo)*/;
        var retType = commandDefinition.CommandReturnType;
        switch (retType)
        {
            case CommandReturnType.Void:
                method.Invoke(obj, parameters);
                break;
            case CommandReturnType.Task:
                await (Task)method.Invoke(obj, parameters)!;
                break;
            case CommandReturnType.ValueTask:
                await (ValueTask)method.Invoke(obj, parameters)!;
                break;
            case CommandReturnType.Dynamic:
                throw new ArgumentException($"Unknown return type of method {method}");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private object? GetBindingModel(Type parameterType,
        PluginCommandDefinition commandDefinition,
        Dictionary<string, ReadOnlyMemory<char>?> options,
        List<ReadOnlyMemory<char>> arguments)
    {
        ModelBindingDefinition modelBindingDefinition;
        if (commandDefinition.ModelBindingDefinition == null)
        {
            var props = parameterType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(k => k.SetMethod is { IsPublic: true });
            var parameterDefinitions = new List<ParameterDefinition>();
            foreach (var propertyInfo in props)
            {
                var targetType = propertyInfo.PropertyType;
                var attrs = propertyInfo.GetCustomAttributes(false);
                var parameterDefinition = GetParameterDefinition(attrs, targetType, propertyInfo);
                if (parameterDefinition != null) parameterDefinitions.Add(parameterDefinition);
            }

            modelBindingDefinition = new ModelBindingDefinition
            {
                TargetType = parameterType,
                ParameterDefinitions = parameterDefinitions
            };
            commandDefinition.ModelBindingDefinition = modelBindingDefinition;
        }
        else
        {
            modelBindingDefinition = commandDefinition.ModelBindingDefinition;
        }

        var instance = Activator.CreateInstance(parameterType);
        int argIndex = 0;
        foreach (var paramDef in modelBindingDefinition.ParameterDefinitions)
        {
            if (paramDef.IsArgument)
            {
                var argValue = GetArgumentValue(arguments, paramDef, ref argIndex);
                paramDef.PropertyInfo.SetValue(instance, argValue);
            }
            else
            {
                var optionValue = GetOptionValue(options, paramDef);
                paramDef.PropertyInfo.SetValue(instance, optionValue);
            }
        }

        return instance;
    }

    private ParameterDefinition? GetParameterDefinition(object[] attrs,
        Type targetType,
        PropertyInfo property)
    {
        var parameterDefinition = new ParameterDefinition
        {
            ParameterName = property.Name!,
            ParameterType = targetType,
            PropertyInfo = property
        };

        bool isReady = false;
        foreach (var attr in attrs)
        {
            if (attr is OptionAttribute option)
            {
                parameterDefinition.Abbr = option.Abbreviate;
                parameterDefinition.DefaultValue = option.DefaultValue;
                parameterDefinition.Name = option.Name;
                parameterDefinition.ValueConverter = _commandLineAnalyzer.DefaultParameterConverter;
                isReady = true;
            }
            else if (attr is ArgumentAttribute argument)
            {
                parameterDefinition.DefaultValue = argument.DefaultValue;
                parameterDefinition.IsArgument = true;
                parameterDefinition.ValueConverter = _commandLineAnalyzer.DefaultParameterConverter;
                isReady = true;
            }
            else if (attr is DescriptionAttribute description)
            {
                parameterDefinition.Description = description.Description;
                //parameterDefinition.HelpAuthority = help.Authority;
            }
        }

        return isReady ? parameterDefinition : null;
    }

    private static object? GetArgumentValue(IReadOnlyList<ReadOnlyMemory<char>> arguments, ParameterDefinition paramDef,
        ref int argIndex)
    {
        object? argValue;
        if (argIndex >= arguments.Count)
        {
            if (paramDef.DefaultValue == DBNull.Value)
            {
                throw new Exception("The specified argument is not found in the input command.");
            }

            argValue = paramDef.DefaultValue is string
                ? paramDef.ValueConverter.Convert(paramDef.ParameterType, ((string)paramDef.DefaultValue).AsMemory())
                : paramDef.DefaultValue;
        }
        else
        {
            var currentArgument = arguments[argIndex++];
            argValue = paramDef.ValueConverter.Convert(paramDef.ParameterType, currentArgument);
        }

        return argValue;
    }

    private static object? GetOptionValue(IReadOnlyDictionary<string, ReadOnlyMemory<char>?> options,
        ParameterDefinition paramDef)
    {
        object? optionValue;
        if (options.TryGetValue(paramDef.Name, out var value))
        {
            optionValue = value == null
                ? true
                : paramDef.ValueConverter.Convert(paramDef.ParameterType, value.Value);
        }
        else
        {
            if (paramDef.DefaultValue == DBNull.Value)
            {
                throw new Exception("The specified option is not found in the input command.");
            }

            optionValue = paramDef.DefaultValue is string
                ? paramDef.ValueConverter.Convert(paramDef.ParameterType, ((string)paramDef.DefaultValue).AsMemory())
                : paramDef.DefaultValue;
        }

        return optionValue;
    }
}