// Unit sprites (pixel-art puppets): alpha-tested, lit by the sun and sky, shadowed, fogged.
// SHADOW: the sun's depth pass (the puppet is rebuilt to face the sun for it, so shadows have the right silhouette).
varying vec4 vColor;
varying vec2 vUV;
varying vec3 vWorld;
varying vec4 vLight;

#ifdef VERTEX
attribute vec4 aPos;
attribute vec4 aColor;
attribute vec2 aUV;

void main()
{
    vColor = aColor;
    vUV = aUV;
    vWorld = aPos.xyz;
#ifdef SHADOW
    gl_Position = aPos * LightViewProj;
    vLight = gl_Position;
#else
    vLight = vec4(0.0);
    gl_Position = aPos * ViewProj;
#endif
    FIXUP_POSITION
}
#endif

#ifdef FRAGMENT
void main()
{
#ifdef SOFT
    // Soft motes (dust, smoke): a round puff with a feathered, ragged edge, blended over the scene (UV = 0..1 across the quad).
    vec2 d = vUV - 0.5;
    float edge = length(d) * 2.0 + (Noise2(vUV * 3.0) - 0.5) * 0.35;
    float a = vColor.a * (1.0 - smoothstep(0.25, 1.0, edge));
    if (a < 0.01) discard;
    float sh = ShadowAt(vWorld - SunDir * ShadowParams.w) * CloudAt(vWorld);
    vec3 sl = max(SkyColor * SpriteLight.x * 1.1 + SunColor * SpriteLight.y * sh + TorchLight(vWorld) + GlowLight(vWorld), vec3(0.0));
    gl_FragColor = vec4(Fog(vColor.rgb * sl, vWorld) * a, a);
    return;
#endif
    vec4 t = texture2D(Atlas, vUV);
    if (t.a < 0.5) discard;
#ifdef SHADOW
    gl_FragColor = vec4(vLight.z / vLight.w, 0.0, 0.0, 1.0);
#else
    // Sprites carry their own painted shading; the scene light only sets how bright they are overall.
    // The shadow lookup is pulled toward the sun so a man is not shadowed by his own sun-facing silhouette.
    float shadow = ShadowAt(vWorld - SunDir * ShadowParams.w) * CloudAt(vWorld);
    vec3 light = SkyColor * SpriteLight.x + SunColor * SpriteLight.y * shadow;
    // At night and in rain men catch the moon and the wet sheen more than the grass does, so they stand out from the
    // dark field: a cool lift, strongest on their bright parts (steel, faces, tabards).
    light += TorchLight(vWorld);
    light = max(light + GlowLight(vWorld + vec3(0.0, 0.6, 0.0)) * 1.3, vec3(0.0)); // spell light catches men more than grass
    float lum = dot(t.rgb, vec3(0.3, 0.55, 0.15));
    light += vec3(0.16, 0.19, 0.27) * (Look.z * 0.9 + Look.w * 0.35) * (0.6 + lum * 0.8);
    // Vertex alpha below 1 marks the fallen: their colours drain toward the earth so the dead sink into the field.
    vec3 base = t.rgb * vColor.rgb;
    base = mix(base, vec3(dot(base, vec3(0.3, 0.55, 0.15))) * vec3(1.04, 0.96, 0.84), 1.0 - vColor.a);
    gl_FragColor = vec4(Fog(base * light, vWorld), 1.0);
#endif
}
#endif
