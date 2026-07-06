using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;

namespace VintageDrawers
{
    /// <summary>
    /// Special ItemSlot that can contain far more than a single stack of items.
    /// </summary>
    public class ItemSlotExpandable : ItemSlot
    {
        private int _baseNumSlots = 32;
        private int _numSlots = 32;

        /// <summary>
        /// Number of slots this slot is able to hold.<br/>
        /// This is modified by the upgrades installed as apart of the given inventory.
        /// </summary>
        public int NumSlots => _numSlots;
        public ItemSlotExpandable(InventoryBase inventory) : base(inventory)
        {
            _baseNumSlots = 32;
            _numSlots = 32;
        }

        public ItemSlotExpandable(InventoryBase inventory, int basenumslots) : base(inventory)
        {
            _baseNumSlots = basenumslots;
            int upgradedslots = StackUpgradeCheck(inventory);
            _numSlots = upgradedslots;
            if (this.Empty)
            {
                this.MaxSlotStackSize = 64 * upgradedslots;
            }
            else
            {
                int contentstacksize = this.itemstack.Collectible.MaxStackSize;
                this.MaxSlotStackSize = contentstacksize * upgradedslots;
            }
        }

        public void UpdateCapacity()
        {
            int newnumstacks = StackUpgradeCheck(this.inventory);
            if (newnumstacks != _numSlots)
            {
                _numSlots = newnumstacks;
                if (this.Empty)
                {
                    this.MaxSlotStackSize = 64 * _numSlots;
                }
                else
                {
                    int contentstacksize = this.itemstack.Collectible.MaxStackSize;
                    this.MaxSlotStackSize = contentstacksize * _numSlots;
                }
            }
        }

        /// <summary>
        /// Checks the inventory upgrade slots for any stack upgrades and returns the number of stacks this slot can hold.<br/>
        /// Formula is: value = Base Num * (foreach upgrade (Base Num * Upgrade Mul))<br/>
        /// So when using the maximum number of diamond upgrades = 32 * (32 * 8 * 128) = 1,048,576 stacks
        /// </summary>
        /// <param name="p_inventory">Inventory, slot ids 1 - 8 need to be the upgrade slots.</param>
        /// <returns>Number of slots this slot can hold.</returns>
        public int StackUpgradeCheck(InventoryBase p_inventory)
        {
            int stackextra = 0;
            if (p_inventory == null) return -1;
            if (p_inventory.Count != 9) return -1;

            for (int x = 1; x < 9; x++)
            {
                if (p_inventory[x].Empty) continue;
                else
                {
                    if (p_inventory[x].Itemstack?.Collectible.Attributes["stack"].AsBool(false) == true)
                    {
                        stackextra += _baseNumSlots * p_inventory[x].Itemstack?.Collectible.Attributes["mul"].AsInt(1) ?? 1;
                    }
                }
            }
            if (stackextra == 0) stackextra = 1;
            // Max number of stacks is thusly 32 (base) * (32 * 8 * 128) = 1048576 stacks with all 8 diamond stack upgrades installed
            // If the drawer item has a stack size of 64, that would be 67,108,864 items in one drawer.
            return stackextra * _baseNumSlots;
        }

        public override int GetRemainingSlotSpace(ItemStack forItemstack)
        {
            if (this.Empty) return 64 * _numSlots;
            return (this.itemstack.Collectible.MaxStackSize * _numSlots) - this.itemstack.StackSize;
        }
    }
}
