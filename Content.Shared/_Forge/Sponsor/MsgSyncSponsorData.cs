using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Sponsors;

public sealed class MsgSyncSponsorData : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public NetUserId UserId;
    public SponsorLevel Level;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        UserId = (NetUserId)buffer.ReadGuid();
        Level = (SponsorLevel)buffer.ReadByte();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(UserId);
        buffer.Write((byte)Level);
    }
}
