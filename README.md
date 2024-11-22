# CustomClothingBase
A mod for ACE, Asheron's Call Emulator, that allows for customized colors and other options through modifying the ClothingTable on the server.

## Installation and Usage

Place compiled files, or [extract Release](https://github.com/OptimShi/CustomClothingBase/releases) in the CustomClothingBase folder in your ACE Mods folder, typically `C:\ACE\Mods\CustomClothingBase\`

If it does not already exist, create a `json` (`C:\ACE\Mods\CustomClothingBase\json\`) folder within the CustomClothingBase folder. Place all of your custom clothing base JSON files in this folder. 

If a ClothingTable entry already exists with the Id you are using, it will attempt to merge your modifications into this (or replace entirely if appropriate). If it's a new entry, it will be added as such and made available to your server. Additioanlly, if a new ClothingTable entry is added a corresponding *bin* folder will be placed in the `CustomClothingBase/stub/` folder. These stub files are almost empty ClothingTable entries with just a ClothingBaseID to trick the server into loading them properly so we can modify them.

*Note that due to how the ClothingTable and the ObjDesc system works, players do not need to download any new files or dat updates for these work. These are completely server side and will be invisble to users except for the new looks!*

## Commands

The following commands are part of the mod.

> `@clothingbase-export` - *Exports a ClothingBase entry to a JSON file in the CustomClothingBase mod folder.*  
**Usage:** `clothingbase-export <ClothingBaseID>`, e.g. `clothingbase-export 0x10000001` *(hex value)* or `clothingbase-export 268435457` *(decimal value)*


> `@clear-clothing-cache` - *Clears the ClothingTable file cache.*  
This will force the ClothingTable to reload; useful to allow you to make changes on the fly while testing or without reloading the server.

## Examples

Please see the [Examples folder](https://github.com/OptimShi/CustomClothingBase/tree/master/Examples) some ideas and examples on how to use this mod.

## Miscellaneous

For reference, these are all the different player parts. Note the Tail, Shoulder, Knee, and Elbow parts (17 - 20, 23 - 28) that are rarely -- if ever -- actually used. Have some fun with these!

| Part | Name|
| ---- | ---- |
| 0 | HUMAN_ABDOMEN |
| 1 | HUMAN_LEFT_UPPER_LEG |
| 2 | HUMAN_LEFT_LOWER_LEG |
| 3 | HUMAN_LEFT_FOOT |
| 4 | HUMAN_LEFT_TOE |
| 5 | HUMAN_RIGHT_UPPER_LEG |
| 6 | HUMAN_RIGHT_LOWER_LEG |
| 7 | HUMAN_RIGHT_FOOT |
| 8 | HUMAN_RIGHT_TOE |
| 9 | HUMAN_CHEST |
| 10 | HUMAN_LEFT_UPPER_ARM |
| 11 | HUMAN_LEFT_LOWER_ARM |
| 12 | HUMAN_LEFT_HAND |
| 13 | HUMAN_RIGHT_UPPER_ARM |
| 14 | HUMAN_RIGHT_LOWER_ARM |
| 15 | HUMAN_RIGHT_HAND |
| 16 | HUMAN_HEAD |
| 17 | HUMAN_TAIL_SEG1 |
| 18 | HUMAN_TAIL_SEG2 |
| 19 | HUMAN_TAIL_SEG3 |
| 20 | HUMAN_TAIL_SEG4 |
| 21 | HUMAN_HEAD_HAIR |
| 22 | HUMAN_HEAD_HELMET |
| 23 | HUMAN_SHOULDER_LEFT |
| 24 | HUMAN_SHOULDER_RIGHT |
| 25 | HUMAN_KNEE_LEFT |
| 26 | HUMAN_KNEE_RIGHT |
| 27 | HUMAN_ELBOW_LEFT |
| 28 | HUMAN_ELBOW_RIGHT |
| 29 | HUMAN_CLOAK_SEG1 |
| 30 | HUMAN_CLOAK_SEG2 |
| 31 | HUMAN_CLOAK_SEG3 |
| 32 | HUMAN_CLOAK_SEG4 |
| 33 | HUMAN_CLOAK_SEG5 |

## Feedback

Please use or the issues here or [join the ACEmulator Discord](https://discord.com/invite/Q4N4NP3J)