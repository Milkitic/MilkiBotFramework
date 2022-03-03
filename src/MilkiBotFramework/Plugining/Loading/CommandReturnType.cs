namespace MilkiBotFramework.Plugining.Loading;

public enum CommandReturnType
{
    Void,
    Task, 
    ValueTask,
    IResponse,
    Task_IResponse,
    ValueTask_IResponse,
    IEnumerable_IResponse,
    IAsyncEnumerable_IResponse,
    Task_, 
    ValueTask_,
    Unknown
}