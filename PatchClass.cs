﻿using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using static ACE.Server.WorldObjects.Player;

namespace CustomClothingBase;

[HarmonyPatch]
public partial class PatchClass
{
    #region Settings
    const int RETRIES = 10;

    public static Settings Settings = new();
    static string settingsPath => Path.Combine(Mod.ModPath, "Settings.json");
    private FileInfo settingsInfo = new(settingsPath);

    //Property key names that will be serialized as hex strings
    static HashSet<string> hexKeys = new()
    {
        "ClothingBaseEffects",
        "PaletteSet",
        "ModelId",
        "Id",
    };

    private static JsonSerializerOptions _serializeOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        Converters = {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            //More involved to handle Dictionary
            new HexKeyDictionaryConverter<uint, ClothingBaseEffect>(),
            new HexKeyDictionaryConverter<uint, ClothingBaseEffectEx>(),
            },
        TypeInfoResolver = new HexTypeResolver(hexKeys),
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private void SaveSettings()
    {
        string jsonString = JsonSerializer.Serialize(Settings, _serializeOptions);

        if (!settingsInfo.RetryWrite(jsonString, RETRIES))
        {
            ModManager.Log($"Failed to save settings to {settingsPath}...", ModManager.LogLevel.Warn);
            Mod.State = ModState.Error;
        }
    }

    private void LoadSettings()
    {
        if (!settingsInfo.Exists)
        {
            ModManager.Log($"Creating {settingsInfo}...");
            SaveSettings();
        }
        else
            ModManager.Log($"Loading settings from {settingsPath}...");

        if (!settingsInfo.RetryRead(out string jsonString, RETRIES))
        {
            Mod.State = ModState.Error;
            return;
        }

        try
        {
            Settings = JsonSerializer.Deserialize<Settings>(jsonString, _serializeOptions);
        }
        catch (Exception)
        {
            ModManager.Log($"Failed to deserialize Settings: {settingsPath}", ModManager.LogLevel.Warn);
            Mod.State = ModState.Error;
            return;
        }
    }
    #endregion

    #region Start/Shutdown
    public void Start()
    {
        //Need to decide on async use
        Mod.State = ModState.Loading;
        LoadSettings();

        if (Mod.State == ModState.Error)
        {
            ModManager.DisableModByPath(Mod.ModPath);
            return;
        }

        if (Settings.WatchContent)
        {
            Directory.CreateDirectory(ContentDir);
            _contentWatcher = new(ContentDir);
            ModManager.Log($"Watching ClothingBase changes in:\n{ContentDir}");
        }

        Mod.State = ModState.Running;
    }

    public void Shutdown()
    {
        _contentWatcher?.Dispose();

        if (Settings.ClearCacheOnShutdown)
            ClearClothingCache();

        if (Mod.State == ModState.Error)
            ModManager.Log($"Improper shutdown: {Mod.ModPath}", ModManager.LogLevel.Error);
    }
    #endregion

    static JsonFileWatcher _contentWatcher;
    static string ModDir => ModManager.GetModContainerByName("CustomClothingBase").FolderPath;
    static string StubDir => Path.Combine(ModDir, "stub");
    static string ContentDir => Path.Combine(ModDir, "json");
    public static string GetFilename(uint fileId) => Path.Combine(ContentDir, $"0x{fileId:X}.json");
    public static bool JsonFileExists(uint fileId) => File.Exists(GetFilename(fileId));

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
            ModManager.Log($"Error creating CustomClothingTable stub for {fileId}", ModManager.LogLevel.Error);
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
                //string jsonString = File.ReadAllText(fileName);
                var clothingTable = JsonSerializer.Deserialize<ClothingTableEx>(jsonString, _serializeOptions).Convert();
                return clothingTable;
            }
            catch (Exception E)
            {
                ModManager.Log(E.GetFullMessage(), ModManager.LogLevel.Error);
                return null;
            }
        }
        return null;
    }

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

    [CommandHandler("export-clothing", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Exports ClothingBase entry to a JSON file in the CustomClothingBase mod folder.")]
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
    #endregion

    private static void ExportClothingBase(uint clothingBaseId)
    {
        //DatManager.PortalDat.ReadFromDat<Palet>
        string exportFilename = GetFilename(clothingBaseId);
        var cbToExport = DatManager.PortalDat.ReadFromDat<ClothingTable>(clothingBaseId);

        // make sure the mod/json folder exists -- if not, create it
        string path = Path.GetDirectoryName(exportFilename);
        Directory.CreateDirectory(path);

        try
        {
            var json = JsonSerializer.Serialize(cbToExport, _serializeOptions);
            File.WriteAllText(exportFilename, json);
            ModManager.Log($"Saved to {exportFilename}");
        }
        catch (Exception E)
        {
            ModManager.Log(E.GetFullMessage(), ModManager.LogLevel.Error);
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

        ModManager.Log($"Removed {count} ClothingTable entires from FileCache");
    }
}

public static class SelectionExtensions
{
    public static bool TryGetCurrentSelection(this Player player, out WorldObject wo, SearchLocations locations = SearchLocations.Everywhere)
    {
        wo = null;

        if (player is null)
            return false;

        //Try to find selected object ID
        var objectId = ObjectGuid.Invalid;
        if (player.HealthQueryTarget.HasValue)
            objectId = new ObjectGuid(player.HealthQueryTarget.Value);
        else if (player.ManaQueryTarget.HasValue)
            objectId = new ObjectGuid(player.ManaQueryTarget.Value);
        else if (player.CurrentAppraisalTarget.HasValue)
            objectId = new ObjectGuid(player.CurrentAppraisalTarget.Value);

        if (objectId == ObjectGuid.Invalid)
            return false;

#if REALM
        wo = player.FindObject(objectId, locations);
#else
        wo = player.FindObject(objectId.Full, locations);
#endif

        return wo is not null;
    }
}