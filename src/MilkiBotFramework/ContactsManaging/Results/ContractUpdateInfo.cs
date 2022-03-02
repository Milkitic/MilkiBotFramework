namespace MilkiBotFramework.ContactsManaging.Results;

public sealed class ContractUpdateInfo 
{
    public ContractUpdateInfo(string? id)
    {
        Id = id;
    }

    public string? Id { get; }
    public string? SubId { get; init; }
    public string? Card { get; init; }
    public string? NickName { get; init; }
    public ContractUpdateRole ContractUpdateRole { get; init; }
    public ContractUpdateType ContractUpdateType { get; init; }
}