// Sky dome: horizon-to-zenith gradient, sun/moon disc, twinkling night stars, distant mountain & hill silhouettes, and drifting clouds.
// Swirl (docs/archive/SPECTACLE.md S3): x = how hard the clouds turn round the zenith, y = how much more of the sky they cover.
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

float ridgeNoise(float x)
{
    float r1 = 1.0 - abs(noise(vec2(x * 1.4, 2.1)) * 2.0 - 1.0);
    float r2 = 1.0 - abs(noise(vec2(x * 3.1, 7.3)) * 2.0 - 1.0);
    float r3 = noise(vec2(x * 7.5, 13.7));
    return r1 * 0.62 + r2 * 0.28 + r3 * 0.10;
}

void main()
{
    vec3 d = normalize(vDir);
    float h = d.y;
    float night = clamp(1.0 - dot(Zenith, vec3(0.3, 0.59, 0.11)) * 4.5, 0.0, 1.0);
    vec3 col = mix(Horizon, Zenith, pow(clamp(h, 0.0, 1.0), 0.55));

    // Twinkling stars at night (above horizon, before clouds & mountains)
    if (night > 0.05 && h > 0.02)
    {
        float az = atan(d.z, d.x);
        vec2 starUV = vec2(az * 42.0, asin(clamp(h, 0.0, 1.0)) * 52.0);
        vec2 cell = floor(starUV);
        vec2 f = fract(starUV) - 0.5;
        float rnd = hash(cell);
        if (rnd > 0.962)
        {
            float dist = length(f);
            float star = smoothstep(0.28, 0.05, dist);
            float twink = 0.65 + 0.35 * sin(Time * (1.8 + rnd * 4.0) + rnd * 62.8);
            vec3 starCol = mix(vec3(0.82, 0.90, 1.0), vec3(1.0, 0.94, 0.80), fract(rnd * 31.7));
            col += starCol * star * twink * night * smoothstep(0.02, 0.22, h) * 0.95;
        }
    }

    // Sun (day/dusk) or Moon (night) disc + corona
    float sd = max(dot(d, -SunDir), 0.0);
    float disc = smoothstep(0.9991, 0.99945, sd);
    if (night < 0.5)
    {
        col += SunColor * (disc * 2.8 + pow(sd, 900.0) * 4.5 + pow(sd, 24.0) * 0.20 + pow(sd, 4.0) * 0.06);
    }
    else
    {
        // Moon with subtle surface craters & silver-blue halo
        float mNoise = noise(d.xy * 110.0 + d.zx * 90.0);
        vec3 moonCol = vec3(0.90, 0.94, 1.0) * (0.84 + 0.16 * mNoise);
        col = mix(col, moonCol, disc * 0.95);
        col += vec3(0.32, 0.42, 0.64) * (pow(sd, 90.0) * 0.35 + pow(sd, 12.0) * 0.08);
    }

    // Drifting clouds
    float cloudMask = 0.0;
    if (h > 0.0)
    {
        vec2 uv = d.xz / (h + 0.12) * 0.9;
        if (Swirl.x > 0.0) // a tempest: the clouds turn round the zenith, faster near it
        {
            float a = Swirl.x * (2.5 / (1.0 + length(uv)) + Time * 0.35);
            uv = mat2(cos(a), -sin(a), sin(a), cos(a)) * uv;
        }
        uv += Time * vec2(0.006, 0.002);
        cloudMask = smoothstep(0.5 - 0.25 * Swirl.y, 0.82 - 0.2 * Swirl.y, fbm(uv * 1.4)) * smoothstep(0.0, 0.18, h);
        vec3 cloudDay = mix(vec3(1.0, 0.99, 0.96), Horizon * 0.8, smoothstep(0.5, 0.9, fbm(uv * 1.4 + 0.37)));
        vec3 cloudNight = mix(Zenith * 1.8 + vec3(0.05, 0.06, 0.10), Horizon * 0.75, 0.5);
        vec3 cloud = mix(cloudDay, cloudNight, night);
        cloud += SunColor * pow(sd, 6.0) * 0.35;
        col = mix(col, cloud, cloudMask * 0.85);
    }

    // Layered distant mountains & rolling hills along the horizon
    if (h > -0.04 && h < 0.16)
    {
        float az = atan(d.z, d.x);
        float mtnH = 0.015 + ridgeNoise(az * 2.8) * 0.078;
        float hillH = 0.004 + noise(vec2(az * 4.5 + 3.1, 1.2)) * 0.036;
        float sunSide = max(dot(normalize(vec3(d.x, 0.0, d.z)), normalize(vec3(-SunDir.x, 0.0, -SunDir.z))), 0.0);

        if (h < mtnH)
        {
            float peak = clamp((h - 0.015) / max(0.01, mtnH - 0.015), 0.0, 1.0);
            vec3 mtnBase = mix(Horizon * 0.78, Zenith * 0.65 + Horizon * 0.35, 0.45);
            vec3 mtnCol = mix(mtnBase, mtnBase + SunColor * 0.22, sunSide * peak * 0.7);
            // Subtle snow/mist highlight on highest ridges
            mtnCol = mix(mtnCol, Horizon * 1.05, smoothstep(0.75, 1.0, peak) * 0.30);
            float mtnEdge = smoothstep(mtnH, mtnH - 0.004, h);
            col = mix(col, mtnCol, mtnEdge * 0.88);
        }
        if (h < hillH)
        {
            vec3 hillCol = mix(Horizon * 0.64, Horizon * 0.72 + SunColor * 0.12, sunSide * 0.5);
            float hillEdge = smoothstep(hillH, hillH - 0.003, h);
            col = mix(col, hillCol, hillEdge * 0.92);
        }
    }

    gl_FragColor = vec4(min(col, vec3(1.0)), 1.0);
}
#endif
