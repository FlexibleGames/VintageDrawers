using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageDrawers
{
    public class DrawerBlock : Block
    {
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
    }
}
