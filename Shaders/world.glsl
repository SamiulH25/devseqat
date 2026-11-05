// Terrain and foliage. Techniques define TERRAIN or FOLIAGE, plus SHADOW for the sun's depth pass.
// Foliage vertices carry (sway, leaf) in their UV: sway = how far wind moves it, leaf = how leafy (soft, translucent) it is.
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
#ifdef FOLIAGE
    // Wind: two waves travelling over the field, slow gusts, and a steady lean (Wind.w), scaled by sway.
    float phase = dot(p.xz, vec2(0.13, 0.17));
    float gust = 0.6 + 0.4 * sin(Time * 0.35 + p.x * 0.01);
    float w = (sin(Time * 1.9 + phase) * 0.6 + sin(Time * 3.7 + phase * 2.3) * 0.25) * gust;
    p.xz += Wind.xy * (w + Wind.w) * Wind.z * aUV.x;
    p.y -= abs(w) * Wind.z * aUV.x * 0.25;
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
#ifdef TERRAIN
    albedo *= texture2D(Detail, vUV).rgb * 1.05;
#endif
    vec3 n = normalize(vNormal);
    vec3 l = -SunDir;
    float ndl = dot(n, l);
    float diffuse = max(ndl, 0.0);
#ifdef FOLIAGE
    diffuse = mix(diffuse, ndl * 0.5 + 0.5, vUV.y * 0.6); // leaves scatter light: softer terminator
#endif
    float shadow = ShadowAt(vWorld + n * ShadowParams.w);
    vec3 ambient = mix(GroundColor, SkyColor, n.y * 0.5 + 0.5);
    vec3 col = albedo * (ambient + SunColor * diffuse * shadow);
#ifdef FOLIAGE
    // Backlit leaves glow.
    vec3 toCam = normalize(CameraPos - vWorld);
    col += albedo * SunColor * pow(max(dot(-toCam, l), 0.0), 4.0) * vUV.y * 0.6 * shadow;
#endif
    gl_FragColor = vec4(Fog(col, vWorld), 1.0);
#endif
}
#endif
