// Real-time 3D Pixel Art unit shader (spike/3d-pixel-art, Phase 3).
// Techniques:
//   Unit3D       - stepped 4-band cel-shaded color + procedural armor/cloth micro-detail to RT0 + view normal & linear depth to RT1 (MRT)
//   Unit3DShadow - writes depth into the sun's shadow map (real 3D directional shadows!)
//
// Vertex layout: VertexPNCT (aPos, aNormal, aColor, aUV).
//   aColor.rgb = resolved material albedo * baked AO
//   aColor.a   = packed (patternId << 4) | metalQuantized(0..15)
//                patternId: 0 = smooth, 1 = chainmail rings, 2 = lamellar/scale shingles, 3 = quilted cloth, 4 = brushed steel/wood
//   aUV        = swim-free object-local meter coordinates (box-projected per triangle at load time)
varying vec3 vWorld;
varying vec3 vNormal;
varying vec4 vColor;
varying vec2 vUV;
varying vec4 vLight;

#ifdef VERTEX
attribute vec4 aPos;
attribute vec3 aNormal;
attribute vec4 aColor;
attribute vec2 aUV;

void main()
{
    vec3 p = aPos.xyz;
    vWorld = p;
    vNormal = aNormal;
    vColor = aColor;
    vUV = aUV;
#ifdef SHADOW
    gl_Position = vec4(p, 1.0) * LightViewProj;
    vLight = gl_Position;
#else
    vLight = vec4(0.0);
    gl_Position = vec4(p, 1.0) * ViewProj;
#endif
    FIXUP_POSITION
}
#endif

#ifdef FRAGMENT
void main()
{
#ifdef SHADOW
    gl_FragColor = vec4(vLight.z / vLight.w, 0.0, 0.0, 1.0);
#else
    vec3 n = normalize(vNormal);
    vec3 l = normalize(-SunDir);
    vec3 v = normalize(CameraPos - vWorld);

    float ndotl = dot(n, l);
    float sh = ShadowAt(vWorld + n * 0.06) * CloudAt(vWorld);

    // Unpack surface pattern ID (0..4) and metallic weight (0..1) from constant-per-triangle aColor.a.
    float aInt = floor(vColor.a * 255.0 + 0.5);
    float patId = floor(aInt / 16.0);
    float metal = mod(aInt, 16.0) / 15.0;

    // 4-band stepped cel shading (crisp low-poly tonal steps with clean shadow/mid/lit separation, never blowing out steel to pure white).
    float rawLit = clamp((ndotl * 0.76 + 0.24) * mix(0.42, 1.0, sh), 0.0, 1.0);
    float band = 0.22;
    band += smoothstep(0.24, 0.28, rawLit) * 0.26;
    band += smoothstep(0.52, 0.56, rawLit) * 0.28;
    band += smoothstep(0.78, 0.82, rawLit) * 0.24;

    // Subtle distance-faded surface micro-detail (kept gentle so crisp low-poly planes stay clean, never noisy static).
    float dist = length(CameraPos - vWorld);
    float detailFade = 1.0 - smoothstep(10.0, 26.0, dist);

    vec3 albedo = vColor.rgb;
    float specBoost = 1.0;

    if (detailFade > 0.01 && patId > 0.5)
    {
        if (patId < 1.5)
        {
            // Pattern 1: Subtle chainmail weave tone.
            vec2 p = vUV * 20.0;
            p.x += step(1.0, mod(floor(p.y), 2.0)) * 0.5;
            float r = length(fract(p) - 0.5);
            float ring = smoothstep(0.18, 0.32, r) * (1.0 - smoothstep(0.36, 0.48, r));
            float hole = 1.0 - smoothstep(0.08, 0.20, r);
            albedo *= 1.0 + (ring * 0.05 - hole * 0.06) * detailFade;
            specBoost = 1.0 + ring * 0.18 * detailFade;
        }
        else if (patId < 2.5)
        {
            // Pattern 2: Subtle lamellar / scale horizontal bands.
            vec2 p = vUV * vec2(10.0, 10.0);
            p.x += step(1.0, mod(floor(p.y), 2.0)) * 0.5;
            vec2 f = fract(p);
            float botSeam = 1.0 - smoothstep(0.0, 0.18, f.y);
            float topLip = smoothstep(0.76, 0.92, f.y);
            albedo *= 1.0 + (topLip * 0.07 - botSeam * 0.10) * detailFade;
            specBoost = 1.0 + topLip * 0.18 * detailFade;
        }
        else if (patId < 3.5)
        {
            // Pattern 3: Very subtle cloth weave (keeps flat-shaded low-poly cloth crisp).
            vec2 p = vUV * 9.0;
            float d1 = abs(fract(p.x + p.y) - 0.5);
            float d2 = abs(fract(p.x - p.y) - 0.5);
            float stitch = 1.0 - smoothstep(0.0, 0.08, min(d1, d2));
            albedo *= 1.0 + (0.02 - stitch * 0.05) * detailFade;
        }
        else
        {
            // Pattern 4: Clean brushed steel / wood sheen.
            float grain = sin(vUV.x * 28.0 + vUV.y * 4.0) * 0.5 + 0.5;
            albedo *= 1.0 + (grain - 0.5) * 0.04 * detailFade;
        }
    }

    // Balanced cool-shadow / warm-key lighting so midtone steel stays rich silver-grey and only true specular glints hit bright white.
    float hemi = n.y * 0.5 + 0.5;
    vec3 amb = mix(GroundColor * 0.52, SkyColor * 0.62, hemi) * vec3(0.86, 0.92, 1.08);
    vec3 direct = SunColor * (band * 0.78);

    vec3 col = albedo * (amb + direct);

    // Crisp quantized metallic specular highlight on steel/plate/brass (never blows out whole limbs).
    if (metal > 0.2)
    {
        vec3 h = normalize(l + v);
        float spec = pow(max(dot(n, h), 0.0), 22.0) * sh * specBoost;
        float specBand = smoothstep(0.36, 0.42, spec);
        col += SunColor * mix(vec3(0.82, 0.88, 0.98), albedo * 1.15, 0.35) * specBand * metal * 0.52;
    }

    // Subtle stepped rim light on sun-grazing edges.
    float rim = pow(1.0 - max(dot(n, v), 0.0), 2.8) * max(ndotl * 0.5 + 0.5, 0.0) * sh;
    col += (SunColor * 0.50 + SkyColor * 0.50) * smoothstep(0.52, 0.60, rim) * 0.14;

    // Night torches & moving spell glows.
    col += albedo * (TorchLight(vWorld) + GlowLight(vWorld));

    // Distance fog matching world.glsl.
    float fog = clamp((dist - FogParams.x) / max(1.0, FogParams.y - FogParams.x), 0.0, 1.0);
    col = mix(col, FogColor, fog * FogParams.z);

    // RT0: lit cel-shaded color.
    gl_FragData[0] = vec4(clamp(col, 0.0, 1.0), 1.0);

    // RT1: view-space normal (RGB in 0..1) + encoded linear depth (A in 0.02..1.0; larger = closer, 0 = background!).
    vec3 vn = normalize((vec4(n, 0.0) * View).xyz);
    float viewZ = max(0.1, -(vec4(vWorld, 1.0) * View).z);
    float encDepth = clamp(1.0 / (1.0 + viewZ * 0.065), 0.02, 1.0);
    gl_FragData[1] = vec4(vn * 0.5 + 0.5, encDepth);
#endif
}
#endif
