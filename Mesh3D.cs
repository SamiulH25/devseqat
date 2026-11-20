using System.Text;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// Compact binary 3D modular mesh kit exported by Blender (`tools/build_meshes_3d.py` -> `Data/meshes/units3d.bin`).
/// Caches all part arrays and Look->Mesh mappings once so per-frame battle rendering allocates ZERO strings or arrays.
sealed class MeshKit3D
{
    public readonly struct Vert
    {
        public readonly Vector3 Pos, Norm;
        public readonly float U, V;
        public readonly byte R, G, B;
        public readonly byte MatId;

        public Vert(Vector3 pos, Vector3 norm, float u, float v, byte r, byte g, byte b, byte matId)
        {
            Pos = pos; Norm = norm; U = u; V = v; R = r; G = g; B = b; MatId = matId;
        }
    }

    /// Surface pattern IDs packed into VertexPNCT Color.A high nibble (read by Shaders/unit3d.glsl).
    public const byte PatSmooth = 0, PatMail = 1, PatScale = 2, PatQuilt = 3, PatBrushed = 4;

    /// Pre-resolved modular mesh references for one `Look` (zero string lookups during rendering!).
    public sealed class LookMeshes
    {
        public required Vert[] Chest, Shoulders, Belt, Skirt, TabardPanel;
        public required Vert[] Head, Helm, UArm, FArm, Thigh, Shin, Weapon, Shield;
        public required bool FullEnclosedHelm, HasCape, HasPlume, HasShield, HasTabard;
        public required byte ChestPat, ShldPat, SkirtPat, LimbPat, LegPat;
        public required byte BeltVarGroup, ShldVarGroup;
        public required string ShieldKind;
        public required bool WeaponEdgeZ, OffHandForward;
        public required float OffHandOffset, BladeReach;
    }

    public readonly Dictionary<string, Vert[]> Parts = new();
    readonly Dictionary<Look, LookMeshes> lookCache = new(ReferenceEqualityComparer.Instance);

    // Pre-cached common, mount, beast, prop, emblem, and LOD parts (zero dictionary lookups in hot loop).
    public Vert[] Plume = Array.Empty<Vert>(), Cape = Array.Empty<Vert>(), Bow = Array.Empty<Vert>();
    public Vert[] Quiver = Array.Empty<Vert>(), Arrow = Array.Empty<Vert>(), Javelin = Array.Empty<Vert>();
    public Vert[] WebSnare = Array.Empty<Vert>(), BannerPole = Array.Empty<Vert>();
    public Vert[][] Emblems = Array.Empty<Vert[]>();
    public Vert[] HorseBody = Array.Empty<Vert>(), HorseBodyBarded = Array.Empty<Vert>(), HorseBodyScale = Array.Empty<Vert>(), HorseBodyFur = Array.Empty<Vert>();
    public Vert[] HorseNeck = Array.Empty<Vert>(), HorseNeckBarded = Array.Empty<Vert>(), HorseNeckScale = Array.Empty<Vert>();
    public Vert[] HorseHead = Array.Empty<Vert>(), HorseHeadBarded = Array.Empty<Vert>(), HorseHeadScale = Array.Empty<Vert>();
    public Vert[] HorseLegUp = Array.Empty<Vert>(), HorseLegLow = Array.Empty<Vert>(), HorseTail = Array.Empty<Vert>();
    public Vert[] CamelBody = Array.Empty<Vert>(), CamelNeck = Array.Empty<Vert>(), CamelHead = Array.Empty<Vert>();
    public Vert[] WolfBody = Array.Empty<Vert>(), WolfHead = Array.Empty<Vert>(), WolfLeg = Array.Empty<Vert>(), WolfTail = Array.Empty<Vert>();
    public Vert[] BearBody = Array.Empty<Vert>(), BearHead = Array.Empty<Vert>(), BearLeg = Array.Empty<Vert>();
    public Vert[] SpiderAbdomen = Array.Empty<Vert>(), SpiderHead = Array.Empty<Vert>(), SpiderLeg = Array.Empty<Vert>();
    public Vert[] WyvernBody = Array.Empty<Vert>(), WyvernNeck = Array.Empty<Vert>(), WyvernHead = Array.Empty<Vert>();
    public Vert[] WyvernWing = Array.Empty<Vert>(), WyvernLeg = Array.Empty<Vert>(), WyvernTail = Array.Empty<Vert>(), WyvernSting = Array.Empty<Vert>();
    public Vert[] LodFoot = Array.Empty<Vert>(), LodHorse = Array.Empty<Vert>();
    // Pre-cached modular belt & shoulder pools for zero-alloc per-soldier variety within regiments!
    public Vert[][] BeltPoolLight = Array.Empty<Vert[]>(), BeltPoolHeavy = Array.Empty<Vert[]>(), BeltPoolNorth = Array.Empty<Vert[]>();
    public Vert[][] ShldPoolKnight = Array.Empty<Vert[]>(), ShldPoolFoot = Array.Empty<Vert[]>();
    public Vert[][] ChestPoolPlate = Array.Empty<Vert[]>(), SkirtPoolPlate = Array.Empty<Vert[]>();

