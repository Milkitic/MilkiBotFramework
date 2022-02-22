using MilkiBotFramework.ContractsManaging.Models;

namespace MilkiBotFramework.ContractsManaging.Results;

public sealed class PrivateInfoResult : ResultInfoBase
{
    public PrivateInfo? PrivateInfo { get; init; }
}