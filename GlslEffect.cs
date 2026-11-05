using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// One uniform of a GLSL effect. Size is in vec4 slots: a float or a vector takes 1, a matrix 4.
public readonly record struct Uniform(string Name, EffectParameterClass Class, int Rows, int Columns)
{
    public static Uniform Float(string n) => new(n, EffectParameterClass.Scalar, 1, 1);
    public static Uniform Vec2(string n) => new(n, EffectParameterClass.Vector, 1, 2);
    public static Uniform Vec3(string n) => new(n, EffectParameterClass.Vector, 1, 3);
    public static Uniform Vec4(string n) => new(n, EffectParameterClass.Vector, 1, 4);
    public static Uniform Mat4(string n) => new(n, EffectParameterClass.Matrix, 4, 4);
    public int Slots => Class == EffectParameterClass.Matrix ? 4 : 1;
}

/// A texture an effect samples. The sampler state travels with the effect, so callers never set it.
public readonly record struct TextureSlot(string Name, int Slot, SamplerState State);

/// A vertex input: GLSL attribute name and the vertex-declaration usage it binds to.
public readonly record struct Attribute(string Name, VertexElementUsage Usage, int Index = 0);

/// Builds MonoGame effects straight from GLSL, with no shader compiler: DesktopGL's compiled effect format (MGFX, profile 0)
/// simply carries GLSL text, so we write that format ourselves (layout as read by MonoGame 3.8.6's Effect/Shader classes).
///
/// The shader file is shared by both stages: the builder prepends "#version 120", "#define VERTEX" or "#define FRAGMENT",
/// the technique's own defines, and one uniform array "U" holding every parameter, with a #define per parameter
/// (matrices as mat4 columns, so write `pos * WorldViewProj` as in HLSL row-vector style).
/// Vertex shaders must finish with FIXUP_POSITION, which applies MonoGame's GL y-flip / half-pixel fix-up (posFixup)
/// and maps D3D depth (0..1) to GL clip space.
public static class GlslEffect
{
    public sealed record Technique(string Name, params string[] Defines);

    static readonly int Signature = BitConverter.IsLittleEndian ? 0x5846474D : 0x4D474658; // "MGFX"

