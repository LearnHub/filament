//------------------------------------------------------------------------------
// Fog
//------------------------------------------------------------------------------

vec4 fog(vec4 color, vec3 view) {
    if (frameUniforms.fogDensity > 0.0) {

        // AVN: simplified fog to match FogExp2 in three.js 
        // https://github.com/mrdoob/three.js/blob/master/src/renderers/shaders/ShaderChunk/fog_fragment.glsl.js

        vec3 fogColor = frameUniforms.fogColor;

#if   defined(BLEND_MODE_OPAQUE)
        // nothing to do here
#elif defined(BLEND_MODE_TRANSPARENT)
        fogColor *= color.a;
#elif defined(BLEND_MODE_ADD)
        fogColor = vec3(0.0);
#elif defined(BLEND_MODE_MASKED)
        // nothing to do here
#elif defined(BLEND_MODE_MULTIPLY)
        // FIXME: unclear what to do here
#elif defined(BLEND_MODE_SCREEN)
        // FIXME: unclear what to do here
#endif
        // Already squared when setting the uniform
        float fogDensity2 = frameUniforms.fogDensity;
        // Skip sqrt
        float fogDepth2 = view.x * view.x + view.y * view.y + view.z * view.z;
        float fogFactor = exp2(-fogDensity2 * fogDepth2);
        color.rgb = mix(fogColor, color.rgb, fogFactor);
    }
    return color;
}
