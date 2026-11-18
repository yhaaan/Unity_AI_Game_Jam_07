// Sprite seen through water: wobbly refraction + drifting caustic light + water tint.
// Time comes from the global _FusionT (set by FusionTankSequence) so baked previews animate too.
Shader "RTD/UnderwaterSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Wave ("Refraction amount (uv)", Float) = 0.006
        _WaveFreq ("Refraction frequency", Float) = 5
        _WaveSpeed ("Refraction speed", Float) = 2
        _Caustic ("Caustic strength", Float) = 0.22
        _CausticScale ("Caustic scale", Float) = 1.3
        _Tint ("Water colour", Color) = (0.55, 0.88, 1, 1)
        _TintAmt ("Water colour amount", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Wave, _WaveFreq, _WaveSpeed, _Caustic, _CausticScale, _TintAmt, _FusionT, _FusionWhirl;
            float4 _Tint;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 wp : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // cheap caustic network: thin bright lines where three warped sine fields cross zero
            float Caustic (float2 p, float t)
            {
                float a = sin(p.x * 1.7 + t * 1.1 + sin(p.y * 1.3 + t * 0.7) * 1.6);
                float b = sin(p.y * 2.1 - t * 0.9 + sin(p.x * 1.1 - t * 0.5) * 1.4);
                float c = sin((p.x + p.y) * 1.3 + t * 1.3 + sin(p.x * 0.8 - p.y * 0.6 + t) * 1.2);
                float n = (a + b + c) / 3.0;
                return pow(saturate(1.0 - abs(n) * 2.6), 3.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _FusionT;
                float2 uv = i.uv;
                float w = _Wave * (1.0 + _FusionWhirl);
                uv.x += sin(i.wp.y * _WaveFreq + t * _WaveSpeed) * w;
                uv.y += sin(i.wp.x * _WaveFreq * 0.8 + t * _WaveSpeed * 1.3) * w * 0.6;
                fixed4 c = tex2D(_MainTex, uv) * i.color;
                c.rgb = lerp(c.rgb, c.rgb * _Tint.rgb, _TintAmt);
                c.rgb += Caustic(i.wp * _CausticScale, t) * _Caustic;
                return c;
            }
            ENDCG
        }
    }
}
