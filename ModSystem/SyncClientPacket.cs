using ProtoBuf;

namespace TranslocatorEngineering.ModSystem {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SyncClientPacket {
        public int MaximumLinkRange;
    }
}
