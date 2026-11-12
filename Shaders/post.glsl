// Full-screen pass over the rendered scene: FXAA (the game runs without MSAA), colour grade, vignette.
// Grade: x = saturation, y = contrast, z = vignette strength, w = FXAA on (1) / off (0). Texel = 1 / screen size.
// Tint: rgb = the light's colour grade by hour and weather (1,1,1 = none), w = bloom strength (0 = off).
// Awe (docs/archive/SPECTACLE.md S1): colour drains outside a circle: xy = its centre (uv), z = radius (in screen heights), w = amount.
// Time: seconds, for the shimmer. Awe.z < 0 drains inside the circle (a dread). Warp: the Black Flame at the screen's edge.
// Moment: x = letterbox bars (fraction of the height each), y = heat shimmer strength, z = white flash, w = shimmer top (uv y).
varying vec2 vUV;

#ifdef VERTEX
attribute vec4 aPos;
attribute vec2 aUV;

void main()
{
    vUV = aUV;
    gl_Position = vec4(aPos.xy, 0.0, 1.0);
    FIXUP_POSITION
}
#endif

#ifdef FRAGMENT
const vec3 Luma = vec3(0.299, 0.587, 0.114);

// FXAA in its compact "console" form (after Timothy Lottes): blur only along detected edges.
vec3 Fxaa(vec2 uv)
{
    vec2 px = Texel;
    vec3 nw = texture2D(Scene, uv + vec2(-1.0, -1.0) * px).rgb;
    vec3 ne = texture2D(Scene, uv + vec2(1.0, -1.0) * px).rgb;
    vec3 sw = texture2D(Scene, uv + vec2(-1.0, 1.0) * px).rgb;
    vec3 se = texture2D(Scene, uv + vec2(1.0, 1.0) * px).rgb;
    vec3 m = texture2D(Scene, uv).rgb;
    float lNW = dot(nw, Luma), lNE = dot(ne, Luma), lSW = dot(sw, Luma), lSE = dot(se, Luma), lM = dot(m, Luma);
    float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
    float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
    vec2 dir = vec2(-((lNW + lNE) - (lSW + lSE)), (lNW + lSW) - (lNE + lSE));
    float reduce = max((lNW + lNE + lSW + lSE) * 0.03125, 1.0 / 128.0);
    float rcp = 1.0 / (min(abs(dir.x), abs(dir.y)) + reduce);
    dir = clamp(dir * rcp, vec2(-8.0), vec2(8.0)) * px;
    vec3 a = 0.5 * (texture2D(Scene, uv + dir * (1.0 / 3.0 - 0.5)).rgb + texture2D(Scene, uv + dir * (2.0 / 3.0 - 0.5)).rgb);
    vec3 b = a * 0.5 + 0.25 * (texture2D(Scene, uv - dir * 0.5).rgb + texture2D(Scene, uv + dir * 0.5).rgb);
    float lB = dot(b, Luma);
    return (lB < lMin || lB > lMax) ? a : b;
}

// A cheap glow: the bright part of a ring of taps round the pixel (sun on helms, fires, snow in sun).
vec3 Bloom(vec2 uv)
{
    vec3 sum = vec3(0.0);
    for (int i = 0; i < 8; i++)
    {
        float a = float(i) * 0.7854;
        vec2 o = vec2(cos(a), sin(a)) * Texel;
        sum += max(texture2D(Scene, uv + o * 4.0).rgb - 0.72, 0.0) + max(texture2D(Scene, uv + o * 10.0).rgb - 0.72, 0.0) * 0.6;
    }
    return sum / 12.8;
}

void main()
{
    if (vUV.y < Moment.x || vUV.y > 1.0 - Moment.x) { gl_FragColor = vec4(0.0, 0.0, 0.0, 1.0); return; } // the beat's bars
    vec2 uv = vUV;
    if (Moment.y > 0.0) // heat over fire: the air wavers, more toward the bottom of the screen
    {
        float rise = smoothstep(Moment.w - 0.25, Moment.w + 0.2, uv.y);
        uv.x += sin(uv.y * 90.0 + uv.x * 13.0 - Time * 7.0) * 0.0025 * Moment.y * rise;
    }
    vec3 col = Grade.w > 0.5 ? Fxaa(uv) : texture2D(Scene, uv).rgb;
    if (Tint.w > 0.0) col += Bloom(vUV) * Tint.w;
    col *= mix(vec3(1.0), Tint.rgb, 0.6);
    float l = dot(col, Luma);
    col = mix(vec3(l), col, Grade.x);
    col = (col - 0.5) * Grade.y + 0.5;
    // Split tone: warm highlights, cool shadows (the same idea as the sprite ramps).
    col += vec3(0.035, 0.018, -0.02) * smoothstep(0.45, 1.0, l) + vec3(-0.02, 0.0, 0.03) * (1.0 - smoothstep(0.0, 0.45, l));
    vec2 c = (vUV - 0.5) * vec2(1.3, 1.0);
    col *= 1.0 - Grade.z * smoothstep(0.35, 0.95, length(c));
    if (Awe.w > 0.0)
    {
        vec2 d = vec2((vUV.x - Awe.x) * (Texel.y / Texel.x), vUV.y - Awe.y); // in screen heights, so the circle is round
        float r = abs(Awe.z), outside = smoothstep(r * 0.7, r * 1.3, length(d));
        if (Awe.z < 0.0) outside = 1.0 - outside; // a dread: the colour drains from inside the circle instead
        float g = dot(col, Luma);
        col = mix(col, vec3(g) * vec3(0.92, 0.95, 1.0), Awe.w * outside * 0.82); // a trace of colour stays: men still read by side
        if (Awe.z > 0.0) col *= 1.0 + vec3(0.1, 0.07, 0.02) * Awe.w * (1.0 - outside); // the one who matters stands in warm light
    }
    if (Warp > 0.0) // the Black Flame: the edges of the world burn dark and waver
    {
        float e = smoothstep(0.28, 0.85, length(c) + sin(vUV.y * 23.0 + Time * 3.0) * 0.04 * Warp);
        col = mix(col, vec3(0.16, 0.04, 0.22) * (0.6 + 0.4 * sin(Time * 5.0 + vUV.x * 9.0)), e * Warp * 0.85);
    }
    col = mix(col, vec3(1.0), Moment.z);
    gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
#endif
