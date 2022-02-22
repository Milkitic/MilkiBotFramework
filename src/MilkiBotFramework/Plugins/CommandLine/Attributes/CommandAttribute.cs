using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MilkiBotFramework.Plugins.CommandLine.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CommandAttribute : Attribute
{
}