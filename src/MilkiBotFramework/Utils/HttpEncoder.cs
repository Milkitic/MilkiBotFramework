using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace MilkiBotFramework.Utils;

internal static class HttpEncoder
{
    public static int UrlEncode(string? str, Span<char> chars)
    {
        if (str == null)
        {
            return 0;
        }

        byte[]? bytes = null;
        Span<byte> span = str.Length * 4 <= FrameworkConstants.MaxStackArrayLength
            ? stackalloc byte[str.Length * 4]
            : bytes = Encoding.UTF8.GetBytes(str);
        var isStack = bytes == null;

        if (isStack)
        {
            var wrote = Encoding.UTF8.GetBytes(str, span);
            span = span[..wrote];
        }

        var bufferLength = span.Length * 3;
        byte[]? bytesRent = null;
        Span<byte> buffer = bufferLength <= FrameworkConstants.MaxStackArrayLength
            ? stackalloc byte[bufferLength]
            : bytesRent = ArrayPool<byte>.Shared.Rent(bufferLength);
        try
        {
            if (bytesRent != null) buffer = bytesRent.AsSpan(0, bufferLength);

            UrlEncode(span, buffer, out var wroteLength);
            buffer = buffer[..wroteLength];
            return Encoding.ASCII.GetChars(buffer, chars);
        }
        finally
        {
            if (bytesRent != null) ArrayPool<byte>.Shared.Return(bytesRent);
        }
    }

    public static bool UrlEncode(Span<byte> bytes, Span<byte> buffer, out int wroteLength)
    {
        int count = bytes.Length;
        if (!ValidateUrlEncodingParameters(bytes, buffer.Length))
        {
            wroteLength = 0;
            return false;
        }

        int cSpaces = 0;
        int cUnsafe = 0;

        // count them first
        for (int i = 0; i < count; i++)
        {
            char ch = (char)bytes[i];

            if (ch == ' ')
            {
                cSpaces++;
            }
            else if (!IsUrlSafeChar(ch))
            {
                cUnsafe++;
            }
        }

        // nothing to expand?
        if (cSpaces == 0 && cUnsafe == 0)
        {
            bytes.CopyTo(buffer);
            wroteLength = count;
            return true;
        }

        // expand not 'safe' characters into %XX, spaces to +s
        var total = count + cUnsafe * 2;
        if (buffer.Length < total)
        {
            wroteLength = 0;
            return false;
        }

        int pos = 0;

        for (int i = 0; i < count; i++)
        {
            byte b = bytes[i];
            char ch = (char)b;

            if (IsUrlSafeChar(ch))
            {
                buffer[pos++] = b;
            }
            else if (ch == ' ')
            {
                buffer[pos++] = (byte)'+';
            }
            else
            {
                buffer[pos++] = (byte)'%';
                buffer[pos++] = (byte)ToCharLower(b >> 4);
                buffer[pos++] = (byte)ToCharLower(b);
            }
        }

        wroteLength = total;
        return true;
    }

    private static bool ValidateUrlEncodingParameters(Span<byte> bytes, int count)
    {
        if (count == 0)
        {
            return false;
        }

        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return true;
    }

    // Set of safe chars, from RFC 1738.4 minus '+'
    private static bool IsUrlSafeChar(char ch)
    {
        if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return true;
        }

        switch (ch)
        {
            case '-':
            case '_':
            case '.':
            case '!':
            case '*':
            case '(':
            case ')':
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ToCharLower(int value)
    {
        value &= 0xF;
        value += '0';

        if (value > '9')
        {
            value += ('a' - ('9' + 1));
        }

        return (char)value;
    }
}