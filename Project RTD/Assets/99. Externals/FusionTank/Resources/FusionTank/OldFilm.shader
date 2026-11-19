// Old film projection for a RawImage: sepia, grain, flicker, gate weave, scratches, dust, vignette
// plus TV "static" snow (_Static 0..1) for the 지지직 cuts. Time comes from _FilmT (unscaled, set by FilmFusionIntro).
Shader "RTD/OldFilm"
{
    Properties
    {
        [PerRendererData] _MainTex ("Picture", 2D) = "black" {}
        _FilmT ("Time", Float) = 0
        _Static ("Static snow", Range(0, 1)) = 0
        _Sepia ("Sepia", Range(0, 1)) = 0.85
        _Fade ("Brightness", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _FilmT, _Static, _Sepia, _Fade;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }

            float H (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag (v2f i) : SV_Target
            {
                float fr = floor(_FilmT * 16.0);                  // projector frame (16 fps)
                float sf = floor(_FilmT * 30.0);                  // static changes faster
                float2 uv = i.uv;
                // gate weave + an occasional vertical hop
                uv += (float2(H(float2(fr, 1.3)), H(float2(fr, 7.1))) - 0.5) * float2(0.003, 0.005);
                uv.y += step(0.96, H(float2(fr, 3.3))) * 0.012;
                // static: torn scanlines
                float row = floor(i.uv.y * 160.0);
                uv.x += (H(float2(row, sf)) - 0.5) * 0.08 * _Static * _Static;

                float3 c = tex2D(_MainTex, uv).rgb;
                float l = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(c, l * float3(1.08, 0.94, 0.72), _Sepia);
                c = (c - 0.5) * 1.12 + 0.5;
                c *= 1.0 + (H(float2(fr, 2.7)) - 0.5) * 0.18;     // flicker
                c += (H(i.uv * float2(640.0, 360.0) + fr * 1.71) - 0.5) * 0.13;   // grain

                // scratches: thin wavering vertical lines
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float on = step(0.55, H(float2(fr * 0.13 + k, 11.0 + k)));
                    float sx = H(float2(floor(fr / 3.0) + k * 7.0, 5.0)) + sin(i.uv.y * 9.0 + k) * 0.002;
                    float d = abs(i.uv.x - sx);
                    float s = saturate(1.0 - d / 0.0012) * on * (0.55 + 0.45 * sin(i.uv.y * 23.0 + fr));
                    c = lerp(c, (k == 1) ? float3(0.95, 0.9, 0.8) : float3(0.12, 0.1, 0.08), s * 0.7);
                }
                // dust and hairs
                [unroll] for (int j = 0; j < 5; j++)
                {
                    float2 p = float2(H(float2(fr + j * 1.7, 3.0)), H(float2(fr + j * 2.3, 9.0)));
                    float r = 0.002 + 0.007 * H(float2(j, fr));
                    float d = length((i.uv - p) * float2(1.777, 1.0));
                    c = lerp(c, float3(0.07, 0.06, 0.05), saturate((r - d) / (r * 0.5)) * step(0.45, H(float2(j * 3.1, fr))));
                }

                // projector vignette + rounded gate
                float2 q = i.uv * 2.0 - 1.0;
                c *= saturate(1.0 - dot(q * float2(0.7, 0.85), q * float2(0.7, 0.85)) * 0.5);
                float f = pow(pow(abs(q.x), 10.0) + pow(abs(q.y), 10.0), 0.1);
                c *= saturate((1.0 - f) * 60.0);

                // TV static snow with a rolling bar
                float sn = H(floor(i.uv * float2(320.0, 180.0)) + sf * 1.37);
                float bar = 0.75 + 0.25 * sin(i.uv.y * 4.0 - _FilmT * 7.0);
                c = lerp(c, sn * bar * float3(0.95, 0.95, 1.0), _Static);

                return float4(saturate(c) * _Fade, i.color.a);
            }
            ENDCG
        }
    }
}
