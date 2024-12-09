using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

//ClothingTable
//  ClothingBaseEffectEx (Dic)
//      CloObjectEffect (List)
//          Index
//          ModelId
//          CloTextureEffect (List)
//              OldTexture
//              NewTexture
//  CloSubPalEffectEx (Dic)
//      Icon
//      CloSubPalette (List)
//          CloSubPaletteRange (List)
//              Offset
//              NumColors
public class ClothingTableEx : ClothingTable
{
    public Dictionary<uint, ClothingBaseEffectEx> ClothingBaseEffects { get; set; } = new();
    public Dictionary<uint, CloSubPalEffectEx> ClothingSubPalEffects { get; set; } = new();

    public ClothingTable Convert()
    {
        ClothingTable value = new();
        foreach (var cbe in ClothingBaseEffects)
            value.ClothingBaseEffects.Add( cbe.Key, cbe.Value.Convert());

        foreach (var cbe in ClothingSubPalEffects)
            value.ClothingSubPalEffects.Add(cbe.Key, cbe.Value.Convert());

        return value;
    }
}

public class CloSubPalEffectEx : CloSubPalEffect
{
    public uint Icon { get; set; }
    public List<CloSubPaletteEx> CloSubPalettes { get; set; } = new();

    public CloSubPalEffect Convert()
    {
        CloSubPalEffect value = new();
        value.Icon = Icon;
        value.CloSubPalettes.AddRange(CloSubPalettes.ConvertAll(x => x.Convert()));
        return value;
    }
}

public class CloSubPaletteEx : CloSubPalette
{
    public List<CloSubPaletteRange> Ranges { get; set; } = new();
    public uint PaletteSet { get; set; }

    public CloSubPalette Convert()
    {
        CloSubPalette value = new();
        value.PaletteSet = PaletteSet;
        value.Ranges.AddRange(Ranges);
        return value;
    }
}

public class ClothingBaseEffectEx : ClothingBaseEffect
{
    public List<CloObjectEffectExt> CloObjectEffects { get; set; } = new();

    public ClothingBaseEffect Convert()
    {
        ClothingBaseEffect value = new();
        var converted = CloObjectEffects.ConvertAll(x => x.Convert());
        value.CloObjectEffects.AddRange(CloObjectEffects.ConvertAll(x => x.Convert()));
        return value;
    }
}

public class CloObjectEffectExt : CloObjectEffect
{
    public uint Index { get; set; }
    public uint ModelId { get; set; }
    public List<CloTextureEffectEx> CloTextureEffects { get; set; } = new();

    public CloObjectEffect Convert()
    {
        CloObjectEffect value = new();
        value.Index = Index;
        value.ModelId = ModelId;
        value.CloTextureEffects.AddRange(CloTextureEffects);
        return value;
    }
}

public class CloTextureEffectEx : CloTextureEffect { }