    public static Effect Create(GraphicsDevice gd, string name, string source, Uniform[] uniforms, TextureSlot[] textures,
        Attribute[] attributes, params Technique[] techniques)
    {
        int slots = Math.Max(1, uniforms.Sum(u => u.Slots));
        var prelude = new StringBuilder();
        prelude.Append($"uniform vec4 U[{slots}];\n");
        int at = 0;
        foreach (var u in uniforms)
        {
            prelude.Append(u.Class switch
            {
                EffectParameterClass.Matrix => $"#define {u.Name} mat4(U[{at}], U[{at + 1}], U[{at + 2}], U[{at + 3}])\n",
                _ when u.Columns == 1 => $"#define {u.Name} U[{at}].x\n",
                _ when u.Columns == 2 => $"#define {u.Name} U[{at}].xy\n",
                _ when u.Columns == 3 => $"#define {u.Name} U[{at}].xyz\n",
                _ => $"#define {u.Name} U[{at}]\n",
            });
            at += u.Slots;
        }
        foreach (var t in textures) prelude.Append($"uniform sampler2D {t.Name};\n");

        string Stage(bool vertex, Technique tech)
        {
            var s = new StringBuilder("#version 120\n");
            s.Append(vertex ? "#define VERTEX\n" : "#define FRAGMENT\n");
            foreach (var d in tech.Defines) s.Append($"#define {d}\n");
            if (vertex)
            {
                s.Append("uniform vec4 posFixup;\n");
                s.Append("#define FIXUP_POSITION gl_Position.y *= posFixup.y; gl_Position.xy += posFixup.zw * gl_Position.ww; gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;\n");
            }
            s.Append(prelude).Append("#line 1\n").Append(source);
            return s.ToString();
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8);
        w.Write(Signature);
        w.Write((byte)11);                                   // format version
        w.Write((byte)0);                                    // profile 0 = OpenGL
        w.Write(HashCode.Combine(name, source, string.Join(",", techniques.SelectMany(t => t.Defines)))); // effect cache key

        // One constant buffer "U" holding every parameter.
        w.Write(1);
        w.Write("U");
        w.Write((short)(slots * 16));
        w.Write(uniforms.Length);
        at = 0;
        for (int i = 0; i < uniforms.Length; i++)
        {
            w.Write(i);
            w.Write((ushort)(at * 16));
            at += uniforms[i].Slots;
        }

        // Shaders: a vertex and a fragment shader per technique.
        w.Write(techniques.Length * 2);
        foreach (var tech in techniques)
            for (int stage = 0; stage < 2; stage++)
            {
                bool vertex = stage == 0;
                w.Write(vertex);
                w.Write(name + ".glsl");
                w.Write(tech.Name + (vertex ? ".vs" : ".fs"));
                var code = Encoding.ASCII.GetBytes(Stage(vertex, tech));
                w.Write(code.Length);
                w.Write(code);
                var samplers = vertex ? Array.Empty<TextureSlot>() : textures; // DesktopGL has no vertex textures
                w.Write((byte)samplers.Length);
                foreach (var t in samplers)
                {
                    w.Write((byte)0);                        // SamplerType.Sampler2D
                    w.Write((byte)t.Slot);                   // texture slot
                    w.Write((byte)t.Slot);                   // sampler slot
                    w.Write(true);                           // carries its own sampler state
                    w.Write((byte)t.State.AddressU); w.Write((byte)t.State.AddressV); w.Write((byte)t.State.AddressW);
                    var bc = t.State.BorderColor;
                    w.Write(bc.R); w.Write(bc.G); w.Write(bc.B); w.Write(bc.A);
                    w.Write((byte)t.State.Filter);
                    w.Write(t.State.MaxAnisotropy);
                    w.Write(t.State.MaxMipLevel);
                    w.Write(t.State.MipMapLevelOfDetailBias);
                    w.Write(t.Name);
                    w.Write((byte)(uniforms.Length + Array.IndexOf(textures, t))); // the texture's parameter index
                }
                w.Write((byte)1);                            // uses constant buffer 0
                w.Write((byte)0);
                var attrs = vertex ? attributes : Array.Empty<Attribute>();
                w.Write((byte)attrs.Length);
                foreach (var a in attrs)
                {
                    w.Write(a.Name);
                    w.Write((byte)a.Usage);
                    w.Write((byte)a.Index);
                    w.Write((short)0);                       // location: looked up by name at link time
                }
            }

        // Parameters: the uniforms, then one texture parameter per sampler.
        w.Write(uniforms.Length + textures.Length);
        foreach (var u in uniforms)
        {
            w.Write((byte)u.Class);
            w.Write((byte)EffectParameterType.Single);
            w.Write(u.Name);
            w.Write("");                                     // semantic
            w.Write(0);                                      // annotations
            w.Write((byte)u.Rows);
            w.Write((byte)u.Columns);
            w.Write(0);                                      // array elements
            w.Write(0);                                      // struct members
            for (int k = 0; k < u.Rows * u.Columns; k++) w.Write(0f);
        }
        foreach (var t in textures)
        {
            w.Write((byte)EffectParameterClass.Object);
            w.Write((byte)EffectParameterType.Texture2D);
            w.Write(t.Name);
            w.Write("");
            w.Write(0);
            w.Write((byte)0); w.Write((byte)0);
            w.Write(0); w.Write(0);
        }

        // Techniques, one pass each, no render states (the caller sets those).
        w.Write(techniques.Length);
        for (int i = 0; i < techniques.Length; i++)
        {
            w.Write(techniques[i].Name);
            w.Write(0);
            w.Write(1);
            w.Write("P0");
            w.Write(0);
            w.Write(i * 2);
            w.Write(i * 2 + 1);
            w.Write(false); w.Write(false); w.Write(false);
        }
        w.Write(Signature);
        w.Flush();
        return new Effect(gd, ms.ToArray());
    }
}
