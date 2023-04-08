namespace TranslocatorEngineering.ModSystem
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using HarmonyLib;
    using Vintagestory.API.Common;
    using Vintagestory.API.Datastructures;
    using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;

    [HarmonyPatch(typeof(BlockEntityStaticTranslocator))]
    [HarmonyPatch("DoRepair")]
    public class Patch_BlockEntityStaticTranslocator_DoRepair
    {
        private static void Prefix(BlockEntityStaticTranslocator __instance)
        {
            (__instance as ModifiedBlockEntityStaticTranslocator)?.OnDoRepair();
        }
    }
    public class ModifiedBlockEntityStaticTranslocator : BlockEntityStaticTranslocator
    {
        // easy access to base class's privates
        private static readonly Type BaseType = typeof(BlockEntityStaticTranslocator);
        public bool CanTeleport { get => this.XXX_GetFieldValue<bool>(BaseType, "canTeleport"); set => this.XXX_SetFieldValue(BaseType, "canTeleport", value); }
        public int RepairState { get => this.XXX_GetFieldValue<int>(BaseType, "repairState"); set => this.XXX_SetFieldValue(BaseType, "repairState", value); } 
        public bool FindNextChunk { get => this.XXX_GetFieldValue<bool>(BaseType, "findNextChunk"); set => this.XXX_SetFieldValue(BaseType, "findNextChunk", value); }

        // extra properties
        private int gearsAdded = 0;
        private bool wasPlaced = false;
        private double lastDestinationAssignmentTimestamp = 0;

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            // Api.Logger.Notification("XXXYYY: BlockEntity.OnBlockBroken on " + Api.Side);
            base.OnBlockBroken(byPlayer);
            if (this.Api.Side == EnumAppSide.Client)
            { return; }
            // unlink paired translocator
            if (this.tpLocation != null)
            {
                this.Api.ModLoader.GetModSystem<TranslocatorEngineeringMod>().SetDestinationOrQueue(this.tpLocation, null); // unlink linked translocator
            }
        }
        public ItemStack[] GetDrops()
        {
            // Api.Logger.Notification("XXXYYY: BlockEntity GetDrops");
            var list = new List<ItemStack>
            {
                new ItemStack(this.Api.World.GetBlock(new AssetLocation("game:metal-parts")), 2)
            };
            for (var i = 0; i < this.gearsAdded; i += 1)
            {
                list.Add(new ItemStack(this.Api.World.GetItem(new AssetLocation("game:gear-temporal")), 1)); // gears don't stack
            }
            return list.ToArray();
        }
        // called by Patch_BlockEntityStaticTranslocator_DoRepair
        public void OnDoRepair()
        {
            if (this.FullyRepaired)
            { return; }
            if (this.RepairState == 1)
            { return; } // metal parts are being added
            this.gearsAdded += 1;
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                var queuedAssignment = api.ModLoader.GetModSystem<TranslocatorEngineeringMod>().PullQueuedDestinationAssignment(this.Pos);
                if (queuedAssignment != null)
                {
                    // api.Logger.Notification($"XXX: ModifiedBlockEntityStaticTranslocator.Initialize: queuedAssignment! {queuedAssignment.dstPos?.ToString()} ({queuedAssignment.timestamp})");
                    this.SetDestination(queuedAssignment.dstPos, queuedAssignment.timestamp);
                }
            }
        }
        public void SetDestination(BlockPos dstPos, double timestamp)
        { // also called by TranslocatorEngineeringMod.SetDestinationOrQueue
          // if this assignment is old news, ignore it (consider player visits A, then B (linking A to B), then visits A's previous dst before returning to A)
            if (timestamp < this.lastDestinationAssignmentTimestamp)
            {
                // Api.Logger.Notification($"XXX: ModifiedBlockEntityStaticTranslocator.SetDestination: old news! {timestamp} < {lastDestinationAssignmentTimestamp} for {dstPos?.ToString()}");
                return;
            }
            this.lastDestinationAssignmentTimestamp = timestamp;
            // if this assignment is a link (not an unlink), and we are already linked, unlink the current destination
            if (dstPos != null && this.tpLocation != null)
            {
                // Api.Logger.Notification($"XXX: ModifiedBlockEntityStaticTranslocator.SetDestination: linking already linked! unlink current destination {this.tpLocation?.ToString()} @ preversed timestamp {timestamp}");
                this.Api.ModLoader.GetModSystem<TranslocatorEngineeringMod>().SetDestinationOrQueue(this.tpLocation, null, timestamp); // same timestamp!
            }
            // n.b. Api is null?!?!?!?!?!?!?!?
            // Api.Logger.Notification($"XXX: ModifiedBlockEntityStaticTranslocator.SetDestination: finishing up by setting tpLocation and CanTeleport: {this.tpLocation?.ToString()} => {dstPos?.ToString()}");
            this.tpLocation = dstPos;
            this.CanTeleport = dstPos != null;
        }
        public void Link(BlockPos otherPos, BlockPos otherDstPos, double otherDstTimestamp)
        {
            if (otherPos.Equals(this.Pos))
            { return; } // can't link to self!
                        // Api.Logger.Notification($"XXX: ModifiedBlockEntityStaticTranslocator.Link: start: otherPos {otherPos?.ToString()} otherDstPos {otherDstPos?.ToString()} otherDstTimestamp {otherDstTimestamp}");
            this.SetDestination(otherPos, this.Api.World.Calendar.TotalDays);
            this.Api.ModLoader.GetModSystem<TranslocatorEngineeringMod>().SetDestinationOrQueue(otherPos, this.Pos); // link stored translocator
            // if "other" translocator had a destination when the linker was syncronized, null out its destination (but using the old timestamp)
            // ... this fixes the awkward situation in which you sync A, teleport from A to B, then link C right next to B, and C still appears linked until A is chunkloaded
            if (otherDstPos != null)
            {
                // Api.Logger.Notification($"XXX: ModifiedBlockEntityStaticTranslocator.Link: fixup for otherDstPos @ {otherDstTimestamp}");
                this.Api.ModLoader.GetModSystem<TranslocatorEngineeringMod>().SetDestinationOrQueue(otherDstPos, null, otherDstTimestamp);
            }
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this.wasPlaced = tree.GetBool("wasPlaced", false);
            var defaultGearsAdded = 0;
            if (!this.wasPlaced)
            {
                if (this.RepairState == 2 || this.RepairState == 3)
                { // 2: non-clockmaker has added 1 gear; 3: non-clockmaker has added 2 gears OR clockmaker has added 1
                    defaultGearsAdded = 1; // gotta round down to avoid cheesing
                }
                else if (this.RepairState == 4)
                { // 4: fully fixed, either via 2 gears for the clockmaker or 3 for non-clockmakers
                    defaultGearsAdded = 2; // gotta round down to avoid cheesing
                }
            }
            this.gearsAdded = tree.GetInt("gearsAdded", defaultGearsAdded);
            this.lastDestinationAssignmentTimestamp = tree.GetDouble("lastDestinationAssignmentTimestamp", 0);
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("gearsAdded", this.gearsAdded);
            tree.SetBool("wasPlaced", this.wasPlaced);
            tree.SetDouble("lastDestinationAssignmentTimestamp", this.lastDestinationAssignmentTimestamp);
        }
        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            // confusingly, OnBlockPlaced gets called with null when the Translocator is repaired with Metal Parts!
            if (byItemStack == null)
            { return; } // repairing with Metal Parts is not being "placed"
            this.wasPlaced = true;
            this.FindNextChunk = false; // disable automatically searching for target
            // when "placed", translocator is fully repaired (metal parts and 2 gears, as if repaired by clockmaker)
            this.RepairState = 4;
            this.gearsAdded = 2;
            this.setupGameTickers();
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (this.FullyRepaired && this.wasPlaced && !this.CanTeleport)
            {
                dsc.AppendLine("Unlinked.");
            }
            else
            {
                base.GetBlockInfo(forPlayer, dsc);
            }
        }
    }
}
