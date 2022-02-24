using System;
using System.Linq;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
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
                    var model = GetBindingModel(paramDef.ParameterType, commandDefinition, commandLineResult);
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
                    object? argValue;
                    if (argIndex >= commandLineResult.Arguments.Count)
                    {
                        if (paramDef.DefaultValue == DBNull.Value)
                        {
                            throw new Exception("The specified argument is not found in the input command.");
                        }

                        argValue = paramDef.DefaultValue;
                    }
                    else
                    {
                        var currentArgument = commandLineResult.Arguments[argIndex++];
                        argValue = paramDef.ValueConverter.Convert(paramDef.ParameterType, currentArgument);
                    }

                    parameters[i] = argValue;
                }
                else
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

                        optionValue = paramDef.DefaultValue;
                    }

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

    private object GetBindingModel(Type parameterType, PluginCommandDefinition commandDefinition, CommandLineResult commandLineResult)
    {
        throw new NotImplementedException();
    }
}