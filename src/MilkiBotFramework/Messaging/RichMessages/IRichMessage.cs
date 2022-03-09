namespace MilkiBotFramework.Messaging.RichMessages;

public interface IRichMessage
{
    /// <summary>
    /// Warn: The operator will change the original object
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static IRichMessage operator +(IRichMessage a, IRichMessage b)
    {
        if (a is RichMessage rich1 && b is RichMessage rich2)
        {
            rich1.RichMessages.AddRange(rich2.RichMessages);
            return rich1;
        }

        if (a is RichMessage r1)
        {
            r1.RichMessages.Add(b);
            return r1;
        }

        if (b is RichMessage r2)
        {
            r2.RichMessages.Insert(0, a);
            return r2;
        }

        return new RichMessage(a, b);
    }

    ValueTask<string> EncodeAsync();
}