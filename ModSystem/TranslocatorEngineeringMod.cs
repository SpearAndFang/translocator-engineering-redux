//[assembly: ModInfo("TranslocatorEngineering")]

namespace TranslocatorEngineering.ModSystem
{

    using ProtoBuf;
    using System;
    using HarmonyLib;
    //using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Server;
    //using Vintagestory.API.Config;
    //using System.Linq;
    using System.Collections.Generic;
    //using Vintagestory.Server;
    //using Vintagestory.Client.NoObf;
    using Vintagestory.Common;
    using System.Reflection;
    using Vintagestory.API.MathTools;
    using Vintagestory.API.Util;


    public class TranslocatorEngineeringMod : ModSystem
    {
        //private ModConfig config;
        public ModConfig config;
        private static bool alreadyPatched = false;
        private ICoreAPI api;
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.Logger.Debug("[TranslocatorEngineering] Start");
            base.Start(api);

            this.config = ModConfig.Load(api);

            // force register StaticTranslocator, overwriting registration from SurvivalCoreSystem, so that existing Block(Entities?) use our new code without remapping
            ForceRegisterBlockEntityType(api, "StaticTranslocator", typeof(ModifiedBlockEntityStaticTranslocator));
            ForceRegisterBlockClass(api, "BlockStaticTranslocator", typeof(ModifiedBlockStaticTranslocator));

            // register classes
            api.RegisterItemClass("ItemCrowbar", typeof(ItemCrowbar));
            api.RegisterItemClass("ItemLinker", typeof(ItemLinker));

            // patch, preventing double patching!
            if (!alreadyPatched)
            {
                var harmony = new Harmony("goxmeor.TranslocatorEngineering");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                alreadyPatched = true;
            }
        }
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class QueuedAssignment
        {
            public BlockPos dstPos;
            public double timestamp;
            public override string ToString()
            {
                return $"[QueuedAssignment:{this.dstPos}@{this.timestamp}]";
            }
        }
        private Dictionary<BlockPos, QueuedAssignment> queuedTranslocatorDestinationAssignments = new Dictionary<BlockPos, QueuedAssignment>(); // for attenuating translocators in unloaded chunks
        public override void StartServerSide(ICoreServerAPI sapi)
        {
            // persist queuedTranslocatorDestinationAssignments in world save
            sapi.Event.SaveGameLoaded += () => {
                var data = sapi.WorldManager.SaveGame.GetData("queuedTranslocatorDestinationAssignments");
                if (data != null)
                {
                    this.queuedTranslocatorDestinationAssignments = SerializerUtil.Deserialize<Dictionary<BlockPos, QueuedAssignment>>(data);
                    // api.Logger.Notification("XXX: SaveGameLoaded: loaded queuedTranslocatorDestinationAssignments: " + queuedTranslocatorDestinationAssignments.Select(e => $"{e.Key}: {e.Value}").Join());
                }
                else
                {
                    // api.Logger.Notification("XXX: SaveGameLoaded: nothing to load for queuedTranslocatorDestinationAssignments");
                }
            };
            sapi.Event.GameWorldSave += () => sapi.WorldManager.SaveGame.StoreData("queuedTranslocatorDestinationAssignments", SerializerUtil.Serialize(this.queuedTranslocatorDestinationAssignments));
        }
        public void SetDestinationOrQueue(BlockPos srcPos, BlockPos dstPos)
        {
            this.SetDestinationOrQueue(srcPos, dstPos, this.api.World.Calendar.TotalDays);
        }
        public void SetDestinationOrQueue(BlockPos srcPos, BlockPos dstPos, double timestamp)
        {
            var chunk = this.api.World.BlockAccessor.GetChunkAtBlockPos(srcPos);
            if (chunk == null)
            {
                this.queuedTranslocatorDestinationAssignments[srcPos] = new QueuedAssignment() { dstPos = dstPos, timestamp = timestamp };
                // api.Logger.Notification($"XXX: SetDestinationOrQueue: queued: {dstPos} ({timestamp})");
            }
            else
            {
                if (!(this.api.World.BlockAccessor.GetBlockEntity(srcPos) is ModifiedBlockEntityStaticTranslocator blockEntity))
                {
                    // api.Logger.Notification($"XXX: SetDestinationOrQueue: skip presumably destroyed: {dstPos} ({timestamp})");
                    return; // maybe it was destroyed in the meantime?
                }
                else
                {
                    // api.Logger.Notification($"XXX: SetDestinationOrQueue: found BE right away! calling SetDestination: {dstPos} ({timestamp})");
                    blockEntity.SetDestination(dstPos, timestamp);
                }
            }
        }
        public QueuedAssignment PullQueuedDestinationAssignment(BlockPos srcPos)
        {
            if (this.queuedTranslocatorDestinationAssignments.ContainsKey(srcPos))
            {
                this.queuedTranslocatorDestinationAssignments.TryGetValue(srcPos, out var queuedAssignment);
                this.queuedTranslocatorDestinationAssignments.Remove(srcPos);
                return queuedAssignment;
            }
            return null;
        }
        private static void ForceRegisterBlockEntityType(ICoreAPI api, string className, Type blockentity)
        {
            var classRegistry = api.ClassRegistry.XXX_GetFieldValue<ClassRegistry>("registry");
            classRegistry.blockEntityClassnameToTypeMapping[className] = blockentity;
            classRegistry.blockEntityTypeToClassnameMapping[blockentity] = className;
        }
        private static void ForceRegisterBlockClass(ICoreAPI api, string blockClass, Type block)
        {
            var classRegistry = api.ClassRegistry.XXX_GetFieldValue<ClassRegistry>("registry");
            classRegistry.BlockClassToTypeMapping[blockClass] = block;
        }
    }
}
