﻿namespace MilkiBotFramework.ContractsManaging.Results;

public sealed class ContractUpdateResult : ResultInfoBase
{
    public ContractUpdateResult(bool isSuccess, string? id, ContractUpdateType contractUpdateType)
    {
        IsSuccess = isSuccess;
        Id = id;
        ContractUpdateType = contractUpdateType;
    }

    public string? Id { get; }
    public ContractUpdateType ContractUpdateType { get; }
}