using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageDrawers
{
    public class DrawerControllerBlock : Block
    {

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            StringBuilder sb = new StringBuilder();

            DrawerControllerBE? be = world.BlockAccessor.GetBlockEntity(pos) as DrawerControllerBE;
            if (be == null)
            {
                return "Controller BE is null?";
            }

            // Refresh the network if it's empty (cheap due to internal cooldown)
            if (be.ConnectedDrawerCount == 0)
            {
                be.DiscoverNetwork();
            }
            
            sb.AppendLine($"{Lang.Get("Connected drawers")}: {be.ConnectedDrawerCount}");
            sb.AppendLine($"{Lang.Get("Search radius")}: {be.MaxRadius} {Lang.Get("blocks")}");
            sb.AppendLine($"{Lang.Get("Last scan")}: {(world.ElapsedMilliseconds - be.LastDiscoveryTime) / 1000} sec");

            if (be.ConnectedDrawerCount == 0)
            {
                sb.AppendLine("No drawers connected/found.");
            }

            return sb.ToString();
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack? byItemStack = null)
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
            if (activeSlot == null)
            {
                return false;
            }

            DrawerControllerBE? controllerBE = world.BlockAccessor.GetBlockEntity(blockSel.Position) as DrawerControllerBE;
            if (controllerBE == null)
            {
                return false;
            }

            long now = world.ElapsedMilliseconds;
            bool isDoubleClick = now - controllerBE._lastInteractTime < 500;
            controllerBE._lastInteractTime = now;

            // Player has an item in hand -> insert the whole stack
            if (!activeSlot.Empty && activeSlot.Itemstack != null)
            {
                int hadnum = activeSlot.Itemstack.StackSize;
                bool success = controllerBE.TryInsertStack(activeSlot);
                int inserted = hadnum - (activeSlot.Empty ? hadnum : activeSlot.Itemstack.StackSize);

                if (success && inserted > 0)
                {                    
                    //if (inserted < hadnum) activeSlot.TakeOut(inserted); // not needed, the TryPut takes things out already
                    activeSlot.MarkDirty();

                    if (world.Side == EnumAppSide.Server)
                    {
                        world.PlaySoundAt(new AssetLocation("game:sounds/player/buildhigh"),
                            blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                            null, 0.8f, 1.1f);
                    }
                }
                return success;
            }

            // Case 2: Empty hand + double click → mass insert from player's inventory
            if (activeSlot.Empty && isDoubleClick && world.Side == EnumAppSide.Server)
            {
                bool anythingInserted = controllerBE.TryPutPlayerInventory(byPlayer);
                //if (anythingInserted)
                //{
                //    // send a packet if you want client feedback later
                //}
                return true;
            }

            return false;
        }
    }
}