using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageDrawers
{
    public class DrawerControllerBlock : Block
    {
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
            DrawerControllerBE? be = world.BlockAccessor.GetBlockEntity(blockPos) as DrawerControllerBE;
            if (be != null)
            {
                be.DiscoverNetwork();
            }
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            DrawerControllerBE? be = world.BlockAccessor.GetBlockEntity(pos) as DrawerControllerBE;
            if (be != null)
            {
                be.InvalidateNetwork();
            }

            base.OnBlockRemoved(world, pos);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);
            DrawerControllerBE? be = world.BlockAccessor.GetBlockEntity(pos) as DrawerControllerBE;
            if (be != null)
            {
                be.InvalidateNetwork();
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {                
            if (byPlayer == null || byPlayer.InventoryManager == null)
            {
                return false;
            }

            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot == null || activeSlot.Empty)
            {
                // Empty hand - could be used later for manual network refresh
                return false;
            }

            ItemStack heldStack = activeSlot.Itemstack;
            if (heldStack == null)
            {
                return false;
            }

            DrawerControllerBE? controllerBE = world.BlockAccessor.GetBlockEntity(blockSel.Position) as DrawerControllerBE;
            if (controllerBE == null)
            {
                return false;
            }

            bool inserted = controllerBE.TryInsertIntoNetwork(heldStack.Clone());
            if (inserted)
            {
                activeSlot.TakeOut(1);
                activeSlot.MarkDirty();

                if (world.Side == EnumAppSide.Server)
                {
                    world.PlaySoundAt(new AssetLocation("game:sounds/player/buildhigh"), blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5, null, 0.8f, 1.1f);
                }

                return true;
            }
            return false;
        }
    }
}