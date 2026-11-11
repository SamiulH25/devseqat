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
    float sway = aUV.x < 50.0 ? aUV.x : 0.0; // field patches carry 100+ in x (World.Land.cs): they never sway
    p.xz += Wind.xy * (w + Wind.w) * Wind.z * sway;
    p.y -= abs(w) * Wind.z * sway * 0.25;
    // Grass where many men have walked lies flattened (the ground map's red channel; blades only: lit straight up).
    vTrample = 0.0;
    if (Field.y > 0.5 && aNormal.y > 0.999 && aUV.y > 0.99)
    {
        vTrample = texture2DLod(GroundMap, p.xz * Field.x + 0.5, 0.0).r;
        p.y -= vTrample * 0.32 * aUV.x;
        p.xz += Wind.xy * vTrample * 0.22 * aUV.x;
    }
    // Grass right at the lens would fill the view as big flat spikes: inside ~5 m the blade tips sink into the ground.
    if (aNormal.y > 0.999 && aUV.y > 0.99 && aUV.x > 0.0)
        p.y -= (1.0 - smoothstep(1.5, 5.0, length(p - CameraPos))) * 0.6;
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
#ifndef SHADOW
// The campaign map's land colour (the grounded look): olive rather than lime, drifting in hue across the country, in
// patches tens of metres across (lush hollows, sun-bleached rises), with grass texture. Shared by the ground and the
// field plots laid on it, so a plot's feathered edge meets the meadow in the same colour.
vec3 MapLand(vec3 c, vec2 w)
{
    vec4 fine = texture2D(Detail, w * 0.12), mid = texture2D(Detail, w * 0.031 + vec2(0.37, 0.61));
    float broad = texture2D(Detail, w * 0.0035).a;
    vec3 land = c * mix(vec3(0.93, 1.0, 0.9), vec3(1.07, 1.0, 0.88), broad);
    land = mix(land, vec3(dot(land, vec3(0.3, 0.55, 0.15))) * vec3(0.95, 1.0, 0.7), 0.3);
    vec4 patchy = texture2D(Detail, w * 0.0105 + vec2(0.13, 0.71));
    float dry = smoothstep(0.38, 0.68, patchy.a * 0.7 + broad * 0.3);
    land = mix(land * vec3(0.84, 0.96, 0.82), land * vec3(1.12, 1.04, 0.74), dry);
    land *= 0.86 + 0.28 * patchy.r;
    return land * (0.8 + 0.4 * mix(fine.r, mid.r, 0.35));
}
#endif
void main()
{
#ifdef SHADOW
    gl_FragColor = vec4(vLight.z / vLight.w, 0.0, 0.0, 1.0);
#else
    vec3 albedo = vColor.rgb;
    vec3 n = normalize(vNormal);
    float puddle = 0.0;
    float fieldTag = step(50.0, vUV.x); // a field patch: x = 100 + metres across its rows, y = how strongly they show
    float leaf = max(vUV.y, 0.0) * (1.0 - fieldTag);
#ifdef FOLIAGE
    if (vUV.y < -9.5) // water (World.Water.cs): rivers carry x = across (-1..1), y = -10 - metres along; still water x = 5
    {
        float still = step(2.0, vUV.x);
        float along = -10.0 - vUV.y;
        vec2 q = mix(vec2(along * 0.45 - Time * 0.9, vUV.x * 1.6), vWorld.xz * 0.45 + vec2(Time * 0.05, Time * 0.04), still);
        float rip = Noise2(q) * 0.6 + Noise2(q * 2.7 + vec2(-Time * 0.3, 3.0)) * 0.4;
        float rad = (vUV.x - 5.0) / 1.12; // still water: 0 at the middle, 1 at the shore
        float edge = mix(abs(vUV.x), smoothstep(0.55, 1.0, rad), still);
        vec3 wc = vColor.rgb * mix(vec3(1.0), vec3(1.22, 1.18, 1.04), smoothstep(0.5, 1.0, edge)); // paler shallows by the banks
        wc = mix(vec3(dot(wc, vec3(0.299, 0.587, 0.114))), wc, Palette.x) * Palette.y;
        vec3 wl = -SunDir;
        vec3 rn = normalize(vec3((rip - 0.5) * 0.5, 1.0, (Noise2(q * 1.3 + 7.0) - 0.5) * 0.5));
        vec3 vw = normalize(CameraPos - vWorld);
        float wsh = ShadowAt(vWorld + vec3(0.0, ShadowParams.w, 0.0)) * CloudAt(vWorld);
        vec3 wamb = SkyColor;
        vec3 wcol = wc * (wamb + SunColor * max(wl.y, 0.0) * wsh) * (0.9 + rip * 0.2);
        float fres = pow(1.0 - max(dot(vw, rn), 0.0), 3.0);
        wcol = mix(wcol, FogColor * 0.85, fres * 0.55);                                                   // the sky in it
        wcol += SunColor * pow(max(dot(reflect(-wl, rn), vw), 0.0), 60.0) * 0.6 * wsh;                     // sun glint
        float streak = (1.0 - smoothstep(0.0, 0.4, abs(vUV.x))) * smoothstep(0.45, 0.75, Noise2(vec2(along * 0.06 - Time * 0.3, vUV.x * 0.7))) * (1.0 - still);
        wcol += (wamb + SunColor * 0.5) * vec3(0.13, 0.15, 0.16) * streak;                                // the bright thread of the current
        float foam = smoothstep(0.8, 1.0, edge) * smoothstep(0.45, 0.75, Noise2(q * 3.0 + 1.0)) * (1.0 - still);
        wcol = mix(wcol, vec3(0.86, 0.88, 0.86) * (wamb + SunColor * 0.6), foam * 0.45);                  // foam at the banks
        wcol += vec3(0.25) * Field.w * smoothstep(0.9, 0.97, Noise2(vWorld.xz * 6.0 + vec2(Time * 3.1, -Time * 2.3))); // rain pocks
        // Ice in winter: a cold blue sheet, cracked, darker toward a rim at the banks so it does not melt into the snow.
        float crack = 1.0 - smoothstep(0.0, 0.02, abs(Noise2(vWorld.xz * 0.3) - 0.5));
        vec3 ice = vec3(0.6, 0.72, 0.84) * (1.0 - 0.25 * crack) * mix(1.0, 0.72, smoothstep(0.6, 1.0, edge));
        wcol = mix(wcol, ice * (wamb + SunColor * max(wl.y, 0.0) * wsh), Look.x * 0.85);
        gl_FragColor = vec4(Fog(wcol, vWorld), 1.0);
        return;
    }
    if (vUV.y < -2.5 && vUV.y > -3.5) // a window (World.Windows): dark glass by day, lamplight in most at night
    {
        float lit = smoothstep(0.15, 0.55, Look.z) * step(Hash2(floor(vWorld.xz * 1.3)), 0.7);
        if (lit > 0.01)
        {
            vec3 lamp = vec3(1.0, 0.7, 0.34) * (1.5 + 0.15 * sin(Time * 2.3 + vWorld.x * 7.0));
            gl_FragColor = vec4(Fog(mix(vColor.rgb * 0.4, lamp, lit), vWorld), 1.0);
            return;
        }
    }
    else if (vUV.y < -1.5) // a torch flame: unlit, flickering
    {
        float fl = 0.85 + 0.25 * sin(Time * 17.0 + vWorld.x * 3.0) * sin(Time * 9.0 + vWorld.y * 5.0);
        gl_FragColor = vec4(Fog(vColor.rgb * fl * 1.25, vWorld), 1.0);
        return;
    }
    if (vUV.y < -0.5 && vUV.y > -1.5) albedo *= Masonry(vWorld, n, length(vWorld - CameraPos));
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
    if (Field.y < 0.5) // the campaign map: grounded materials (World.Materials.cs) blended by slope over the land's colour
    {
        vec2 w = vWorld.xz;
        vec4 fine = texture2D(Detail, w * 0.12), mid = texture2D(Detail, w * 0.031 + vec2(0.37, 0.61));
        float broad = texture2D(Detail, w * 0.0035).a;
        float slope = 1.0 - n.y;
        vec3 grass = MapLand(vColor.rgb, w), land = grass;
        vec3 earth = mix(land, vec3(0.43, 0.35, 0.26), 0.6) * (0.78 + 0.44 * mix(fine.g, mid.g, 0.4));
        vec3 rock = mix(vec3(0.46, 0.44, 0.41), vec3(0.52, 0.48, 0.42), broad) * (0.66 + 0.6 * mix(fine.b, mid.b, 0.5));
        float wEarth = smoothstep(0.16, 0.3, slope + (mid.g - 0.5) * 0.12);
        float wRock = smoothstep(0.3, 0.5, slope + (mid.b - 0.5) * 0.18);
        albedo = mix(mix(grass, earth, wEarth), rock, wRock);
        albedo = mix(albedo, vColor.rgb, smoothstep(0.75, 0.9, dot(vColor.rgb, vec3(0.333)))); // snow and pale ground keep their own colour
        albedo *= mix(0.62, 1.0, vColor.a);                                                   // shade in the hollows
    }
    else albedo *= texture2D(Detail, vUV).rgb * 1.05;
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
    // The battlefield's own wear (WL6): bare, worn earth in patches and along sheep tracks, seen from any distance.
    if (Field.y > 0.5)
    {
        float worn = smoothstep(0.7, 0.8, Noise2(vWorld.xz * 0.045 + 21.0) * 0.75 + Noise2(vWorld.xz * 0.3) * 0.25);
        float track = (1.0 - smoothstep(0.0, 0.025, abs(Noise2(vWorld.xz * 0.012 + 5.0) - 0.5))) * smoothstep(0.4, 0.6, Noise2(vWorld.xz * 0.02 + 9.0));
        albedo = mix(albedo, vec3(0.4, 0.34, 0.25) * (0.85 + 0.3 * Noise2(vWorld.xz * 1.7)), max(worn * 0.45, track * 0.4) * smoothstep(0.85, 0.97, n.y) * (1.0 - Look.x));
    }
    // Trampled earth where the fighting has been.
    if (Field.y > 0.5) albedo = mix(albedo, vec3(0.34, 0.28, 0.2), texture2D(GroundMap, vWorld.xz * Field.x + 0.5).r * 0.5);
    // Rain: standing water in the low flat places.
    if (Field.w > 0.0) puddle = Field.w * smoothstep(0.8, 0.86, Noise2(vWorld.xz * 0.28 + 11.0) * 0.7 + Noise2(vWorld.xz * 1.3) * 0.3) * smoothstep(0.97, 0.995, n.y);
#endif
#ifdef FOLIAGE
    albedo = mix(albedo, albedo * vec3(0.78, 0.74, 0.55), vTrample * 0.6); // bruised, flattened blades
    if (Field.y < 0.5 && fieldTag > 0.5) // map fields: the edge takes the meadow's treatment, the crop keeps its colour
        albedo = mix(MapLand(albedo, vWorld.xz), albedo * (0.86 + 0.28 * texture2D(Detail, vWorld.xz * 0.12).g), smoothstep(0.0, 0.5, vColor.a));
    if (Field.y < 0.5 && leaf > 0.5 && fieldTag < 0.5) // map canopies: leaf clumps, darker underneath and inside
    {
        float clump = Noise2(vWorld.xz * 1.7 + vWorld.y * 2.3) * 0.6 + Noise2(vWorld.xz * 4.1 - vWorld.y * 3.7) * 0.4;
        albedo *= (0.74 + 0.5 * clump) * mix(0.62, 1.06, smoothstep(-0.7, 0.75, n.y));
    }
#endif
    // The look preset (docs/WORLD_LIFE.md WL0): Palette x = saturation, y = lightness, z = warmth of the land's own colours.
    albedo = mix(vec3(dot(albedo, vec3(0.299, 0.587, 0.114))), albedo, Palette.x) * Palette.y;
    albedo *= vec3(1.0 + Palette.z * 0.07, 1.0 + Palette.z * 0.02, 1.0 - Palette.z * 0.09);
    albedo *= 1.0 - Field.w * 0.22; // wet ground and leaves are darker
    float trodden = step(-0.4, vUV.y) * step(vUV.y, -0.1); // roads (World.Water.cs `Trodden`)
    albedo = mix(albedo, vec3(0.9, 0.92, 0.96), Look.x * smoothstep(0.5, 0.85, n.y) * 0.85 * (1.0 - 0.8 * trodden)); // snow lies on what faces up, little on roads
    albedo *= 1.0 - Look.x * trodden * 0.3; // trodden snow: wet dark slush
#ifdef FOLIAGE
    if (fieldTag > 0.5) // furrows and crop rows, fading out with distance before they could shimmer
    {
        float rowd = length(vWorld - CameraPos);
        albedo *= 1.0 + sin((vUV.x - 100.0) * 4.49) * 0.12 * vUV.y * (1.0 - smoothstep(35.0, 110.0, rowd)) * (1.0 - Look.x); // only up close; snow hides them
    }
#endif
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
    // Backlit leaves glow.
    vec3 toCam = normalize(CameraPos - vWorld);
    col += albedo * SunColor * pow(max(dot(-toCam, l), 0.0), 4.0) * leaf * 0.6 * shadow;
#endif
    gl_FragColor = vec4(Fog(col, vWorld), 1.0);
#endif
}
#endif