    public static MeshKit3D? TryLoad()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var path = Path.Combine(dir, "Data", "meshes", "units3d.bin");
            if (File.Exists(path)) return Load(path);
        }
        return null;
    }

    public static MeshKit3D Load(string path)
    {
        var kit = new MeshKit3D();
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.UTF8);
        var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
        if (magic != "IMSH") throw new InvalidDataException($"bad mesh pack magic: {magic}");
        uint ver = br.ReadUInt32();
        if (ver != 1) throw new InvalidDataException($"unsupported mesh pack version: {ver}");
        uint partCount = br.ReadUInt32();
        for (int p = 0; p < partCount; p++)
        {
            ushort nlen = br.ReadUInt16();
            string name = Encoding.UTF8.GetString(br.ReadBytes(nlen));
            uint vcount = br.ReadUInt32();
            var verts = new Vert[vcount];
            for (int i = 0; i < vcount; i++)
            {
                float px = br.ReadSingle(), py = br.ReadSingle(), pz = br.ReadSingle();
                float nx = br.ReadSingle(), ny = br.ReadSingle(), nz = br.ReadSingle();
                float mat = br.ReadSingle();
                _ = br.ReadSingle();
                byte r = br.ReadByte(), g = br.ReadByte(), b = br.ReadByte();
                _ = br.ReadByte();
                verts[i] = new Vert(new Vector3(px, py, pz), new Vector3(nx, ny, nz), px, py, r, g, b, (byte)Math.Clamp((int)MathF.Round(mat), 0, 15));
            }
            // Compute seamless per-triangle box-projected object-local (U, V) in meters so procedural armor patterns never swim or stretch.
            for (int i = 0; i + 2 < verts.Length; i += 3)
            {
                var p0 = verts[i].Pos; var p1 = verts[i + 1].Pos; var p2 = verts[i + 2].Pos;
                var fn = Vector3.Cross(p1 - p0, p2 - p0);
                float ax = MathF.Abs(fn.X), ay = MathF.Abs(fn.Y), az = MathF.Abs(fn.Z);
                for (int k = 0; k < 3; k++)
                {
                    ref readonly var v = ref verts[i + k];
                    float u, vv;
                    if (ay >= ax && ay >= az) { u = v.Pos.X; vv = v.Pos.Z; }
                    else if (ax >= az) { u = v.Pos.Z; vv = v.Pos.Y; }
                    else { u = v.Pos.X; vv = v.Pos.Y; }
                    verts[i + k] = new Vert(v.Pos, v.Norm, u, vv, v.R, v.G, v.B, v.MatId);
                }
            }
            kit.Parts[name] = verts;
        }
        kit.Plume = kit.Get("plume"); kit.Cape = kit.Get("cape"); kit.Bow = kit.Get("weapon_bow");
        kit.Quiver = kit.Get("quiver"); kit.Arrow = kit.Get("weapon_arrow"); kit.Javelin = kit.Get("weapon_javelin");
        kit.WebSnare = kit.Get("web_snare"); kit.BannerPole = kit.Get("banner_pole");
        kit.Emblems = new Vert[9][];
        kit.Emblems[(int)Emblem.Chevron] = kit.Get("emblem_chevron");
        kit.Emblems[(int)Emblem.Saltire] = kit.Get("emblem_saltire");
        kit.Emblems[(int)Emblem.Lozenge] = kit.Get("emblem_lozenge");
        kit.Emblems[(int)Emblem.None] = Array.Empty<Vert>();
        kit.Emblems[(int)Emblem.Bend] = kit.Get("emblem_bend");
        kit.Emblems[(int)Emblem.Cross] = kit.Get("emblem_cross");
        kit.Emblems[(int)Emblem.Pale] = kit.Get("emblem_pale");
        kit.Emblems[(int)Emblem.Fess] = kit.Get("emblem_fess");
        kit.Emblems[(int)Emblem.Sun] = kit.Get("emblem_sun");
        kit.HorseBody = kit.Get("horse_body"); kit.HorseBodyBarded = kit.Get("horse_body_barded");
        kit.HorseBodyScale = kit.Get("horse_body_scale"); kit.HorseBodyFur = kit.Get("horse_body_fur");
        kit.HorseNeck = kit.Get("horse_neck"); kit.HorseNeckBarded = kit.Get("horse_neck_barded"); kit.HorseNeckScale = kit.Get("horse_neck_scale");
        kit.HorseHead = kit.Get("horse_head"); kit.HorseHeadBarded = kit.Get("horse_head_barded"); kit.HorseHeadScale = kit.Get("horse_head_scale");
        kit.HorseLegUp = kit.Get("horse_leg_up"); kit.HorseLegLow = kit.Get("horse_leg_low"); kit.HorseTail = kit.Get("horse_tail");
        kit.CamelBody = kit.Get("camel_body"); kit.CamelNeck = kit.Get("camel_neck"); kit.CamelHead = kit.Get("camel_head");
        kit.WolfBody = kit.Get("wolf_body"); kit.WolfHead = kit.Get("wolf_head"); kit.WolfLeg = kit.Get("wolf_leg"); kit.WolfTail = kit.Get("wolf_tail");
        kit.BearBody = kit.Get("bear_body"); kit.BearHead = kit.Get("bear_head"); kit.BearLeg = kit.Get("bear_leg");
        kit.SpiderAbdomen = kit.Get("spider_abdomen"); kit.SpiderHead = kit.Get("spider_head"); kit.SpiderLeg = kit.Get("spider_leg");
        kit.WyvernBody = kit.Get("wyvern_body"); kit.WyvernNeck = kit.Get("wyvern_neck"); kit.WyvernHead = kit.Get("wyvern_head");
        kit.WyvernWing = kit.Get("wyvern_wing"); kit.WyvernLeg = kit.Get("wyvern_leg"); kit.WyvernTail = kit.Get("wyvern_tail"); kit.WyvernSting = kit.Get("wyvern_sting");
        kit.LodFoot = kit.Get("lod_foot"); kit.LodHorse = kit.Get("lod_horse");
        kit.BeltPoolLight = new[] { kit.Get("belt_leather"), kit.Get("belt_pouch"), kit.Get("belt_baldric") };
        kit.BeltPoolHeavy = new[] { kit.Get("belt_plate"), kit.Get("belt_leather"), kit.Get("belt_baldric"), kit.Get("belt_gilded") };
        kit.BeltPoolNorth = new[] { kit.Get("belt_cross_straps"), kit.Get("belt_baldric"), kit.Get("belt_pouch") };
        kit.ShldPoolKnight = new[] { kit.Get("shld_pauldrons_steel"), kit.Get("shld_pauldrons_fluted"), kit.Get("shld_pauldrons_lance"), kit.Get("shld_pauldrons_dark") };
        kit.ShldPoolFoot = new[] { kit.Get("shld_mail_aventail"), kit.Get("shld_gorget_steel"), kit.Get("shld_pauldrons_steel") };
        kit.ChestPoolPlate = new[] { kit.Get("chest_plate"), kit.Get("chest_plate_fluted"), kit.Get("chest_plate_dark"), kit.Get("chest_breastplate") };
        kit.SkirtPoolPlate = new[] { kit.Get("skirt_fauld_steel"), kit.Get("skirt_fauld_long"), kit.Get("skirt_fauld_dark") };
        return kit;
    }

    public Vert[] Get(string name) => Parts.TryGetValue(name, out var v) ? v : Array.Empty<Vert>();

    public LookMeshes Resolve(Look look)
    {
        if (lookCache.TryGetValue(look, out var cached)) return cached;
        bool bones = look.Body == "bones" || look.Face == "skull";
        string limbTag = bones ? "bones" : MapLimb(look.Body);
        string legTag = bones ? "bones" : MapLimb(look.Legs);
        string helmKey = MapHelm(look.Head);
        string headKey = look.Face switch
        {
            "goblin" => "head_goblin",
            "troll" => "head_troll",
            "skull" => "head_skull",
            "ghoul" => "head_ghoul",
            _ => $"head_{(look.Visage >= 0 ? Variety.Parts(look.Visage).Style : 0)}",
        };
        var (chestKey, shldKey, beltKey, skirtKey, forceTabard, beltVar, shldVar) = MapArmorLayers(look.Body);
        string weaponKey = look.Weapon != "none" ? MapWeapon(look.Weapon) : "";
        string shieldKey = look.Shield != "none" ? MapShield(look.Shield) : "";
        static byte LayerPat(string k) => k switch
        {
            "chest_mail" or "shld_mail_aventail" or "skirt_mail" => PatMail,
            "chest_lamellar" or "chest_scale" or "shld_lamellar" or "shld_scale" or "skirt_lamellar" or "skirt_scale" => PatScale,
            "chest_tunic" or "chest_surcoat" or "chest_brigandine" or "chest_robe" or "shld_collar_cloth" or "shld_cowl_cloth" or "skirt_tunic" or "skirt_deel" or "skirt_kaftan" or "skirt_tabard" => PatQuilt,
            "chest_plate" or "chest_plate_fluted" or "chest_plate_dark" or "chest_plate_gilded" or "chest_breastplate" or "chest_mirror" or "shld_gorget_steel" or "shld_pauldrons_steel" or "shld_pauldrons_gilded" or "shld_pauldrons_fluted" or "shld_pauldrons_dark" or "shld_pauldrons_lance" or "skirt_fauld_steel" or "skirt_fauld_gilded" or "skirt_fauld_dark" or "skirt_fauld_long" => PatBrushed,
            _ => PatSmooth,
        };
        byte LimbPatOf(string tag) => tag switch { "mail" => PatMail, "plate" => PatBrushed, "cloth" => PatQuilt, _ => PatSmooth };
        bool showTabardPanel = (forceTabard || look.Tabard) && chestKey != "chest_robe" && chestKey != "chest_bones" && skirtKey != "skirt_kaftan" && skirtKey != "skirt_deel" && chestKey != "chest_surcoat";
        var resolved = new LookMeshes
        {
            Chest = Get(chestKey),
            Shoulders = shldKey.Length > 0 ? Get(shldKey) : Array.Empty<Vert>(),
            Belt = beltKey.Length > 0 ? Get(beltKey) : Array.Empty<Vert>(),
            Skirt = skirtKey.Length > 0 ? Get(skirtKey) : Array.Empty<Vert>(),
            TabardPanel = showTabardPanel ? Get("skirt_tabard") : Array.Empty<Vert>(),
            Head = Get(headKey),
            Helm = helmKey.Length > 0 ? Get(helmKey) : Array.Empty<Vert>(),
            UArm = Get($"uarm_{limbTag}"),
            FArm = Get($"farm_{limbTag}"),
            Thigh = Get($"thigh_{legTag}"),
            Shin = Get($"shin_{legTag}"),
            Weapon = weaponKey.Length > 0 ? Get(weaponKey) : Array.Empty<Vert>(),
            Shield = shieldKey.Length > 0 ? Get(shieldKey) : Array.Empty<Vert>(),
            FullEnclosedHelm = helmKey is "helm_greathelm" or "helm_greathelm_wings" or "helm_greathelm_sun" or "helm_greathelm_horns" or "helm_dragon" or "helm_armet" or "helm_bascinet",
            HasCape = look.Cape,
            HasPlume = look.Plume,
            HasShield = look.Shield != "none",
            HasTabard = look.Tabard,
            ChestPat = LayerPat(chestKey),
            ShldPat = LayerPat(shldKey),
            SkirtPat = LayerPat(skirtKey),
            LimbPat = LimbPatOf(limbTag),
            LegPat = LimbPatOf(legTag),
            BeltVarGroup = beltVar,
            ShldVarGroup = shldVar,
            ShieldKind = shieldKey,
            WeaponEdgeZ = weaponKey is "weapon_scimitar" or "weapon_falchion" or "weapon_axe" or "weapon_warhammer" or "weapon_daneaxe" or "weapon_halberd" or "weapon_glaive" or "weapon_billhook",
            OffHandForward = weaponKey is "weapon_spear" or "weapon_pike" or "weapon_halberd" or "weapon_glaive" or "weapon_billhook",
            OffHandOffset = weaponKey switch
            {
                "weapon_greatsword" or "weapon_flamberge" => 0.24f,
                "weapon_longsword" => 0.18f,
                "weapon_daneaxe" => 0.32f,
                "weapon_spear" or "weapon_pike" or "weapon_halberd" or "weapon_glaive" or "weapon_billhook" => 0.34f,
                _ => 0.22f,
            },
            BladeReach = weaponKey switch
            {
                "weapon_greatsword" or "weapon_flamberge" => 1.38f,
                "weapon_daneaxe" => 1.36f,
                "weapon_longsword" => 1.12f,
                "weapon_halberd" or "weapon_glaive" or "weapon_billhook" => 1.88f,
                "weapon_pike" => 2.10f,
                "weapon_spear" => 1.60f,
                "weapon_scimitar" => 0.94f,
                _ => 0.84f,
            },
        };
        return lookCache[look] = resolved;
    }

    /// Map every one of the 29+ body armor IDs to a curated 4-layer modular combo: (Chest, Shoulders, Belt, Skirt, TabardPanel, BeltVarGroup, ShldVarGroup).
    static (string Chest, string Shld, string Belt, string Skirt, bool Tabard, byte BeltVar, byte ShldVar) MapArmorLayers(string body) => body switch
    {
        "bones"             => ("chest_bones", "", "", "", false, 0, 0),
        "robe"              => ("chest_robe", "shld_cowl_cloth", "belt_stole", "", false, 0, 0),
        "shroud"            => ("chest_robe", "shld_linen_shawl", "belt_sash_dark", "", false, 0, 0),
        "plate_gilded"      => ("chest_plate_gilded", "shld_pauldrons_gilded", "belt_gilded", "skirt_fauld_gilded", true, 0, 0),
        "plate_fluted"      => ("chest_plate_fluted", "shld_pauldrons_fluted", "belt_plate", "skirt_fauld_long", false, 2, 0),
        "plate_dark"        => ("chest_plate_dark", "shld_pauldrons_dark", "belt_plate", "skirt_fauld_dark", false, 2, 0),
        "plate_lance"       => ("chest_plate", "shld_pauldrons_lance", "belt_plate", "skirt_fauld_long", false, 2, 0),
        "plate_surcoat"     => ("chest_surcoat", "shld_pauldrons_steel", "belt_plate", "skirt_fauld_steel", true, 2, 1),
        "plate_fur"         => ("chest_plate_dark", "shld_fur_mantle", "belt_cross_straps", "skirt_fauld_dark", false, 3, 0),
        "plate"             => ("chest_plate", "shld_pauldrons_steel", "belt_plate", "skirt_fauld_steel", false, 2, 1),
        "breastplate"       => ("chest_breastplate", "shld_gorget_steel", "belt_pouch", "skirt_tunic", false, 1, 2),
        "mirror"            => ("chest_mirror", "shld_lamellar", "belt_sash_dark", "skirt_mail", false, 0, 0),
        "brigandine_gilded" => ("chest_brigandine", "shld_pauldrons_gilded", "belt_gilded", "skirt_fauld_gilded", false, 0, 0),
        "brigandine"        => ("chest_brigandine", "shld_pauldrons_steel", "belt_leather", "skirt_mail", false, 1, 2),
        "coatofplates"      => ("chest_brigandine", "shld_mail_aventail", "belt_baldric", "skirt_tunic", false, 1, 2),
        "mail_hauberk"      => ("chest_mail", "shld_mail_aventail", "belt_baldric", "skirt_mail", false, 1, 0),
        "mail_fur"          => ("chest_mail", "shld_fur_mantle", "belt_cross_straps", "skirt_mail", false, 3, 0),
        "mail"              => ("chest_surcoat", "shld_mail_aventail", "belt_pouch", "skirt_mail", false, 1, 0),
        "lamellar_gilded"   => ("chest_lamellar", "shld_pauldrons_gilded", "belt_gilded", "skirt_lamellar", false, 0, 0),
        "lamellar_dark"     => ("chest_scale", "shld_lamellar", "belt_sash_dark", "skirt_mail", false, 0, 0),
        "lamellar"          => ("chest_lamellar", "shld_lamellar", "belt_sash_dark", "skirt_lamellar", false, 0, 0),
        "scale_iron"        => ("chest_scale", "shld_gorget_steel", "belt_pouch", "skirt_fauld_steel", false, 1, 0),
        "scale"             => ("chest_scale", "shld_scale", "belt_baldric", "skirt_scale", false, 1, 0),
        "fur"               => ("chest_leather", "shld_fur_mantle", "belt_cross_straps", "skirt_tunic", false, 3, 0),
        "gambeson_fur"      => ("chest_tunic", "shld_fur_mantle", "belt_pouch", "skirt_tunic", false, 3, 0),
        "deel_fur"          => ("chest_tunic", "shld_fur_mantle", "belt_sash_trim", "skirt_deel", false, 0, 0),
        "deel"              => ("chest_tunic", "shld_fur_collar", "belt_sash_trim", "skirt_deel", false, 0, 0),
        "kaftan"            => ("chest_tunic", "shld_linen_shawl", "belt_sash_trim", "skirt_kaftan", false, 0, 0),
        "burnous"           => ("chest_tunic", "shld_linen_shawl", "belt_sash_dark", "skirt_kaftan", false, 0, 0),
        "jupon"             => ("chest_surcoat", "shld_gorget_steel", "belt_gilded", "skirt_tunic", false, 0, 0),
        "leather"           => ("chest_leather", "", "belt_baldric", "skirt_tunic", false, 1, 0),
        "rags"              => ("chest_leather", "", "belt_leather", "skirt_tunic", false, 0, 0),
        "gambeson"          => ("chest_tunic", "shld_collar_cloth", "belt_pouch", "skirt_tunic", false, 1, 0),
        _                   => ("chest_tunic", "", "belt_leather", "skirt_tunic", false, 1, 0),
    };

    static string MapLimb(string gear) => gear switch
    {
        "bones" => "bones",
        "plate" or "plate_fluted" or "plate_surcoat" or "plate_gilded" or "plate_fur" or "plate_dark" or "plate_lance" or "breastplate" or "splint" or "mirror" => "plate",
        "mail" or "mail_hauberk" or "coatofplates" or "brigandine" or "brigandine_gilded" or "mail_fur" or "lamellar" or "scale" or "scale_iron" or "lamellar_gilded" or "lamellar_dark" => "mail",
        _ => "cloth",
    };

    static string MapHelm(string head)
    {
        if (head is "" or "none") return "";
        string h = SpriteSheet.HelmBase(head);
        return h switch
        {
            "greathelm_wings" or "winged_sallet" => "helm_greathelm_wings",
            "greathelm_sun" or "greathelm_crown" => "helm_greathelm_sun",
            "greathelm_horns" or "ibex" => "helm_greathelm_horns",
            "dragon" or "lion" => "helm_dragon",
            "bascinet" => "helm_bascinet",
            "armet" or "faceplate" or "frogmouth" => "helm_armet",
            "greathelm" or "greathelm_sugarloaf" or "greathelm_visor" or "bucket" => "helm_greathelm",
            "sallet_crest" => "helm_sallet_crest",
            "sallet" or "sallet_visored" or "sallet_bevor" => "helm_sallet",
            "barbute" or "barbute_crest" => "helm_barbute",
            "morion" => "helm_morion",
            "kettle" or "kettle_brimmed" or "chapel" => "helm_kettle",
            "spangen_horns" or "steppe_horns" => "helm_spangen_horns",
            "spangen" or "spangen_spectacle" or "spangen_wolf" or "galea" => "helm_spangen",
            "bearhead" or "whitebear" => "helm_bearhead",
            "tug" => "helm_tug",
            "steppe" or "steppe_mask" or "kulah" => "helm_steppe",
            "spike_turban" => "helm_spike_turban",
            "turban" or "litham" or "corsair" => "helm_turban",
            "spike" => "helm_spike",
            "coif" => "helm_coif",
            "gjermundbu" => "helm_spangen",
            "nasal" => "helm_nasal",
            "brigand_hat" => "helm_brigand_hat",
            "hood" => "helm_hood",
            "furcap" => "helm_furcap",
            _ => "helm_cap",
        };
    }

    static string MapWeapon(string w) => w switch
    {
        "scimitar" or "saber" => "weapon_scimitar",
        "falchion" => "weapon_falchion",
        "flamberge" => "weapon_flamberge",
        "greatsword" or "greatsword_claymore" => "weapon_greatsword",
        "longsword" or "longsword_estoc" or "longsword_gilded" => "weapon_longsword",
        "warhammer" or "hammer" or "maul" => "weapon_warhammer",
        "morningstar" => "weapon_morningstar",
        "mace" => "weapon_mace",
        "daneaxe" or "daneaxe_bearded" or "bardiche" => "weapon_daneaxe",
        "axe" or "axe_bearded" => "weapon_axe",
        "glaive" => "weapon_glaive",
        "billhook" => "weapon_billhook",
        "halberd" => "weapon_halberd",
        "lance" or "pennonlance" => "weapon_lance",
        "pike" => "weapon_pike",
        "javelin" => "weapon_javelin",
        "spear" or "spear_winged" => "weapon_spear",
        "crossbow" or "crossbow_heavy" or "arbalest" => "weapon_crossbow",
        "bow" or "bow_horn" or "longbow" or "recurve" => "weapon_bow",
        "sling" => "weapon_sling",
        "club" => "weapon_club",
        "staff" or "crozier" => "weapon_staff",
        _ => "weapon_sword",
    };

    static string MapShield(string sh) => sh switch
    {
        "pavise" => "shield_pavise",
        "tower" => "shield_tower",
        "kalkan" or "dhal" => "shield_kalkan",
        "kite" => "shield_kite",
        "round" or "round_bossed" or "round_iron" or "targe" or "buckler" => "shield_round",
        _ => "shield_heater",
    };
}

