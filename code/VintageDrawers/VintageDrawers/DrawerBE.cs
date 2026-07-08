using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageDrawers
{
    public class DrawerBE : BlockEntityOpenableContainer, ITexPositionSource
    {
        /// <summary>
        /// Slot 0 is item storage, slots 1 - 8 are upgrade slots
        /// </summary>
        [AllowNull]
        private InventoryGeneric _inventory;
        [AllowNull]
        private ICoreClientAPI _capi;
        [AllowNull]
        private ICoreServerAPI _sapi;
        [AllowNull]
        private DrawerGUI _drawerGUI;

        private static readonly int _packetClientLeftClick = 6444;
        private static readonly int _packetLockedError = 6555;
        private static readonly int _packetPutAll = 6666;

        private long _lastInteractTime = 0;

        private bool _locked = false;
        /// <summary>
        /// Is this Drawer locked to a specific collectable type?
        /// </summary>
        public bool Locked => _locked;
        [AllowNull]
        private ItemStack _lockedToStack;

        private bool _labelEnabled = false;
        public bool LabelEnabled => _labelEnabled;

        private bool _valueEnabled = false;
        public bool ValueEnabled => _valueEnabled;
        /// <summary>
        /// If Locked == true, this is the ItemStack that it is locked too.
        /// </summary>
        public ItemStack LockedToStack => _lockedToStack;

        // represents the base number of slots this type of drawer can hold of a single item
        // storage of items is actually in a single slot
        private int _baseNumSlots = 32;

        #region RenderingVariables
        private string? _hOrient;
        private string? _vOrient;
        private Vec3f? _labelRot1;
        private Vec3f? _labelRot2;
        protected Shape? _nowTesselatingShape;
        public MeshData? _mainMeshData1;
        public MeshData? _mainMeshData2;

        private DrawerLabelRenderer? _labelRenderer1;
        private DrawerLabelRenderer? _labelRenderer2;

        private long _tickListenerHandle;
        public bool? _labelFace1OppositeIsOpaque;
        public bool? _labelFace2OppositeIsOpaque;

        private int _previousItemStackID = -1;
        public bool _shouldDrawMesh = true;
        private string? _curMat;
        private string? _curLining;
        private ITexPositionSource? _glassTextureSource;
        private ITexPositionSource? _tmpTextureSource;
        private ITexPositionSource? _storedItemTextureSource;
        private Dictionary<string, AssetLocation>? _shapeTextures;
        private bool? _tesselatingSpecial;
        private bool? _tesselatingTextureShape;
        private bool? _tesselatingModBlock;
        #endregion

        public DrawerBE()
        {
            this._mainMeshData1 = new MeshData(true);
        }

        public override void Initialize(ICoreAPI api)
        {
            Api = api;
            _hOrient = base.Block.LastCodePart(0);
            _vOrient = base.Block.LastCodePart(1);
            if (_inventory == null) InitializeInventory(api.World);
            _labelEnabled = true;
            _valueEnabled = true;
            base.Initialize(api);
            _inventory?.Pos = Pos;
            _inventory?.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", api);
            _inventory?.ResolveBlocksOrItems();
            if (_inventory != null && !_inventory[0].Empty)
            {
                (_inventory[0] as ItemSlotExpandable)!.UpdateCapacity();
            }
            foreach (long listener in TickHandlers)
            {
                Api.Event.UnregisterGameTickListener(listener);
            }
            if (api.Side == EnumAppSide.Client)
            {
                _capi = api as ICoreClientAPI;

                SetLabelRotation();
                UpdateMeshAndLabelRenderer();
                _tickListenerHandle = RegisterGameTickListener(new Action<float>(UpdateTick), 4000 + Api.World.Rand.Next(), 0);
            }
            else
            {
                _sapi = api as ICoreServerAPI;
            }
        }

        private void UpdateTick(float dt)
        {
            // this ticks on the client 4 seconds after loading.
            UpdateMeshAndLabelRenderer();
            NeighborBlockChanged();
            MarkDirty(true);
            Api.Event.UnregisterGameTickListener(_tickListenerHandle);
            _tickListenerHandle = 0L;
        }

        private void SetLabelRotation()
        {
            string text = base.Block.LastCodePart(1) + "-" + base.Block.LastCodePart(0);
            if (text != null)
            {
                switch (text.Length)
                {
                    case 7:
                        {
                            char c = text[3];
                            if (c != 'e')
                            {
                                if (c == 'w')
                                {
                                    if (text == "up-west")
                                    {
                                        _labelRot1 = new Vec3f(1.5707964f, 0f, 1.5707964f);
                                        _labelRot2 = new Vec3f(-1.5707964f, 0f, -1.5707964f);
                                        return;
                                    }
                                }
                            }
                            else if (text == "up-east")
                            {
                                _labelRot1 = new Vec3f(1.5707964f, 0f, -1.5707964f);
                                _labelRot2 = new Vec3f(-1.5707964f, 0f, 1.5707964f);
                                return;
                            }
                            break;
                        }
                    case 8:
                        {
                            char c = text[3];
                            if (c != 'n')
                            {
                                if (c == 's')
                                {
                                    if (text == "up-south")
                                    {
                                        _labelRot1 = new Vec3f(1.5707964f, 0f, 0f);
                                        _labelRot2 = new Vec3f(-1.5707964f, 0f, 0f);
                                        return;
                                    }
                                }
                            }
                            else if (text == "up-north")
                            {
                                _labelRot1 = new Vec3f(-1.5707964f, 3.1415927f, 0f);
                                _labelRot2 = new Vec3f(1.5707964f, 3.1415927f, 0f);
                                return;
                            }
                            break;
                        }
                    case 9:
                        {
                            char c = text[5];
                            if (c != 'e')
                            {
                                if (c == 'w')
                                {
                                    if (text == "down-west")
                                    {
                                        _labelRot1 = new Vec3f(-1.5707964f, 0f, -1.5707964f);
                                        _labelRot2 = new Vec3f(1.5707964f, 0f, 1.5707964f);
                                        return;
                                    }
                                }
                            }
                            else if (text == "down-east")
                            {
                                _labelRot1 = new Vec3f(-1.5707964f, 0f, 1.5707964f);
                                _labelRot2 = new Vec3f(1.5707964f, 0f, -1.5707964f);
                                return;
                            }
                            break;
                        }
                    case 10:
                        {
                            char c = text[5];
                            if (c != 'n')
                            {
                                if (c == 's')
                                {
                                    if (text == "down-south")
                                    {
                                        _labelRot1 = new Vec3f(-1.5707964f, 0f, 0f);
                                        _labelRot2 = new Vec3f(-1.5707964f, 3.1415927f, 3.1415927f);
                                        return;
                                    }
                                }
                            }
                            else if (text == "down-north")
                            {
                                _labelRot1 = new Vec3f(1.5707964f, 3.1415927f, 0f);
                                _labelRot2 = new Vec3f(1.5707964f, 0f, 3.1415927f);
                                return;
                            }
                            break;
                        }
                    case 11:
                        {
                            char c = text[7];
                            if (c != 'e')
                            {
                                if (c == 'w')
                                {
                                    if (text == "center-west")
                                    {
                                        _labelRot1 = new Vec3f(0f, -1.5707964f, 0f);
                                        _labelRot2 = new Vec3f(0f, 1.5707964f, 0f);
                                        return;
                                    }
                                }
                            }
                            else if (text == "center-east")
                            {
                                _labelRot1 = new Vec3f(0f, 1.5707964f, 0f);
                                _labelRot2 = new Vec3f(0f, -1.5707964f, 0f);
                                return;
                            }
                            break;
                        }
                    case 12:
                        {
                            char c = text[7];
                            if (c != 'n')
                            {
                                if (c == 's')
                                {
                                    if (text == "center-south")
                                    {
                                        _labelRot1 = new Vec3f(0f, 0f, 0f);
                                        _labelRot2 = new Vec3f(0f, 3.1415927f, 0f);
                                        return;
                                    }
                                }
                            }
                            else if (text == "center-north")
                            {
                                _labelRot1 = new Vec3f(0f, 3.1415927f, 0f);
                                _labelRot2 = new Vec3f(0f, 0f, 0f);
                                return;
                            }
                            break;
                        }
                }
            }
            _labelRot1 = Vec3f.Zero;
            _labelRot2 = new Vec3f(0f, 3.1415927f, 0f);
        }

        protected virtual void InitializeInventory(IWorldAccessor world)
        {
            Block block = base.Block;
            if (block == null) block = world.BlockAccessor.GetBlock(this.Pos);
            if (block != null && block.Attributes != null)
            {
                _baseNumSlots = block.Attributes["quantitySlots"].AsInt(0);
            }
            _inventory = new InventoryGeneric(9, null, null, delegate (int id, InventoryGeneric self)
            {
                if (id == 0) return new ItemSlotExpandable(self, _baseNumSlots);
                else return new ItemSlot(self);
            });
            for (int x = 1; x < 9; x++)
            {
                _inventory[x].MaxSlotStackSize = 1;
                _inventory[x].StorageType = EnumItemStorageFlags.Custom4;
            }
            _inventory.SlotModified += OnSlotModified;
            _inventory.OnGetAutoPullFromSlot = new GetAutoPullFromSlotDelegate(GetAutoPullFromSlot);
            _inventory.OnGetAutoPushIntoSlot = new GetAutoPushIntoSlotDelegate(GetAutoPushIntoSlot);            
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);
            if (this.Api.Side == EnumAppSide.Server)
            {
                if (!this.Api.World.Claims.TryAccess(fromPlayer, this.Pos, EnumBlockAccessFlags.Use))
                {
                    return;
                }
                if (this.Api.World.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true).IsLockedForInteract(this.Pos, fromPlayer))
                {
                    ((ICoreServerAPI)this.Api).Network.SendBlockEntityPacket((IServerPlayer)fromPlayer, this.Pos, DrawerBE._packetLockedError, null);
                    return;
                }
                if (packetid == DrawerBE._packetClientLeftClick)
                {
                    bool takeBulk = false;
                    if (data.Length != 0 && data[0] == 1)
                    {
                        takeBulk = true;
                    }
                    TryTake(fromPlayer, takeBulk);
                }
            }
        }
        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (_capi != null)
            {
                if (packetid == DrawerBE._packetLockedError)
                {
                    _capi.TriggerIngameError(this, "locked", Lang.Get("ingameerror-locked", Array.Empty<object>()));
                }
                if (packetid == DrawerBE._packetPutAll)
                {
                    this.Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/build"), _capi.World.Player.Entity, _capi.World.Player, true, 16f, 1f);
                }
            }
            base.OnReceivedServerPacket(packetid, data);
            if (_drawerGUI != null && _drawerGUI.IsOpened()) _drawerGUI.Update();
        }

        private void OnSlotModified(int id)
        {
            (_inventory[0] as ItemSlotExpandable)?.UpdateCapacity();
            if (_drawerGUI != null && _drawerGUI.IsOpened())
            {
                _drawerGUI.Update();
            }

            if (Api.World.BlockAccessor.GetChunkAtBlockPos(Pos) != null)
            {
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
            }
            MarkDirty(false);
        }

        public override void OnBlockBroken(IPlayer? byPlayer = null)
        {
            if (_labelRenderer1 != null)
            {
                _labelRenderer1.Dispose();
                _labelRenderer1 = null;
            }
            if (_labelRenderer2 != null)
            {
                _labelRenderer2.Dispose();
                _labelRenderer2 = null;
            }
            _mainMeshData1 = null;
            _mainMeshData2 = null;
            base.OnBlockBroken(byPlayer);
            if (_drawerGUI != null)
            {
                _drawerGUI.TryClose();
                _drawerGUI.Dispose();
                _drawerGUI = null;
            }
        }
        public override void OnBlockRemoved()
        {
            if (_labelRenderer1 != null)
            {
                _labelRenderer1.Dispose();
                _labelRenderer1 = null;
            }
            if (_labelRenderer2 != null)
            {
                _labelRenderer2.Dispose();
                _labelRenderer2 = null;
            }
            _mainMeshData1 = null;
            _mainMeshData2 = null;
            base.OnBlockRemoved();
            if (_drawerGUI != null)
            {
                _drawerGUI.TryClose();
                _drawerGUI.Dispose();
                _drawerGUI = null;
            }
        }

        public override void OnBlockUnloaded()
        {
            if (_labelRenderer1 != null)
            {
                _labelRenderer1.Dispose();
                _labelRenderer1 = null;
            }
            if (_labelRenderer2 != null)
            {
                _labelRenderer2.Dispose();
                _labelRenderer2 = null;
            }
            _mainMeshData1 = null;
            _mainMeshData2 = null;
            base.OnBlockUnloaded();
            if (_drawerGUI != null)
            {
                _drawerGUI.TryClose();
                _drawerGUI.Dispose();
                _drawerGUI = null;
            }
        }

        private ItemSlot? GetAutoPushIntoSlot(BlockFacing atFace, ItemSlot fromSlot)
        {
            if (!IsAllowed(fromSlot))
            {
                return null;
            }
            if (_inventory[0].Empty) return _inventory[0];
            return _inventory[0]; //_inventory.GetBestSuitedSlot(fromSlot, null, null).slot;
        }

        private ItemSlot? GetAutoPullFromSlot(BlockFacing atFace)
        {
            if (_inventory[0].Empty) return null;
            return _inventory[0];
        }

        private bool CheckForChiseledBlock(ICoreClientAPI capi, ItemStack stack, out MeshData? mesh)
        {
            mesh = null;
            if (stack.Class != EnumItemClass.Block)
            {
                return false;
            }
            if (!(stack.Block is BlockChisel))
            {
                return false;
            }
            ITreeAttribute treeAttribute = stack.Attributes;
            if (treeAttribute == null)
            {
                treeAttribute = new TreeAttribute();
            }
            int[] blockIds = BlockEntityMicroBlock.MaterialIdsFromAttributes(treeAttribute, capi.World);
            uint[]? array = null;
            IntArrayAttribute? intArrayAttribute = treeAttribute["cuboids"] as IntArrayAttribute;
            if (intArrayAttribute != null)
            {
                array = intArrayAttribute.AsUint;
            }
            if (array == null)
            {
                LongArrayAttribute? longArrayAttribute = treeAttribute["cuboids"] as LongArrayAttribute;
                if (longArrayAttribute != null)
                {
                    array = longArrayAttribute.AsUint;
                }
            }
            List<uint> voxelCuboids;
            if (array == null)
            {
                voxelCuboids = new List<uint>();
            }
            else
            {
                voxelCuboids = new List<uint>(array);
            }
            mesh = BlockEntityMicroBlock.CreateMesh(capi, voxelCuboids, blockIds, null, null, null, 0);
            if (mesh != null)
            {
                for (int i = 0; i < mesh.GetVerticesCount(); i++)
                {
                    mesh.Flags[i] &= -256;
                }
                for (int j = 0; j < mesh.RenderPassCount; j++)
                {
                    if (mesh.RenderPassesAndExtraBits[j] != 3)
                    {
                        mesh.RenderPassesAndExtraBits[j] = 1;
                    }
                }
                return true;
            }
            return false;
        }

        private bool CheckForSpecialBlocks(ItemStack stack, out MeshData? mesh)
        {
            if (stack.Class != EnumItemClass.Block)
            {
                mesh = null;
                return false;
            }
            if (stack.Block.ShapeInventory == null)
            {
                mesh = null;
                return false;
            }
            if (stack.Attributes != null)
            {
                string stacktype = stack.Attributes.GetString("type", null);
                if (stacktype != null)
                {
                    _tesselatingModBlock = true;
                    _tmpTextureSource = _capi.Tesselator.GetTextureSource(stack.Block, 0, false);
                    string? text = stack.Block.Attributes["shape"][stacktype].AsString(null);
                    if (text != null)
                    {
                        AssetLocation assetLocation;
                        if (text.StartsWith("game:"))
                        {
                            assetLocation = new AssetLocation(text).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                        }
                        else
                        {
                            assetLocation = new AssetLocation(stack.Block.Code.Domain, text).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                        }
                        if (assetLocation != null)
                        {
                            IAsset asset = this.Api.Assets.TryGet(assetLocation, true);
                            if (asset != null)
                            {
                                Shape shape = asset.ToObject<Shape>(null);
                                if (shape != null)
                                {
                                    _shapeTextures = shape.Textures;
                                    _capi.Tesselator.TesselateShape("vintdrawers content shape", shape, out mesh, this, null, 0, 0, 0, null, null);
                                    if (mesh != null)
                                    {
                                        for (int i = 0; i < mesh.RenderPassCount; i++)
                                        {
                                            if (mesh.RenderPassesAndExtraBits[i] != 3)
                                            {
                                                mesh.RenderPassesAndExtraBits[i] = 1;
                                            }
                                        }
                                        int num = -503318784; // wtf?
                                        for (int j = 0; j < mesh.GetVerticesCount(); j++)
                                        {
                                            mesh.Flags[j] &= num;
                                        }
                                        mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, -1.5707964f, 0f);
                                        return true;
                                    }
                                }
                            }
                        }                        
                    }
                }
            }
            mesh = null;
            return false;
        }

        private bool CheckForSpecials(ItemStack stack, out MeshData mesh)
        {
            mesh = new MeshData(true);
            if (stack.Attributes == null)
            {
                return false;
            }
            string @string = stack.Attributes.GetString("material", null);
            string string2 = stack.Attributes.GetString("lining", null);
            string string3 = stack.Attributes.GetString("glass", null);
            if (@string == null || string2 == null || string3 == null)
            {
                return false;
            }
            _tesselatingSpecial = true;
            _tmpTextureSource = _capi.Tesselator.GetTextureSource(stack.Block, 0, false);
            Shape shapeBase = _capi.Assets.TryGet("shapes/" + stack.Block.Shape.Base.Path + ".json", true).ToObject<Shape>(null);
            _curMat = @string;
            _curLining = string2;
            Block? block = _capi.World.GetBlock(new AssetLocation("glass-" + string3));
            _glassTextureSource = _capi.Tesselator.GetTextureSource(block, 0, false);
            _capi.Tesselator.TesselateShape("VintDrawers-blocklantern", shapeBase, out mesh, this, null, 0, 0, 0, null, null);
            if (mesh != null)
            {
                for (int i = 0; i < mesh.GetVerticesCount(); i++)
                {
                    mesh.Flags[i] &= -256;
                }
                for (int j = 0; j < mesh.RenderPassCount; j++)
                {
                    if (mesh.RenderPassesAndExtraBits[j] != 3)
                    {
                        mesh.RenderPassesAndExtraBits[j] = 1;
                    }
                }
                return true;
            }
            return false;
        }

        private MeshData? GenArmorMesh(ICoreClientAPI capi, ItemStack itemstack)
        {
            JsonObject attributes = itemstack.Collectible.Attributes;
            EntityProperties? entityType = _capi.World.GetEntityType(new AssetLocation("player"));
            if (entityType == null) return null;
            Shape loadedShape = entityType.Client.LoadedShape;
            AssetLocation? @base = entityType.Client.Shape.Base;
            Shape shape = new Shape
            {
                Elements = loadedShape.CloneElements(),
                Animations = loadedShape.Animations,
                AnimationsByCrc32 = loadedShape.AnimationsByCrc32,
                JointsById = loadedShape.JointsById,
                TextureWidth = loadedShape.TextureWidth,
                TextureHeight = loadedShape.TextureHeight,
                Textures = null
            };
            if (attributes != null && attributes["attachShape"].Exists)
            {
                return null;
            }
            if (itemstack.Class != EnumItemClass.Item)
            {
                return null;
            }
            CompositeShape shape2 = itemstack.Item.Shape;
            if (shape2 == null)
            {
                capi.World.Logger.Warning("Entity armor {0} {1} does not define a shape through either the shape property or the attachShape Attribute. Armor pieces will be invisible.", new object[]
                {
                    itemstack.Class,
                    itemstack.Collectible.Code
                });
                return null;
            }
            AssetLocation assetLocation = shape2.Base.CopyWithPath("shapes/" + shape2.Base.Path + ".json");
            IAsset asset = capi.Assets.TryGet(assetLocation, true);
            if (asset == null)
            {
                capi.World.Logger.Warning("Entity armor shape {0} defined in {1} {2} not found, was supposed to be at {3}. Armor piece will be invisible.", new object[]
                {
                    shape2.Base,
                    itemstack.Class,
                    itemstack.Collectible.Code,
                    assetLocation
                });
                return null;
            }
            Shape shape3;
            try
            {
                shape3 = asset.ToObject<Shape>(null);
            }
            catch (Exception ex)
            {
                capi.World.Logger.Warning("Exception thrown when trying to load entity armor shape {0} defined in {1} {2}. Armor piece will be invisible. Exception: {3}", new object[]
                {
                    shape2.Base,
                    itemstack.Class,
                    itemstack.Collectible.Code,
                    ex
                });
                return null;
            }
            shape.Textures = shape3.Textures;
            foreach (ShapeElement shapeElement in shape3.Elements)
            {
                if (shapeElement.StepParentName != null)
                {
                    ShapeElement elementByName = shape.GetElementByName(shapeElement.StepParentName, StringComparison.InvariantCultureIgnoreCase);
                    if (elementByName == null)
                    {
                        capi.World.Logger.Warning("Entity armor shape {0} defined in {1} {2} requires step parent element with name {3}, but no such element was found in shape {3}. Will not be visible.", new object[]
                        {
                            shape2.Base,
                            itemstack.Class,
                            itemstack.Collectible.Code,
                            shapeElement.StepParentName,
                            @base
                        });
                    }
                    else if (elementByName.Children == null)
                    {
                        elementByName.Children = new ShapeElement[]
                        {
                            shapeElement
                        };
                    }
                    else
                    {
                        elementByName.Children = elementByName.Children.Append(shapeElement);
                    }
                }
                else
                {
                    capi.World.Logger.Warning("Entity armor shape element {0} in shape {1} defined in {2} {3} did not define a step parent element. Will not be visible.", new object[]
                    {
                        shapeElement.Name ?? "Null",
                        shape2.Base,
                        itemstack.Class,
                        itemstack.Collectible.Code
                    });
                }
            }
            MeshData result;
            capi.Tesselator.TesselateShapeWithJointIds("entity", shape, out result, this, new Vec3f(), null, null);
            return result;
        }

        private MeshData? GenMeshData(ITesselatorAPI tesselator)
        {
            ItemStack? storedItemStack = GetStoredItemStack();
            if (storedItemStack == null)
            {
                return null;
            }
            _tesselatingSpecial = false;
            _tesselatingTextureShape = false;
            _tesselatingModBlock = false;
            _nowTesselatingShape = null;
            _tmpTextureSource = null;
            _glassTextureSource = null;
            Dictionary<string, MeshData?> orCreate = ObjectCacheUtil.GetOrCreate<Dictionary<string, MeshData?>>(this.Api, "VintageDrawerMeshes", () => new Dictionary<string, MeshData?>());
            string text = storedItemStack.GetName();
            if (storedItemStack.Class == EnumItemClass.Block)
            {
                text = text + "-" + _inventory[0].GetStackDescription(_capi.World, false);
            }
            MeshData? meshData;
            if (orCreate.TryGetValue(text, out meshData))
            {
                return meshData;
            }
            if (storedItemStack.Block != null && storedItemStack.Block is BlockShapeFromAttributes)
            {
                BlockShapeFromAttributes? blockShapeFromAttributes = storedItemStack.Block as BlockShapeFromAttributes;
                if (blockShapeFromAttributes != null)
                {
                    IShapeTypeProps? shapeTypeProps = (blockShapeFromAttributes != null) ? blockShapeFromAttributes.GetTypeProps(storedItemStack.Attributes.GetString("type", null), storedItemStack.Clone(), null) : null;
                    if (shapeTypeProps != null)
                    {
                        meshData = blockShapeFromAttributes?.GetOrCreateMesh(shapeTypeProps, null, null).Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, 3.1415927f + shapeTypeProps.Rotation.Y * 0.017453292f, 0f).Scale(new Vec3f(0.5f, 0.5f, 0.5f), -1f, 1f, 1f);
                    }
                    if (meshData != null)
                    {
                        return orCreate[text] = meshData;
                    }
                }
            }
            if (storedItemStack.Collectible.Attributes != null && storedItemStack.Collectible.Attributes["wearableAttachment"].AsBool(false))
            {
                MeshData? meshData2 = this.GenArmorMesh(_capi, storedItemStack);
                if (meshData2 != null)
                {
                    meshData = meshData2;
                    for (int i = 0; i < meshData.RenderPassCount; i++)
                    {
                        if (meshData.RenderPassesAndExtraBits[i] != 3)
                        {
                            meshData.RenderPassesAndExtraBits[i] = 1;
                        }
                    }
                    return orCreate[text] = meshData;
                }
            }
            CompositeShape? compositeShape;
            if (storedItemStack.Class == EnumItemClass.Item)
            {
                _storedItemTextureSource = _capi.Tesselator.GetTextureSource(storedItemStack.Item, false);
                if (storedItemStack.Item.Shape != null)
                {
                    if (storedItemStack.Item != null)
                    {
                        if (storedItemStack.Item.GetHeldItemName(storedItemStack) == "Rope")
                        {
                            goto bounce;
                        }
                        try
                        {
                            _capi.Tesselator.TesselateItem(storedItemStack.Item, out meshData, this);
                        }
                        catch (Exception)
                        {
                            this.Api.World.Logger.Warning(storedItemStack.GetName() + " Item threw Exception! Shape.Base: " + storedItemStack.Item.Shape.Base.ToString());
                            try
                            {
                                _capi.Tesselator.TesselateItem(storedItemStack.Item, out meshData);
                            }
                            catch (Exception)
                            {
                                this.Api.World.Logger.Warning(storedItemStack.GetName() + " Item threw Exception again! Shape.Base: " + storedItemStack.Item.Shape.Base.ToString());
                                Shape shapeBase = _capi.Assets.TryGet(base.Block.Shape.Base.Clone(), true).ToObject<Shape>(null);
                                tesselator.TesselateShape("vintdrawer content shape", shapeBase, out meshData, this, null, 0, 0, 0, null, null);
                            }
                        }
                    }
                    if (meshData != null)
                    {
                        for (int j = 0; j < meshData.RenderPassCount; j++)
                        {
                            if (meshData.RenderPassesAndExtraBits[j] != 3)
                            {
                                meshData.RenderPassesAndExtraBits[j] = 1;
                            }
                        }
                        int num = -503318784;
                        for (int k = 0; k < meshData.GetVerticesCount(); k++)
                        {
                            meshData.Flags[k] &= num;
                        }
                        if (storedItemStack.Collectible != null)
                        {
                            if (storedItemStack.Collectible.Code.ToString().EndsWith("quartz"))
                            {
                                if (storedItemStack.GetName().ToLower().Equals("clear quartz"))
                                {
                                    for (int l = 0; l < meshData.RenderPassCount; l++)
                                    {
                                        meshData.RenderPassesAndExtraBits[l] = 2;
                                    }
                                }
                                else
                                {
                                    for (int m = 0; m < meshData.RenderPassCount; m++)
                                    {
                                        meshData.RenderPassesAndExtraBits[m] = 1;
                                    }
                                }
                            }
                            if (storedItemStack.Collectible.Code.ToString().Contains("game:pounder-"))
                            {
                                meshData.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.375f, 0.375f, 0.375f);
                                meshData.Translate(new Vec3f(0f, -0.5f, 0f));
                            }
                            if (storedItemStack.Collectible.Code.ToString().Contains("game:boat-sailed-"))
                            {
                                meshData.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.4f, 0.4f, 0.4f);
                                meshData.Translate(new Vec3f(0.5f, -1f, 0f));
                            }
                            if (storedItemStack.Collectible.Code.ToString().Contains("game:roller"))
                            {
                                meshData.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.75f, 0.75f, 0.75f);
                                meshData.Translate(new Vec3f(0f, -0.1f, 0f));
                            }
                            if (storedItemStack.Collectible.Code.ToString().Contains("game:spear-"))
                            {
                                meshData.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.4f, 0.4f, 0.4f);
                                meshData.Translate(new Vec3f(0.6f, -0.5f, 0f));
                                string text2 = storedItemStack.Collectible.Code.ToString();
                                if (text2.Contains("copper") || text2.Contains("bronze") || text2.Contains("iron") || text2.Contains("steel") || text2.Contains("silver") || text2.Contains("gold"))
                                {
                                    meshData.Translate(new Vec3f(0.25f, 0.25f, 0f));
                                }
                                if (text2.Contains("scrap"))
                                {
                                    meshData.Translate(new Vec3f(0.35f, 0.25f, 0f));
                                }
                                if (text2.Contains("hacking"))
                                {
                                    meshData.Translate(new Vec3f(0.25f, 0.25f, 0f));
                                }
                            }
                        }
                        return orCreate[text] = meshData;
                    }
                }
            bounce:
                if (storedItemStack.Item?.GetHeldItemName(storedItemStack) == "Rope")
                {
                    compositeShape = null;
                }
                else
                {
                    compositeShape = storedItemStack.Item?.Shape;
                }
                if (compositeShape == null)
                {
                    _tesselatingTextureShape = true;
                    _capi.Tesselator.TesselateItem(storedItemStack.Item, out meshData, this);
                    if (meshData != null)
                    {
                        int num2 = -503318784;
                        for (int n = 0; n < meshData.GetVerticesCount(); n++)
                        {
                            meshData.Flags[n] &= num2;
                        }
                        for (int num3 = 0; num3 < meshData.RenderPassCount; num3++)
                        {
                            if (meshData.RenderPassesAndExtraBits[num3] != 3)
                            {
                                meshData.RenderPassesAndExtraBits[num3] = 1;
                            }
                        }
                    }
                    if (storedItemStack.Item?.GetType().ToString().Contains("ItemWorkItem") ?? false)
                    {
                        meshData?.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.3f, 0.3f, 0.3f);
                        for (int num4 = 0; num4 < meshData?.RenderPassCount; num4++)
                        {
                            meshData.RenderPassesAndExtraBits[num4] = 0;
                        }
                    }                    
                    return orCreate[text] = meshData;
                }
            }
            else
            {
                compositeShape = storedItemStack.Block?.ShapeInventory;
                _storedItemTextureSource = _capi.Tesselator.GetTextureSource(storedItemStack.Block, 0, false);
                if (compositeShape == null)
                {
                    if (this.CheckForChiseledBlock(_capi, storedItemStack, out meshData))
                    {
                        return meshData;
                    }
                    if (this.CheckForSpecials(storedItemStack, out meshData))
                    {
                        return orCreate[text] = meshData;
                    }
                    meshData = _capi.TesselatorManager.GetDefaultBlockMesh(storedItemStack.Block).Clone();
                    if (meshData != null)
                    {
                        int num5 = -503318784;
                        for (int num6 = 0; num6 < meshData.GetVerticesCount(); num6++)
                        {
                            meshData.Flags[num6] &= num5;
                        }
                        if (storedItemStack.Block?.BlockMaterial == EnumBlockMaterial.Plant && (storedItemStack.Block.Code.ToString().Contains("bush") || storedItemStack.Block.Code.ToString().Contains("sapling")))
                        {
                            for (int num7 = 0; num7 < meshData.ClimateColorMapIds.Length; num7++)
                            {
                                if (meshData.ClimateColorMapIds[num7] > 0)
                                {
                                    meshData.ClimateColorMapIds[num7] = 7;
                                }
                            }
                            for (int num8 = 0; num8 < meshData.SeasonColorMapIds.Length; num8++)
                            {
                                if (meshData.SeasonColorMapIds[num8] > 0)
                                {
                                    meshData.SeasonColorMapIds[num8] = 10;
                                }
                            }
                        }
                        for (int num9 = 0; num9 < meshData.RenderPassCount; num9++)
                        {
                            if (meshData.RenderPassesAndExtraBits[num9] != 3)
                            {
                                meshData.RenderPassesAndExtraBits[num9] = 1;
                            }
                        }
                        if (storedItemStack.Block?.Code.ToString().Contains("game:door-") ?? false)
                        {
                            if (storedItemStack.Block.Code.ToString().Contains("1x3") || storedItemStack.Block.Code.ToString().Contains("2x2"))
                            {
                                meshData.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.7f, 0.7f, 0.7f);
                                meshData.Translate(new Vec3f(0f, 0.25f, 0f));
                            }
                            else if (storedItemStack.Block.Code.ToString().Contains("2x4"))
                            {
                                meshData.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.6f, 0.6f, 0.6f);
                                meshData.Translate(new Vec3f(0f, 0.7f, 0f));
                            }
                        }
                        return orCreate[text] = meshData;
                    }
                }
            }
            List<IAsset> list;
            if (compositeShape?.Base.Path.EndsWith("*") ?? false)
            {
                list = this.Api.Assets.GetMany(compositeShape.Base.Clone().WithPathPrefixOnce("shapes/").Path.Substring(0, compositeShape.Base.Path.Length - 1), compositeShape.Base.Domain, true);
            }
            else
            {
                list = new List<IAsset>
                {
                    this.Api.Assets.TryGet(compositeShape?.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"), true)
                };
            }
            
            if (list != null && list.Count > 0)
            {
                if (this.CheckForSpecialBlocks(storedItemStack, out meshData))
                {
                    return orCreate[text] = meshData;
                }
                for (int num10 = 0; num10 < 1; num10++)
                {
                    Shape shape = list[num10].ToObject<Shape>(null);
                    _shapeTextures = shape.Textures;
                    try
                    {
                        tesselator.TesselateShape("vintdrawer content shape", shape, out meshData, this, null, 0, 0, 0, null, null);
                    }
                    catch
                    {
                        try
                        {
                            tesselator.TesselateShape(storedItemStack.Collectible, shape, out meshData, null, null, null);
                        }
                        catch
                        {
                            this.Api.World.Logger.Warning(storedItemStack.GetName() + " Block threw Exception! Shape.Base: " + storedItemStack.Block?.Shape.Base.ToString());
                            shape = _capi.Assets.TryGet("game:shapes/block/basic/cube.json", true).ToObject<Shape>(null);
                            tesselator.TesselateShape("vintdrawer content shape", shape, out meshData, this, null, 0, 0, 0, null, null);
                        }
                    }
                    int num11 = -503318784;
                    for (int num12 = 0; num12 < meshData.GetVerticesCount(); num12++)
                    {
                        meshData.Flags[num12] &= num11;
                    }
                    for (int num13 = 0; num13 < meshData.RenderPassCount; num13++)
                    {
                        if (meshData.RenderPassesAndExtraBits[num13] != 3)
                        {
                            meshData.RenderPassesAndExtraBits[num13] = 1;
                        }
                    }
                }
            }
            else
            {
                this.Api.World.Logger.Error("VintageDrawers: Content asset {0} not found,", new object[]
                {
                    compositeShape?.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json") ?? "null"
                });
            }
            if (storedItemStack.Collectible != null && storedItemStack.Collectible.Code.ToString().Contains("game:pulverizerframe-"))
            {
                meshData?.Translate(new Vec3f(0f, -0.4f, 0f));
            }
            if (storedItemStack.Collectible != null && storedItemStack.Collectible.Code.ToString().Contains("game:wallpaper-"))
            {
                meshData?.Translate(new Vec3f(0.45f, 0.45f, 0.2f));
                meshData?.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), -1.55f, 0f, 0f);
            }
            return orCreate[text] = meshData;
        }

        /// <summary>
        /// Install Upgrade, if a slot is available.<br/>
        /// Called when sneak right-clicking a drawer with an upgrade in hand.
        /// </summary>
        public bool UpdateInventory(IPlayer player)
        {
            ItemSlot upgradeitem = player.InventoryManager.ActiveHotbarSlot;
            
            bool isStack = upgradeitem.Itemstack?.Collectible.Attributes["stack"].AsBool(false) ?? false;
            string? tier = upgradeitem.Itemstack?.Collectible.Variant["tier"];
            if (tier != null && (tier != "void" || tier != "push" || tier != "pull")) tier = null;            
            if (tier != null)
            {
                for (int x = 1; x < 9; x++)
                {
                    // iterate through the upgrade slots
                    string? installedtier = (_inventory[x].Empty) ? null : _inventory[x].Itemstack?.Collectible.Variant["tier"];             
                    // only one special type is valid
                    if (installedtier != null && tier != null && installedtier == tier) return false;
                }
            }            
            for (int x = 1; x < 9; x++)
            {
                if (_inventory[x].Empty)
                {
                    _inventory[x].Itemstack = upgradeitem.TakeOut(1);
                    _inventory[x].MarkDirty();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Toggles the lock on/off<br/>
        /// Optionally allows to override to locked state.
        /// </summary>
        /// <param name="isOverride">Set to true to force-lock the state if not locked, doesn't toggle.</param>
        private void ToggleDrawerLock(bool isOverride = false)
        {
            if (isOverride)
            {
                if (!_locked)
                {
                    _locked = true;
                    if (!_inventory[0].Empty)
                    {
                        _lockedToStack = _inventory[0].Itemstack?.Clone();
                        _lockedToStack?.StackSize = 1;
                    }
                }
                return;
            }
            if (_locked)
            {
                _locked = false;
                _lockedToStack = null;
            }
            else
            {
                _locked = true;
                if (!_inventory[0].Empty)
                {
                    _lockedToStack = _inventory[0].Itemstack?.Clone();
                    _lockedToStack?.StackSize = 1;
                }
            }
            MarkDirty(true);
        }
            
        private void ToggleDrawerLabel(bool isOverride = false)
        {
            if (isOverride) { _labelEnabled = true; return; }
            _labelEnabled = !_labelEnabled;
        }

        private void ToggleDrawerValue(bool isOverride = false)
        {
            if (isOverride) { _valueEnabled = true; return; }
            _valueEnabled = !_valueEnabled;
        }

        private bool IsAllowed(ItemSlot fromSlot)
        {
            if (fromSlot == null || fromSlot.Itemstack == null || this.Api == null)
            {
                return false;
            }
            if (!this._inventory[0].Empty && _inventory[0].Itemstack?.StackSize == _inventory[0].MaxSlotStackSize)
            {
                return false;
            }
            ItemStack? storedItemStack = this.GetStoredItemStack();
            if (storedItemStack == null && fromSlot.Itemstack.Block != null)
            {
                BlockContainer? blockContainer = fromSlot.Itemstack.Block as BlockContainer;
                if (blockContainer != null)
                {
                    ItemStack[] nonEmptyContents = blockContainer.GetNonEmptyContents(this.Api.World, fromSlot.Itemstack);
                    if (nonEmptyContents != null && nonEmptyContents.Length != 0)
                    {
                        if (this._capi != null)
                        {
                            this._capi.TriggerIngameError(this, "cantstore", Lang.Get("vintagedrawers:error-filledcontainer", Array.Empty<object>()));
                        }
                        return false;
                    }
                }
            }
            if (storedItemStack != null && !storedItemStack.Equals(this.Api.World, fromSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                bool flag = false;
                if (storedItemStack.Collectible != null && fromSlot.Itemstack.Collectible != null && storedItemStack.Collectible.Code != fromSlot.Itemstack.Collectible.Code)
                {
                    return false;
                }
                if (fromSlot.Itemstack.Block is BlockCrock && storedItemStack.Block is BlockCrock)
                {
                    if (fromSlot.Itemstack.Block != null)
                    {
                        BlockContainer? blockContainer2 = fromSlot.Itemstack.Block as BlockContainer;
                        if (blockContainer2 != null)
                        {
                            ItemStack[] nonEmptyContents2 = blockContainer2.GetNonEmptyContents(this.Api.World, fromSlot.Itemstack);
                            if (nonEmptyContents2 != null && nonEmptyContents2.Length != 0)
                            {
                                return false;
                            }
                        }
                    }
                    flag = true;
                }
                if (!flag)
                {
                    return false;
                }
            }
            CollectibleObject? collectible = fromSlot.Itemstack.Collectible;
            if (collectible == null)
            {
                return false;
            }
            if (collectible.TransitionableProps != null && collectible.TransitionableProps.Length != 0)
            {
                if (this._capi != null)
                {
                    this._capi.TriggerIngameError(this, "cantstore", Lang.Get("vintagedrawers:error-perishable", Array.Empty<object>()));
                }
                return false;
            }
            if (collectible.HasTemperature(fromSlot.Itemstack))
            {
                if (collectible.GetTemperature(this.Api.World, fromSlot.Itemstack) >= 20f)
                {
                    if (this._capi != null)
                    {
                        this._capi.TriggerIngameError(this, "cantstore", Lang.Get("vintagedrawers:error-toohot", Array.Empty<object>()));
                    }
                    return false;
                }
                collectible.SetTemperature(this.Api.World, fromSlot.Itemstack, 0f, false);
            }
            return true;
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            string face = blockSel.Face.Code;
            string horient = base.Block.Variant["horizontal"];
            bool isSneaking = byPlayer.Entity.Controls.Sneak;
            bool openGUI = false;
            if (base.Block.Variant["vertical"] == "center")
            {
                if (blockSel.Face.Opposite.Code == horient && isSneaking)
                {
                    openGUI = true;
                }
            }
            else
            {
                if (face == base.Block.Variant["vertical"] && isSneaking)
                {
                    openGUI = true;
                }
            }
            if (openGUI && _capi != null)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    _drawerGUI = new DrawerGUI(Lang.Get("gui-onedrawer"), _inventory, Pos, _capi, this);
                    _drawerGUI.Update();
                    return _drawerGUI;
                });
            }
            return true;
        }

        internal bool OnPlayerInteract(IPlayer byPlayer)
        {
            ItemSlot activeslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeslot == null) return false;

            long elapsedms = Api.World.ElapsedMilliseconds;
            bool bouncer = elapsedms - _lastInteractTime < 500L; // half second trigger, a quick double right click
            _lastInteractTime = elapsedms;
            bool sprintsneak = byPlayer.Entity.Controls.Sneak | byPlayer.Entity.Controls.Sprint;
            bool isEmpty = _inventory[0].Empty;
            if (activeslot.Empty)
            {
                OnPlayerRightClick(byPlayer, byPlayer.CurrentBlockSelection);
                if (bouncer && Api.Side == EnumAppSide.Server && TryPutAll(byPlayer))
                {
                    _sapi.Network.SendBlockEntityPacket((IServerPlayer)byPlayer, Pos, DrawerBE._packetPutAll, null);
                    return true;
                }
            }
            else
            {
                if (!sprintsneak && activeslot.Itemstack.Collectible.Code.Path.Contains("drawerkey"))
                {
                    string keytype = activeslot.Itemstack.Collectible.Variant["type"];
                    if (keytype == "lock") ToggleDrawerLock();
                    else if (keytype == "label") ToggleDrawerLabel();
                    else if (keytype == "value") ToggleDrawerValue();
                    return false;
                }
                if (!IsAllowed(activeslot))
                {
                    return false;
                }
                if (TryPut(byPlayer.InventoryManager.ActiveHotbarSlot, true))
                {
                    if (isEmpty)
                    {
                        (_inventory[0] as ItemSlotExpandable)!.UpdateCapacity();
                    }
                    IClientPlayer? clientPlayer = byPlayer as IClientPlayer;
                    if (clientPlayer != null)
                    {
                        clientPlayer.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    }
                    Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f, 1f);
                    return true;
                }
            }
            return false;
        }

        public void OnPlayerLeftClick(IPlayer player)
        {
            if (_capi != null)
            {
                if (_inventory[0].Empty)
                {
                    return;
                }
                byte[] array = new byte[1];
                if (player.Entity.Controls.Sneak)
                {
                    array[0] = 1;
                }
                _capi.Network.SendBlockEntityPacket(this.Pos, DrawerBE._packetClientLeftClick, array);
            }
        }

        private bool TryPut(ItemSlot fromSlot, bool putBulk)
        {
            bool result = false;
            int numToMove = 1;
            if (putBulk)
            {
                numToMove = fromSlot.StackSize;
            }            

            ItemSlotExpandable bulkslot = (ItemSlotExpandable)_inventory[0];
            if (bulkslot == null)
            {
                return result;
            }
            int moved = bulkslot.TryTakeFrom(Api.World, fromSlot, numToMove);
            //int moved = fromSlot.TryPutInto(this.Api.World, bulkslot, num);
            if (moved > 0)
            {
                result = true;
            }
            result = true;
            return result;
        }

        private bool TryPutAll(IPlayer byPlayer)
        {
            if (_inventory.Empty && _lockedToStack == null)
            {
                return false;
            }
            if (GetStoredItemStack() == null)
            {
                return false;
            }
            bool result = false;
            string inventoryName = byPlayer.InventoryManager.GetInventoryName("backpack");
            IInventory inventory = byPlayer.InventoryManager.GetInventory(inventoryName);
            IInventory hotbarInventory = byPlayer.InventoryManager.GetHotbarInventory();
            if (hotbarInventory != null)
            {
                for (int i = 0; i < hotbarInventory.Count - 1; i++)
                {
                    if (!hotbarInventory[i].Empty && IsAllowed(hotbarInventory[i]) && this.TryPut(hotbarInventory[i], true))
                    {
                        result = true;
                    }
                }
            }
            if (inventory != null)
            {
                for (int j = 0; j < inventory.Count; j++)
                {
                    if (!inventory[j].Empty && IsAllowed(inventory[j]) && this.TryPut(inventory[j], true))
                    {
                        result = true;
                    }
                }
            }
            return result;
        }

        private bool TryTake(IPlayer byPlayer, bool takeBulk)
        {
            if (_inventory.Empty)
            {
                return false;
            }
            bool filthy = false;
            int numToTake = 1;
            if (takeBulk)
            {
                numToTake = this.GetStoredItemMaxStackSize();
            }
            int num2 = numToTake;
            ItemStack? itemStack = null;

            ItemSlotExpandable? bulkslot = GetStoredItemSlot();
            if (bulkslot == null) return false; // the drawer doesn't have a bulkslot?
            filthy = true;
            if (itemStack == null)
            {
                itemStack = bulkslot.TakeOut(numToTake);
            }
            if (itemStack == null) return false; // nothing to grab?
            
            if (byPlayer.InventoryManager.TryGiveItemstack(itemStack, true))
            {
                if (itemStack.Block != null && itemStack.Block.Sounds != null)
                {
                    this.Api.World.PlaySoundAt(itemStack.Block.Sounds.Place.Location, byPlayer.Entity, byPlayer, true, 16f, 1f);
                }
                else
                {
                    this.Api.World.PlaySoundAt(new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f, 1f);
                }
            }
            if (itemStack.StackSize > 0)
            {
                Vec3f zero = Vec3f.Zero;
                Vec3d zero2 = Vec3d.Zero;
                zero.Set(0.5f, 0.4f, 0.5f);
                string a = _hOrient!;
                if (!(a == "north"))
                {
                    if (!(a == "south"))
                    {
                        if (!(a == "east"))
                        {
                            if (a == "west")
                            {
                                zero.X = 1f;
                                zero2.X = 0.025;
                                zero2.Z = (this.Api.World.Rand.NextDouble() - 0.5) * 0.009999999776482582;
                            }
                        }
                        else
                        {
                            zero.X = 0f;
                            zero2.X = -0.025;
                            zero2.Z = (this.Api.World.Rand.NextDouble() - 0.5) * 0.009999999776482582;
                        }
                    }
                    else
                    {
                        zero.Z = 0f;
                        zero2.Z = -0.025;
                        zero2.X = (this.Api.World.Rand.NextDouble() - 0.5) * 0.009999999776482582;
                    }
                }
                else
                {
                    zero.Z = 1f;
                    zero2.Z = 0.025;
                    zero2.X = (this.Api.World.Rand.NextDouble() - 0.5) * 0.009999999776482582;
                }
                a = _vOrient!;
                if (!(a == "center"))
                {
                    if (!(a == "up"))
                    {
                        if (a == "down")
                        {
                            zero.X = (zero.Z = 0.5f);
                            zero.Y = 0f;
                            zero2.Y = 0.0;
                            zero2.X = (this.Api.World.Rand.NextDouble() - 0.5) * 0.009999999776482582;
                            zero2.Z = (this.Api.World.Rand.NextDouble() - 0.5) * 0.009999999776482582;
                        }
                    }
                    else
                    {
                        zero.X = (zero.Z = 0.5f);
                        zero.Y = 1f;
                        zero2.Y = 0.05000000074505806;
                        zero2.X = (this.Api.World.Rand.NextDouble() - 0.5) * 0.009999999776482582;
                        zero2.Z = (this.Api.World.Rand.NextDouble() - 0.5) * 0.009999999776482582;
                    }
                }
                this.Api.World.SpawnItemEntity(itemStack, this.Pos.ToVec3d().Add(zero), zero2);
            }
            if (filthy)
            {
                this.MarkDirty(false, null);
            }
            return filthy;
        }

        /// <summary>
        /// Returns what amount this drawer is storing.
        /// </summary>
        /// <returns>Amount stored, or 0 if empty</returns>
        public int GetInventoryCount()
        {
            if (_inventory[0].Empty) return 0;
            return _inventory[0].Itemstack?.StackSize ?? 0;
        }

        /// <summary>
        /// Returns how many items can fit in this drawer.<br/>
        /// Scales by the max stack size of the item.
        /// </summary>
        /// <returns>Total storage amount</returns>
        public int GetStoredMaxStackSize()
        {
            return _inventory[0].MaxSlotStackSize;
        }

        /// <summary>
        /// What is the max stack size of the item in the drawer.
        /// </summary>
        /// <returns></returns>
        public int GetStoredItemMaxStackSize()
        {
            if (_inventory[0].Empty)
            {
                if (_locked && _lockedToStack != null)
                {
                    return _lockedToStack.Collectible.MaxStackSize;
                }
                return 0;
            }
            return _inventory[0].Itemstack?.Collectible.MaxStackSize ?? 0;
        }

        public ItemSlotExpandable? GetStoredItemSlot()
        {
            return _inventory[0] as ItemSlotExpandable;
        }

        public ItemStack? GetStoredItemStack()
        {
            if (_inventory[0].Empty)
            {
                if (LockedToStack == null)
                {
                    return null;
                }
                else
                {
                    return LockedToStack;
                }
            }
            ItemSlotExpandable itemSlot = (ItemSlotExpandable)_inventory[0];
            if (itemSlot == null)
            {
                return null;
            }
            return itemSlot.Itemstack;
        }

        public void NeighborBlockChanged()
        {
            if (this.Api != null)
            {
                _labelFace1OppositeIsOpaque = false;
                _labelFace2OppositeIsOpaque = false;
                string a = _hOrient!;
                if (!(a == "north"))
                {
                    if (!(a == "south"))
                    {
                        if (!(a == "east"))
                        {
                            if (a == "west")
                            {
                                _labelFace1OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.EastCopy(1)).SideOpaque[BlockFacing.EAST.Index];
                                _labelFace2OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.WestCopy(1)).SideOpaque[BlockFacing.WEST.Index];
                            }
                        }
                        else
                        {
                            _labelFace1OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.WestCopy(1)).SideOpaque[BlockFacing.WEST.Index];
                            _labelFace2OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.EastCopy(1)).SideOpaque[BlockFacing.EAST.Index];
                        }
                    }
                    else
                    {
                        _labelFace1OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.NorthCopy(1)).SideOpaque[BlockFacing.NORTH.Index];
                        _labelFace2OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.SouthCopy(1)).SideOpaque[BlockFacing.SOUTH.Index];
                    }
                }
                else
                {
                    _labelFace1OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.SouthCopy(1)).SideOpaque[BlockFacing.SOUTH.Index];
                    _labelFace2OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.NorthCopy(1)).SideOpaque[BlockFacing.NORTH.Index];
                }
                a = _vOrient!;
                if (!(a == "center"))
                {
                    if (!(a == "up"))
                    {
                        if (a == "down")
                        {
                            _labelFace1OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.DownCopy(1)).SideOpaque[BlockFacing.UP.Index];
                            _labelFace2OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.UpCopy(1)).SideOpaque[BlockFacing.DOWN.Index];
                        }
                    }
                    else
                    {
                        _labelFace1OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.UpCopy(1)).SideOpaque[BlockFacing.DOWN.Index];
                        _labelFace2OppositeIsOpaque = this.Api.World.BlockAccessor.GetBlock(this.Pos.DownCopy(1)).SideOpaque[BlockFacing.UP.Index];
                    }
                }
                if (_labelRenderer1 != null)
                {
                    _labelRenderer1.ShouldDraw = !_labelFace1OppositeIsOpaque.Value;
                }
                if ( false && _labelRenderer2 != null)
                {
                    _labelRenderer2.ShouldDraw = !_labelFace2OppositeIsOpaque.Value;
                }
            }
        }

        public TextureAtlasPosition? this[string textureCode]
        {
            get
            {
                if (_tesselatingSpecial.GetValueOrDefault(false))
                {
                    if (textureCode == "material")
                    {
                        return _tmpTextureSource?[_curMat!];
                    }
                    if (textureCode == "material-deco")
                    {
                        return _tmpTextureSource?["deco-" + _curMat];
                    }
                    if (textureCode == "lining")
                    {
                        if (_curLining == "plain")
                        {
                            return _tmpTextureSource?[_curMat!];
                        }
                        return _tmpTextureSource?[_curLining!];
                    }
                    else
                    {
                        if (textureCode == "glass")
                        {
                            return _glassTextureSource?["material"];
                        }
                        return _tmpTextureSource?[textureCode];
                    }
                }
                else
                {
                    ItemStack? storedItemStack = GetStoredItemStack();
                    if (storedItemStack != null && storedItemStack.Block != null && textureCode == "painting")
                    {
                        return _capi.Tesselator.GetTextureSource(storedItemStack.Block, 0, false)[textureCode];
                    }
                    AssetLocation? assetLocation = null;
                    IAsset? asset = null;
                    CompositeTexture? compositeTexture;
                    if (_tesselatingModBlock.GetValueOrDefault(false))
                    {
                        string key = textureCode;
                        string? stacktype = storedItemStack?.Attributes.GetString("type", null);
                        if (stacktype != null)
                        {
                            textureCode = stacktype + "-" + textureCode;
                        }
                        if (storedItemStack != null && (storedItemStack.Block?.Textures.TryGetValue(textureCode, out compositeTexture) ?? false))
                        {
                            assetLocation = compositeTexture.Baked.BakedName;
                            TextureAtlasPosition textureAtlasPosition = _capi.BlockTextureAtlas[assetLocation];
                            if (textureAtlasPosition != null)
                            {
                                return textureAtlasPosition;
                            }
                        }
                        else if (storedItemStack?.Block?.Textures.TryGetValue(key, out compositeTexture) ?? false)
                        {
                            assetLocation = compositeTexture.Baked.BakedName;
                            TextureAtlasPosition textureAtlasPosition = _capi.BlockTextureAtlas[assetLocation];
                            if (textureAtlasPosition != null)
                            {
                                return textureAtlasPosition;
                            }
                        }
                    }
                    if (storedItemStack != null && storedItemStack.Class == EnumItemClass.Item && storedItemStack.Item.Textures.TryGetValue(textureCode, out compositeTexture))
                    {
                        assetLocation = compositeTexture.Baked.BakedName;
                        if (assetLocation.GetName().Equals("clearquartz"))
                        {
                            assetLocation = new AssetLocation("game:item/resource/ungraded/quartz");
                        }
                        TextureAtlasPosition textureAtlasPosition = _capi.BlockTextureAtlas[assetLocation];
                        if (textureAtlasPosition != null)
                        {
                            return textureAtlasPosition;
                        }
                        asset = _capi.Assets.TryGet(assetLocation.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"), true);
                        if (asset != null)
                        {
                            AssetLocation path = new AssetLocation();
                            if (asset.Location != null && asset.Location.FirstPathPart(0) == "textures")
                            {
                                path = new AssetLocation(asset.Location.ToString().Replace("textures/", ""));
                            }
                            int num;
                            _capi.BlockTextureAtlas.GetOrInsertTexture(path, out num, out textureAtlasPosition, null, 0f);
                            return textureAtlasPosition;
                        }
                    }
                    if (assetLocation == null && _shapeTextures != null)
                    {
                        _shapeTextures.TryGetValue(textureCode, out assetLocation);
                    }
                    if (assetLocation != null)
                    {
                        TextureAtlasPosition textureAtlasPosition = _capi.BlockTextureAtlas[assetLocation];
                        if (textureAtlasPosition == null)
                        {
                            asset = _capi.Assets.TryGet(assetLocation.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"), true);
                            if (asset != null)
                            {
                                AssetLocation path2 = new AssetLocation();
                                if (asset.Location != null && asset.Location.FirstPathPart(0) == "textures")
                                {
                                    path2 = new AssetLocation(asset.Location.ToString().Replace("textures/", ""));
                                }
                                int num;
                                _capi.BlockTextureAtlas.GetOrInsertTexture(path2, out num, out textureAtlasPosition, null, 0f);
                            }
                        }
                        return textureAtlasPosition;
                    }
                    if (storedItemStack != null && storedItemStack.Class == EnumItemClass.Item)
                    {
                        assetLocation = storedItemStack.Item.FirstTexture.Base;
                        TextureAtlasPosition textureAtlasPosition = _capi.BlockTextureAtlas[assetLocation];
                        if (textureAtlasPosition != null)
                        {
                            return textureAtlasPosition;
                        }
                        if (_tesselatingTextureShape.GetValueOrDefault(false))
                        {
                            assetLocation = storedItemStack.Item.FirstTexture.Base;
                        }
                        if (assetLocation != null)
                        {
                            asset = _capi.Assets.TryGet(assetLocation.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"), true);
                        }
                        if (asset != null)
                        {
                            AssetLocation path3 = new AssetLocation();
                            if (asset.Location != null && asset.Location.FirstPathPart(0) == "textures")
                            {
                                path3 = new AssetLocation(asset.Location.ToString().Replace("textures/", ""));
                            }
                            int num;
                            _capi.BlockTextureAtlas.GetOrInsertTexture(path3, out num, out textureAtlasPosition, null, 0f);
                            return textureAtlasPosition;
                        }
                    }
                    return _storedItemTextureSource?[textureCode];
                }
            }
        }

        public void TranslateMesh(MeshData mesh)
        {
            if (mesh == null)
            {
                return;
            }
            ItemStack? storedItemStack = GetStoredItemStack();
            if (storedItemStack == null)
            {
                return;
            }
            ModelTransform guiTransform;
            if (storedItemStack.Class == EnumItemClass.Item)
            {
                guiTransform = storedItemStack.Item.GuiTransform;
            }
            else
            {
                guiTransform = storedItemStack.Block.GuiTransform;
            }
            float[] array = Mat4f.Create();
            Mat4f.Identity(array);
            Vec3f vec3f = new Vec3f();
            vec3f.X = 0.25f * guiTransform.ScaleXYZ.X;
            vec3f.Y = 0.25f * guiTransform.ScaleXYZ.Y;
            vec3f.Z = 0.25f * guiTransform.ScaleXYZ.Z;
            float num = guiTransform.Rotation.X * 0.017453292f;
            if (storedItemStack.Class == EnumItemClass.Item)
            {
                num += 3.1415927f;
            }
            Mat4f.Scale(array, array, new float[]
            {
                1f,
                1f,
                -1f
            });
            Mat4f.RotateX(array, array, num);
            Mat4f.RotateY(array, array, 0.017453292f * guiTransform.Rotation.Y);
            Mat4f.RotateZ(array, array, 0.017453292f * guiTransform.Rotation.Z);
            Mat4f.Scale(array, array, new float[]
            {
                vec3f.X,
                vec3f.Y,
                vec3f.Z
            });
            Mat4f.Translate(array, array, -guiTransform.Origin.X, -guiTransform.Origin.Y, -guiTransform.Origin.Z);
            mesh.MatrixTransform(array);
            mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 1f, 1f, 0.005f);
            mesh.Translate(0.5f, 0.565f, 0.51f);
            mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), _labelRot1!.X, _labelRot1!.Y + 3.1415927f, -_labelRot1!.Z);
        }

        private void UpdateMesh()
        {
            ItemStack? storedItemStack = GetStoredItemStack();
            int num = -1;
            if (storedItemStack != null)
            {
                num = storedItemStack.Id;
            }
            if (num == _previousItemStackID)
            {
                return; // bounce if we don't need to rebuild the mesh
            }
            _previousItemStackID = num;
            MeshData? meshData = GenMeshData(_capi.Tesselator);
            MeshData? meshData2 = null;
            if (meshData != null)
            {
                meshData2 = meshData.Clone();
            }
            if (meshData2 != null)
            {
                TranslateMesh(meshData2);
                this.UpdateXYZFaces(meshData2);
                _mainMeshData1 = meshData2;                
            }
            this.MarkDirty(true, null);
        }

        private void UpdateXYZFaces(MeshData m1)
        {
            byte b = 0;
            string a = _vOrient!;
            if (!(a == "up"))
            {
                if (!(a == "down"))
                {
                    if (a == "center")
                    {
                        string a2 = _hOrient!;
                        if (!(a2 == "north"))
                        {
                            if (!(a2 == "south"))
                            {
                                if (!(a2 == "east"))
                                {
                                    if (a2 == "west")
                                    {
                                        b = (byte)(BlockFacing.EAST.Index + 1);
                                    }
                                }
                                else
                                {
                                    b = (byte)(BlockFacing.WEST.Index + 1);
                                }
                            }
                            else
                            {
                                b = (byte)(BlockFacing.NORTH.Index + 1);
                            }
                        }
                        else
                        {
                            b = (byte)(BlockFacing.SOUTH.Index + 1);
                        }
                    }
                }
                else
                {
                    b = (byte)(BlockFacing.DOWN.Index + 1);
                }
            }
            else
            {
                b = (byte)(BlockFacing.UP.Index + 1);
            }
            if (b > 0)
            {
                for (int i = 0; i < m1.XyzFaces.Length; i++)
                {
                    m1.XyzFaces[i] = b;
                }
            }
        }

        public void UpdateMeshAndLabelRenderer()
        {
            if (this.Api == null || this.Api.Side == EnumAppSide.Server)
            {
                return;
            }            
            
            if (_inventory[0].Empty && _lockedToStack == null)
            {
                _mainMeshData1 = null;
                _mainMeshData2 = null;
                _previousItemStackID = -1;
                if (_labelRenderer1 != null)
                {
                    _labelRenderer1.SetNewTextAndRotation(string.Empty, ColorUtil.BlackArgb, _labelRot1!);
                    _labelRenderer1._drawLockIcon = false;
                }
                //if (this.twoSided && this.labelRenderer2 != null)
                //{
                //    this.labelRenderer2.SetNewTextAndRotation(string.Empty, ColorUtil.BlackArgb, this.labelRotation2);
                //    this.labelRenderer2.DrawLockIcon = false;
                //}
                return;
            }
            this.UpdateMesh();
            string text = GetInventoryCount().ToString();
            if (_labelRenderer1 != null)
            {
                _labelRenderer1.SetNewTextAndRotation(_labelEnabled ? text : string.Empty, ColorUtil.ToRgba(255, 0, 0, 0), _labelRot1!);
                _labelRenderer1._drawLockIcon = _locked;
            }
            else
            {
                _labelRenderer1 = new DrawerLabelRenderer(this, this.Pos, _capi);
                _labelRenderer1.SetNewTextAndRotation(_labelEnabled ? text : string.Empty, ColorUtil.ToRgba(255, 0, 0, 0), _labelRot1!);
                _labelRenderer1._drawLockIcon = _locked;
                this.NeighborBlockChanged();
            }
            //if (this.twoSided)
            //{
            //    if (this.labelRenderer2 != null)
            //    {
            //        this.labelRenderer2.SetNewTextAndRotation(this.text, ColorUtil.ToRgba(255, 0, 0, 0), this.labelRotation2);
            //        this.labelRenderer2.DrawLockIcon = !this.lockedItemInventory.Empty;
            //        return;
            //    }
            //    this.labelRenderer2 = new BetterCrateLabelRender(this, this.Pos, this.cApi);
            //    this.labelRenderer2.SetNewTextAndRotation(this.text, ColorUtil.ToRgba(255, 0, 0, 0), this.labelRotation2);
            //    this.labelRenderer2.DrawLockIcon = !this.lockedItemInventory.Empty;
            //    this.NeighborBlockChanged();
            //}
        }

        public Size2i? AtlasSize => _capi.BlockTextureAtlas.Size;

        public override InventoryBase Inventory => _inventory;

        public override string InventoryClassName => "onedrawer";

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("islocked", Locked);
            if (LockedToStack != null)
            {
                tree.SetItemstack("lockedstack", LockedToStack);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {            
            if (this._inventory == null) InitializeInventory(worldForResolving);
            base.FromTreeAttributes(tree, worldForResolving);
            _locked = tree.GetBool("islocked");
            if (_locked)
            {
                _lockedToStack = tree.GetItemstack("lockedstack");
            }
            if (Api != null && worldForResolving.Side == EnumAppSide.Client)
            {
                UpdateMeshAndLabelRenderer();
                MarkDirty(true, null);
            }
        }
    }
}
