// Diagonal light falling from the lab ceiling. Drawn additively on a quad that StageAtmosphere
// stretches over the whole view; the beams themselves come from LightShafts.cginc.
Shader "RTD/Light Shafts"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Light Color", Color) = (1, 0.95, 0.82, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.22
        _ShaftSlant ("Slant (x per unit drop)", Float) = 0.55
        _ShaftSpacing ("Beam Spacing (world)", Float) = 5.5
        _ShaftParallax ("Parallax", Float) = 0.15
        _ShaftReach ("Reach (view heights)", Float) = 1.1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "LightShafts.cginc"

            fixed4 _Color;
            float _Intensity;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 world : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float m = ShaftMask(i.world, _Time.y);
                // a little glow right under the ceiling where the light enters
                float drop = max(0, _ShaftView.y - i.world.y);
                float source = exp(-drop * 1.8) * 0.5;
                return fixed4(_Color.rgb, saturate(m * (1 + source)) * _Intensity * i.color.a);
            }
            ENDCG
        }
    }
}
