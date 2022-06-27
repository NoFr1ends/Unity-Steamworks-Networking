using System.IO;
using UnityEngine;

namespace Packets
{
    public struct PlayerUpdate : IPacket
    {
        public Vector2 Position;
        public float Rotation;
        
        public void Serialize(BinaryWriter bw)
        {
            bw.Write(Position.x);
            bw.Write(Position.y);
            bw.Write(Rotation);
        }

        public void Deserialize(BinaryReader br)
        {
            Position.x = br.ReadSingle();
            Position.y = br.ReadSingle();
            Rotation = br.ReadSingle();
        }
    }
}