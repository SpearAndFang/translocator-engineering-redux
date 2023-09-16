namespace TranslocatorEngineering.ModSystem
{
    using System.Collections.Generic;
    //using System.Diagnostics;
    using Vintagestory.API.Common;
    //using Vintagestory.API.Common.Entities;
    using Vintagestory.API.MathTools;
    //using Vintagestory.API.Util;
    using Vintagestory.GameContent;
    using TranslocatorEngineering.ModConfig;

    public class ModifiedBlockStaticTranslocator : BlockStaticTranslocator
    {
        private readonly double RecoveryChanceGateArray = ModConfig.Loaded.RecoveryChanceGateArray;
        private readonly double RecoveryChanceParticulationComponent = ModConfig.Loaded.RecoveryChanceParticulationComponent;
        private readonly bool AlwaysDropAllCrystalShards = ModConfig.Loaded.AlwaysDropAllCrystalShards;

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (itemslot.Itemstack?.Item?.Code.Path == "crowbar")
            {
                return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
            }
            else
            {
                return 1f; //not going to happen
            }
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            //api.Logger.Notification("YYY: block.OnBlockBroken on " + api.Side);
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }
        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            //api.Logger.Notification("YYY: block.GetDrops on " + api.Side);
            var list = new List<ItemStack>();
            var scrapDropQty = 0;
            if (this.AlwaysDropAllCrystalShards)
            {
                list.Add(new ItemStack(this.api.World.GetItem(new AssetLocation("translocatorengineeringredux:coalescencecrystalshard")), 6));
            }
            else
            {
                list.Add(new ItemStack(this.api.World.GetItem(new AssetLocation("translocatorengineeringredux:coalescencecrystalshard")), this.api.World.Rand.Next(5, 6)));
            }
            var metalPartsQty = this.api.World.Rand.Next(2, 4);
            list.Add(new ItemStack(this.api.World.GetBlock(new AssetLocation("game:metal-parts")), metalPartsQty));
            scrapDropQty += 4 - metalPartsQty;

            //if (this.api.World.Rand.NextDouble() < 0.8)
            if (this.api.World.Rand.NextDouble() < this.RecoveryChanceGateArray)
            {
                list.Add(new ItemStack(this.api.World.GetItem(new AssetLocation("translocatorengineeringredux:gatearray")), 1));
            }
            else
            {
                scrapDropQty += 1;
            }
            //if (this.api.World.Rand.NextDouble() < 0.8)
            if (this.api.World.Rand.NextDouble() < this.RecoveryChanceParticulationComponent)
            {
                list.Add(new ItemStack(this.api.World.GetItem(new AssetLocation("translocatorengineeringredux:particulationcomponent")), 1));
            }
            else
            {
                scrapDropQty += 1;
            }
            list.Add(new ItemStack(this.api.World.GetItem(new AssetLocation("translocatorengineeringredux:powercore")), 1));
            list.Add(new ItemStack(this.api.World.GetBlock(new AssetLocation("game:glassslab-plain-down-free")), 1));
            if (scrapDropQty > 0)
            {
                list.Add(new ItemStack(this.api.World.GetBlock(new AssetLocation("game:metal-scraps")), scrapDropQty));
            }
            //
            if (this.api.World.BlockAccessor.GetBlockEntity(pos) is ModifiedBlockEntityStaticTranslocator blockEntity)
            {
                list.AddRange(blockEntity.GetDrops());
            }
            return list.ToArray();
        }
        public void OnCrowbarPried(IPlayer player, BlockSelection blockSel)
        {
            // no more break block!
            //api.Logger.Notification("XXXYYY: block.OnCrowbarPried on " + api.Side);
            //this.api.World.BlockAccessor.BreakBlock(blockSel.Position, player);
        }
    }
}
