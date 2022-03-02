using System;
using System.Collections.Generic;
using System.Reflection;

namespace MilkiBotFramework.Plugining.Loading
{
    public sealed class CommandInfo
    {
        public CommandInfo(string command,
            string description,
            MethodInfo methodInfo,
            CommandReturnType commandReturnType,
            IReadOnlyList<CommandParameterInfo> parameterInfos)
        {
            Command = command;
            Description = description;
            MethodInfo = methodInfo;
            CommandReturnType = commandReturnType;
            ParameterInfos = parameterInfos;
        }

        public string Command { get; }
        public string Description { get; }
        public MethodInfo MethodInfo { get; }
        public CommandReturnType CommandReturnType { get; }
        public IReadOnlyList<CommandParameterInfo> ParameterInfos { get; }
        public ModelBindingInfo? ModelBindingInfo { get; internal set; }
    }

    public class ModelBindingInfo
    {
        public Type TargetType { get; init; }
        public List<CommandParameterInfo> ParameterInfos { get; init; }
    }
}
