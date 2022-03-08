using MilkiBotFramework.Messaging;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace MilkiBotFramework.Plugining.Configuration;

public class MessageIdentityConverter : IYamlTypeConverter
{
    private readonly Type _memberInfo = typeof(MessageIdentity);
    public bool Accepts(Type type)
    {
        return type == _memberInfo;
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        var s = parser.Consume<Scalar>();
        var str = s.Value;
        return MessageIdentity.Parse(str);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        emitter.Emit(new Scalar(value?.ToString() ?? ""));
    }
}