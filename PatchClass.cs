using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;
using CustomClothingBase.JsonConverters;
using System.Diagnostics;
using System.Text;
using static ACE.Server.WorldObjects.Player;

namespace CustomClothingBase;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static JsonSerializerOptions _jsonSettings;

    static JsonFileWatcher _contentWatcher;
    static string ModDir => ModManager.GetModContainerByName("CustomClothingBase").FolderPath;
    static string StubDir => Path.Combine(ModDir, "stub");
    static string ContentDir => Path.Combine(ModDir, "json");
    public static string GetFilename(uint fileId) => Path.Combine(ContentDir, $"{fileId:X}.json");
    public static bool JsonFileExists(uint fileId) => File.Exists(GetFilename(fileId));

    public override Task OnWorldOpen()
    {
        Settings = SettingsContainer.Settings;

        _jsonSettings = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString,
        };
        _jsonSettings.Converters.Add(new HexUintJsonConverter());
        _jsonSettings.Converters.Add(new HexKeyDictionaryConverter<uint, ClothingBaseEffect>());
        _jsonSettings.Converters.Add(new HexKeyDictionaryConverter<uint, ClothingBaseEffectEx>());
              
        if (Settings.WatchContent)
        {
            Directory.CreateDirectory(ContentDir);
            _contentWatcher = new(ContentDir);
            ModManager.Log($"CustomClothingBase: Watching ClothingBase changes in:\n{ContentDir}");
        }

        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        _contentWatcher?.Dispose();

        if (Settings.ClearCacheOnShutdown)
            ClearClothingCache();

        base.Stop();
    }

    #region Patches
    /// <summary>
    /// ClothingTable.Unpack. We're going to go ahead and add our custom information into this.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="__instance"></param>
    /// 
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClothingTable), nameof(ClothingTable.Unpack), new Type[] { typeof(BinaryReader) })]
    public static void PostUnpack(BinaryReader reader, ref ClothingTable __instance)
    {
        ClothingTable? cb = GetJsonClothing(__instance.Id);
        if (cb != null)
        {
            __instance = MergeClothingTable(__instance, cb);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DatDatabase), nameof(DatDatabase.GetReaderForFile), new Type[] { typeof(uint) })]
    public static bool PreGetReaderForFile(uint fileId, ref DatDatabase __instance, ref DatReader __result)
    {
        // File already exists in the dat file -- proceed to let it run normally
        if (__instance.AllFiles.ContainsKey(fileId))
            return true;
        else if (__instance.Header.DataSet == DatDatabaseType.Portal && fileId > 0x10000000 && fileId <= 0x10FFFFFF)
        {
            // We're trying to load a ClothingTable entry that does not exist in the Portal.dat. Does it exist as JSON?
            if (JsonFileExists(fileId) && createStubClothingBase(fileId))
            {
                string directory = ModManager.GetModContainerByName("CustomClothingBase").FolderPath;
                string stubFilename = Path.Combine(directory, "stub", $"{fileId:X8}.bin");

                // Load our stub into the DatReader
                __result = new DatReader(stubFilename, 0, 12, 16);

                // No need to run the original function -- we've cheated it with the result!
                return false;
            }
        }
        //Return true to execute original
        return true;
    }

    /// <summary>
    /// Override the Unpack of a PaletteSet. This is needed to adjust for the fact that it might actually be a Palette entry that we have manually defined in our CustomClothingBase
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="__instance"></param>
    /// <returns></returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PaletteSet), nameof(PaletteSet.Unpack), new Type[] { typeof(BinaryReader) })]
    public static bool PreUnpack(BinaryReader reader, ref PaletteSet __instance)
    {
        // Check if this is REALLY a Palette in disguise!
        uint id = reader.ReadUInt32();
        if (id >= 0x04000000 && id <= 0x04FFFFFF)
        {
            ModManager.Log($"CustomClothingBase: Loading Palette {id:X8} as a PaletteSet");
            __instance.Id = id;
            __instance.PaletteList.Add(id);
            //Return false to override
            return false;
        }

        // Reset our position to the start
        reader.BaseStream.Position = 0;

        //Return true to execute original
        return true;
    }


    #endregion

    /// <summary>
    /// Inserts cb2 contents into cb. If a ClothingBaseEffect or ClothingSubPalEffects exists, it will overwrite it.
    /// </summary>
    /// <param name="cb"></param>
    /// <param name="cb2"></param>
    /// <returns></returns>
    private static ClothingTable MergeClothingTable(ClothingTable cb, ClothingTable cb2)
    {
        foreach (var cbe in cb2.ClothingBaseEffects)
        {
            if (cb.ClothingBaseEffects.ContainsKey(cbe.Key))
                cb.ClothingBaseEffects[cbe.Key] = cbe.Value;
            else
                cb.ClothingBaseEffects.Add(cbe.Key, cbe.Value);
        }
        foreach (var csp in cb2.ClothingSubPalEffects)
        {
            if (cb.ClothingSubPalEffects.ContainsKey(csp.Key))
                cb.ClothingSubPalEffects[csp.Key] = csp.Value;
            else
                cb.ClothingSubPalEffects.Add(csp.Key, csp.Value);
        }
        return cb;
    }

    /// <summary>
    /// Clears the DatManager.PortalDat.FileCache of all ClothingTable entries, allowing us to pull any new or updated custom data
    /// </summary>
    /// <param name="session"></param>
    /// <param name="parameters"></param>
    [CommandHandler("clear-clothing-cache", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Clears the ClothingTable file cache.")]
    public static void HandleClearConsoleCache(Session session, params string[] parameters)
    {
        ClearClothingCache();
    }

    /// <summary>
    /// Exports a ClothingBase entry to a JSON file
    /// </summary>
    /// <param name="session"></param>
    /// <param name="parameters"></param>
    [CommandHandler("clothingbase-export", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Exports a ClothingBase entry to a JSON file in the CustomClothingBase mod folder.")]
    public static void HandleExportClothing(Session session, params string[] parameters)
    {
        string syntax = "clothingbase-export <id>";
        if (parameters == null || parameters?.Length < 1)
        {
            ModManager.Log(syntax);
            return;
        }

        uint clothingBaseId;
        if (parameters[0].StartsWith("0x"))
        {
            string hex = parameters[0].Substring(2);
            if (!uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out clothingBaseId))
            {
                ModManager.Log(syntax);
                return;
            }
        }
        else
        if (!uint.TryParse(parameters[0], out clothingBaseId))
        {
            ModManager.Log(syntax);
            return;
        }

        ExportClothingBase(clothingBaseId);
    }

    /// <summary>
    /// TODO - This will ultimately dump a copy of what
    /// </summary>
    /// <param name="session"></param>
    /// <param name="parameters"></param>
    //[CommandHandler("export-as-clothingbase", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1, "Exports the appearance of a an item to a specific JSON file in the CustomClothingBase mod folder.", "/export-clothing [ClothingBaseEntry]")//]
    public static void HandleExportWeenieClothing(Session session, params string[] parameters)
    {
        if (session?.Player is not Player player)
            return;

        //Selected object approach from /delete
        var objectId = ObjectGuid.Invalid;

        if (session.Player.HealthQueryTarget.HasValue)
            objectId = new ObjectGuid(session.Player.HealthQueryTarget.Value);
        else if (session.Player.ManaQueryTarget.HasValue)
            objectId = new ObjectGuid(session.Player.ManaQueryTarget.Value);
        else if (session.Player.CurrentAppraisalTarget.HasValue)
            objectId = new ObjectGuid(session.Player.CurrentAppraisalTarget.Value);

        if (objectId == ObjectGuid.Invalid)
            ChatPacket.SendServerMessage(session, "Delete failed. Please identify the object you wish to delete first.", ChatMessageType.Broadcast);

        var wo = session.Player.FindObject(objectId.Full, Player.SearchLocations.Everywhere, out _, out Container rootOwner, out bool wasEquipped);
        if (wo is null)
        {
            player.SendMessage($"No object is selected.");
            return;
        }

        if (wo.ClothingBase is null)
        {
            player.SendMessage($"No ClothingBase found for {wo.Name}");
            return;
        }

        ExportClothingBase(wo.ClothingBase.Value);

        player.SendMessage($"Exported ClothingBase {wo.ClothingBase:X} for {wo.Name} to:\n{GetFilename(wo.ClothingBase.Value)}");
    }

    private static bool createStubClothingBase(uint fileId)
    {
        // Create stub directory if it doesn't already exist       
        Directory.CreateDirectory(StubDir);

        string stubFilename = Path.Combine(StubDir, $"{fileId:X8}.bin");

        // If this already exists, no need to recreate
        if (File.Exists(stubFilename))
            return true;

        try
        {
            using (FileStream fs = new FileStream(stubFilename, FileMode.Create))
            {
                using (System.IO.BinaryWriter writer = new(fs))
                {
                    writer.Write((int)0); // NextAddress, since this is a dat "block", this goes first
                    writer.Write(fileId);
                    writer.Write((int)0); // num ClothingBaseEffects
                    writer.Write((int)0); // num ClothingSubPalEffects
                }
            }
        }
        catch (Exception ex)
        {
            ModManager.Log($"CustomClothingBase: Error creating CustomClothingTable stub for {fileId} - {ex.Message}", ModManager.LogLevel.Error);
            return false;
        }

        return true;
    }

    private static ClothingTable? GetJsonClothing(uint fileId)
    {
        var fileName = GetFilename(fileId);
        if (JsonFileExists(fileId))
        {
            try
            {
                //Add retries
                FileInfo file = new(fileName);
                if (!file.RetryRead(out var jsonString, 10))
                    return null;

                var ctx = JsonSerializer.Deserialize<ClothingTableEx>(jsonString, _jsonSettings);
                var clothingTable = ctx.Convert();
                return clothingTable;
            }
            catch (Exception E)
            {
                ModManager.Log("CustomClothingBase: CustomClothingBase.GetJsonClothing - " + E.GetFullMessage(), ModManager.LogLevel.Error);
                return null;
            }
        }
        return null;
    }

    private static void ExportClothingBase(uint clothingBaseId)
    {
        if (clothingBaseId < 0x10000000 || clothingBaseId > 0x10FFFFFF)
        {
            ModManager.Log($"CustomClothingBase: {clothingBaseId:X8} is not a valid ClothingBase between 0x10000000 and 0x10FFFFFF");
            return;
        }

        if (!DatManager.PortalDat.AllFiles.ContainsKey(clothingBaseId))
        {
            ModManager.Log($"CustomClothingBase: ClothingBase {clothingBaseId:X8} not found.");
            return;
        }

        string exportFilename = GetFilename(clothingBaseId);
        var cbToExport = DatManager.PortalDat.ReadFromDat<ClothingTable>(clothingBaseId);

        // make sure the mod/json folder exists -- if not, create it
        string path = Path.GetDirectoryName(exportFilename);
        Directory.CreateDirectory(path);

        try
        {
            var json = JsonSerializer.Serialize(cbToExport, _jsonSettings);
            File.WriteAllText(exportFilename, json);
            ModManager.Log($"CustomClothingBase: Saved to {exportFilename}");
        }
        catch (Exception E)
        {
            ModManager.Log("CustomClothingBase: " + E.GetFullMessage(), ModManager.LogLevel.Error);
        }
    }

    public static void ClearClothingCache()
    {
        uint count = 0;
        foreach (var e in DatManager.PortalDat.FileCache)
        {
            if (e.Key > 0x10000000 && e.Key <= 0x10FFFFFF)
            {
                DatManager.PortalDat.FileCache.TryRemove(e);
                count++;
            }
        }

        if (PlayerManager.GetAllOnline().FirstOrDefault() is not Player player)
            return;

        player.EnqueueBroadcast(new GameMessageObjDescEvent(player));

        ModManager.Log($"CustomClothingBase: Removed {count} ClothingTable entires from FileCache");
    }
}
