// Real-time 3D Pixel Art unit shader (spike/3d-pixel-art).
// Techniques:
//   Unit3D       - stepped 4-band cel-shaded color to RT0 + view-space normal & linear depth to RT1 (MRT)
//   Unit3DShadow - writes depth into the sun's shadow map (real 3D directional shadows!)
//
// Vertex layout: VertexPNCT (aPos, aNormal, aColor, aUV).
//   aColor.rgb = resolved material albedo * baked AO
//   aUV.x      = metallic/specular weight (0 = matte cloth/skin/coat, 1 = gleaming steel/brass)
//   aUV.y      = unit ID hash (0..1) to help separate closely-packed soldiers of similar depth if needed
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

    // 4-band stepped cel shading (vibrant, painterly tonal steps — never crushed black in shadow!).
    // Gentle wrap on ndotl so faces under helms/hoods and round limbs read crisply from every angle.
    float rawLit = clamp((ndotl * 0.72 + 0.28) * mix(0.45, 1.0, sh), 0.0, 1.0);
    float band = 0.18;
    band += smoothstep(0.22, 0.26, rawLit) * 0.26;
    band += smoothstep(0.48, 0.52, rawLit) * 0.30;
    band += smoothstep(0.75, 0.79, rawLit) * 0.28;

    // Warm/cool split-tone ambient: shadow side stays rich cool-sky lit, sunlit side glows warm.
    float hemi = n.y * 0.5 + 0.5;
    vec3 amb = mix(GroundColor * 0.85, SkyColor * 1.05, hemi) * vec3(0.92, 0.96, 1.06);
    vec3 direct = SunColor * (band * 1.15);

    vec3 albedo = vColor.rgb;
    vec3 col = albedo * (amb + direct);

    // Crisp quantized metallic specular glint on steel/plate/brass (helm domes, breastplates, blades, chanfrons).
    float metal = vUV.x;
    if (metal > 0.2)
    {
        vec3 h = normalize(l + v);
        float spec = pow(max(dot(n, h), 0.0), 16.0) * sh;
        float specBand = smoothstep(0.28, 0.34, spec);
        col += SunColor * mix(vec3(0.76, 0.82, 0.92), albedo * 1.35, 0.45) * specBand * metal * 0.78;
    }

    // Subtle stepped rim light on the sun-grazing silhouette so dark armor & black horses separate cleanly from grass.
    float rim = pow(1.0 - max(dot(n, v), 0.0), 2.6) * max(ndotl * 0.5 + 0.5, 0.0) * sh;
    col += (SunColor * 0.55 + SkyColor * 0.45) * smoothstep(0.48, 0.55, rim) * 0.22;

    // Night torches & moving spell glows.
    col += albedo * (TorchLight(vWorld) + GlowLight(vWorld));

    // Distance fog matching world.glsl.
    float dist = length(CameraPos - vWorld);
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
