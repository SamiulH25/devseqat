// Full-screen pass over the rendered scene: FXAA (the game runs without MSAA), colour grade, vignette.
// Grade: x = saturation, y = contrast, z = vignette strength, w = FXAA on (1) / off (0). Texel = 1 / screen size.
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

void main()
{
    vec3 col = Grade.w > 0.5 ? Fxaa(vUV) : texture2D(Scene, vUV).rgb;
    float l = dot(col, Luma);
    col = mix(vec3(l), col, Grade.x);
    col = (col - 0.5) * Grade.y + 0.5;
    // Split tone: warm highlights, cool shadows (the same idea as the sprite ramps).
    col += vec3(0.035, 0.018, -0.02) * smoothstep(0.45, 1.0, l) + vec3(-0.02, 0.0, 0.03) * (1.0 - smoothstep(0.0, 0.45, l));
    vec2 c = (vUV - 0.5) * vec2(1.3, 1.0);
    col *= 1.0 - Grade.z * smoothstep(0.35, 0.95, length(c));
    gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
#endif
