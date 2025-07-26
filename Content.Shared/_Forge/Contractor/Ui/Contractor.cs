using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Contractor;

[Serializable, NetSerializable]
public sealed class ContractorState : EuiStateBase
{
}

[Serializable, NetSerializable]
public sealed class ContractorAcceptedMessage(bool accepted) : EuiMessageBase
{
    public readonly bool Accepted = accepted;
}

[Serializable, NetSerializable]
public enum ContractsUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ContractsUpdateStateMessage : BoundUserInterfaceMessage
{
    public List<ContractData> AvailableContracts { get; }
    public List<ActiveContract> ActiveContracts { get; }

    public ContractsUpdateStateMessage(List<ContractData> availableContracts, List<ActiveContract> activeContracts)
    {
        AvailableContracts = availableContracts;
        ActiveContracts = activeContracts;
    }
}

[Serializable, NetSerializable]
public sealed class AcceptContractMessage : BoundUserInterfaceMessage
{
    public string ContractId;

    public AcceptContractMessage(string contractId)
    {
        ContractId = contractId;
    }
}

[Serializable, NetSerializable]
public sealed class CompleteContractMessage : BoundUserInterfaceMessage
{
    public string ContractId;

    public CompleteContractMessage(string contractId)
    {
        ContractId = contractId;
    }
}

[Serializable, NetSerializable]
public sealed class OpenContractorShopMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class ContractData
{
    public string ContractId { get; }
    public NetEntity TargetEntity { get; }
    public string TargetName { get; }
    public string TargetJob { get; }
    public string Description { get; }
    public int Reward { get; }
    public int TelecrystalReward { get; }
    public NetEntity EvacPoint { get; }
    public string EvacPointName { get; }
    public float Difficulty { get; }
    public TimeSpan Deadline { get; }

    public ContractData(
        string id,
        NetEntity targetEntity,
        string targetName,
        string targetJob,
        string description,
        int reward,
        int telecrystalReward,
        NetEntity evacPoint,
        string evacPointName,
        float difficulty,
        TimeSpan deadline)
    {
        ContractId = id;
        TargetEntity = targetEntity;
        TargetName = targetName;
        TargetJob = targetJob;
        Description = description;
        Reward = reward;
        TelecrystalReward = telecrystalReward;
        EvacPoint = evacPoint;
        EvacPointName = evacPointName;
        Difficulty = difficulty;
        Deadline = deadline;
    }
}

[Serializable, NetSerializable]
public sealed class ActiveContract
{
    public string ContractId { get; }
    public NetEntity ContractorEntity { get; }
    public NetEntity TargetEntity { get; }
    public string TargetName { get; }
    public string TargetJob { get; }
    public string Description { get; }
    public int Reward { get; }
    public int TelecrystalReward { get; }
    public NetEntity EvacPoint { get; }
    public string EvacPointName { get; }
    public TimeSpan TimeRemaining { get; }
    public ContractStatus Status { get; }

    public ActiveContract(
        string id,
        NetEntity contractorEntity,
        NetEntity targetEntity,
        string targetName,
        string targetJob,
        string description,
        int reward,
        int telecrystalReward,
        NetEntity evacPoint,
        string evacPointName,
        TimeSpan timeRemaining,
        ContractStatus status)
    {
        ContractId = id;
        ContractorEntity = contractorEntity;
        TargetEntity = targetEntity;
        TargetName = targetName;
        TargetJob = targetJob;
        Description = description;
        Reward = reward;
        TelecrystalReward = telecrystalReward;
        EvacPoint = evacPoint;
        EvacPointName = evacPointName;
        TimeRemaining = timeRemaining;
        Status = status;
    }
}

[Serializable, NetSerializable]
public enum ContractStatus : byte
{
    Active,
    Completed
}
