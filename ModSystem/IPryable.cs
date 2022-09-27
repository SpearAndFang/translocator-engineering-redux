namespace TranslocatorEngineering.ModSystem
{
    using Vintagestory.API.Common;

    public interface IPryable
    {
        //this was commented out because?
        float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter);
        void OnCrowbarPried(IPlayer player, BlockSelection blockSel);
    }
}
