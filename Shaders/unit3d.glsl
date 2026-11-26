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
#ifdef SKINNED
attribute vec4 aBones;
attribute vec4 aWeights;

mat4 GetBone(float idx)
{
    if (idx < 0.5) return Bone0;
    if (idx < 1.5) return Bone1;
    if (idx < 2.5) return Bone2;
    if (idx < 3.5) return Bone3;
    if (idx < 4.5) return Bone4;
    if (idx < 5.5) return Bone5;
    if (idx < 6.5) return Bone6;
    return Bone7;
}
#endif

void main()
{
#ifdef SKINNED
    mat4 b0 = GetBone(aBones.x), b1 = GetBone(aBones.y), b2 = GetBone(aBones.z), b3 = GetBone(aBones.w);
    vec4 lp = vec4(aPos.xyz, 1.0);
    vec4 sp = (lp * b0) * aWeights.x + (lp * b1) * aWeights.y + (lp * b2) * aWeights.z + (lp * b3) * aWeights.w;
    vec3 p = (sp * World).xyz;
    vec4 ln = vec4(aNormal, 0.0);
    vec3 sn = (ln * b0).xyz * aWeights.x + (ln * b1).xyz * aWeights.y + (ln * b2).xyz * aWeights.z + (ln * b3).xyz * aWeights.w;
    vNormal = normalize((vec4(sn, 0.0) * World).xyz);
#else
    vec3 p = aPos.xyz;
    vNormal = aNormal;
#endif
    vWorld = p;
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
    vec3 geomN = normalize(vNormal);
    vec3 n = geomN;
    vec3 l = normalize(-SunDir);
    vec3 v = normalize(CameraPos - vWorld);
    float sh = ShadowAt(vWorld + n * 0.06) * CloudAt(vWorld);

    // Unpack surface pattern ID (0..4) and metallic weight (0..1) from constant-per-triangle aColor.a.
    float aInt = floor(vColor.a * 255.0 + 0.5);
    float patId = floor(aInt / 16.0);
    float metal = mod(aInt, 16.0) / 15.0;

    // Subtle distance-faded surface micro-detail (kept gentle so crisp low-poly planes stay clean, never noisy static).
    float dist = length(CameraPos - vWorld);
    float detailFade = 1.0 - smoothstep(10.0, 26.0, dist);

    vec3 albedo = vColor.rgb;
    float specBoost = 1.0;

    if (HasUnitTex > 0.5)
    {
#ifdef SKINNED
        vec2 bumpUV = vUV;
        vec3 texCol = texture2D(UnitTex, bumpUV).rgb;
        albedo *= texCol;
#else
        vec2 bumpUV = vUV;
        bool isClothPat = (patId >= 2.5 && patId < 3.5) || patId >= 10.5;
        if (detailFade > 0.01)
        {
            // Sample crisp 32x32 PointClamp pixel-art tile; keep cloth subtle so cel-shaded planes read cleanly.
            vec2 tile = vec2(mod(floor(patId + 0.5), 4.0), floor((patId + 0.5) / 4.0));
            bumpUV = (tile + fract(vUV * 2.4)) * vec2(0.25, 0.25);
            vec3 tex = texture2D(UnitTex, bumpUV).rgb;
            float texStr = isClothPat ? 0.26 : 0.68;
            albedo *= mix(vec3(1.0), tex, detailFade * texStr);
        }
#endif
        // Tangent-free normal mapping from UnitTex luminance gradient (chainmail, scales, fur, plate seams)
        if (detailFade > 0.02)
        {
            float eps = 1.0 / 128.0;
            float hC = dot(texture2D(UnitTex, bumpUV).rgb, vec3(0.3, 0.59, 0.11));
            float hX = dot(texture2D(UnitTex, bumpUV + vec2(eps, 0.0)).rgb, vec3(0.3, 0.59, 0.11));
            float hY = dot(texture2D(UnitTex, bumpUV + vec2(0.0, eps)).rgb, vec3(0.3, 0.59, 0.11));
            vec3 dpdx = dFdx(vWorld);
            vec3 dpdy = dFdy(vWorld);
            vec2 duvdx = dFdx(bumpUV);
            vec2 duvdy = dFdy(bumpUV);
            vec3 r1 = cross(dpdy, n);
            vec3 r2 = cross(n, dpdx);
            float det = dot(dpdx, r1);
            if (abs(det) > 1e-6)
            {
                float bumpStr = 0.24;
#ifndef SKINNED
                if (isClothPat) bumpStr = 0.07;
#endif
                vec2 dh = vec2(hX - hC, hY - hC) * (bumpStr * detailFade);
                vec3 surfGrad = sign(det) * (dh.x * (r1 * duvdx.x + r2 * duvdy.x) + dh.y * (r1 * duvdx.y + r2 * duvdy.y));
                n = normalize(abs(det) * n - surfGrad);
            }
        }
    }

    float ndotl = dot(n, l);
    // 4-band stepped cel shading (driven by normal-mapped n)
    float rawLit = clamp((ndotl * 0.78 + 0.22) * mix(0.40, 1.0, sh), 0.0, 1.0);
    float band = 0.18;
    band += smoothstep(0.22, 0.26, rawLit) * 0.28;
    band += smoothstep(0.50, 0.54, rawLit) * 0.30;
    band += smoothstep(0.76, 0.80, rawLit) * 0.24;

    if (detailFade > 0.01 && patId > 0.5 && patId < 5.5)
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
        else if (patId < 4.5)
        {
            // Pattern 4: Clean brushed steel / wood sheen.
            float grain = sin(vUV.x * 28.0 + vUV.y * 4.0) * 0.5 + 0.5;
            albedo *= 1.0 + (grain - 0.5) * 0.04 * detailFade;
        }
        else
        {
            // Pattern 5: Tufted fur / hair streaks (crisp directional pixel-art fur texture).
            vec2 p = vUV * vec2(14.0, 7.0);
            float row = floor(p.y);
            p.x += sin(row * 12.9898) * 0.5;
            float tuft = abs(fract(p.x) - 0.5) * 2.0;
            float tip = fract(p.y);
            float furShade = (1.0 - tuft * 0.14) * (0.90 + 0.14 * tip);
            albedo *= mix(1.0, furShade, detailFade);
        }
    }

    // Sun-coupled biome ground bounce: undersides pick up sunlit ground/snow color instead of static brown.
    float sunUp = clamp(-SunDir.y * 1.4, 0.0, 1.0);
    vec3 groundBounce = GroundColor * (0.48 + 0.28 * sunUp) + SunColor * vec3(0.10, 0.12, 0.06) * sunUp;
    groundBounce = mix(groundBounce, SkyColor * 0.85 + vec3(0.28, 0.32, 0.38) * sunUp, Look.x * 0.75);
    float hemi = n.y * 0.5 + 0.5;
    vec3 amb = mix(groundBounce, SkyColor * 0.64, hemi) * vec3(0.88, 0.93, 1.06);
    vec3 direct = SunColor * (band * 0.78);

    vec3 col = albedo * (amb + direct);

    // Crisp quantized metallic specular + rain-slicked wetness sheen (Look.w) on armor, cloth, and hides.
    float wet = Look.w;
    if (metal > 0.2 || wet > 0.01)
    {
        vec3 h = normalize(l + v);
        float spec = pow(max(dot(n, h), 0.0), mix(16.0, 22.0, step(0.2, metal))) * sh * specBoost;
        float specBand = smoothstep(0.34, 0.40, spec);
        float specWeight = max(metal * 0.52, wet * 0.34);
        col += mix(SunColor, SkyColor * 1.3, wet * 0.35) * mix(vec3(0.82, 0.88, 0.98), albedo * 1.15, 0.35) * specBand * specWeight;
    }

    // Stepped rim light on sun-grazing edges (gives crisp silhouette separation at dusk, night, and noon).
    float rim = pow(1.0 - max(dot(n, v), 0.0), 2.6) * max(ndotl * 0.5 + 0.5, 0.0) * sh;
    col += (SunColor * 0.55 + SkyColor * 0.45) * smoothstep(0.48, 0.56, rim) * 0.20;

    // Night torches & moving spell glows.
    col += albedo * (TorchLight(vWorld) + GlowLight(vWorld));

    // Distance fog matching world.glsl.
    float fog = clamp((dist - FogParams.x) / max(1.0, FogParams.y - FogParams.x), 0.0, 1.0);
    col = mix(col, FogColor, fog * FogParams.z);

    // RT0: lit cel-shaded color.
    gl_FragData[0] = vec4(clamp(col, 0.0, 1.0), 1.0);

    // RT1: unperturbed geometry view-space normal (RGB in 0..1) + linear depth, so outline detection stays crisp without texture bump noise.
    vec3 vn = normalize((vec4(geomN, 0.0) * View).xyz);
    float viewZ = max(0.1, -(vec4(vWorld, 1.0) * View).z);
    float encDepth = clamp(1.0 / (1.0 + viewZ * 0.065), 0.02, 1.0);
    gl_FragData[1] = vec4(vn * 0.5 + 0.5, encDepth);
#endif
}
#endif
