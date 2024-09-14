using MilkiBotFramework.Messaging;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace MilkiBotFramework.Plugining.Configuration;

public class MessageIdentityConverter : IYamlTypeConverter
{
    private static readonly Type MemberInfo = typeof(MessageIdentity);

    public bool Accepts(Type type)
    {
        return type == MemberInfo;
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var s = parser.Consume<Scalar>();
        var str = s.Value;
        return MessageIdentity.Parse(str);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        emitter.Emit(new Scalar(value?.ToString() ?? ""));
    }
}