namespace MilkiBotFramework.Messaging;

[Flags]
public enum MessageType
{
    Private = 1, Channel = 2, Notice = 4, Meta = 8
}