//BillyGalbreath 1.4.7
using ProtoBuf;

namespace TranslocatorEngineering.ModSystem
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SyncClientPacket
    {
        public int MaximumLinkRange;
    }
}
//End BillyGalbreath 1.4.7
