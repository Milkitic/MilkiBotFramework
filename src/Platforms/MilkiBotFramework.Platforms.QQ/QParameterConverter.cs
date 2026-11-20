using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Platforms.QQ;

/// <summary>为历史 QQ 命令参数格式补充链接图片转换。</summary>
public class QParameterConverter : DefaultParameterConverter
{
    /// <summary>将包含图片 URL 的文本转换为 <see cref="LinkImage"/>，其他参数交给默认转换器。</summary>
    /// <param name="targetType">命令参数的目标类型。</param>
    /// <param name="source">待转换的参数文本。</param>
    /// <returns>转换得到的参数。</returns>
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
