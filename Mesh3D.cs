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
        public readonly byte R, G, B;
        public readonly byte MatId;

        public Vert(Vector3 pos, Vector3 norm, byte r, byte g, byte b, byte matId)
        {
            Pos = pos; Norm = norm; R = r; G = g; B = b; MatId = matId;
        }
    }

    /// Pre-resolved modular mesh references for one `Look` (zero string lookups during rendering!).
    public sealed class LookMeshes
    {
        public required Vert[] Torso, Head, Helm, UArm, FArm, Thigh, Shin, Weapon, Shield;
        public required bool FullEnclosedHelm, HasCape, HasPlume, HasShield;
    }

    public readonly Dictionary<string, Vert[]> Parts = new();
    readonly Dictionary<Look, LookMeshes> lookCache = new(ReferenceEqualityComparer.Instance);

    // Pre-cached common / horse / LOD parts (zero dictionary lookups in hot loop).
    public Vert[] Plume = Array.Empty<Vert>(), Cape = Array.Empty<Vert>();
    public Vert[] Bow = Array.Empty<Vert>();
    public Vert[] HorseBody = Array.Empty<Vert>(), HorseBodyBarded = Array.Empty<Vert>();
    public Vert[] HorseNeck = Array.Empty<Vert>(), HorseNeckBarded = Array.Empty<Vert>();
    public Vert[] HorseHead = Array.Empty<Vert>(), HorseHeadBarded = Array.Empty<Vert>();
    public Vert[] HorseLegUp = Array.Empty<Vert>(), HorseLegLow = Array.Empty<Vert>(), HorseTail = Array.Empty<Vert>();
    public Vert[] LodFoot = Array.Empty<Vert>(), LodHorse = Array.Empty<Vert>();

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
                verts[i] = new Vert(new Vector3(px, py, pz), new Vector3(nx, ny, nz), r, g, b, (byte)Math.Clamp((int)MathF.Round(mat), 0, 15));
            }
            kit.Parts[name] = verts;
        }
        kit.Plume = kit.Get("plume");
        kit.Cape = kit.Get("cape");
        kit.Bow = kit.Get("weapon_bow");
        kit.HorseBody = kit.Get("horse_body");
        kit.HorseBodyBarded = kit.Get("horse_body_barded");
        kit.HorseNeck = kit.Get("horse_neck");
        kit.HorseNeckBarded = kit.Get("horse_neck_barded");
        kit.HorseHead = kit.Get("horse_head");
        kit.HorseHeadBarded = kit.Get("horse_head_barded");
        kit.HorseLegUp = kit.Get("horse_leg_up");
        kit.HorseLegLow = kit.Get("horse_leg_low");
        kit.HorseTail = kit.Get("horse_tail");
        kit.LodFoot = kit.Get("lod_foot");
        kit.LodHorse = kit.Get("lod_horse");
        return kit;
    }

    public Vert[] Get(string name) => Parts.TryGetValue(name, out var v) ? v : Array.Empty<Vert>();

    public LookMeshes Resolve(Look look)
    {
        if (lookCache.TryGetValue(look, out var cached)) return cached;
        string limbTag = MapLimb(look.Body);
        string legTag = MapLimb(look.Legs);
        string helmKey = MapHelm(look.Head);
        int headStyle = look.Visage >= 0 ? Variety.Parts(look.Visage).Style : 0;
        var resolved = new LookMeshes
        {
            Torso = Get(MapTorso(look.Body)),
            Head = Get($"head_{headStyle}"),
            Helm = helmKey.Length > 0 ? Get(helmKey) : Array.Empty<Vert>(),
            UArm = Get($"uarm_{limbTag}"),
            FArm = Get($"farm_{limbTag}"),
            Thigh = Get($"thigh_{legTag}"),
            Shin = Get($"shin_{legTag}"),
            Weapon = look.Weapon != "none" ? Get(MapWeapon(look.Weapon)) : Array.Empty<Vert>(),
            Shield = look.Shield != "none" ? Get(MapShield(look.Shield)) : Array.Empty<Vert>(),
            FullEnclosedHelm = helmKey == "helm_greathelm",
            HasCape = look.Cape,
            HasPlume = look.Plume,
            HasShield = look.Shield != "none",
        };
        return lookCache[look] = resolved;
    }

    static string MapTorso(string body) => body switch
    {
        "plate" or "plate_fluted" or "plate_surcoat" or "plate_gilded" or "plate_fur" or "breastplate" or "lamellar" or "lamellar_gilded" or "scale" or "scale_iron" => "torso_plate",
        "mail" or "mail_hauberk" or "coatofplates" or "brigandine" or "brigandine_gilded" or "mail_fur" => "torso_mail",
        "robe" => "torso_robe",
        _ => "torso_tunic",
    };

    static string MapLimb(string gear) => gear switch
    {
        "plate" or "plate_fluted" or "plate_surcoat" or "plate_gilded" or "plate_fur" or "breastplate" or "splint" => "plate",
        "mail" or "mail_hauberk" or "coatofplates" or "brigandine" or "brigandine_gilded" or "mail_fur" or "lamellar" or "scale" or "scale_iron" or "lamellar_gilded" => "mail",
        _ => "cloth",
    };

    static string MapHelm(string head)
    {
        if (head is "" or "none") return "";
        string h = SpriteSheet.HelmBase(head);
        if (h.StartsWith("kettle") || h == "chapel" || h == "morion") return "helm_kettle";
        if (h.StartsWith("sallet") || h == "winged_sallet" || h == "barbute" || h == "barbute_crest") return "helm_sallet";
        if (h.StartsWith("greathelm") || h is "armet" or "bascinet" or "bucket" or "lion" or "dragon") return "helm_greathelm";
        if (h is "nasal" or "gjermundbu" or "coif" or "kulah" or "spike") return "helm_nasal";
        if (h.StartsWith("spangen") || h.StartsWith("steppe") || h is "ibex" or "tug" or "galea") return "helm_spangen";
        if (h.Contains("turban") || h is "litham" or "corsair") return "helm_turban";
        if (h == "hood") return "helm_hood";
        if (h is "furcap" or "bearhead" or "whitebear") return "helm_furcap";
        return "helm_cap";
    }

    static string MapWeapon(string w)
    {
        if (w.Contains("greatsword") || w == "flamberge") return "weapon_greatsword";
        if (w.Contains("longsword")) return "weapon_longsword";
        if (w.Contains("sword") || w is "scimitar" or "falchion" or "saber") return "weapon_sword";
        if (w.Contains("axe") || w == "bardiche") return "weapon_axe";
        if (w.Contains("mace") || w is "morningstar" or "hammer" or "warhammer" or "maul") return "weapon_mace";
        if (w.Contains("lance")) return "weapon_lance";
        if (w.Contains("pike")) return "weapon_pike";
        if (w.Contains("halberd") || w is "billhook" or "glaive") return "weapon_halberd";
        if (w.Contains("spear") || w == "javelin") return "weapon_spear";
        if (w.Contains("crossbow") || w == "arbalest") return "weapon_crossbow";
        if (w.Contains("bow") || w == "recurve") return "weapon_bow";
        if (w.Contains("staff") || w == "crozier") return "weapon_staff";
        return "weapon_sword";
    }

    static string MapShield(string sh)
    {
        if (sh.Contains("kite") || sh.Contains("pavise")) return "shield_kite";
        if (sh.Contains("round") || sh is "targe" or "dhal" or "kalkan" or "buckler") return "shield_round";
        return "shield_heater";
    }
}

