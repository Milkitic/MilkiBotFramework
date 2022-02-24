using System;

namespace MilkiBotFramework.Plugining.Loading;

public class DefaultParameterConverter : IParameterConverter
{
    public static DefaultParameterConverter Instance { get; } = new();

    private static readonly Type _typeBoolean = typeof(bool);
    private static readonly Type _typeByte = typeof(byte);
    private static readonly Type _typeSbyte = typeof(sbyte);
    private static readonly Type _typeUInt16 = typeof(ushort);
    private static readonly Type _typeUInt32 = typeof(uint);
    private static readonly Type _typeUInt64 = typeof(ulong);
    private static readonly Type _typeInt16 = typeof(short);
    private static readonly Type _typeInt32 = typeof(int);
    private static readonly Type _typeInt64 = typeof(long);
    private static readonly Type _typeSingle = typeof(float);
    private static readonly Type _typeDouble = typeof(double);
    private static readonly Type _typeString = typeof(string);
    private static readonly Type _typeRoMemoryChar = typeof(ReadOnlyMemory<char>);

    private static readonly Type _typeNullable = typeof(Nullable<>);
    private static readonly Type _typeEnum = typeof(Enum);

    private static readonly Type _typeTimespan = typeof(TimeSpan);

    public virtual object Convert(Type targetType, ReadOnlyMemory<char> source)
    {
        var type = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == _typeNullable
            ? targetType.GetGenericArguments()[0]
            : targetType;

        if (type == _typeByte) return byte.Parse(source.Span);
        if (type == _typeSbyte) return sbyte.Parse(source.Span);
        if (type == _typeUInt16) return ushort.Parse(source.Span);
        if (type == _typeUInt32) return uint.Parse(source.Span);
        if (type == _typeUInt64) return ulong.Parse(source.Span);
        if (type == _typeInt16) return short.Parse(source.Span);
        if (type == _typeInt32) return int.Parse(source.Span);
        if (type == _typeInt64) return long.Parse(source.Span);
        if (type == _typeSingle) return float.Parse(source.Span);
        if (type == _typeDouble) return double.Parse(source.Span);
        if (type == _typeString) return source.ToString();
        if (type == _typeRoMemoryChar) return source;
        if (type == _typeBoolean)
        {
            if (source.Length == 0) return true;
            if (source.Length == 1)
            {
                if (source.Span[0] == '0') return false;
                if (source.Span[0] == '1') return true;
                throw new ArgumentException(null, nameof(source));
            }

            return bool.Parse(source.Span);
        }

        if (type.IsSubclassOf(_typeEnum))
        {
            if (int.TryParse(source.Span, out var value))
            {
                var convert = Enum.ToObject(type, int.Parse(source.Span));
                if (!Enum.IsDefined(type, convert))
                    throw new ArgumentOutOfRangeException(nameof(source), convert,
                        "The specified number is out of the Enum type's range");
                return convert;
            }

            return Enum.Parse(type, source.Span, true);
        }

        if (type == _typeTimespan) return TimeSpan.Parse(source.Span);
        throw new NotSupportedException($"Not support target type: \"{targetType}\"");
    }
}