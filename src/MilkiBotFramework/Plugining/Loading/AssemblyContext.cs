﻿using System.Collections.Generic;
using System.Reflection;

namespace MilkiBotFramework.Plugining.Loading;

internal class AssemblyContext
{
    public string AssemblyName { get; init; }
    public Assembly Assembly { get; init; }
    public List<PluginDefinition> PluginDefinitions { get; } = new();
}