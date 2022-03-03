namespace MilkiBotFramework.Plugining.Loading;

public interface IParameterConverter
{
    object Convert(Type actualType, ReadOnlyMemory<char> source);
}