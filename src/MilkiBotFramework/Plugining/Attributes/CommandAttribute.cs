using System;

namespace MilkiBotFramework.Plugining.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CommandAttribute : Attribute
{
}