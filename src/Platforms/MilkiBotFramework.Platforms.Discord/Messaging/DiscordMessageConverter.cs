using System.Text;
using System.Text.RegularExpressions;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.Discord.Messaging;

public class DiscordMessageConverter : IRichMessageConverter
{
    // 匹配 Discord 用户提及格式: <@userId> 或 <@!userId>
    private static readonly Regex AtPattern = new(@"<@!?(\d+)>", RegexOptions.Compiled);

    // 匹配 Discord 频道引用格式: <#channelId>
    private static readonly Regex ChannelPattern = new(@"<#(\d+)>", RegexOptions.Compiled);

    // 匹配 Discord 角色提及格式: <@&roleId>
    private static readonly Regex RolePattern = new(@"<@&(\d+)>", RegexOptions.Compiled);

    public async ValueTask<string> EncodeAsync(IRichMessage message)
    {
        if (message is RichMessage rich)
        {
            var sb = new StringBuilder();
            foreach (var subMessage in rich.RichMessages)
                sb.Append(await EncodeAsync(subMessage));
            return sb.ToString();
        }

        if (message is At at)
            return $"<@{at.UserId}>";
        if (message is Reply)
            return string.Empty; // Discord 通过 MessageReference 处理回复
        if (message is Text text)
            return text.Content;
        if (message is MemoryImage or FileImage or LinkImage)
            return string.Empty; // 图片通过附件发送

        return await message.EncodeAsync();
    }

    public RichMessage Decode(ReadOnlyMemory<char> message)
    {
        var text = message.ToString();
        var richMessages = new List<IRichMessage>();

        // 使用联合匹配拆分消息，解析 Discord 特殊格式
        var combinedPattern = new Regex(@"(<@!?\d+>)|(<#\d+>)|(<@&\d+>)", RegexOptions.Compiled);
        var lastIndex = 0;

        foreach (Match match in combinedPattern.Matches(text))
        {
            // 添加匹配前的普通文本
            if (match.Index > lastIndex)
            {
                var plainText = text[lastIndex..match.Index];
                if (plainText.Length > 0)
                    richMessages.Add(new Text(plainText));
            }

            var matchValue = match.Value;

            // 解析用户提及 <@userId> 或 <@!userId>
            var atMatch = AtPattern.Match(matchValue);
            if (atMatch.Success)
            {
                richMessages.Add(new At(atMatch.Groups[1].Value));
                lastIndex = match.Index + match.Length;
                continue;
            }

            // 解析频道引用 <#channelId> - 替换为文本表示（框架无 ChannelReference 类型）
            var channelMatch = ChannelPattern.Match(matchValue);
            if (channelMatch.Success)
            {
                richMessages.Add(new Text($"#{channelMatch.Groups[1].Value}"));
                lastIndex = match.Index + match.Length;
                continue;
            }

            // 解析角色提及 <@&roleId> - 替换为文本表示
            var roleMatch = RolePattern.Match(matchValue);
            if (roleMatch.Success)
            {
                richMessages.Add(new Text($"@&{roleMatch.Groups[1].Value}"));
                lastIndex = match.Index + match.Length;
                continue;
            }
        }

        // 添加剩余的普通文本
        if (lastIndex < text.Length)
        {
            var remaining = text[lastIndex..];
            if (remaining.Length > 0)
                richMessages.Add(new Text(remaining));
        }

        return richMessages.Count == 0
            ? new RichMessage(new Text(text))
            : new RichMessage(richMessages);
    }
}