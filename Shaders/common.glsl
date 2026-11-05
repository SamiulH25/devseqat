// Shared lighting helpers for world.glsl and sprite.glsl (fragment stage only).
// Needs uniforms LightViewProj, ShadowParams, CameraPos, SunDir, SunColor, FogColor, FogParams and the ShadowMap sampler.
#ifdef FRAGMENT

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
