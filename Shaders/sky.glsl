// Sky dome: horizon-to-zenith gradient, sun disc and glow, and drifting procedural clouds.
varying vec3 vDir;

#ifdef VERTEX
attribute vec4 aPos;

void main()
{
    vDir = aPos.xyz;
    gl_Position = aPos * ViewProj; // ViewProj here includes the camera translation (the dome follows the eye)
    FIXUP_POSITION
}
#endif

#ifdef FRAGMENT
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p)
{
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm(vec2 p)
{
    float s = 0.0, a = 0.5;
    for (int i = 0; i < 5; i++) { s += a * noise(p); p *= 2.03; a *= 0.5; }
    return s;
}

void main()
{
    vec3 d = normalize(vDir);
    float h = d.y;
    vec3 col = mix(Horizon, Zenith, pow(clamp(h, 0.0, 1.0), 0.55));
    float sd = max(dot(d, -SunDir), 0.0);
    col += SunColor * (pow(sd, 900.0) * 6.0 + pow(sd, 24.0) * 0.18 + pow(sd, 4.0) * 0.05);
    if (h > 0.0)
    {
        vec2 uv = d.xz / (h + 0.12) * 0.9 + Time * vec2(0.006, 0.002);
        float c = smoothstep(0.5, 0.82, fbm(uv * 1.4)) * smoothstep(0.0, 0.18, h);
        vec3 cloud = mix(vec3(1.0, 0.99, 0.96), Horizon * 0.8, smoothstep(0.5, 0.9, fbm(uv * 1.4 + 0.37)));
        cloud += SunColor * pow(sd, 6.0) * 0.35;
        col = mix(col, cloud, c * 0.85);
    }
    gl_FragColor = vec4(min(col, vec3(1.0)), 1.0);
}
#endif
