namespace MilkiBotFramework.ContactsManaging.Results;

public abstract class ResultInfoBase
{
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public Exception? Exception { get; init; }
}