using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageDrawers
{
    public class DrawerGUI: GuiDialogBlockEntity
    {
        private DrawerBE _beDrawer;

        public DrawerGUI(string title, InventoryBase inventory, BlockPos pos, ICoreClientAPI capi, DrawerBE drawerBE) : base(title, inventory, pos, capi)
        {
            _beDrawer = drawerBE;
            if (base.IsDuplicate) return;

            capi.World.Player.InventoryManager.OpenInventory(inventory);
            SetupDialog();
        }

        private void OnSlotModified(int slotid)
        {
            capi.Event.EnqueueMainThreadTask(new Action(SetupDialog), "setupdrawerdialog");
        }

        public void SetupDialog()
        {
            ItemSlot? hoveredslot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredslot != null && hoveredslot.Inventory == base.Inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredslot);
            }
            else hoveredslot = null;

            int titlebarheight = 31;
            double slotpadding = GuiElementItemSlotGridBase.unscaledSlotPadding;

            ElementBounds dialogbounds = ElementBounds.Fixed(244, 222 + titlebarheight);
            ElementBounds dialog = ElementBounds.Fill.WithFixedPadding(0);
            dialog.BothSizing = ElementSizing.FitToChildren;

            ElementBounds bulkSlot = ElementStdBounds.SlotGrid(EnumDialogArea.None, 98, 12 + titlebarheight, 1, 1);
            ElementBounds bulkText = ElementBounds.Fixed(8, 64 + titlebarheight, 228, 14);

            ElementBounds upgradeText = ElementBounds.Fixed(8, 85 + titlebarheight, 228, 14);
            ElementBounds upgradeInset = ElementBounds.Fixed(8, 103 + titlebarheight, 228, 114);

            ElementBounds upgradeSlots = ElementStdBounds.SlotGrid(EnumDialogArea.None, 12, 107 + titlebarheight, 4, 2);

            dialog.WithChildren(new ElementBounds[]
            { 
                dialogbounds,
                bulkSlot,
                bulkText,
                upgradeText,
                upgradeInset,
                upgradeSlots
            });

            ElementBounds window = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            if (capi.Settings.Bool["immersiveMouseMode"])
            {
                window.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-12, 0);
            }
            else
            {
                window.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(20, 0);
            }
            BlockPos l_pos = base.BlockEntityPosition;

            CairoFont centerwhite = CairoFont.WhiteDetailText().WithWeight(Cairo.FontWeight.Normal).WithOrientation(EnumTextOrientation.Center);
            CairoFont leftwhite = centerwhite.Clone().WithOrientation(EnumTextOrientation.Left);

            this.SingleComposer = capi.Gui.CreateCompo("onedrawerdlg" + l_pos.ToString(), window)
                .AddShadedDialogBG(dialog, true, 5)
                .AddDialogTitleBar(Lang.Get("vintagedrawers:gui-onedrawer"), new Action(OnTitleBarClosed), null, null)
                .BeginChildElements(dialog)

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 1, [0], bulkSlot, "bulkslot")
                .AddDynamicText(GetSlotText(), centerwhite, bulkText, "bulktext")

                .AddStaticText(Lang.Get("vintagedrawers:upgrades"), leftwhite, upgradeText, "upgradetext")
                .AddInset(upgradeInset, 2, 0f)

                .AddItemSlotGrid(Inventory, new Action<object>(SendInvPacket), 4, [1, 2, 3, 4, 5, 6, 7, 8], upgradeSlots, "upgradeslots")
                .EndChildElements()
                .Compose(true);
        }

        public void Update()
        {
            if (!IsOpened()) return;
            if (base.SingleComposer != null)
            {
                SingleComposer.GetDynamicText("bulktext").SetNewText(GetSlotText());
            }
        }

        public void OnTitleBarClosed()
        {
            this.TryClose();
        }

        public string GetSlotText()
        {
            string output = string.Empty;
            if (Inventory[0].Empty) output += "0";
            else output += Inventory[0].Itemstack!.StackSize;
            output += " / ";
            output += Inventory[0].MaxSlotStackSize;
            return output;
        }

        private void SendInvPacket(object obj)
        {
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, obj);
        }
    }
}
