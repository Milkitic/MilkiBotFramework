using System;

namespace MilkiBotFramework.Plugins;

public interface IValueConverter
{
    object Deserialize(Type actualType, string source);
    string Serialize(Type sourceType, object data);
}

public class DefaultConverter : IValueConverter
{
    public static DefaultConverter Instance { get; } = new();

    public object Deserialize(Type targetType, string source)
    {
        object parsed;
        try
        {
            var actualType = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>)
                ? targetType.GetGenericArguments()[0]
                : targetType;

            if (actualType == typeof(int))
            {
                parsed = Convert.ToInt32(source);
            }
            else if (actualType == typeof(long))
            {
                parsed = Convert.ToInt64(source);
            }
            else if (actualType == typeof(short))
            {
                parsed = Convert.ToInt16(source);
            }
            else if (actualType == typeof(float))
            {
                parsed = Convert.ToSingle(source);
            }
            else if (actualType == typeof(double))
            {
                parsed = Convert.ToDouble(source);
            }
            else if (actualType == typeof(string))
            {
                parsed = source; // Convert.ToString(cmd);
            }
            else if (actualType == typeof(bool))
            {
                string tmpCmd = source == "" ? "true" : source;
                if (tmpCmd == "0")
                    tmpCmd = "false";
                else if (tmpCmd == "1")
                    tmpCmd = "true";
                parsed = Convert.ToBoolean(tmpCmd);
            }
            else if (actualType.IsSubclassOf(typeof(Enum)))
            {
                parsed = Enum.ToObject(actualType, Convert.ToInt32(source));
            }
            //else if (actualType == typeof(MessageTypes.LinkImage))
            //{
            //    var startI = source.IndexOf(",url=", StringComparison.Ordinal);
            //    parsed = null;

            //    if (startI != -1)
            //    {
            //        var endI = source.IndexOf("]", startI, StringComparison.Ordinal);
            //        if (endI != -1)
            //        {
            //            parsed = new MessageTypes.LinkImage(source.Substring(startI + 5, endI - startI - 5));
            //        }
            //    }
            //}
            else
            {
                throw new NotSupportedException($"Not support target type: \"{targetType}\"");
            }
        }
        catch (Exception ex)
        {
            //Logger.Exception(ex);
            throw;
        }

        return parsed;
    }

    public string Serialize(Type sourceType, object data)
    {
        throw new NotImplementedException();
    }
}