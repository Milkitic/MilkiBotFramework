using System;

namespace MilkiBotFramework.Plugins.CommandLine.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class ArgumentAttribute : ParameterAttribute
{
}