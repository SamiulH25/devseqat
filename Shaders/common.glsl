// Shared lighting helpers for world.glsl and sprite.glsl (fragment stage only, but the noise is for both).
// Needs uniforms LightViewProj, ShadowParams, CameraPos, SunDir, SunColor, FogColor, FogParams, Field, Look and the ShadowMap sampler.
// Field: x = 1 / ground-map size in metres, y = ground map on (1), z = cloud shadow strength, w = wetness.
// Look: x = snow cover, y = close-up ground detail on (1), z = night (0 day .. 1 full night), w = rain.

// Cheap value noise (docs/archive/VISUALS.md V4): mottled ground, patches, puddles, cloud shadows.
float Hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float Noise2(vec2 p)
{
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(Hash2(i), Hash2(i + vec2(1.0, 0.0)), f.x), mix(Hash2(i + vec2(0.0, 1.0)), Hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

#ifdef FRAGMENT

// Shadows of clouds drifting over the land with the wind: 1 = in sun.
float CloudAt(vec3 world)
{
    if (Field.z <= 0.0) return 1.0;
    vec2 p = world.xz * 0.011 + Wind.xy * Time * 0.018;
    float n = Noise2(p) * 0.65 + Noise2(p * 2.3 + 7.0) * 0.35;
    return 1.0 - Field.z * smoothstep(0.48, 0.72, n);
}

// Fraction of sunlight reaching `world` (1 = lit): a 3x3 percentage-closer filter over the sun's shadow map.
// ShadowParams: x = texel size, y = depth bias, z = enabled.
// The shadow map was rendered into a render target, which MonoGame's GL path stores upside down: hence 0.5 - 0.5 y.
float ShadowAt(vec3 world)
{
    if (ShadowParams.z < 0.5) return 1.0;
    vec4 lp = vec4(world, 1.0) * LightViewProj;
    vec3 c = lp.xyz / lp.w;
    float edge = max(abs(c.x), abs(c.y));
    if (edge > 1.0 || c.z > 1.0) return 1.0;
    vec2 uv = vec2(c.x * 0.5 + 0.5, 0.5 - c.y * 0.5);
    float d = c.z - ShadowParams.y;
    float s = 0.0;
    for (int i = -1; i <= 1; i++)
        for (int j = -1; j <= 1; j++)
            s += step(d, texture2D(ShadowMap, uv + vec2(float(i), float(j)) * ShadowParams.x).r);
    s /= 9.0;
    return mix(s, 1.0, smoothstep(0.85, 1.0, edge)); // fade out at the edge of the map
}

// Light from a besieged town's torches (Torch: x = the row's z, y = its height, z = strength, w = spacing): the nearest
// torch on the row, flickering, falling off with distance. The row is fixed, so there is no list of lights.
vec3 TorchLight(vec3 w)
{
    if (Torch.z <= 0.0) return vec3(0.0);
    float x = floor(w.x / Torch.w + 0.5) * Torch.w;
    if (abs(x) > 240.5) return vec3(0.0);
    vec3 d = w - vec3(x, Torch.y, Torch.x);
    float d2 = dot(d, d);
    float flick = 0.86 + 0.14 * sin(Time * 13.0 + x * 1.7) * sin(Time * 7.3 + x * 0.6);
    return vec3(1.0, 0.6, 0.28) * Torch.z * flick * 7.0 / (d2 + 3.0) * (1.0 - smoothstep(60.0, 150.0, d2));
}

// Moving lights (docs/archive/SPECTACLE.md S1): up to four a frame, picked by the battle screen (a smite's pillar, a bolt, fire,
// a Transcendent captain). Glow: xyz = place, w = strength (negative takes light away: the Black Flame). GlowCol: rgb =
// colour, w = reach in metres; the light falls off smoothly and is gone by three times the reach.
vec3 OneGlow(vec3 w, vec4 g, vec4 c)
{
    if (g.w == 0.0) return vec3(0.0);
    vec3 d = w - g.xyz;
    float k = dot(d, d) / (c.w * c.w);
    return c.rgb * g.w / (1.0 + k * 6.0) * (1.0 - smoothstep(2.0, 9.0, k));
}
vec3 GlowLight(vec3 w)
{
    return OneGlow(w, Glow0, GlowCol0) + OneGlow(w, Glow1, GlowCol1) + OneGlow(w, Glow2, GlowCol2) + OneGlow(w, Glow3, GlowCol3);
}

// Dressed stone: courses 0.55 m high of blocks 1.1 m long, alternate courses offset, darker mortar, each block its own
// shade, and rain streaks down the face; fades to plain far away (the lines would shimmer).
float Masonry(vec3 w, vec3 n, float dist)
{
    vec2 p = abs(n.y) > 0.7 ? w.xz : abs(n.x) > abs(n.z) ? vec2(w.z, w.y) : vec2(w.x, w.y);
    float row = floor(p.y / 0.55);
    float u = p.x / 1.1 + row * 0.5;
    vec2 f = vec2(fract(u) * 1.1, fract(p.y / 0.55) * 0.55);
    float mortar = min(min(f.x, 1.1 - f.x), min(f.y, 0.55 - f.y));
    float shade = (0.86 + Hash2(vec2(floor(u), row)) * 0.2) * mix(0.6, 1.0, smoothstep(0.015, 0.045, mortar));
    shade *= 0.88 + 0.12 * Noise2(vec2(w.x * 0.9 + w.z * 0.9, w.y * 0.12));
    return mix(shade, 0.95, smoothstep(50.0, 120.0, dist));
}

// Distance fog, thinner on high ground, tinted warm toward the sun.
// FogParams: x = start, y = end, z = how much fog remains 40 m up.
vec3 Fog(vec3 col, vec3 world)
{
    vec3 v = world - CameraPos;
    float d = length(v);
    float f = clamp((d - FogParams.x) / (FogParams.y - FogParams.x), 0.0, 1.0);
    f = f * f * (3.0 - 2.0 * f);
    f *= mix(1.0, FogParams.z, clamp(world.y / 40.0, 0.0, 1.0));
    float sun = pow(max(dot(v / max(d, 0.001), -SunDir), 0.0), 8.0);
    return mix(col, FogColor + SunColor * sun * 0.25, f);
}

#endif
