using System;
using System.IO;
using UnityEngine;

namespace Packets
{
    public struct SpawnPlayer : IPacket
    {
        public ulong SteamId;
        public Vector2 Position;
        
        public void Serialize(BinaryWriter bw)
        {
            bw.Write(SteamId);
            bw.Write(Position.x);
            bw.Write(Position.y);
        }

        public void Deserialize(BinaryReader br)
        {
            SteamId = br.ReadUInt64();
            Position.x = br.ReadSingle();
            Position.y = br.ReadSingle();
        }
    }
}