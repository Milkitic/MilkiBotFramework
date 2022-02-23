using System;

namespace MilkiBotFramework.Plugining.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AuthorAttribute : Attribute
{
    public AuthorAttribute(params string[] author)
    {
        Author = author;
    }
    public string[] Author { get; }
}