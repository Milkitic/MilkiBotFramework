using System;

namespace MilkiBotFramework.Plugins.CommandLine;

public class CommandLineException : Exception
{
    public CommandLineException(string? message) : base(message)
    {

    }
}