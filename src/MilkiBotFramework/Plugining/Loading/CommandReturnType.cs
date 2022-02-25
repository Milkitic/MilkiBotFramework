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
    Dynamic
}