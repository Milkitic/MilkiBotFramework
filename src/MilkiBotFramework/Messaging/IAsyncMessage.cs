using System;
using System.Threading.Tasks;

namespace MilkiBotFramework.Messaging;

public interface IAsyncMessage
{
    Task<IAsyncMessageResponse> GetNextMessageAsync(int seconds = 10);
    Task<IAsyncMessageResponse> GetNextMessageAsync(TimeSpan dueTime);
}