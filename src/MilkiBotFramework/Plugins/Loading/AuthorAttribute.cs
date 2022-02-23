using System;

namespace MilkiBotFramework.Plugins.Loading;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AuthorAttribute : Attribute
{
    public AuthorAttribute(params string[] author)
    {
        Author = author;
    }
    public string[] Author { get; }
}