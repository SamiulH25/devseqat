// Terrain and foliage. Techniques define TERRAIN or FOLIAGE, plus SHADOW for the sun's depth pass.
// Foliage vertices carry (sway, leaf) in their UV: sway = how far wind moves it, leaf = how leafy (soft, translucent) it is.
varying vec3 vWorld;
varying vec3 vNormal;
varying vec4 vColor;
varying vec2 vUV;
varying vec4 vLight;
varying float vTrample;

#ifdef VERTEX
attribute vec4 aPos;
attribute vec3 aNormal;
attribute vec4 aColor;
attribute vec2 aUV;

void main()
{
    vec3 p = aPos.xyz;
#ifdef FOLIAGE
    // Wind: two waves travelling over the field, slow gusts, and a steady lean (Wind.w), scaled by sway.
    float phase = dot(p.xz, vec2(0.13, 0.17));
    float gust = 0.6 + 0.4 * sin(Time * 0.35 + p.x * 0.01);
    float w = (sin(Time * 1.9 + phase) * 0.6 + sin(Time * 3.7 + phase * 2.3) * 0.25) * gust;
    p.xz += Wind.xy * (w + Wind.w) * Wind.z * aUV.x;
    p.y -= abs(w) * Wind.z * aUV.x * 0.25;
    // Grass where many men have walked lies flattened (the ground map's red channel; blades only: lit straight up).
    vTrample = 0.0;
    if (Field.y > 0.5 && aNormal.y > 0.999 && aUV.y > 0.99)
    {
        vTrample = texture2DLod(GroundMap, p.xz * Field.x + 0.5, 0.0).r;
        p.y -= vTrample * 0.32 * aUV.x;
        p.xz += Wind.xy * vTrample * 0.22 * aUV.x;
    }
#else
    vTrample = 0.0;
#endif
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
    vec3 albedo = vColor.rgb;
    vec3 n = normalize(vNormal);
    float puddle = 0.0;
    float leaf = max(vUV.y, 0.0);
#ifdef FOLIAGE
    if (vUV.y < -1.5) // a torch flame: unlit, flickering
    {
        float fl = 0.85 + 0.25 * sin(Time * 17.0 + vWorld.x * 3.0) * sin(Time * 9.0 + vWorld.y * 5.0);
        gl_FragColor = vec4(Fog(vColor.rgb * fl * 1.25, vWorld), 1.0);
        return;
    }
    if (vUV.y < -0.5) albedo *= Masonry(vWorld, n, length(vWorld - CameraPos));
    // Command view: leaves on the line of sight to men below are thinned away (dithered), so they show through the wood.
    // The ground map's green channel marks the men; look down the view ray from this leaf to the ground for any.
    if (Cut > 0.0 && Field.y > 0.5 && leaf > 0.05 && n.y < 0.999)
    {
        vec3 r = normalize(vWorld - CameraPos);
        if (r.y < -0.15)
        {
            float men = 0.0;
            for (int i = 0; i < 5; i++)
            {
                vec3 q = vWorld + r * (float(i) * 2.2 / -r.y);
                men = max(men, texture2D(GroundMap, q.xz * Field.x + 0.5).g);
            }
            if (Hash2(floor(gl_FragCoord.xy)) < men * Cut * 0.88) discard;
        }
    }
#endif
#ifdef TERRAIN
    albedo *= texture2D(Detail, vUV).rgb * 1.05;
    // Close up, the ground breaks into mottled grass and patches of clover and dry stalks (faded in within ~40 m).
    float eyeDist = length(vWorld - CameraPos);
    float near = Look.y * (1.0 - smoothstep(20.0, 55.0, eyeDist));
    if (near > 0.0)
    {
        float m = Noise2(vWorld.xz * 3.1) * 0.6 + Noise2(vWorld.xz * 9.0) * 0.4;
        albedo *= 1.0 + (m - 0.5) * 0.45 * near;
        // At your feet: blade-sized speckle, specks of bare soil and tiny flowers (only very close: finer would shimmer).
        float feet = Look.y * (1.0 - smoothstep(5.0, 20.0, eyeDist));
        if (feet > 0.0)
        {
            float fine = Noise2(vWorld.xz * 27.0) * 0.55 + Noise2(vWorld.xz * 61.0 + 5.0) * 0.45;
            albedo *= 1.0 + (fine - 0.5) * 0.55 * feet;
            albedo = mix(albedo, vec3(0.3, 0.25, 0.17), smoothstep(0.8, 0.88, Noise2(vWorld.xz * 6.5 + 2.0)) * 0.45 * feet);
            float fleck = smoothstep(0.9, 0.95, Noise2(vWorld.xz * 15.0 + 17.0)) * smoothstep(0.55, 0.7, Noise2(vWorld.xz * 0.6 + 4.0));
            albedo = mix(albedo, mix(vec3(0.95, 0.88, 0.4), vec3(0.95, 0.95, 0.92), step(0.5, Hash2(floor(vWorld.xz * 15.0)))), fleck * 0.7 * feet);
        }
        float patch = Noise2(vWorld.xz * 0.35 + 3.0);
        albedo = mix(albedo, albedo * vec3(1.1, 1.05, 0.78), smoothstep(0.6, 0.78, patch) * 0.7 * near);
        albedo = mix(albedo, albedo * vec3(0.82, 0.95, 0.8), smoothstep(0.62, 0.8, 1.0 - patch) * 0.6 * near);
    }
    // Trampled earth where the fighting has been.
    if (Field.y > 0.5) albedo = mix(albedo, vec3(0.34, 0.28, 0.2), texture2D(GroundMap, vWorld.xz * Field.x + 0.5).r * 0.5);
    // Rain: standing water in the low flat places.
    if (Field.w > 0.0) puddle = Field.w * smoothstep(0.8, 0.86, Noise2(vWorld.xz * 0.28 + 11.0) * 0.7 + Noise2(vWorld.xz * 1.3) * 0.3) * smoothstep(0.97, 0.995, n.y);
#endif
#ifdef FOLIAGE
    albedo = mix(albedo, albedo * vec3(0.78, 0.74, 0.55), vTrample * 0.6); // bruised, flattened blades
#endif
    albedo *= 1.0 - Field.w * 0.22; // wet ground and leaves are darker
    albedo = mix(albedo, vec3(0.9, 0.92, 0.96), Look.x * smoothstep(0.5, 0.85, n.y) * 0.85); // snow lies on what faces up
    vec3 l = -SunDir;
    float ndl = dot(n, l);
    float diffuse = max(ndl, 0.0);
#ifdef FOLIAGE
    diffuse = mix(diffuse, ndl * 0.5 + 0.5, leaf * 0.6); // leaves scatter light: softer terminator
#endif
    float shadow = ShadowAt(vWorld + n * ShadowParams.w) * CloudAt(vWorld);
    vec3 ambient = mix(GroundColor, SkyColor, n.y * 0.5 + 0.5);
    vec3 col = albedo * max(ambient + SunColor * diffuse * shadow + TorchLight(vWorld + n * 0.3) + GlowLight(vWorld + n * 0.3), vec3(0.0));
    if (Field.w > 0.0)
    {
        // A wet sheen toward the sun, and puddles that mirror the sky.
        vec3 v = normalize(CameraPos - vWorld);
        float spec = pow(max(dot(reflect(-l, n), v), 0.0), 20.0);
        col += SunColor * spec * Field.w * (0.18 + puddle * 0.8) * shadow;
        col = mix(col, mix(col * 0.45, FogColor * 0.7, 0.5), puddle * 0.6);
    }
#ifdef FOLIAGE
    // Rivers (flat, blue, no UVs) ripple and glint as the water moves.
    if (vUV.x + vUV.y < 0.001 && vUV.y > -0.5 && n.y > 0.99 && vColor.b - vColor.r > 0.12)
    {
        float rip = Noise2(vWorld.xz * 0.7 + vec2(Time * 0.45, Time * 0.3)) * 0.6 + Noise2(vWorld.xz * 2.3 - vec2(Time * 0.2, Time * 0.6)) * 0.4;
        col *= 0.9 + rip * 0.22;
        vec3 vw = normalize(CameraPos - vWorld);
        vec3 rn = normalize(vec3((rip - 0.5) * 0.5, 1.0, (Noise2(vWorld.xz * 1.1 + Time * 0.35) - 0.5) * 0.5));
        col += SunColor * pow(max(dot(reflect(-l, rn), vw), 0.0), 40.0) * 0.5 * shadow;
    }
    // Backlit leaves glow.
    vec3 toCam = normalize(CameraPos - vWorld);
    col += albedo * SunColor * pow(max(dot(-toCam, l), 0.0), 4.0) * leaf * 0.6 * shadow;
#endif
    gl_FragColor = vec4(Fog(col, vWorld), 1.0);
#endif
}
#endif
