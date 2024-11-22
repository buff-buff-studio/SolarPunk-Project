using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace Solis.Packets
{
    public class CarryableObjectGrabPacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public string HandId { get; set; }
        public bool IsCarrying { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(HandId);
            writer.Write(IsCarrying);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            HandId = reader.ReadString();
            IsCarrying = reader.ReadBoolean();
        }
    }
}