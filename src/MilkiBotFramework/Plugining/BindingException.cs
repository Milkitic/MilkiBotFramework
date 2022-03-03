namespace MilkiBotFramework.Plugining;

public class BindingException : Exception
{
    public BindingSource BindingSource { get; }
    public BindingFailureType BindingFailureType { get; }

    public BindingException(string message,
        BindingSource bindingSource,
        BindingFailureType bindingFailureType,
        Exception? innerException = null)
        : base(message, innerException)
    {
        BindingSource = bindingSource;
        BindingFailureType = bindingFailureType;
    }
}