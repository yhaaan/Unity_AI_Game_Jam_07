// Soft dust motes for a ParticleSystem. Each mote is faint in the dark and lights up
// inside the ceiling beams (same mask as RTD/Light Shafts), like dust caught in sunlight.
Shader "RTD/Shaft Dust"
{
    Properties
    {
        _Color ("Dust Color", Color) = (1, 0.96, 0.86, 1)
        _Ambient ("Visibility Outside Beams", Range(0, 1)) = 0.12
        _InBeam ("Brightness Inside Beams", Float) = 1.6
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
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "LightShafts.cginc"

            fixed4 _Color;
            float _Ambient, _InBeam;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // particles are simulated in world space, so the vertex is already a world position
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2;
                float mote = saturate(1 - r);
                mote *= mote;
                float lit = _Ambient + ShaftMask(i.world, _Time.y) * _InBeam;
                return fixed4(_Color.rgb * i.color.rgb, saturate(mote * lit * i.color.a));
            }
            ENDCG
        }
    }
}
