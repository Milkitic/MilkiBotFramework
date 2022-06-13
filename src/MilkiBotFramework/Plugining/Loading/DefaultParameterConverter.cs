namespace MilkiBotFramework.Plugining.Loading;

public class DefaultParameterConverter : IParameterConverter
{
    public static DefaultParameterConverter Instance { get; } = new();

    public virtual object Convert(Type targetType, ReadOnlyMemory<char> source)
    {
        var type = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == StaticTypes.TypeNullable
            ? targetType.GetGenericArguments()[0]
            : targetType;

        if (type == StaticTypes.Byte) return byte.Parse(source.Span);
        if (type == StaticTypes.Sbyte) return sbyte.Parse(source.Span);
        if (type == StaticTypes.UInt16) return ushort.Parse(source.Span);
        if (type == StaticTypes.UInt32) return uint.Parse(source.Span);
        if (type == StaticTypes.UInt64) return ulong.Parse(source.Span);
        if (type == StaticTypes.Int16) return short.Parse(source.Span);
        if (type == StaticTypes.Int32) return int.Parse(source.Span);
        if (type == StaticTypes.Int64) return long.Parse(source.Span);
        if (type == StaticTypes.Single) return float.Parse(source.Span);
        if (type == StaticTypes.Double) return double.Parse(source.Span);
        if (type == StaticTypes.String) return source.ToString();
        if (type == StaticTypes.ReadOnlyMemory_Char) return source;
        if (type == StaticTypes.Boolean)
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

        if (type.IsSubclassOf(StaticTypes.Enum))
        {
            if (int.TryParse(source.Span, out _))
            {
                var convert = Enum.ToObject(type, int.Parse(source.Span));
                if (!Enum.IsDefined(type, convert))
                    throw new ArgumentOutOfRangeException(nameof(source), convert,
                        "The specified number is out of the Enum type's range");
                return convert;
            }

            return Enum.Parse(type, source.Span, true);
        }

        if (type == StaticTypes.Timespan) return TimeSpan.Parse(source.Span);
        throw new NotSupportedException($"Not support target type: \"{targetType}\"");
    }
}