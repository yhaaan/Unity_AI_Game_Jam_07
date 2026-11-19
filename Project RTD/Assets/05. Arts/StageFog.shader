// Soft drifting floor smoke between the main platforms and the dark foreground.
// Drawn on a quad that StageAtmosphere keeps over the lower part of the view (uv.y 0 = view bottom).
//   * two sheets at different depths: a far, wide, faint one and a near, puffier one that rolls upward
//   * each puff is lit from above-left (bright crowns, cool shadowed bellies) so it reads as volume
//   * where the ceiling light shafts cross it, the smoke glows warm (same beam mask as RTD/Light Shafts)
Shader "RTD/Stage Fog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Lit Smoke Color", Color) = (0.86, 0.9, 0.98, 1)
        _ShadowColor ("Shadowed Smoke Color", Color) = (0.42, 0.48, 0.62, 1)
        _GlowColor ("Glow In Light Beams", Color) = (1, 0.9, 0.7, 1)
        _Glow ("Beam Glow Strength", Range(0, 2)) = 0.9
        _Density ("Density", Range(0, 1)) = 0.75
        _Scale ("Noise Scale (world)", Float) = 0.42
        _Speed ("Drift Speed", Float) = 0.35
        _Rise ("Rise Speed", Float) = 0.12
        _Parallax ("Parallax (camera follow)", Float) = -0.2
        _Falloff ("Vertical Falloff", Float) = 1.1
        _Puff ("Puff Threshold (higher = fewer, more separate puffs)", Range(0.2, 0.8)) = 0.48
        _FarSheet ("Far Sheet Strength", Range(0, 1)) = 0.45
        _ShaftSlant ("Slant (match Light Shafts)", Float) = 0.55
        _ShaftSpacing ("Beam Spacing (match Light Shafts)", Float) = 5.5
        _ShaftParallax ("Parallax (match Light Shafts)", Float) = 0.15
        _ShaftReach ("Reach (match Light Shafts)", Float) = 1.1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "LightShafts.cginc"

            fixed4 _Color, _ShadowColor, _GlowColor;
            float _Glow, _Density, _Scale, _Speed, _Rise, _Parallax, _Falloff, _Puff, _FarSheet;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.color = v.color;
                return o;
            }

            float Hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm (float2 p)
            {
                float v = 0, a = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            // puff density at a noise-space point (0 = clear air, 1 = thick smoke)
            float Puffs (float2 p, float2 warp, float t)
            {
                float billow = Fbm(p + warp * 1.6 + float2(t, -t * _Rise * 3));
                float wisp = Fbm(p * 2.3 + float2(-t * 1.7, t * 0.4 - t * _Rise * 6));
                return smoothstep(_Puff - 0.22, _Puff + 0.18, billow * 0.7 + wisp * 0.4);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;

                // --- near sheet: its own depth, rolling slowly upward ---
                float2 p = float2(i.world.x - _WorldSpaceCameraPos.x * _Parallax, i.world.y) * _Scale;
                float2 warp = float2(Fbm(p * 0.7 + float2(t * 0.6, -t * 0.2)), Fbm(p * 0.7 + float2(5.2 - t * 0.3, 1.3)));
                float nearD = Puffs(p, warp, t);
                // light from above-left: compare with a sample nudged toward the light
                float towardLight = Puffs(p + float2(-0.06, 0.1), warp, t);
                float lit = saturate(0.55 + (nearD - towardLight) * 2.2);

                // --- far sheet: wider, slower, flatter, scrolls with the world more closely ---
                float2 q = float2(i.world.x - _WorldSpaceCameraPos.x * (_Parallax * 0.4), i.world.y) * (_Scale * 0.55) + 31.7;
                float farD = smoothstep(0.35, 0.75, Fbm(q + float2(t * 0.45, -t * _Rise)));

                // thick at the bottom of the view, thinning upward; the far sheet hangs a little higher
                float vNear = pow(saturate(1 - i.uv.y), _Falloff);
                float vFar = pow(saturate(1.15 - i.uv.y), _Falloff * 0.8);
                float sides = smoothstep(0, 0.06, i.uv.x) * smoothstep(1, 0.94, i.uv.x);

                float aNear = nearD * vNear;
                float aFar = farD * vFar * _FarSheet;

                // colour: shadowed bellies -> lit crowns; the far sheet is flat and cooler
                float3 nearCol = lerp(_ShadowColor.rgb, _Color.rgb, lit);
                float3 farCol = lerp(_ShadowColor.rgb, _Color.rgb, 0.4);
                float a = saturate(aNear + aFar * (1 - aNear));
                float3 col = (nearCol * aNear + farCol * aFar * (1 - aNear)) / max(a, 1e-4);

                // light shafts passing through the smoke make it glow warm
                float beam = ShaftBeams(i.world, _Time.y, 0) * lerp(0.5, 1, ShaftFall(i.world));
                col = lerp(col, _GlowColor.rgb, saturate(beam * _Glow * 0.8));
                a = saturate(a * (1 + beam * _Glow * 0.35));

                return fixed4(col, a * sides * _Density * i.color.a);
            }
            ENDCG
        }
    }
}
