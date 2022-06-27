using System.Collections.Generic;
using System.IO;

namespace Packets
{
    public class WorldState : IPacket
    {
        public Dictionary<ulong, PlayerUpdate> EntityStates = new ();
        
        public void Serialize(BinaryWriter bw)
        {
            bw.Write(EntityStates.Count);
            foreach (var state in EntityStates)
            {
                bw.Write(state.Key);
                state.Value.Serialize(bw);
            }
        }

        public void Deserialize(BinaryReader br)
        {
            var count = br.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var key = br.ReadUInt64();
                var value = new PlayerUpdate();
                value.Deserialize(br);

                EntityStates[key] = value;
            }
        }
    }
}