/// Fast pre-resolved 14-slot material table per unit (eliminates per-vertex switch & float conversion in the hot loop).
struct UnitMatTable
{
    public const int Slots = 14;
    // Normalized 0..1 base color * tint per material slot (pre-divided by 255 for fast byte-shade multiplication!).
    public Vector3 C0, C1, C2, C3, C4, C5, C6, C7, C8, C9, C10, C11, C12, C13;
    public Vector3 Tint;

    public static readonly float[] Metals =
    {
        0f, 0f, 0f, 0f, 0.95f, 0.55f, 0.90f, 0f, 0f, 0f, 0f, 0f, 0f, 0f
    };

    public static void Fill(int paletteIdx, int visage, int coatIdx, bool camel, Color tint, Span<Vector3> cols)
    {
        var side = Style.Sides[Math.Clamp(paletteIdx, 0, Style.Sides.Length - 1)];
        var (sk, hr, _) = visage >= 0 ? Variety.Parts(visage) : (-1, -1, 0);
        Color skin = sk >= 0 ? Variety.Skins[sk] : Style.Skin;
        Color hair = hr >= 0 ? Variety.Hairs[hr] : Style.Hair;
        var coatVec = camel ? Style.CamelCoat : Style.Coats[Math.Abs(coatIdx) % Style.Coats.Length];
        var tv = tint.ToVector3();

        Vector3 Pack(Color c, float boost = 1f) => new(
            MathF.Min(1f, c.R * (boost / 255f)) * tv.X,
            MathF.Min(1f, c.G * (boost / 255f)) * tv.Y,
            MathF.Min(1f, c.B * (boost / 255f)) * tv.Z);

        cols[0] = tv;                                               // mat 0: literal baked RGB * tint
        cols[1] = Pack(side.Cloth, 1.16f);                          // primary faction cloth
        cols[2] = Pack(side.ClothDark, 1.18f);                      // dark faction cloth
        cols[3] = Pack(side.Trim, 1.10f);                           // trim / heraldry
        cols[4] = Pack(new Color(196, 204, 218));                   // polished steel / plate
        cols[5] = Pack(new Color(110, 116, 128));                   // iron / mail
        cols[6] = Pack(Style.Brass);                                // brass / gold
        cols[7] = Pack(new Color(128, 86, 52));                     // leather
        cols[8] = Pack(new Color(146, 102, 60));                    // wood
        cols[9] = Pack(skin);                                       // skin
        cols[10] = Pack(hair);                                      // hair / beard
        cols[11] = Vector3.Clamp(coatVec * 1.04f, Vector3.Zero, Vector3.One) * tv;  // horse coat
        cols[12] = Vector3.Clamp(coatVec * 0.38f, Vector3.Zero, Vector3.One) * tv;  // horse mane / tail
        cols[13] = Pack(new Color(60, 46, 38));                     // hoof / dark bridle
    }
}
