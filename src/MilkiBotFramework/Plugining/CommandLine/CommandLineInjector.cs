using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.CommandLine;

public class CommandLineInjector
{
    private readonly ICommandLineAnalyzer _commandLineAnalyzer;

    public CommandLineInjector(ICommandLineAnalyzer commandLineAnalyzer)
    {
        _commandLineAnalyzer = commandLineAnalyzer;
    }

    public async Task InjectParameters<T>(string input,
        PluginCommandDefinition commandDefinition,
        T obj,
        IServiceProvider serviceProvider) where T : PluginBase
    {
        var success = _commandLineAnalyzer.TryAnalyze(input, out var result, out var ex);
        if (!success) throw ex!;
        await InjectParametersAndRunAsync(result!, commandDefinition, obj, serviceProvider);
    }

    public async Task InjectParametersAndRunAsync<T>(CommandLineResult commandLineResult,
        PluginCommandDefinition commandDefinition,
        T obj,
        IServiceProvider serviceProvider) where T : PluginBase
    {
        bool modelBind = false;
        bool parameterBind = false;
        foreach (var definition in commandDefinition.ParameterDefinitions)
        {
            if (definition.IsServiceArgument)
            {
                var result = serviceProvider.GetService(definition.ParameterType);
                if (result == null)
                {
                    if (modelBind)
                        throw new ArgumentException($"Could not resolve type {definition.ParameterType}. Only one model binding declaration is supported.");
                    if (parameterBind)
                        throw new ArgumentException($"Could not resolve type {definition.ParameterType}. Combination of model binding and parameter binding is not supported.");
                    modelBind = true;

                    var model = GetBindingModel(definition.ParameterType, commandDefinition, commandLineResult);
                    // model binding
                }
                else
                {
                    // di
                }
            }
            else
            {
                if (modelBind)
                    throw new ArgumentException($"Could not resolve type {definition.ParameterType}. Combination of model binding and parameter binding is not supported.");
                parameterBind = true;

                // parameter binding
            }
        }

        if (obj == null) throw new ArgumentNullException(nameof(obj));
    }

    private object GetBindingModel(Type parameterType, PluginCommandDefinition commandDefinition, CommandLineResult commandLineResult)
    {
        throw new NotImplementedException();
    }
}