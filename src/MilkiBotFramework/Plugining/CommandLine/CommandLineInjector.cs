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
    private static readonly Type _typeString = typeof(string);

    private readonly ICommandLineAnalyzer _commandLineAnalyzer;

    public CommandLineInjector(ICommandLineAnalyzer commandLineAnalyzer)
    {
        _commandLineAnalyzer = commandLineAnalyzer;
    }

    public async IAsyncEnumerable<IResponse?> InjectParameters(string input,
        CommandInfo commandInfo,
        PluginBase obj,
        MessageContext messageContext,
        IServiceProvider serviceProvider)
    {
        var success = _commandLineAnalyzer.TryAnalyze(input, out var result, out var ex);
        if (!success) throw ex!;
        await foreach (var runResult in InjectParametersAndRunAsync(result!, commandInfo, obj, messageContext, serviceProvider))
        {
            yield return runResult;
        }
    }

    public async IAsyncEnumerable<IResponse?> InjectParametersAndRunAsync(CommandLineResult commandLineResult,
        CommandInfo commandInfo,
        PluginBase obj,
        MessageContext messageContext,
        IServiceProvider serviceProvider)
    {
        var parameterInfos = commandInfo.ParameterInfos;
        var parameterCount = parameterInfos.Count;

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
            var paramDef = parameterInfos[i];

            if (paramDef.IsServiceArgument)
            {
                if (paramDef.ParameterType == StaticTypes.MessageContext)
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
                    var model = GetBindingModel(paramDef.ParameterType, commandInfo, options, commandLineResult.Arguments);
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

        var method = commandInfo.MethodInfo/* obj.GetType().GetMethod(commandInfo.MethodInfo)*/;
        var retType = commandInfo.CommandReturnType;
        switch (retType)
        {
            case CommandReturnType.Void:
                method.Invoke(obj, parameters);
                yield break;
            case CommandReturnType.Task:
                await (Task)method.Invoke(obj, parameters)!;
                yield break;
            case CommandReturnType.ValueTask:
                await (ValueTask)method.Invoke(obj, parameters)!;
                yield break;
            case CommandReturnType.IResponse:
                yield return (IResponse)method.Invoke(obj, parameters)!;
                yield break;
            case CommandReturnType.Task_IResponse:
                {
                    var result = (Task<IResponse>)method.Invoke(obj, parameters)!;
                    yield return await result;
                    yield break;
                }
            case CommandReturnType.ValueTask_IResponse:
                {
                    var result = (ValueTask<IResponse>)method.Invoke(obj, parameters)!;
                    yield return await result;
                    yield break;
                }
            case CommandReturnType.IEnumerable_IResponse:
                {
                    var result = (IEnumerable<IResponse>)method.Invoke(obj, parameters)!;
                    foreach (var response in result)
                        yield return response;
                    yield break;
                }
            case CommandReturnType.IAsyncEnumerable_IResponse:
                {
                    var result = (IAsyncEnumerable<IResponse>)method.Invoke(obj, parameters)!;
                    await foreach (var response in result)
                        yield return response;
                    yield break;
                }
            case CommandReturnType.Dynamic:
            default:
                throw new ArgumentOutOfRangeException(nameof(retType), retType, $"Unknown return type of method \"{method.Name}\"");
        }

        throw new NotImplementedException();
        yield break;
    }

    private object? GetBindingModel(Type parameterType,
        CommandInfo commandInfo,
        Dictionary<string, ReadOnlyMemory<char>?> options,
        List<ReadOnlyMemory<char>> arguments)
    {
        ModelBindingInfo modelBindingInfo;
        if (commandInfo.ModelBindingInfo == null)
        {
            var props = parameterType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(k => k.SetMethod is { IsPublic: true });
            var parameterInfos = new List<CommandParameterInfo>();
            foreach (var propertyInfo in props)
            {
                var targetType = propertyInfo.PropertyType;
                var attrs = propertyInfo.GetCustomAttributes(false);
                var parameterInfo = GetParameterInfo(attrs, targetType, propertyInfo);
                if (parameterInfo != null) parameterInfos.Add(parameterInfo);
            }

            modelBindingInfo = new ModelBindingInfo
            {
                TargetType = parameterType,
                ParameterInfos = parameterInfos
            };
            commandInfo.ModelBindingInfo = modelBindingInfo;
        }
        else
        {
            modelBindingInfo = commandInfo.ModelBindingInfo;
        }

        var instance = Activator.CreateInstance(parameterType);
        int argIndex = 0;
        foreach (var paramDef in modelBindingInfo.ParameterInfos)
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

    private CommandParameterInfo? GetParameterInfo(object[] attrs,
        Type targetType,
        PropertyInfo property)
    {
        var parameterInfo = new CommandParameterInfo
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
                parameterInfo.Abbr = option.Abbreviate;
                parameterInfo.DefaultValue = option.DefaultValue;
                parameterInfo.Name = option.Name;
                parameterInfo.ValueConverter = _commandLineAnalyzer.DefaultParameterConverter;
                isReady = true;
            }
            else if (attr is ArgumentAttribute argument)
            {
                parameterInfo.DefaultValue = argument.DefaultValue;
                parameterInfo.IsArgument = true;
                parameterInfo.ValueConverter = _commandLineAnalyzer.DefaultParameterConverter;
                isReady = true;
            }
            else if (attr is DescriptionAttribute description)
            {
                parameterInfo.Description = description.Description;
                //parameterInfo.HelpAuthority = help.Authority;
            }
        }

        return isReady ? parameterInfo : null;
    }

    private static object? GetArgumentValue(IReadOnlyList<ReadOnlyMemory<char>> arguments, CommandParameterInfo paramDef,
        ref int argIndex)
    {
        object? argValue;
        if (argIndex >= arguments.Count)
        {
            if (paramDef.DefaultValue == DBNull.Value)
            {
                throw new Exception("The specified argument is not found in the input command.");
            }

            argValue = paramDef.DefaultValue is string && paramDef.ParameterType != _typeString
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
        CommandParameterInfo paramDef)
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

            optionValue = paramDef.DefaultValue is string && paramDef.ParameterType != _typeString
                ? paramDef.ValueConverter.Convert(paramDef.ParameterType, ((string)paramDef.DefaultValue).AsMemory())
                : paramDef.DefaultValue;
        }

        return optionValue;
    }
}