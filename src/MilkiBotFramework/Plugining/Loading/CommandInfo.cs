using System.Collections.Generic;
using System.Reflection;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugining.Loading
{
    public sealed class CommandInfo
    {
        public CommandInfo(string command,
            string description,
            MethodInfo methodInfo,
            CommandReturnType commandReturnType,
            MessageAuthority authority, 
            MessageType messageType,
            IReadOnlyList<CommandParameterInfo> parameterInfos)
        {
            Command = command;
            Description = description;
            MethodInfo = methodInfo;
            CommandReturnType = commandReturnType;
            Authority = authority;
            MessageType = messageType;
            ParameterInfos = parameterInfos;
        }

        public string Command { get; }
        public string Description { get; }
        public MethodInfo MethodInfo { get; }
        public CommandReturnType CommandReturnType { get; }
        public MessageAuthority Authority { get; }
        public MessageType MessageType { get; }
        public IReadOnlyList<CommandParameterInfo> ParameterInfos { get; }
        public ModelBindingInfo? ModelBindingInfo { get; internal set; }
    }
}
