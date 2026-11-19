// "Walking on a little planet": bends the rendered picture so the world curves away on both sides.
// The centre column (where the camera keeps the player) is untouched; toward the left/right edges
// everything sinks a little, like the horizon of a small round world.
// Runs as a Full Screen Pass on the 2D renderer. Strength comes from the global _WorldBend that
// StageAtmosphere sets in stages (0 everywhere else, so menus and cutscenes stay flat).
Shader "RTD/Curved World"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "Curved World"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _WorldBend;   // how far the edges sink, as a fraction of the screen height

            half4 Frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float m = saturate(_WorldBend);
                if (m < 1e-4)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // circular arc rather than a parabola: flat in the middle, rounding off toward the edges
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float x = (uv.x - 0.5) * aspect;                  // horizontal distance in screen heights
                float halfW = 0.5 * aspect;
                float radius = (halfW * halfW + m * m) / (2 * m); // circle through the centre and dropping m at the edge
                float drop = radius - sqrt(max(0, radius * radius - x * x));

                // zoom in by m so the sunken edges never pull in pixels from outside the frame
                float2 src;
                src.x = 0.5 + (uv.x - 0.5) * (1 - m);
                src.y = uv.y * (1 - m) + drop;
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, src);
            }
            ENDHLSL
        }
    }
}
