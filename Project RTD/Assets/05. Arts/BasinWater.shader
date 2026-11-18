Shader "ProjectRTD/2D/Basin Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Mask", 2D) = "white" {}
        _Tint ("Water Tint", Color) = (0.13, 0.54, 0.85, 1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.32
        _Opacity ("Water Opacity", Range(0, 1)) = 0.45
        _Distortion ("Distortion (pixels)", Range(0, 6)) = 2
        _Speed ("Wave Speed", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            // Renderer2D captures Default before drawing WaterOverlay.
            TEXTURE2D(_CameraSortingLayerTexture); SAMPLER(sampler_CameraSortingLayerTexture);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _TintStrength, _Distortion, _Speed, _Opacity;
            CBUFFER_END

            struct Attributes { float3 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.pos);
                o.pos = TransformWorldToHClip(world);
                o.world = world.xy;
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 wave = float2(sin(i.world.y * 5.0 + t + 0.5 * sin(i.world.x * 1.7 - t)),
                                     0.35 * sin(i.world.x * 3.0 - t * 0.7));
                float2 uv = GetNormalizedScreenSpaceUV(i.pos) + wave * _Distortion / GetScaledScreenParams().xy;
                half3 scene = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, saturate(uv)).rgb;
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                // Basin uses a full rectangular sprite. A moving crest and a thin
                // light band remain visible even over a featureless background.
                float crest = 0.981 + 0.009 * sin(i.world.x * 2.9 + t * 1.6)
                                    + 0.005 * sin(i.world.x * 5.7 - t);
                float depth = crest - i.uv.y;
                float aa = max(fwidth(depth), 0.001);
                mask *= smoothstep(-aa, aa, depth);
                float rim = 1.0 - smoothstep(0.008, 0.019 + aa, depth);
                float shallow = exp2(-5.0 * max(depth, 0.0));
                half3 water = lerp(scene, _Tint.rgb, _TintStrength) * (0.85 + 0.15 * shallow);
                return half4(lerp(water, half3(0.36, 0.84, 1.0), rim * 0.75), mask * _Opacity * _Tint.a);
            }
            ENDHLSL
        }
    }
}
