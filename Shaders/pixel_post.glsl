// Real-time 3D Pixel Art composite + unified outline + convex catchlight + upscale pass (spike/3d-pixel-art).
// Samples the low-res pixel-art Scene & NormDepth targets with PointClamp (zero blur, zero mixels!).
//
// Texel      = 1 / pixelTargetSize (size of one crisp pixel-art pixel in UV space)
// Grade      = (saturation, contrast, vignette, pixelModeFlag)
// Tint       = (rgb color grade, bloom strength)
// OutlineCfg = (outerOutlineStrength, depthSepStrength, convexHighlightStrength, transparentBg)
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

vec3 UnpackNormal(vec3 rgb) { return normalize(rgb * 2.0 - 1.0); }

void main()
{
    if (vUV.y < Moment.x || vUV.y > 1.0 - Moment.x) { gl_FragColor = vec4(0.0, 0.0, 0.0, 1.0); return; }

    vec2 uv = vUV;
    if (Moment.y > 0.0)
    {
        float rise = smoothstep(Moment.w - 0.25, Moment.w + 0.2, uv.y);
        uv.x += sin(uv.y * 90.0 + uv.x * 13.0 - Time * 7.0) * 0.0025 * Moment.y * rise;
    }

    // Snap strictly to pixel-art texel centers so every edge is 100% razor-sharp!
    vec2 puv = (floor(uv / Texel) + 0.5) * Texel;
    vec3 col = texture2D(Scene, puv).rgb;

    vec4 ndC = texture2D(NormDepth, puv);
    vec4 ndL = texture2D(NormDepth, puv + vec2(-Texel.x, 0.0));
    vec4 ndR = texture2D(NormDepth, puv + vec2( Texel.x, 0.0));
    vec4 ndU = texture2D(NormDepth, puv + vec2(0.0, -Texel.y));
    vec4 ndD = texture2D(NormDepth, puv + vec2(0.0,  Texel.y));

    bool isUnit = ndC.a > 0.005;
    float maxNA = max(max(ndL.a, ndR.a), max(ndU.a, ndD.a));

    bool transparentBg = OutlineCfg.w > 0.5;
    if (!isUnit)
    {
        // Case 1: Outer 1-pixel ink silhouette drawn cleanly around every unit & horse!
        // Drawn on the background pixel immediately bordering a unit so thin weapons/legs are never eaten away.
        if (maxNA > 0.005 && OutlineCfg.x > 0.01)
        {
            // Pick the closest bordering unit pixel to tint the ink outline harmoniously.
            vec2 bestOff = vec2(-Texel.x, 0.0);
            float bestA = ndL.a;
            if (ndR.a > bestA) { bestA = ndR.a; bestOff = vec2(Texel.x, 0.0); }
            if (ndU.a > bestA) { bestA = ndU.a; bestOff = vec2(0.0, -Texel.y); }
            if (ndD.a > bestA) { bestA = ndD.a; bestOff = vec2(0.0, Texel.y); }
            vec3 uCol = texture2D(Scene, puv + bestOff).rgb;
            vec3 ink = mix(uCol * 0.20, vec3(0.07, 0.06, 0.10), 0.58);
            if (transparentBg) col = ink;
            else
            {
                // Fade outline gently very far away so distant armies don't turn into a solid black smear.
                float farFade = smoothstep(0.03, 0.16, bestA);
                col = mix(col, ink, OutlineCfg.x * farFade);
            }
        }
        else if (transparentBg)
        {
            gl_FragColor = vec4(0.0, 0.0, 0.0, 0.0);
            return;
        }
    }
    else
    {
        // Case 2: Internal depth separation (shield over chest, rider over horse, near soldier over far soldier).
        // Smooth joints (knees, elbows, horse neck) have continuous depth -> ZERO ugly boxes across joints!
        float closerDiff = maxNA - ndC.a;
        float depthThresh = 0.011 + 0.022 * ndC.a; // perspective-scaled depth step
        float nearWeight = smoothstep(0.08, 0.24, ndC.a); // soften internal edges at far Command Overview zoom
        if (closerDiff > depthThresh && OutlineCfg.y > 0.01)
        {
            vec3 ink = mix(col * 0.26, vec3(0.08, 0.07, 0.11), 0.45);
            col = mix(col, ink, OutlineCfg.y * 0.85 * nearWeight);
        }
        else if (OutlineCfg.z > 0.01 && nearWeight > 0.05)
        {
            // Case 3: Subtle t3ssel8r convex armor/muscle catchlight & concave crease!
            vec3 nC = UnpackNormal(ndC.rgb);
            float convex = 0.0;
            float concave = 0.0;

            // Compare against upper and left neighbors (facing light direction) for crisp pixel-art highlights.
            if (ndL.a > 0.005 && abs(ndC.a - ndL.a) < depthThresh)
            {
                vec3 nL = UnpackNormal(ndL.rgb);
                float d = 1.0 - dot(nC, nL);
                if (d > 0.30)
                {
                    if (ndC.a >= ndL.a - 0.002) convex = max(convex, d);
                    else concave = max(concave, d);
                }
            }
            if (ndU.a > 0.005 && abs(ndC.a - ndU.a) < depthThresh)
            {
                vec3 nU = UnpackNormal(ndU.rgb);
                float d = 1.0 - dot(nC, nU);
                if (d > 0.30)
                {
                    if (ndC.a >= ndU.a - 0.002) convex = max(convex, d);
                    else concave = max(concave, d);
                }
            }

            if (convex > 0.30) col *= 1.0 + OutlineCfg.z * 0.16 * nearWeight;
            else if (concave > 0.36) col *= 1.0 - OutlineCfg.z * 0.18 * nearWeight;
        }
    }

    // Color grade & split-tone matching Ironfield's post.glsl.
    col *= mix(vec3(1.0), Tint.rgb, 0.6);
    float l = dot(col, Luma);
    col = mix(vec3(l), col, Grade.x);
    col = (col - 0.5) * Grade.y + 0.5;
    col += vec3(0.035, 0.018, -0.02) * smoothstep(0.45, 1.0, l) + vec3(-0.02, 0.0, 0.03) * (1.0 - smoothstep(0.0, 0.45, l));
    vec2 c = (vUV - 0.5) * vec2(1.3, 1.0);
    if (!transparentBg) col *= 1.0 - Grade.z * smoothstep(0.35, 0.95, length(c));
    if (Awe.w > 0.0)
    {
        vec2 d = vec2((vUV.x - Awe.x) * (Texel.y / Texel.x), vUV.y - Awe.y);
        float r = abs(Awe.z), outside = smoothstep(r * 0.7, r * 1.3, length(d));
        if (Awe.z < 0.0) outside = 1.0 - outside;
        float g = dot(col, Luma);
        col = mix(col, vec3(g) * vec3(0.92, 0.95, 1.0), Awe.w * outside * 0.82);
        if (Awe.z > 0.0) col *= 1.0 + vec3(0.1, 0.07, 0.02) * Awe.w * (1.0 - outside);
    }
    if (Warp > 0.0)
    {
        float e = smoothstep(0.28, 0.85, length(c) + sin(vUV.y * 23.0 + Time * 3.0) * 0.04 * Warp);
        col = mix(col, vec3(0.16, 0.04, 0.22) * (0.6 + 0.4 * sin(Time * 5.0 + vUV.x * 9.0)), e * Warp * 0.85);
    }
    col = mix(col, vec3(1.0), Moment.z);
    gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
#endif
