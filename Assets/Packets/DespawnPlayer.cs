using System;
using System.IO;

namespace Packets
{
    public struct DespawnPlayer : IPacket
    {
        public ulong SteamId;
        
        public void Serialize(BinaryWriter bw)
        {
            bw.Write(SteamId);
        }

        public void Deserialize(BinaryReader br)
        {
            SteamId = br.ReadUInt64();
        }
    }
}