using System;

namespace MilkiBotFramework.Plugins
{
    public sealed class PluginCommandDefinition
    {
        public PluginCommandDefinition(string command,
            string description,
            string sourceMethodName,
            ParameterDefinition[] argumentDefinitions)
        {
            Command = command;
            Description = description;
            SourceMethodName = sourceMethodName;
            ArgumentDefinitions = argumentDefinitions;
        }

        public string Command { get; }
        public string Description { get; }
        public string SourceMethodName { get; }
        public ParameterDefinition[] ArgumentDefinitions { get; }
    }

    public sealed class ParameterDefinition
    {
        private IValueConverter? _valueConverter = DefaultConverter.Instance;

        public ParameterDefinition(string name, string parameterName, Type parameterType)
        {
            Name = name;
            ParameterName = parameterName;
            ParameterType = parameterType;
        }

        public string Name { get; }
        public string ParameterName { get; }
        public Type ParameterType { get; }

        public char? Abbr { get; init; }
        public bool IsArgument { get; init; }

        public IValueConverter? ValueConverter
        {
            get => _valueConverter;
            set => _valueConverter = value ?? DefaultConverter.Instance;
        }

        public object? DefaultValue { get; init; }
        public string? HelpText { get; init; }
    }
}
