using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VintageDrawers
{
    public static class ApiExtensions
    {        
        public static T LoadOrCreateConfig<T>(this ICoreAPI api, string filename) where T : new()
        {
            try
            {
                T tconfig = api.LoadModConfig<T>(filename);
                if (tconfig != null)
                {
                    return tconfig;
                }
            }
            catch (Exception value)
            {
                ILogger logger = api.World.Logger;
                string format = "{0}";
                object[] array = new object[1];
                int num = 0;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(55, 2);
                defaultInterpolatedStringHandler.AppendLiteral("Failed loading file (");
                defaultInterpolatedStringHandler.AppendFormatted(filename);
                defaultInterpolatedStringHandler.AppendLiteral("), error ");
                defaultInterpolatedStringHandler.AppendFormatted<Exception>(value);
                defaultInterpolatedStringHandler.AppendLiteral(". Will initialize new one");
                array[num] = defaultInterpolatedStringHandler.ToStringAndClear();
                logger.Error(format, array);
            }
            T tconfig2 = Activator.CreateInstance<T>();
            api.StoreModConfig<T>(tconfig2, filename);
            return tconfig2;
        }
    }

    public class VintageDrawersModSystem : ModSystem
    {

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            Mod.Logger.Notification("Assembling Drawers: " + api.Side);
            RegisterClasses(api);
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            if (api.Side == EnumAppSide.Client)
            {
                DrawerConfig.Current = api.LoadOrCreateConfig<DrawerConfig>("DrawerConfig.json");
            }
            // an else would be for any server-specific configs
        }

        private void RegisterClasses(ICoreAPI api)
        {
            api.RegisterBlockClass("DrawerBlock", typeof(DrawerBlock));
            api.RegisterBlockEntityClass("DrawerBE", typeof(DrawerBE));
            api.RegisterItemClass("DrawerUpgrade", typeof(DrawerUpgrade));
            api.RegisterBlockClass("DrawerControllerBlock", typeof(DrawerControllerBlock));
            api.RegisterBlockEntityClass("DrawerControllerBE", typeof(DrawerControllerBE));
        }
    }
}
