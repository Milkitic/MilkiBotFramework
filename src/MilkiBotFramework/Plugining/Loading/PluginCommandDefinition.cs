using System;
using System.Collections.Generic;

namespace MilkiBotFramework.Plugining.Loading
{
    public sealed class PluginCommandDefinition
    {
        public PluginCommandDefinition(string command,
            string description,
            string sourceMethodName,
            IReadOnlyList<ParameterDefinition> parameterDefinitions)
        {
            Command = command;
            Description = description;
            SourceMethodName = sourceMethodName;
            ParameterDefinitions = parameterDefinitions;
        }

        public string Command { get; }
        public string Description { get; }
        public string SourceMethodName { get; }
        public IReadOnlyList<ParameterDefinition> ParameterDefinitions { get; }
    }

    public sealed class ParameterDefinition
    {
        private IValueConverter? _valueConverter = DefaultConverter.Instance;

        public string Name { get; internal set; }
        public string ParameterName { get; internal set; }
        public Type ParameterType { get; internal set; }

        public char? Abbr { get; internal set; }
        public bool IsArgument { get; internal set; }

        public IValueConverter? ValueConverter
        {
            get => _valueConverter;
            internal set => _valueConverter = value ?? DefaultConverter.Instance;
        }

        public object? DefaultValue { get; internal set; }
        public string? Description { get; internal set; }
        public bool IsServiceArgument { get; set; }
    }
}
