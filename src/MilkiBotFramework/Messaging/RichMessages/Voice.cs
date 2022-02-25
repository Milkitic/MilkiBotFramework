﻿namespace MilkiBotFramework.Messaging.RichMessages;

public class Voice : IRichMessage
{
    public Voice(string path) => Path = path;
    public string Path { get; set; }
    public virtual string Encode() => "[Voice]";
}