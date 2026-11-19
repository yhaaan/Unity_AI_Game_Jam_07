// Dims the room except where the ceiling light falls: a dark wash whose holes follow the beams
// (same mask as RTD/Light Shafts), widened into a soft halo and carried down to the floor as light pools.
Shader "RTD/Shaft Ambient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Shadow Color", Color) = (0.03, 0.035, 0.08, 1)
        _Darkness ("Darkness", Range(0, 1)) = 0.5
        _Halo ("Halo Width (world)", Float) = 0.9
        _FloorLight ("Light Kept At The Floor", Range(0, 1)) = 0.6
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

            fixed4 _Color;
            float _Darkness, _Halo, _FloorLight;

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
                float t = _Time.y;
                // widen the beams into a soft glow by sampling them a little to each side
                float glow = ShaftBeams(i.world, t, 0) * 0.3
                           + (ShaftBeams(i.world, t, _Halo * 0.5) + ShaftBeams(i.world, t, -_Halo * 0.5)) * 0.22
                           + (ShaftBeams(i.world, t, _Halo) + ShaftBeams(i.world, t, -_Halo)) * 0.13;
                // beams stay strongest near the ceiling but still pool some light on the floor
                glow = saturate(glow * 1.6) * lerp(_FloorLight, 1, ShaftFall(i.world));
                float shade = _Darkness * (1 - glow);
                // tiny dither keeps the long dark gradient from banding
                float dither = (frac(sin(dot(floor(i.pos.xy), float2(12.9898, 78.233))) * 43758.5453) - 0.5) / 255.0;
                return fixed4(_Color.rgb, saturate(shade + dither) * i.color.a);
            }
            ENDCG
        }
    }
}