/// Fast pre-resolved 14-slot material table per unit (supports human visages, non-human races, mounts, and beasts).
struct UnitMatTable
{
    public const int Slots = 14;
    public static readonly float[] Metals =
    {
        0f, 0f, 0f, 0f, 0.95f, 0.55f, 0.90f, 0f, 0f, 0f, 0f, 0f, 0f, 0f
    };
    /// Quantized metallic nibbles (0..15) packed into VertexPNCT Color.A low nibble.
    public static readonly byte[] MetalQ =
    {
        0, 0, 0, 0, 14, 9, 14, 0, 0, 0, 0, 0, 0, 0
    };

    public static void Fill(int paletteIdx, int visage, int coatIdx, bool camel, Color tint, Span<Vector3> cols, string face = "man")
    {
        var side = Style.Sides[Math.Clamp(paletteIdx, 0, Style.Sides.Length - 1)];
        var (sk, hr, _) = visage >= 0 ? Variety.Parts(visage) : (-1, -1, 0);
        Color skin = face switch
        {
            "goblin" => Style.GoblinSkin,
            "troll" => new Color(118, 128, 96),
            "skull" => Style.Bone,
            "ghoul" => new Color(152, 160, 146),
            _ => sk >= 0 ? Variety.Skins[sk] : Style.Skin,
        };
        Color hair = hr >= 0 ? Variety.Hairs[hr] : Style.Hair;
        var coatVec = camel ? Style.CamelCoat : Style.Coats[Math.Abs(coatIdx) % Style.Coats.Length];
        var tv = tint.ToVector3();

        Vector3 Pack(Color c, float boost = 1f) => new(
            MathF.Min(1f, c.R * (boost / 255f)) * tv.X,
            MathF.Min(1f, c.G * (boost / 255f)) * tv.Y,
            MathF.Min(1f, c.B * (boost / 255f)) * tv.Z);

        cols[0] = tv;
        cols[1] = Pack(side.Cloth, 1.16f);
        cols[2] = Pack(side.ClothDark, 1.18f);
        cols[3] = Pack(side.Trim, 1.10f);
        cols[4] = Pack(new Color(196, 204, 218));
        cols[5] = Pack(new Color(110, 116, 128));
        cols[6] = Pack(Style.Brass);
        cols[7] = Pack(new Color(128, 86, 52));
        cols[8] = Pack(new Color(146, 102, 60));
        cols[9] = Pack(skin);
        cols[10] = Pack(hair);
        cols[11] = Vector3.Clamp(coatVec * 1.04f, Vector3.Zero, Vector3.One) * tv;
        cols[12] = Vector3.Clamp(coatVec * 0.38f, Vector3.Zero, Vector3.One) * tv;
        cols[13] = Pack(new Color(60, 46, 38));
    }

    /// Fill material slots 11..13 for a 3D beast (wolf/hound/warg, bear, spider, wyvern).
    public static void FillBeast(Color body, Color accent, Color dark, Color tint, Span<Vector3> cols)
    {
        var tv = tint.ToVector3();
        Vector3 Pack(Color c) => new(c.R / 255f * tv.X, c.G / 255f * tv.Y, c.B / 255f * tv.Z);
        cols[0] = tv;
        cols[11] = Pack(body);
        cols[12] = Pack(accent);
        cols[13] = Pack(dark);
    }
}
