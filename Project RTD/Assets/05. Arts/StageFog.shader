// Soft drifting floor smoke between the main platforms and the dark foreground.
// Drawn on a quad that StageAtmosphere keeps over the lower part of the view (uv.y 0 = view bottom).
// Noise is sampled in world space with a parallax offset, so the smoke sits at its own depth
// while two octave layers drift and roll over time.
Shader "RTD/Stage Fog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Smoke Color", Color) = (0.78, 0.84, 0.96, 1)
        _Density ("Density", Range(0, 1)) = 0.38
        _Scale ("Noise Scale (world)", Float) = 0.32
        _Speed ("Drift Speed", Float) = 0.35
        _Parallax ("Parallax (camera follow)", Float) = -0.2
        _Falloff ("Vertical Falloff", Float) = 1.6
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

            fixed4 _Color;
            float _Density, _Scale, _Speed, _Parallax, _Falloff;

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

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                // smoke at its own depth: offset by the camera so it scrolls slower/faster than the world
                float2 p = float2(i.world.x - _WorldSpaceCameraPos.x * _Parallax, i.world.y) * _Scale;
                // domain-warped fbm: big slow billows plus a faster wisp layer drifting the other way
                float2 warp = float2(Fbm(p * 0.7 + float2(t * 0.6, -t * 0.2)), Fbm(p * 0.7 + float2(5.2 - t * 0.3, 1.3)));
                float billow = Fbm(p + warp * 1.6 + float2(t, 0));
                float wisp = Fbm(p * 2.3 + float2(-t * 1.7, t * 0.4));
                float smoke = saturate((billow * 0.75 + wisp * 0.35) * 1.35 - 0.35);

                // thick at the bottom of the view, thinning out upward, soft at the left/right edges
                float vertical = pow(saturate(1 - i.uv.y), _Falloff);
                float sides = smoothstep(0, 0.06, i.uv.x) * smoothstep(1, 0.94, i.uv.x);
                float a = smoke * vertical * sides * _Density * i.color.a;
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}
