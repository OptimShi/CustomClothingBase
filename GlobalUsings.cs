//extern alias Json;
global using ACE.Common.Extensions;
global using ACE.DatLoader;
global using ACE.Entity.Enum;

global using ACE.Server.Command;
global using ACE.Server.Mods;
#if REALM
global using Session = ACE.Server.Network.ISession;
#endif

global using HarmonyLib;
global using System.Text.Encodings.Web;
global using System.Text.Json;
global using System.Text.Json.Serialization;

