using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageDrawers
{
    public class DrawerControllerBE : BlockEntity
    {
        private List<BlockPos> connectedDrawers = new List<BlockPos>();
        private long lastDiscoveryMs = 0;
        private const int DiscoveryCooldownMs = 2000;

        public int MaxRadius { get; set; } = 16;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                DiscoverNetwork();
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            connectedDrawers.Clear();
        }

        public void InvalidateNetwork()
        {
            connectedDrawers.Clear();
            lastDiscoveryMs = 0;
        }

        public void DiscoverNetwork()
        {
            long now = Api.World.ElapsedMilliseconds;
            if (now - lastDiscoveryMs < DiscoveryCooldownMs && connectedDrawers.Count > 0)
            {
                return;
            }

            connectedDrawers.Clear();
            HashSet<BlockPos> visited = new HashSet<BlockPos>();
            Queue<BlockPos> queue = new Queue<BlockPos>();

            queue.Enqueue(Pos);
            visited.Add(Pos);

            while (queue.Count > 0)
            {
                BlockPos current = queue.Dequeue();
                double distance = current.DistanceTo(Pos);

                if (distance > MaxRadius)
                {
                    continue;
                }

                BlockFacing[] faces = BlockFacing.ALLFACES;
                for (int i = 0; i < faces.Length; i++)
                {
                    BlockFacing face = faces[i];
                    BlockPos neighborPos = current.AddCopy(face);

                    if (visited.Contains(neighborPos))
                    {
                        continue;
                    }

                    Block block = Api.World.BlockAccessor.GetBlock(neighborPos);
                    if (block == null)
                    {
                        continue;
                    }

                    bool isDrawer = block.Code.Path.Contains("drawer") &&
                                    !block.Code.Path.Contains("trim") &&
                                    !block.Code.Path.Contains("controller");

                    bool isTrim = IsDrawerTrim(block);

                    if (isDrawer || isTrim)
                    {
                        visited.Add(neighborPos);

                        if (isDrawer)
                        {
                            DrawerBE drawerBE = Api.World.BlockAccessor.GetBlockEntity(neighborPos) as DrawerBE;
                            if (drawerBE != null)
                            {
                                connectedDrawers.Add(neighborPos);
                            }
                        }

                        if (distance < MaxRadius)
                        {
                            queue.Enqueue(neighborPos);
                        }
                    }
                }
            }

            lastDiscoveryMs = now;
        }

        private bool IsDrawerTrim(Block block)
        {            
            return block.Code.Path.Contains("drawertrim");
        }

        /// <summary>
        /// Smart insertion. Prefers drawers that already contain the item type.
        /// </summary>
        public bool TryInsertIntoNetwork(ItemStack stack)
        {
            if (stack == null || stack.StackSize <= 0)
            {
                return false;
            }

            DiscoverNetwork();

            // Priority 1: Drawers already containing this exact locked item
            for (int i = 0; i < connectedDrawers.Count; i++)
            {
                BlockPos pos = connectedDrawers[i];
                DrawerBE? drawer = Api.World.BlockAccessor.GetBlockEntity(pos) as DrawerBE;
                if (drawer != null && drawer.Locked && drawer.LockedToStack != null &&
                    drawer.LockedToStack.Equals(Api.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    if (drawer.TryPutFromController(stack))
                    {
                        return true;
                    }
                }
            }

            // Priority 2: Drawers that already have this item type
            for (int i = 0; i < connectedDrawers.Count; i++)
            {
                BlockPos pos = connectedDrawers[i];
                DrawerBE? drawer = Api.World.BlockAccessor.GetBlockEntity(pos) as DrawerBE;
                if (drawer != null && drawer.HasItemType(stack) && drawer.CanAccept(stack))
                {
                    if (drawer.TryPutFromController(stack))
                    {
                        return true;
                    }
                }
            }

            // Priority 3: Any drawer with space
            for (int i = 0; i < connectedDrawers.Count; i++)
            {
                BlockPos pos = connectedDrawers[i];
                DrawerBE? drawer = Api.World.BlockAccessor.GetBlockEntity(pos) as DrawerBE;
                if (drawer != null && drawer.CanAccept(stack))
                {
                    if (drawer.TryPutFromController(stack))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}