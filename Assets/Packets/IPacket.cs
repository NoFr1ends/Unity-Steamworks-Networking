using System.IO;

namespace Packets
{
    public interface IPacket
    {
        public void Serialize(BinaryWriter bw);
        public void Deserialize(BinaryReader br);
    }
}