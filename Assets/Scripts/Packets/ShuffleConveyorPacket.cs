using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace Solis.Packets
{
    public class ShuffleConveyorPacket : IPacket
    {
        public NetworkId Id { get; set; }
        public string ShuffleValue { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(ShuffleValue);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            ShuffleValue = reader.ReadString();
        }
    }
}