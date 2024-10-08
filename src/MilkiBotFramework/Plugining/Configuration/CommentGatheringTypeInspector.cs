﻿using System.ComponentModel;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeInspectors;

namespace MilkiBotFramework.Plugining.Configuration;

public class CommentGatheringTypeInspector : TypeInspectorSkeleton
{
    private readonly ITypeInspector _innerTypeDescriptor;

    public CommentGatheringTypeInspector(ITypeInspector innerTypeDescriptor)
    {
        _innerTypeDescriptor = innerTypeDescriptor ?? throw new ArgumentNullException(nameof(innerTypeDescriptor));
    }

    public override string GetEnumName(Type enumType, string name)
    {
        return _innerTypeDescriptor.GetEnumName(enumType, name);
    }

    public override string GetEnumValue(object enumValue)
    {
        return _innerTypeDescriptor.GetEnumValue(enumValue);
    }

    public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container)
    {
        return _innerTypeDescriptor
            .GetProperties(type, container)
            .Select(d => new CommentsPropertyDescriptor(d));
    }

    private sealed class CommentsPropertyDescriptor : IPropertyDescriptor
    {
        private readonly IPropertyDescriptor _baseDescriptor;

        public CommentsPropertyDescriptor(IPropertyDescriptor baseDescriptor)
        {
            _baseDescriptor = baseDescriptor;
            Name = baseDescriptor.Name;
        }


        public void Write(object target, object? value)
        {
            _baseDescriptor.Write(target, value);
        }

        public string Name { get; set; }
        public bool AllowNulls => _baseDescriptor.AllowNulls;
        public bool CanWrite => _baseDescriptor.CanWrite;
        public Type Type => _baseDescriptor.Type;

        public Type? TypeOverride
        {
            get => _baseDescriptor.TypeOverride;
            set => _baseDescriptor.TypeOverride = value;
        }

        public int Order { get; set; }

        public ScalarStyle ScalarStyle
        {
            get => _baseDescriptor.ScalarStyle;
            set => _baseDescriptor.ScalarStyle = value;
        }

        public bool Required => _baseDescriptor.Required;
        public Type? ConverterType => _baseDescriptor.ConverterType;

        public T? GetCustomAttribute<T>() where T : Attribute
        {
            return _baseDescriptor.GetCustomAttribute<T>();
        }

        public IObjectDescriptor Read(object target)
        {
            var description = _baseDescriptor.GetCustomAttribute<DescriptionAttribute>();
            return description != null
                ? new CommentsObjectDescriptor(_baseDescriptor.Read(target), description.Description)
                : _baseDescriptor.Read(target);
        }
    }
}