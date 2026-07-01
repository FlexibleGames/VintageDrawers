## Vintage Drawers Design Doc ##
# Summary: #
Inspired by the Storage Drawers mod for MC this mod sets out to give VS players a similar experience.
Optionally if commissioned, a powered controller interface can be added using VE power to allow for a crafting
interface that can directly pull from the drawers.

# Basic Interaction: #
Each drawer will have a set amount of stacks of items they can hold. (16 Stacks per drawer)
Each drawer will have a set amount of upgrade slots for capacity and features. (8 upgrade slots)

Do we need a 2x1 or 2x2 drawer, or will the normal full drawer be enough?

# Drawer Upgrades: #
Void Upgrade
Capacity Tier Upgrades - Bronze, Brass, Iron, Meteoric Iron, Steel, Gold, Titanium, Diamond
Other Features?

# Drawer Controller: #
Only one allowed per drawer system.
Finds and Connects to all drawers within a X block radius and allows for placing player inventory items
into all connected drawers via double-shift-right-click. Allows for automation insertion via inventory
GetAutoInsertSlot override.

# Drawer Interaction: #
Right Click - Store Hand contents if it matches and there is room
Left Click - Retrieve one item
Shift-Left Click - Retrieve one stack
Shift-Right Click - Store hand contents and all matching items from inventory into drawer.
Allows for automation insertion via inventory GetAutoInsertSlot override.

# Misc Features: #
Drawer Trim - Extends Drawer Controller detection of drawers without adding any storage.
Drawer IO - Allows for insertion of items into entire drawer system. Extraction of a specific item
    based on extraction filter (VE Pipes).

# Drawer Design: #
Drawer visuals will have wood typed variants.
Trim visuals will also have wood typed variants.
Controller and IO will have a more industrial design.

## Technical: ##
Each Drawer will only have one slot for storing the actual item. This slot will have a dynamic
MaxStackSize based on what (if any) upgrades are in the drawer. This will save a huge amount of
data as the drawer will not have to send hundreds or even thousands of slot inventories.
Upgrade slots will have to lock their contents if Storage Tiers are used and current storage exceeds
the amount if upgrade was removed.

Drawer Controller will track block position and type as well as monitor and update as needed. Will NOT
store map to disk or share with clients. This is a server-only interaction to avoid exploits. This map will
be rebuilt when controller loads from disk or is placed.

Drawer IO will pass all interaction to the controller, so a controller will be required to use the IO block.

# Mod Details #
Mod commissioned by Robert