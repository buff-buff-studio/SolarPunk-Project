using System;
using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.Player;

namespace Solis.Packets
{
    public class InteractObjectPacket : IOwnedPacket
    {

        public NetworkId Id { get; set; }
        public InteractionType Interaction { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(Interaction.ToString());
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            Enum.TryParse(reader.ReadString(), true, out InteractionType i);
            Interaction = i;
        }
    }
}