namespace MilkiBotFramework.ContractsManaging;

public sealed class ContractUpdateResult
{
    public ContractUpdateResult(bool isSuccess, string? id, ContractUpdateType contractUpdateType)
    {
        IsSuccess = isSuccess;
        Id = id;
        ContractUpdateType = contractUpdateType;
    }

    public bool IsSuccess { get; }
    public string? Id { get; }
    public ContractUpdateType ContractUpdateType { get; }
}