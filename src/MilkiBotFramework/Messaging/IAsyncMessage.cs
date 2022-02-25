using System;
using System.Threading.Tasks;

namespace MilkiBotFramework.Messaging;

public interface IAsyncMessage
{
    Task<string?> TryGetNextMessage(TimeSpan dueTime);
}