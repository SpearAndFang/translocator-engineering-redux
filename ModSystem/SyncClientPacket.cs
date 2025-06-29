//BillyGalbreath 1.4.7
using ProtoBuf;

namespace TranslocatorEngineering.ModSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SyncClientPacket
    {
        public int MaximumLinkRange;
        public bool AlwaysDropAllCrystalShards;
        public double RecoveryChanceGateArray;
        public double RecoveryChanceParticulationComponent;
    }
}
//End BillyGalbreath 1.4.7
