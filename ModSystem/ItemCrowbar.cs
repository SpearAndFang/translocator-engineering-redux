namespace TranslocatorEngineering.ModSystem
{
    //using System;
    //using System.Collections.Generic;
    //using System.Linq;
    using Vintagestory.API.Common;

    public class ItemCrowbar : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            //var foo = this.api.Side;
            if (blockSel != null && byEntity.Controls.Sneak)
            {
                var block = this.api.World.BlockAccessor.GetBlock(blockSel.Position, BlockLayersAccess.Default);
                var player = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
                handling = EnumHandHandling.PreventDefaultAction;
                // api.Logger.Notification("XXXYYY: Crowbar calling block.OnCrowbarPried on " + api.Side);
                (block as IPryable)?.OnCrowbarPried(player, blockSel);
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}
