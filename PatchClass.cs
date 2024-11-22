using ACE.DatLoader.FileTypes;
using HarmonyLib;
using JsonNet.ContractResolvers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IO;

namespace CustomClothingBase
{   
    [HarmonyPatch]
    public class PatchClass
    {
        #region Settings
        const int RETRIES = 10;

        public static Settings Settings = new();
        static string settingsPath => Path.Combine(Mod.ModPath, "Settings.json");
        private FileInfo settingsInfo = new(settingsPath);

        private JsonSerializerOptions _serializeOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private void SaveSettings()
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(Settings, _serializeOptions);

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
                Settings = System.Text.Json.JsonSerializer.Deserialize<Settings>(jsonString, _serializeOptions);
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

            // Init our Serialize Settings
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };

            Mod.State = ModState.Running;
        }

        public void Shutdown()
        {
            //if (Mod.State == ModState.Running)
            // Shut down enabled mod...

            //If the mod is making changes that need to be saved use this and only manually edit settings when the patch is not active.
            //SaveSettings();

            if (Mod.State == ModState.Error)
                ModManager.Log($"Improper shutdown: {Mod.ModPath}", ModManager.LogLevel.Error);
        }
        #endregion

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
            if (cb != null){
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
            else if(__instance.Header.DataSet == DatDatabaseType.Portal && fileId > 0x10000000 && fileId <= 0x10FFFFFF)
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
            string directory = ModManager.GetModContainerByName("CustomClothingBase").FolderPath;
            string stubDirectory = Path.Combine(directory, "stub");

            // Create stub directory if it doesn't already exist
            if (!Directory.Exists(stubDirectory))
                Directory.CreateDirectory(stubDirectory);

            string stubFilename = Path.Combine(stubDirectory, $"{fileId:X8}.bin");

            // If this already exists, no need to recreate
            if(File.Exists(stubFilename))
                return true;

            try
            {
                using (FileStream fs = new FileStream(stubFilename, FileMode.Create))
                {
                    using (BinaryWriter writer = new BinaryWriter(fs))
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

        private static string getFilename(uint fileId)
        {
            string directory = ModManager.GetModContainerByName("CustomClothingBase").FolderPath;
            string jsonFilename = Path.Combine(directory, "json", $"{fileId:X8}.json");
            return jsonFilename;
        }
        private static bool JsonFileExists(uint fileId)
        {
            if (File.Exists(getFilename(fileId)))
            {
                return true;
            }

            return false;
        }
        
        private static ClothingTable? GetJsonClothing(uint fileId) {
            if (JsonFileExists(fileId)) {
                string jsonString = File.ReadAllText(getFilename(fileId));
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        ContractResolver = new PrivateSetterContractResolver()
                    };
                    var clothingTable = JsonConvert.DeserializeObject<ClothingTable>(jsonString, settings);

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
            uint count = 0;
            foreach(var e in DatManager.PortalDat.FileCache)
            {
                if (e.Key > 0x10000000 && e.Key <= 0x10FFFFFF) 
                {
                    DatManager.PortalDat.FileCache.TryRemove(e);
                    count++;
                }
            }
            ModManager.Log($"Removed {count} ClothingTable entires from FileCache");
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

            string exportFilename = getFilename(clothingBaseId);
            var cbToExport = DatManager.PortalDat.ReadFromDat<ClothingTable>(clothingBaseId);

            // make sure the mod/json folder exists -- if not, create it
            string path = Path.GetDirectoryName(exportFilename);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            
            var json = JsonConvert.SerializeObject(cbToExport);

            File.WriteAllText(exportFilename, json);
            ModManager.Log($"Saved to {exportFilename}");
        }
        #endregion
    }

}
