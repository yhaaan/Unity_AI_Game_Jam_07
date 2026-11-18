// Out-of-focus foreground silhouette: a gaussian-ish disk blur of the sprite, pushed toward a dark tint.
// BlurredSilhouette gives each renderer a padded full-rect sprite and _UVRect (the real sprite rect in UV),
// so the blur can bleed past the outline without picking up neighbouring sprites in a sheet.
Shader "RTD/Sprite Foreground Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _UVRect ("Sprite UV Rect (xMin, yMin, xMax, yMax)", Vector) = (0, 0, 1, 1)
        _BlurTexels ("Blur Radius (texels)", Float) = 16
        [PerRendererData] _BlurTexelsXY ("Per-axis Blur Radius (texels, 0 = use Blur Radius)", Vector) = (0, 0, 0, 0)
        _Tint ("Tint", Color) = (0.05, 0.06, 0.1, 0.92)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0.85
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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _UVRect;
            float _BlurTexels;
            float4 _BlurTexelsXY;
            fixed4 _Tint;
            fixed _TintAmount;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // Samples outside the real sprite rect count as empty, so the padded area holds only the halo.
            // Wide radii step several texels between taps; a coarser mip (when the texture has them) hides the gaps.
            float4 Tap (float2 uv, float lod)
            {
                float inside = step(_UVRect.x, uv.x) * step(uv.x, _UVRect.z) * step(_UVRect.y, uv.y) * step(uv.y, _UVRect.w);
                float4 c = tex2Dlod(_MainTex, float4(uv, 0, lod));
                c.a *= inside;
                return float4(c.rgb * c.a, c.a);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Non-uniformly scaled sprites need a different radius per axis to blur evenly on screen.
                float2 radius = _BlurTexelsXY.x > 0 ? _BlurTexelsXY.xy : _BlurTexels.xx;
                float2 step2 = _MainTex_TexelSize.xy * (radius / 3.0);
                float lod = max(0, log2(max(min(radius.x, radius.y) / 3.0, 1)) - 0.5);
                float4 sum = 0;
                float wsum = 0;
                // 7x7 taps with gaussian weights (sigma ~ radius / 2)
                [unroll] for (int y = -3; y <= 3; y++)
                [unroll] for (int x = -3; x <= 3; x++)
                {
                    float w = exp(-(x * x + y * y) / 4.5);
                    sum += Tap(i.uv + float2(x, y) * step2, lod) * w;
                    wsum += w;
                }
                float a = sum.a / wsum;
                float3 rgb = sum.a > 1e-4 ? sum.rgb / sum.a : 0;
                rgb = lerp(rgb, _Tint.rgb, _TintAmount);
                return fixed4(rgb, a * _Tint.a * i.color.a);
            }
            ENDCG
        }
    }
}
