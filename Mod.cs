using ACE.Shared.Mods;

namespace CustomClothingBase;
public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(CustomClothingBase), new PatchClass(this));
}