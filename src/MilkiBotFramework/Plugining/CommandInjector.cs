using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining;

public class CommandInjector
{
    private readonly ICommandLineAnalyzer _commandLineAnalyzer;
    private readonly ILogger<CommandInjector> _logger;

    public CommandInjector(ICommandLineAnalyzer commandLineAnalyzer, ILogger<CommandInjector> logger)
    {
        _commandLineAnalyzer = commandLineAnalyzer;
        _logger = logger;
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
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (commandInfo.Authority > messageContext.Authority)
        {
            throw new BindingException(
                "The specified command needs a higher authority. Current: " + messageContext.Authority +
                "; Desired: " + commandInfo.Authority,
                new BindingSource(commandInfo, null), BindingFailureType.AuthenticationFailed);
        }

        if (messageContext.MessageIdentity == null ||
            !commandInfo.MessageType.HasFlag(messageContext.MessageIdentity.MessageType))
        {
            throw new BindingException(
                "The specified command only supports message type: " + commandInfo.MessageType +
                "; Current: " + messageContext.MessageIdentity?.MessageType,
                new BindingSource(commandInfo, null), BindingFailureType.MessageTypeFailed);
        }

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
                    var model = GetBindingModel(paramDef.ParameterType, messageContext, commandInfo, options, commandLineResult.Arguments);
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
                    var argValue = GetArgumentValue(commandInfo, messageContext, commandLineResult.Arguments, paramDef, ref argIndex);
                    parameters[i] = argValue;
                }
                else
                {
                    var optionValue = GetOptionValue(commandInfo, messageContext, options, paramDef);
                    parameters[i] = optionValue;
                }
            }
        }


        var method = commandInfo.MethodInfo;
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
            case CommandReturnType.Task_:
                {
                    var task = (Task)method.Invoke(obj, parameters)!;
                    await task.ConfigureAwait(false);
                    _logger.LogWarning($"No response will generated because the command \"" + commandInfo.Command +
                                       "\" was defined as an unknown return type: " + commandInfo.MethodInfo.ReturnType);
                    yield break;
                }
            case CommandReturnType.ValueTask_:
                {
                    var valueTask = (dynamic)method.Invoke(obj, parameters)!; // Will lead to performance issue!!
                    await valueTask.ConfigureAwait(false);
                    _logger.LogWarning($"No response will generated because the command \"" + commandInfo.Command +
                                       "\" was defined as an unknown return type: " + commandInfo.MethodInfo.ReturnType);
                    yield break;
                }
            case CommandReturnType.Unknown:
            default:
                {
                    method.Invoke(obj, parameters);
                    _logger.LogWarning($"No response will generated because the command \"" + commandInfo.Command +
                                       "\" was defined as an unknown return type: " + commandInfo.MethodInfo.ReturnType);
                    yield break;
                }
        }
    }

    private object? GetBindingModel(Type parameterType,
        MessageContext messageContext,
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

            modelBindingInfo = new ModelBindingInfo(parameterType, parameterInfos);
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
                var argValue = GetArgumentValue(commandInfo, messageContext, arguments, paramDef, ref argIndex);
                paramDef.PropertyInfo.SetValue(instance, argValue);
            }
            else
            {
                var optionValue = GetOptionValue(commandInfo, messageContext, options, paramDef);
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
            ParameterName = property.Name,
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

    private static object? GetArgumentValue(CommandInfo commandInfo,
        MessageContext messageContext,
        IReadOnlyList<ReadOnlyMemory<char>> arguments,
        CommandParameterInfo paramDef,
        ref int argIndex)
    {
        object? argValue;
        if (argIndex >= arguments.Count)
        {
            if (paramDef.DefaultValue == DBNull.Value)
            {
                throw new BindingException("The specified argument is not found in the input command.",
                    new BindingSource(commandInfo, paramDef), BindingFailureType.Mismatch);
            }

            try
            {
                argValue = paramDef.DefaultValue is string && paramDef.ParameterType != StaticTypes.String
                    ? paramDef.ValueConverter.Convert(paramDef.ParameterType, ((string)paramDef.DefaultValue).AsMemory())
                    : paramDef.DefaultValue;
            }
            catch (Exception ex)
            {
                throw new BindingException("Convert error",
                    new BindingSource(commandInfo, paramDef), BindingFailureType.ConvertError, ex);
            }
        }
        else
        {
            try
            {
                var currentArgument = arguments[argIndex++];
                argValue = paramDef.ValueConverter.Convert(paramDef.ParameterType, currentArgument);
            }
            catch (Exception ex)
            {
                throw new BindingException("Convert error",
                    new BindingSource(commandInfo, paramDef), BindingFailureType.ConvertError, ex);
            }

            if (EqualityComparer<object>.Default.Equals(argValue, paramDef.DefaultValue)) return argValue;
            if (paramDef.Authority > messageContext.Authority)
            {
                throw new BindingException(
                    "The specified argument needs a higher authority to change the default value. Current: " + messageContext.Authority +
                    "; Desired: " + commandInfo.Authority,
                    new BindingSource(commandInfo, paramDef), BindingFailureType.AuthenticationFailed);
            }
        }

        return argValue;
    }

    private static object? GetOptionValue(CommandInfo commandInfo,
        MessageContext messageContext,
        IReadOnlyDictionary<string, ReadOnlyMemory<char>?> options,
        CommandParameterInfo paramDef)
    {
        object? optionValue;
        if (options.TryGetValue(paramDef.Name!, out var value))
        {
            try
            {
                optionValue = value == null
                ? true
                : paramDef.ValueConverter.Convert(paramDef.ParameterType, value.Value);
            }
            catch (Exception ex)
            {
                throw new BindingException("Convert error",
                    new BindingSource(commandInfo, paramDef), BindingFailureType.ConvertError, ex);
            }

            if (EqualityComparer<object>.Default.Equals(optionValue, paramDef.DefaultValue)) return optionValue;
            if (paramDef.Authority > messageContext.Authority)
            {
                throw new BindingException(
                    "The specified option needs a higher authority to change the default value. Current: " + messageContext.Authority +
                    "; Desired: " + commandInfo.Authority,
                    new BindingSource(commandInfo, paramDef), BindingFailureType.AuthenticationFailed);
            }
        }
        else
        {
            if (paramDef.DefaultValue == DBNull.Value)
            {
                throw new BindingException("The specified option is not found in the input command.",
                    new BindingSource(commandInfo, paramDef), BindingFailureType.Mismatch);
            }

            try
            {
                optionValue = paramDef.DefaultValue is string && paramDef.ParameterType != StaticTypes.String
                ? paramDef.ValueConverter.Convert(paramDef.ParameterType, ((string)paramDef.DefaultValue).AsMemory())
                : paramDef.DefaultValue;
            }
            catch (Exception ex)
            {
                throw new BindingException("Convert error",
                    new BindingSource(commandInfo, paramDef), BindingFailureType.ConvertError, ex);
            }
        }

        return optionValue;
    }
}