namespace TranslocatorEngineering.ModSystem
{
    using System;
    //using System.Collections.Generic;
    //using System.IO;
    //using System.Linq;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Config;
    using Vintagestory.API.MathTools;
    //using Vintagestory.API.Server;
    using Vintagestory.API.Util;


    public class ItemLinker : Item
    {
        private int? _maxDistance;
        private int MaxDistance
        {
            get
            {
                if (!_maxDistance.HasValue)
                {
                    _maxDistance = api.ModLoader.GetModSystem<TranslocatorEngineeringMod>().config.MaximumLinkRange;
                }

                return _maxDistance.Value;
            }
        }
        

        private BlockPos GetStoredSrcPos(ItemStack itemStack)
        {
            if (!itemStack.Attributes.HasAttribute("srcPos"))
            { return null; }
            return SerializerUtil.Deserialize<BlockPos>(itemStack.Attributes.GetBytes("srcPos"), null);
        }
        private void SetStoredSrcPos(ItemStack itemStack, BlockPos blockPos)
        {
            if (blockPos == null)
            {
                itemStack.Attributes.RemoveAttribute("srcPos");
            }
            else
            {
                itemStack.Attributes.SetBytes("srcPos", SerializerUtil.Serialize(blockPos));
            }
        }
        private BlockPos GetStoredDstPos(ItemStack itemStack)
        {
            if (!itemStack.Attributes.HasAttribute("dstPos"))
            { return null; }
            return SerializerUtil.Deserialize<BlockPos>(itemStack.Attributes.GetBytes("dstPos"), null);
        }
        private void SetStoredDstPos(ItemStack itemStack, BlockPos blockPos)
        {
            if (blockPos == null)
            {
                itemStack.Attributes.RemoveAttribute("dstPos");
            }
            else
            {
                itemStack.Attributes.SetBytes("dstPos", SerializerUtil.Serialize(blockPos));
            }
        }
        private double GetStoredTimestamp(ItemStack itemStack)
        {
            return itemStack.Attributes.GetDouble("timestamp");
        }
        private void SetStoredTimestamp(ItemStack itemStack, double value)
        {
            itemStack.Attributes.SetDouble("timestamp", value);
        }
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            var capi = this.api as ICoreClientAPI;
            var itemStack = slot.Itemstack;
            var storedSrcPos = this.GetStoredSrcPos(itemStack);

