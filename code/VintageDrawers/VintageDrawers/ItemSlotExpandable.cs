using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

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

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            if (Inventory != null && Inventory.PutLocked) return false;
            ItemStack source = sourceSlot.Itemstack;
            return source != null &&
                ((source.Collectible.GetStorageFlags(source) & this.StorageType) > (EnumItemStorageFlags)0 &&
                    (itemstack == null || GetMergableQuantity(itemstack, source, priority) > 0)) &&
                GetRemainingSlotSpace(source) > 0;
        }

        public virtual int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (sinkStack.Collectible.Equals(sourceStack, sinkStack, GlobalConstants.IgnoredStackAttributes)
                && sinkStack.StackSize < MaxSlotStackSize)
            {
                return Math.Min(MaxSlotStackSize - sinkStack.StackSize, sourceStack.StackSize);
            }
            return 0;
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
            if (this.Empty) return forItemstack.Collectible.MaxStackSize * _numSlots;
            return (this.itemstack.Collectible.MaxStackSize * _numSlots) - this.itemstack.StackSize;
        }

        public override ItemStack? TakeOut(int quantity)
        {
            if (this.itemstack == null) return null;
            if (quantity >= itemstack.StackSize) return TakeOutWhole();
            ItemStack emptyClone = itemstack.GetEmptyClone();
            emptyClone.StackSize = quantity;
            itemstack.StackSize -= quantity;
            if (itemstack.StackSize <= 0) itemstack = null;
            return emptyClone;
        }

        public override ItemStack TakeOutWhole()
        {
            return base.TakeOutWhole();
        }
        public override bool TryFlipWith(ItemSlot itemSlot)
        {
            return base.TryFlipWith(itemSlot);
        }

        /// <summary>
        /// Attempts to TAKE items from fromSlot into this slot.
        /// </summary>
        /// <param name="fromSlot">Source to PULL from.</param>
        /// <param name="op">Move Operation</param>
        /// <returns>Quantity Moved</returns>
        public virtual int TryTakeFrom(ItemSlot fromSlot, ref ItemStackMoveOperation op)
        {
            if (!CanTakeFrom(fromSlot, EnumMergePriority.AutoMerge) || !fromSlot.CanTake() || fromSlot.Itemstack == null) return 0;
            if (!Inventory.CanContain(this, fromSlot)) return 0;
            if (this.itemstack == null)
            {
                int quant = Math.Min(GetRemainingSlotSpace(fromSlot.Itemstack), op.RequestedQuantity);
                if (quant > 0)
                {
                    this.itemstack = fromSlot.TakeOut(quant);
                    op.MovedQuantity = (op.MovableQuantity = Math.Min(this.StackSize, quant));
                    this.OnItemSlotModified(itemstack);
                    fromSlot.OnItemSlotModified(itemstack);
                }
                return op.MovedQuantity;
            }
            ItemStackMergeOperation mergeop = op.ToMergeOperation(this, fromSlot);
            op = mergeop;
            int origquant = op.RequestedQuantity;
            op.RequestedQuantity = Math.Min(GetRemainingSlotSpace(itemstack), op.RequestedQuantity);
            TryMergeStacks(mergeop);
            if (mergeop.MovedQuantity > 0)
            {
                OnItemSlotModified(itemstack);
                fromSlot.OnItemSlotModified(itemstack);
            }
            op.RequestedQuantity = origquant;
            return mergeop.MovedQuantity;
        }

        public virtual int TryTakeFrom(IWorldAccessor world, ItemSlot fromSlot, int quantity = 1)
        {
            ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, quantity);
            return TryTakeFrom(fromSlot, ref op);
        }
        
        public virtual void TryMergeStacks(ItemStackMergeOperation op)
        {
            // will ignore collectable MaxStackSize and rely on the slots MaxStackSize instead
            op.MovableQuantity = this.GetMergableQuantity(op.SinkSlot.Itemstack, op.SourceSlot.Itemstack, op.CurrentPriority);
            CollectibleObject sinkobj = op.SinkSlot.Itemstack.Collectible;
            if (op.MovableQuantity == 0)
            {
                return;
            }
            if (!op.SinkSlot.CanTakeFrom(op.SourceSlot, op.CurrentPriority))
            {
                return;
            }
            bool doTemperatureAveraging = false;
            bool doTransitionAveraging = false;
            op.MovedQuantity = GameMath.Min(new int[]
            {
                op.SinkSlot.GetRemainingSlotSpace(op.SourceSlot.Itemstack),
                op.MovableQuantity,
                op.RequestedQuantity
            });
            if (sinkobj.HasTemperature(op.SinkSlot.Itemstack) || sinkobj.HasTemperature(op.SourceSlot.Itemstack))
            {
                if (op.CurrentPriority < EnumMergePriority.DirectMerge && Math.Abs(sinkobj.GetTemperature(op.World, op.SinkSlot.Itemstack) - sinkobj.GetTemperature(op.World, op.SourceSlot.Itemstack)) > 30f)
                {
                    op.MovedQuantity = 0;
                    op.MovableQuantity = 0;
                    op.RequiredPriority = new EnumMergePriority?(EnumMergePriority.DirectMerge);
                    return;
                }
                doTemperatureAveraging = true;
            }
            TransitionState[] sourceTransitionStates = sinkobj.UpdateAndGetTransitionStates(op.World, op.SourceSlot);
            TransitionState[] targetTransitionStates = sinkobj.UpdateAndGetTransitionStates(op.World, op.SinkSlot);
            Dictionary<EnumTransitionType, TransitionState> targetStatesByType = null;
            if (sourceTransitionStates != null)
            {
                bool canDirectStack = true;
                bool canAutoStack = true;
                if (targetTransitionStates == null)
                {
                    op.MovedQuantity = 0;
                    op.MovableQuantity = 0;
                    return;
                }
                targetStatesByType = new Dictionary<EnumTransitionType, TransitionState>();
                foreach (TransitionState state in targetTransitionStates)
                {
                    targetStatesByType[state.Props.Type] = state;
                }
                foreach (TransitionState sourceState in sourceTransitionStates)
                {
                    TransitionState targetState = null;
                    if (!targetStatesByType.TryGetValue(sourceState.Props.Type, out targetState))
                    {
                        canAutoStack = false;
                        canDirectStack = false;
                        break;
                    }
                    if (Math.Abs(targetState.TransitionedHours - sourceState.TransitionedHours) > 4f && Math.Abs(targetState.TransitionedHours - sourceState.TransitionedHours) / sourceState.FreshHours > 0.03f)
                    {
                        canAutoStack = false;
                    }
                }
                if (!canAutoStack && op.CurrentPriority < EnumMergePriority.DirectMerge)
                {
                    op.MovedQuantity = 0;
                    op.MovableQuantity = 0;
                    op.RequiredPriority = new EnumMergePriority?(EnumMergePriority.DirectMerge);
                    return;
                }
                if (!canDirectStack)
                {
                    op.MovedQuantity = 0;
                    op.MovableQuantity = 0;
                    return;
                }
                doTransitionAveraging = true;
            }
            if (op.SourceSlot.Itemstack == null)
            {
                op.MovedQuantity = 0;
                return;
            }
            if (op.MovedQuantity <= 0)
            {
                return;
            }
            if (op.SinkSlot.Itemstack == null)
            {
                op.SinkSlot.Itemstack = new ItemStack(op.SourceSlot.Itemstack.Collectible, 0);
            }
            if (doTemperatureAveraging)
            {
                sinkobj.SetTemperature(op.World, op.SinkSlot.Itemstack, ((float)op.SinkSlot.StackSize * sinkobj.GetTemperature(op.World, op.SinkSlot.Itemstack) + (float)op.MovedQuantity * sinkobj.GetTemperature(op.World, op.SourceSlot.Itemstack)) / (float)(op.SinkSlot.StackSize + op.MovedQuantity), true);
            }
            if (doTransitionAveraging)
            {
                float t = (float)op.MovedQuantity / (float)(op.MovedQuantity + op.SinkSlot.StackSize);
                foreach (TransitionState sourceState2 in sourceTransitionStates)
                {
                    TransitionState targetState2 = targetStatesByType[sourceState2.Props.Type];
                    sinkobj.SetTransitionState(op.SinkSlot.Itemstack, sourceState2.Props.Type, sourceState2.TransitionedHours * t + targetState2.TransitionedHours * (1f - t));
                }
            }
            op.SinkSlot.Itemstack.StackSize += op.MovedQuantity;
            op.SourceSlot.Itemstack.StackSize -= op.MovedQuantity;
            if (op.SourceSlot.Itemstack.StackSize <= 0)
            {
                op.SourceSlot.Itemstack = null;
            }
        }

        public override int TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
        {
            if (!sinkSlot.CanTakeFrom(this, EnumMergePriority.AutoMerge) || !this.CanTake() || this.itemstack == null)
            {
                return 0;
            }
            InventoryBase inventoryBase = sinkSlot.Inventory;
            if (inventoryBase != null && !inventoryBase.CanContain(sinkSlot, this))
            {
                return 0;
            }
            if (sinkSlot.Itemstack == null)
            {
                int q = Math.Min(sinkSlot.GetRemainingSlotSpace(this.itemstack), op.RequestedQuantity);
                if (q > 0)
                {
                    sinkSlot.Itemstack = this.TakeOut(q);
                    op.MovedQuantity = (op.MovableQuantity = Math.Min(sinkSlot.StackSize, q));
                    sinkSlot.OnItemSlotModified(sinkSlot.Itemstack);
                    this.OnItemSlotModified(sinkSlot.Itemstack);
                }
                return op.MovedQuantity;
            }
            ItemStackMergeOperation mergeop = op.ToMergeOperation(sinkSlot, this);
            op = mergeop;
            int origRequestedQuantity = op.RequestedQuantity;
            op.RequestedQuantity = Math.Min(sinkSlot.GetRemainingSlotSpace(this.itemstack), op.RequestedQuantity);
            sinkSlot.Itemstack.Collectible.TryMergeStacks(mergeop);
            if (mergeop.MovedQuantity > 0)
            {
                sinkSlot.OnItemSlotModified(sinkSlot.Itemstack);
                this.OnItemSlotModified(sinkSlot.Itemstack);
            }
            op.RequestedQuantity = origRequestedQuantity;
            return mergeop.MovedQuantity;
        }

        public override int TryPutInto(IWorldAccessor world, ItemSlot sinkSlot, int quantity = 1)
        {
            ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Left, (EnumModifierKey)0,
                EnumMergePriority.AutoMerge, quantity);
            return TryPutInto(sinkSlot, ref op);
        }

    }
}
