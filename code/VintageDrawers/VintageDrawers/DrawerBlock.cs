using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageDrawers
{
    public class DrawerBlock : Block
    {
        public DrawerBlock() { }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            if (this.api == null) return string.Empty;
            DrawerBE drawerentity = world.BlockAccessor.GetBlockEntity<DrawerBE>(pos);
            if (drawerentity == null) return string.Empty;
            string output = string.Empty;
            if (!drawerentity.Inventory[0].Empty || drawerentity.LockedToStack != null)
            {
                output += $"{Lang.Get("contents")}: {drawerentity.GetInventoryCount()} / {drawerentity.GetStoredMaxStackSize()} {drawerentity.GetStoredItemStack()?.GetName()}" + Environment.NewLine;
            }
            ItemStack? drawerstack = drawerentity.GetStoredItemStack();
            if (drawerstack != null && drawerstack.Item != null && drawerstack.Item.Durability > 0)
            {
                output += $"{Lang.Get("durability")}: {drawerstack.Collectible.GetRemainingDurability(drawerstack)} / {drawerstack.Collectible.GetMaxDurability(drawerstack)}";
            }
            foreach (BlockBehavior bbeh in this.BlockBehaviors)
            {
                output += bbeh.GetPlacedBlockInfo(world, pos, forPlayer);
            }
            return output;
        }
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return true;
            }
            if (world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>(true).IsLockedForInteract(blockSel.Position, byPlayer))
            {
                if (world.Side == EnumAppSide.Client)
                {
                    ((ICoreClientAPI)world.Api).TriggerIngameError(this, "locked", Lang.Get("ingameerror-locked", Array.Empty<object>()));
                }
                return true;
            }
            if (this.CheckForUpgradeItem(byPlayer, blockSel.Position))
            {
                return true;
            }
            BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (blockEntity is DrawerBE)
            {
                ((DrawerBE)blockEntity).OnPlayerInteract(byPlayer);
            }
            return true;
        }

        public bool CheckForUpgradeItem(IPlayer byPlayer, BlockPos pos)
        {
            if (!byPlayer.Entity.Controls.Sneak)
            {
                return false;
            }
            ItemSlot activeslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            return activeslot != null && !activeslot.Empty && activeslot.Itemstack.Class == EnumItemClass.Item
                && TryToUpgrade(byPlayer, pos);
        }

        public bool TryToUpgrade(IPlayer byPlayer, BlockPos pos)
        {
            DrawerBE? drawerentity = api.World.BlockAccessor.GetBlockEntity(pos) as DrawerBE;
            if (drawerentity == null) return false;
            ItemSlot activeslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeslot == null || activeslot.Empty || !activeslot.Itemstack.Item.Code.Path.Contains("drawerupgrade"))
            {
                return false;
            }
            return drawerentity.UpdateInventory(byPlayer);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!this.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }
            BlockFacing[] array = Block.SuggestedHVOrientation(byPlayer, blockSel);
            string vert = "center";
            if (array[1] == BlockFacing.UP)
            {
                vert = "up";
            }
            else if (array[1] == BlockFacing.DOWN)
            {
                vert = "down";
            }
            AssetLocation code = base.CodeWithVariants(new string[]
            {
                "vertical",
                "horizontal"
            }, new string[]
            {
                vert,
                array[0].Code
            });
            Block block = world.BlockAccessor.GetBlock(code);
            if (block == null)
            {
                return false;
            }
            world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);
            return true;
        }
        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlock(pos) != null)
            {
                string[] components = new string[]
                {
                    "center",
                    "north"
                };
                AssetLocation code = base.CodeWithParts(components);
                return new ItemStack(world.BlockAccessor.GetBlock(code), 1);
            }
            return new ItemStack(world.BlockAccessor.GetBlock(pos), 1);
        }
        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (!itemslot.Empty && itemslot.Itemstack.Class == EnumItemClass.Item)
            {
                EnumTool? tool = itemslot.Itemstack.Item.Tool;
                EnumTool enumTool = EnumTool.Axe;
                if (tool.GetValueOrDefault() == enumTool & tool != null)
                {
                    return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
                }
            }
            BlockEntity blockEntity = player.Entity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (blockEntity != null && blockEntity is DrawerBE _drawer)
            {                
                if (counter % 5 == 0)
                {
                    _drawer.OnPlayerLeftClick(player);
                }
            }
            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
                return;
            }
            EnumTool? activeTool = byPlayer.InventoryManager.ActiveTool;
            EnumTool enumTool = EnumTool.Axe;
            if (activeTool.GetValueOrDefault() == enumTool & activeTool != null)
            {
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
                return;
            }
            if (world.Side == EnumAppSide.Client)
            {
                BlockEntity blockEntity = byPlayer.Entity.World.BlockAccessor.GetBlockEntity(pos);
                if (blockEntity != null && blockEntity is DrawerBE _drawer)
                {
                    _drawer.OnPlayerLeftClick(byPlayer);
                }
            }
        }
        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (world.Side == EnumAppSide.Client)
            {
                BlockEntity be = this.api.World.BlockAccessor.GetBlockEntity(pos);
                if (be != null && be is DrawerBE _drawer)
                {
                    _drawer.NeighborBlockChanged();
                }
            }
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }
    }
}
