namespace CustomClothingBase;

public class Settings
{
    public bool WatchContent { get; set; } = true;
    public bool ClearCacheOnShutdown { get; set; } = true;

    // Property key names that will be serialized as hex strings
    public HashSet<string> HexKeys { get; set; } = new()
    {
        "ClothingBaseEffect",
        "PaletteSet",
        "ModelId",
        "Id",
        "OldTexture",
        "NewTexture",
    };
}