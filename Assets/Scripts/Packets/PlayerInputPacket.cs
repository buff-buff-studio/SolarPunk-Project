using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using Solis.Data;
using UnityEngine;

namespace Solis.Packets
{
    /// <summary>
    /// Used to notify clients that a player has performed an input.
    /// </summary>
    public class PlayerInputPacket : IPacket
    {
        public KeyCode Key { get; set; }
        public NetworkId Id { get; set; }
        public CharacterType CharacterType { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte) Key);
            Id.Serialize(writer);
            writer.Write((byte) CharacterType);
        }

        public void Deserialize(BinaryReader reader)
        {
            Key = (KeyCode) reader.ReadByte();
            Id = NetworkId.Read(reader);
            CharacterType = (CharacterType) reader.ReadByte();
        }
    }
}