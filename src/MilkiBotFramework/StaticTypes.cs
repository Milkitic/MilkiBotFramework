using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;

// ReSharper disable InconsistentNaming

namespace MilkiBotFramework;

public static class StaticTypes
{
    public static readonly Type IResponse = typeof(IResponse);
    public static readonly Type BasicPlugin = typeof(BasicPlugin);
    public static readonly Type BasicPlugin_ = typeof(BasicPlugin<>);
    public static readonly Type ServicePlugin = typeof(ServicePlugin);
    public static readonly Type MessageContext = typeof(MessageContext);

    public static readonly Type Void = typeof(void);
    public static readonly Type Task = typeof(Task);
    public static readonly Type ValueTask = typeof(ValueTask);
    public static readonly Type Task_ = typeof(Task<>);
    public static readonly Type ValueTask_ = typeof(ValueTask<>);
    public static readonly Type IEnumerable_ = typeof(IEnumerable<>);
    public static readonly Type IAsyncEnumerable_ = typeof(IAsyncEnumerable<>);

    public static readonly Type Boolean = typeof(bool);
    public static readonly Type Byte = typeof(byte);
    public static readonly Type Sbyte = typeof(sbyte);
    public static readonly Type UInt16 = typeof(ushort);
    public static readonly Type UInt32 = typeof(uint);
    public static readonly Type UInt64 = typeof(ulong);
    public static readonly Type Int16 = typeof(short);
    public static readonly Type Int32 = typeof(int);
    public static readonly Type Int64 = typeof(long);
    public static readonly Type Single = typeof(float);
    public static readonly Type Double = typeof(double);
    public static readonly Type String = typeof(string);
    public static readonly Type ReadOnlyMemory_Char = typeof(ReadOnlyMemory<char>);

    public static readonly Type TypeNullable = typeof(Nullable<>);
    public static readonly Type Enum = typeof(Enum);

    public static readonly Type Timespan = typeof(TimeSpan);
}