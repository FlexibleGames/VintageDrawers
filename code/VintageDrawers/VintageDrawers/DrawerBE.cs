using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageDrawers
{
    public class DrawerBE : BlockEntityContainer, ITexPositionSource
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
        private MeshData _meshData;

        private static readonly int _packetClientLeftClick = 6444;
        private static readonly int _packetLockedError = 6555;
        private static readonly int _packetPutAll = 6666;

        private bool _locked = false;
        /// <summary>
        /// Is this Drawer locked to a specific collectable type?
        /// </summary>
        public bool Locked => _locked;
        [AllowNull]
        private ItemStack _lockedToStack;
        /// <summary>
        /// If Locked == true, this is the ItemStack that it is locked too.
        /// </summary>
        public ItemStack LockedToStack => _lockedToStack;

        // represents the base number of slots this type of drawer can hold of a single item
        // storage of items is actually in a single slot
        private int _baseNumSlots = 32;

        public DrawerBE()
        {
            this._meshData = new MeshData(true);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Client)
            {
                _capi = api as ICoreClientAPI;
            }
            else
            {
                _sapi = api as ICoreServerAPI;
            }
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

        private void OnSlotModified(int id)
        {
            if (Api.World.BlockAccessor.GetChunkAtBlockPos(Pos) != null)
            {
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
            }
            MarkDirty(false);
        }

        private ItemSlot? GetAutoPushIntoSlot(BlockFacing atFace, ItemSlot fromSlot)
        {
            if (!IsAllowed(fromSlot))
            {
                return null;
            }
            if (_inventory[0].Empty) return _inventory[0];
            return _inventory.GetBestSuitedSlot(fromSlot, null, null).slot;
        }

        private ItemSlot? GetAutoPullFromSlot(BlockFacing atFace)
        {
            if (_inventory[0].Empty) return null;
            return _inventory[0];
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

        public TextureAtlasPosition? this[string textureCode] => throw new NotImplementedException();

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
                // mesh/label stuff
                MarkDirty(true, null);
            }
        }
    }
}
