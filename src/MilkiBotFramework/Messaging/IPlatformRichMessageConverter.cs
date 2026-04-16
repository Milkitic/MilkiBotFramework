namespace MilkiBotFramework.Messaging;

public interface IPlatformRichMessageConverter : IRichMessageConverter
{
    string PlatformId { get; }
}