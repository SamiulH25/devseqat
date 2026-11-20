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

    // 4-band stepped cel shading (vibrant, painterly tonal steps — never crushed black in shadow!).
    float rawLit = clamp((ndotl * 0.72 + 0.28) * mix(0.45, 1.0, sh), 0.0, 1.0);
    float band = 0.18;
    band += smoothstep(0.22, 0.26, rawLit) * 0.26;
    band += smoothstep(0.48, 0.52, rawLit) * 0.30;
    band += smoothstep(0.75, 0.79, rawLit) * 0.28;

    // Distance fade so procedural micro-detail enhances close/mid views and stays 100% shimmer-free far away!
    float dist = length(CameraPos - vWorld);
    float detailFade = 1.0 - smoothstep(10.0, 26.0, dist);

    vec3 albedo = vColor.rgb;
    float specBoost = 1.0;

    if (detailFade > 0.01 && patId > 0.5)
    {
        if (patId < 1.5)
        {
            // Pattern 1: Chainmail interlocking rings (staggered metallic dimple + ring glint).
            vec2 p = vUV * 24.0;
            p.x += step(1.0, mod(floor(p.y), 2.0)) * 0.5;
            float r = length(fract(p) - 0.5);
            float ring = smoothstep(0.16, 0.30, r) * (1.0 - smoothstep(0.35, 0.48, r));
            float hole = 1.0 - smoothstep(0.08, 0.20, r);
            albedo *= 1.0 + (ring * 0.14 - hole * 0.16) * detailFade;
            specBoost = 1.0 + ring * 0.35 * detailFade;
        }
        else if (patId < 2.5)
        {
            // Pattern 2: Lamellar / scale overlapping horizontal shingles with upper catchlight lip.
            vec2 p = vUV * vec2(11.0, 11.0);
            p.x += step(1.0, mod(floor(p.y), 2.0)) * 0.5;
            vec2 f = fract(p);
            float botSeam = 1.0 - smoothstep(0.0, 0.20, f.y);
            float sideSeam = 1.0 - smoothstep(0.0, 0.14, min(f.x, 1.0 - f.x));
            float topLip = smoothstep(0.72, 0.90, f.y) * (1.0 - sideSeam);
            albedo *= 1.0 + (topLip * 0.14 - botSeam * 0.20 - sideSeam * 0.08) * detailFade;
            specBoost = 1.0 + topLip * 0.30 * detailFade;
        }
        else if (patId < 3.5)
        {
            // Pattern 3: Quilted gambeson / padded wool diamond stitching.
            vec2 p = vUV * 9.5;
            float d1 = abs(fract(p.x + p.y) - 0.5);
            float d2 = abs(fract(p.x - p.y) - 0.5);
            float stitch = 1.0 - smoothstep(0.0, 0.10, min(d1, d2));
            albedo *= 1.0 + (0.04 - stitch * 0.13) * detailFade;
        }
        else
        {
            // Pattern 4: Brushed steel fluting / wood grain subtle directional sheen.
            float grain = sin(vUV.x * 34.0 + vUV.y * 5.0) * 0.5 + 0.5;
            albedo *= 1.0 + (grain - 0.5) * 0.08 * detailFade;
        }
    }

    // Warm/cool split-tone ambient: shadow side stays rich cool-sky lit, sunlit side glows warm.
    float hemi = n.y * 0.5 + 0.5;
    vec3 amb = mix(GroundColor * 0.85, SkyColor * 1.05, hemi) * vec3(0.92, 0.96, 1.06);
    vec3 direct = SunColor * (band * 1.15);

    vec3 col = albedo * (amb + direct);

    // Crisp quantized metallic specular glint on steel/plate/brass (helm domes, breastplates, blades, chanfrons).
    if (metal > 0.2)
    {
        vec3 h = normalize(l + v);
        float spec = pow(max(dot(n, h), 0.0), 16.0) * sh * specBoost;
        float specBand = smoothstep(0.28, 0.34, spec);
        col += SunColor * mix(vec3(0.76, 0.82, 0.92), albedo * 1.35, 0.45) * specBand * metal * 0.78;
    }

    // Subtle stepped rim light on the sun-grazing silhouette so dark armor & black horses separate cleanly from grass.
    float rim = pow(1.0 - max(dot(n, v), 0.0), 2.6) * max(ndotl * 0.5 + 0.5, 0.0) * sh;
    col += (SunColor * 0.55 + SkyColor * 0.45) * smoothstep(0.48, 0.55, rim) * 0.22;

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
