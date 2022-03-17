using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Platforms.GoCqHttp;

public class GoCqParameterConverter : DefaultParameterConverter
{
    public override object Convert(Type targetType, ReadOnlyMemory<char> source)
    {
        if (targetType != typeof(LinkImage))
            return base.Convert(targetType, source);

        var span = source.Span;
        var index = span.IndexOf(",url=", StringComparison.Ordinal);

        if (index == -1)
            return base.Convert(targetType, source);

        var subSpan = span[index..];
        var endIndex = subSpan.IndexOf(']');
        if (endIndex == -1)
            return base.Convert(targetType, source);

        return new LinkImage(subSpan.Slice(5, endIndex - 5).ToString());
    }
}