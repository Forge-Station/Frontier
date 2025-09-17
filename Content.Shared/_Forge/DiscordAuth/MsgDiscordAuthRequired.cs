using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.DiscordAuth;

public sealed class MsgDiscordAuthRequired : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public string Link = default!;
    public string ErrorMessage = default!;
    public byte[]? QrCodeBytes { get; set; }

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        ErrorMessage = buffer.ReadString();
        Link = buffer.ReadString();
        var hasQrCode = buffer.ReadBoolean();

        if (hasQrCode)
        {
            var length = buffer.ReadInt32();
            QrCodeBytes = buffer.ReadBytes(length);
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(ErrorMessage);
        buffer.Write(Link);
        buffer.Write(QrCodeBytes != null);

        if (QrCodeBytes != null)
        {
            buffer.Write(QrCodeBytes.Length);
            buffer.Write(QrCodeBytes);
        }
    }
}
