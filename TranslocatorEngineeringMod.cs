//[assembly: ModInfo("TranslocatorEngineering")]

namespace TranslocatorEngineering.ModSystem
{

    using ProtoBuf;
    using System;
    using HarmonyLib;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Server;
    using System.Collections.Generic;
    using Vintagestory.Common;
    using System.Reflection;
    using Vintagestory.API.MathTools;
    using Vintagestory.API.Util;
    using TranslocatorEngineering.ModConfig;


    public class TranslocatorEngineeringMod : ModSystem
    {
        private static bool alreadyPatched = false;
        private IServerNetworkChannel serverChannel;  //BillyGalbreath 1.4.7
        private ICoreAPI api;

        public override void StartPre(ICoreAPI api)
        {
            var cfgFileName = "TranslocatorEngineeringMod.json";
            try
            {
                ModConfig fromDisk;
                if ((fromDisk = api.LoadModConfig<ModConfig>(cfgFileName)) == null)
                { api.StoreModConfig(ModConfig.Loaded, cfgFileName); }
                else
                { ModConfig.Loaded = fromDisk; }
            }
            catch
            { api.StoreModConfig(ModConfig.Loaded, cfgFileName); }
            base.StartPre(api);
        }


        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.Logger.Debug("[TranslocatorEngineering] Start");
            base.Start(api);
            api.World.Logger.Event("started 'Translocator Engineering' mod");

            //this.config = ModConfig.Load(api);

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

        //BillyGalbreath 1.4.7
        public override void StartClientSide(ICoreClientAPI capi)
        {
            capi.Network.RegisterChannel("translocatorengineeringredux")
                .RegisterMessageType<SyncClientPacket>()
                .SetMessageHandler<SyncClientPacket>(packet =>
                {
                    
                    ModConfig.Loaded.MaximumLinkRange = packet.MaximumLinkRange;
                    this.Mod.Logger.Event($"Received MaximumLinkRange of {packet.MaximumLinkRange} from server");

                    ModConfig.Loaded.AlwaysDropAllCrystalShards = packet.AlwaysDropAllCrystalShards;
                    this.Mod.Logger.Event($"Received AlwaysDropAllCrystalShards of {packet.AlwaysDropAllCrystalShards} from server");

                    ModConfig.Loaded.RecoveryChanceGateArray = packet.RecoveryChanceGateArray;
                    this.Mod.Logger.Event($"Received RecoveryChanceGateArray of {packet.RecoveryChanceGateArray} from server");

                    ModConfig.Loaded.RecoveryChanceParticulationComponent = packet.RecoveryChanceParticulationComponent;
                    this.Mod.Logger.Event($"Received RecoveryChanceParticulationComponent of {packet.RecoveryChanceParticulationComponent} from server");

    });
        }
        //End BillyGalbreath 1.4.7

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            // persist queuedTranslocatorDestinationAssignments in world save
            sapi.Event.SaveGameLoaded += () =>
            {
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

            //BillyGalbreath 1.4.7
            // we need to send connecting players the config settings
            sapi.Event.PlayerJoin += this.OnPlayerJoin; // add method so we can remove it in dispose to prevent memory leaks
            // register network channel to send data to clients
            this.serverChannel = sapi.Network.RegisterChannel("translocatorengineeringredux")
                .RegisterMessageType<SyncClientPacket>()
                .SetMessageHandler<SyncClientPacket>((player, packet) => { /* do nothing. idk why this handler is even needed, but it is */ });
            //End BillyGalbreath 1.4.7

        }


        //BillyGalbreath 1.4.7
        private void OnPlayerJoin(IServerPlayer player)
        {
            // send the connecting player the settings it needs to be synced
            this.serverChannel.SendPacket(new SyncClientPacket
            {
                MaximumLinkRange = ModConfig.Loaded.MaximumLinkRange,
                AlwaysDropAllCrystalShards = ModConfig.Loaded.AlwaysDropAllCrystalShards,
                RecoveryChanceGateArray = ModConfig.Loaded.RecoveryChanceGateArray,
                RecoveryChanceParticulationComponent = ModConfig.Loaded.RecoveryChanceParticulationComponent
            }, player);
        }

        public override void Dispose()
        {
            // remove our player join listener so we dont create memory leaks
            if (this.api is ICoreServerAPI sapi)
            {
                sapi.Event.PlayerJoin -= this.OnPlayerJoin;
            }
        }
        //End BillyGalbreath 1.4.7

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