            if (byEntity.Controls.Sneak)
            {
                if (storedSrcPos != null)
                {
                    this.SetStoredSrcPos(itemStack, null);
                    this.SetStoredDstPos(itemStack, null);
                    handling = EnumHandHandling.PreventDefault;
                    capi?.TriggerIngameError(null, null, Lang.Get("translocatorengineeringredux:ingameerror-linker-cleared"));
                    byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), byEntity.Pos.X, byEntity.Pos.Y, byEntity.Pos.Z, null, true, 32f, 1f);
                }
                return;
            }

            if (blockSel?.Position == null)
            { return; }

            var blockEntity = this.api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (!(blockEntity is ModifiedBlockEntityStaticTranslocator blockEntityTranslocator))
            { return; }
            if (!blockEntityTranslocator.FullyRepaired)
            { return; } // can't link an unrepaired translocator

            // if we already have a stored position, ask blockEntityTranslocator to link the translocator at the stored position
            if (storedSrcPos != null && !storedSrcPos.Equals(blockSel.Position))
            {
                // check distance
                var distance = byEntity.Pos.AsBlockPos.DistanceTo(storedSrcPos);
                if (distance > MaxDistance)
                {
                    capi?.TriggerIngameError(null, null, Lang.Get("translocatorengineeringredux:ingameerror-linker-out-of-range"));
                }
                else
                {
                    blockEntityTranslocator.Link(storedSrcPos, this.GetStoredDstPos(itemStack), this.GetStoredTimestamp(itemStack));
                    capi?.TriggerIngameError(null, null, Lang.Get("translocatorengineeringredux:ingameerror-linker-linked"));
                    if (this.api.Side == EnumAppSide.Server)
                    {
                        byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/block/teleporter"), byEntity.Pos.X, byEntity.Pos.Y, byEntity.Pos.Z, null, true, 32f, 1f);
                    }
                    this.SetStoredSrcPos(itemStack, null);
                }
            }
            else
            {
                capi?.TriggerIngameError(null, null, Lang.Get("translocatorengineeringredux:ingameerror-linker-synced"));
                if (this.api.Side == EnumAppSide.Server)
                {
                    byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/tool/padlock"), byEntity.Pos.X, byEntity.Pos.Y, byEntity.Pos.Z, null, true, 32f, 1f);
                }
                this.SetStoredSrcPos(itemStack, blockSel.Position);
                this.SetStoredDstPos(itemStack, blockEntityTranslocator.tpLocation); // can be null
                this.SetStoredTimestamp(itemStack, byEntity.World.Calendar.TotalDays);
            }

            handling = EnumHandHandling.PreventDefault;
        }
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            var translocatorItemStacks = new ItemStack[] { new ItemStack(this.api.World.GetBlock(new AssetLocation("game:statictranslocator-normal-north")), 1) };
            WorldInteraction[] interactions;
            if (this.GetStoredSrcPos(inSlot.Itemstack) != null)
            {
                interactions = new WorldInteraction[] {
          new WorldInteraction
          {
            ActionLangCode = "Link", // "heldhelp-linker-link",
            MouseButton = (EnumMouseButton)2,
            Itemstacks = translocatorItemStacks
          },
          new WorldInteraction
          {
            ActionLangCode = "Desynchronize", // "heldhelp-linker-clear",
            HotKeyCode = "sneak",
            MouseButton = (EnumMouseButton)2
          }
        };
            }
            else
            {
                interactions = new WorldInteraction[] {
          new WorldInteraction
          {
            ActionLangCode = "Synchronize", // "heldhelp-linker-sync",
            MouseButton = (EnumMouseButton)2,
            Itemstacks = translocatorItemStacks
          }
        };
            }
            return ArrayExtensions.Append(interactions, base.GetHeldInteractionHelp(inSlot));
        }
        #region rendering
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            var storedSrcPos = this.GetStoredSrcPos(itemstack);
            if (storedSrcPos == null)
            {
                renderinfo.ModelRef = this.meshrefs[0]; // no lights, no screen
            }
            else
            {
                var distance = capi.World.Player.Entity.Pos.AsBlockPos.DistanceTo(storedSrcPos);
                var lights = (int)GameMath.Clamp(Math.Ceiling(8 - (8 * distance / MaxDistance)), 0, 8);
                if (lights == 0)
                {
                    renderinfo.ModelRef = this.meshrefs[capi.World.Rand.NextDouble() < 0.25 ? 1 : 0];
                }
                else
                {
                    renderinfo.ModelRef = this.meshrefs[1 + lights];
                }
            }
            // renderinfo.ModelRef = meshrefs[(int)((float)capi.World.ElapsedMilliseconds / 1000f) % 10];
        }

        private MeshRef[] meshrefs;
        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client)
            {
                this.OnLoadedClientSide(api as ICoreClientAPI);
            }
        }
        private void OnLoadedClientSide(ICoreClientAPI capi)
        {
            this.meshrefs = new MeshRef[10];
            var key = this.Code.ToString() + "-meshes";
            var shape = capi.Assets.TryGet("translocatorengineeringredux:shapes/item/linker.json").ToObject<Shape>().Clone();
            this.meshrefs[0] = TesselateAndUpload(this, shape, capi);

            // 1.17
            //shape.GetElementByName("Screen").Faces["up"].Glow = 120;
            shape.GetElementByName("Screen", StringComparison.InvariantCultureIgnoreCase).FacesResolved[4].Glow = 120;

            this.meshrefs[1] = TesselateAndUpload(this, shape, capi);
            ShapeElementAdjustUpFaceGlowAndTexture(shape, "Light1", 255, "fire-red");
            this.meshrefs[2] = TesselateAndUpload(this, shape, capi);
            ShapeElementAdjustUpFaceGlowAndTexture(shape, "Light2", 255, "fire-red");
            this.meshrefs[3] = TesselateAndUpload(this, shape, capi);
            ShapeElementAdjustUpFaceGlowAndTexture(shape, "Light3", 255, "fire-red");
            this.meshrefs[4] = TesselateAndUpload(this, shape, capi);
            ShapeElementAdjustUpFaceGlowAndTexture(shape, "Light4", 255, "fire-red");
            this.meshrefs[5] = TesselateAndUpload(this, shape, capi);
            ShapeElementAdjustUpFaceGlowAndTexture(shape, "Light5", 255, "fire-red");
            this.meshrefs[6] = TesselateAndUpload(this, shape, capi);
            ShapeElementAdjustUpFaceGlowAndTexture(shape, "Light6", 255, "fire-red");
            this.meshrefs[7] = TesselateAndUpload(this, shape, capi);
            ShapeElementAdjustUpFaceGlowAndTexture(shape, "Light7", 255, "fire-red");
            this.meshrefs[8] = TesselateAndUpload(this, shape, capi);
            ShapeElementAdjustUpFaceGlowAndTexture(shape, "Light8", 255, "fire-red");
            this.meshrefs[9] = TesselateAndUpload(this, shape, capi);
        }
        private static MeshRef TesselateAndUpload(CollectibleObject collectible, Shape shape, ICoreClientAPI capi)
        {
            capi.Tesselator.TesselateShape(collectible, shape, out var meshData, new Vec3f(0, 0, 0));
            return capi.Render.UploadMesh(meshData);
        }
        private static void ShapeElementAdjustUpFaceGlowAndTexture(Shape shape, string name, int glow, string newTexture)
        {
            // 1.17
            //var upFace = shape.GetElementByName(name).Faces["up"];
            var upFace = shape.GetElementByName(name, StringComparison.InvariantCultureIgnoreCase).FacesResolved[4];
            upFace.Glow = glow;
            if (newTexture != null)
            {
                upFace.Texture = newTexture;
            }
        }

        //REMOVED THIS SO THAT IF SOME OTHER MOD CRASHES THE GAME, THE CLIENT-CRASH DOESN"T END UP REPORTING THIS AS THE ISSUE 
        /*
        public override void OnUnloaded(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client)
            {
                for (var meshIndex = 0; meshIndex < this.meshrefs.Length; meshIndex += 1)
                {
                    this.meshrefs[meshIndex]?.Dispose();
                    this.meshrefs[meshIndex] = null;
                }
            }
        }
        */
        #endregion
    }
}
