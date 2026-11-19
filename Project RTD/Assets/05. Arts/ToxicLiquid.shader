// Cartoon toxic sludge for lab pools: flat toon bands (bright scum line, lime top, deep green body),
// an inked wobbling surface, drifting highlight dashes, outlined bubbles rising through the body and
// blobs that swell on the surface and pop. Everything is procedural in world space on one quad.
// ToxicPool sets _Surface (y of the still surface) and _Bottom (y of the pool floor) per renderer.
Shader "RTD/Toxic Liquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Scum ("Surface Scum", Color) = (0.85, 1, 0.35, 1)
        _Top ("Upper Liquid", Color) = (0.45, 0.9, 0.2, 1)
        _Deep ("Deep Liquid", Color) = (0.12, 0.45, 0.16, 1)
        _Ink ("Outline Ink", Color) = (0.16, 0.12, 0.26, 1)
        _Opacity ("Body Opacity", Range(0, 1)) = 0.9
        _BubbleRate ("Bubble Rate", Float) = 1
        [PerRendererData] _Surface ("Surface Y", Float) = 0
        [PerRendererData] _Bottom ("Bottom Y", Float) = -2
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

            fixed4 _Scum, _Top, _Deep, _Ink;
            float _Opacity, _BubbleRate, _Surface, _Bottom;

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

            float Hash (float n) { return frac(sin(n * 127.1) * 43758.5453); }

            float SurfaceAt (float x, float t)
            {
                // slow, heavy sludge: low waves that roll rather than ripple
                return _Surface + 0.045 * sin(x * 2.2 + t * 1.3) + 0.025 * sin(x * 4.7 - t * 1.9) + 0.012 * sin(x * 9.1 + t * 2.7);
            }

            // ring-outlined bubble: returns (fill, ink, shine) coverage
            float3 Bubble (float2 p, float2 c, float r)
            {
                float d = length(p - c);
                float ink = smoothstep(r + 0.012, r, d) * smoothstep(r - 0.03, r - 0.012, d);
                float fill = smoothstep(r - 0.012, r - 0.03, d);
                float shine = smoothstep(r * 0.32, r * 0.18, length(p - c - float2(-r * 0.35, r * 0.35)));
                return float3(fill, ink, shine);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.world;
                float s = SurfaceAt(p.x, t);
                float depth = s - p.y;                       // > 0 inside the liquid

                float3 col = 0;
                float a = 0;

                // ---- surface blobs that swell and pop (drawn above and below the line) ----
                float cellW = 0.55;
                float cx = floor(p.x / cellW);
                float popInk = 0, popFill = 0, popShine = 0, popRing = 0;
                [unroll] for (int k = -1; k <= 1; k++)
                {
                    float id = cx + k;
                    float h = Hash(id);
                    if (h < 0.35) continue;
                    float cycle = 2.2 + h * 2.5;
                    float life = frac(t * _BubbleRate / cycle + h * 7.3);
                    float2 c = float2((id + 0.25 + 0.5 * Hash(id + 3.1)) * cellW, 0);
                    c.y = SurfaceAt(c.x, t);
                    float grow = smoothstep(0.0, 0.8, life);
                    float r = lerp(0.03, 0.09 + 0.05 * h, grow);
                    if (life < 0.86)
                    {
                        float3 b = Bubble(p, c + float2(0, r * 0.25), r);
                        // only the dome above the surface; below it merges into the liquid
                        float above = step(c.y - 0.01, p.y);
                        popFill = max(popFill, b.x * above);
                        popInk = max(popInk, b.y * above);
                        popShine = max(popShine, b.z * above);
                    }
                    else
                    {
                        // pop: a thin ring flying outward and fading
                        float u = (life - 0.86) / 0.14;
                        float rr = r * (1 + u * 1.6);
                        float d = length(p - c - float2(0, r * 0.25));
                        popRing = max(popRing, (1 - u) * smoothstep(0.014, 0.0, abs(d - rr)) * step(c.y - 0.02, p.y));
                    }
                }

                if (depth > 0)
                {
                    // ---- toon bands ----
                    float body = saturate(depth / max(0.2, s - _Bottom));
                    float band = smoothstep(0.32, 0.34, body + 0.03 * sin(p.x * 3.1 + t * 0.8));
                    col = lerp(_Top.rgb, _Deep.rgb, band);
                    float scum = 1 - smoothstep(0.05, 0.065, depth);
                    col = lerp(col, _Scum.rgb, scum);

                    // drifting highlight dashes just under the scum line
                    float lane = abs(depth - 0.13);
                    float dash = step(0.72, frac(p.x * 0.9 - t * 0.25 + 0.3 * sin(p.y * 4))) * (1 - smoothstep(0.012, 0.022, lane));
                    col = lerp(col, _Scum.rgb, dash * 0.8);

                    // ---- bubbles rising through the body, ink-outlined ----
                    float2 g = float2(p.x * 2.4, p.y * 2.4 - t * 0.55 * _BubbleRate);
                    float2 cell = floor(g);
                    float h = Hash(cell.x * 17.0 + cell.y * 3.7);
                    if (h > 0.55 && depth > 0.12)
                    {
                        float2 c = (cell + float2(0.3 + 0.4 * Hash(cell.x + cell.y * 9.1), 0.5)) / 2.4;
                        c.y += t * 0.55 * _BubbleRate / 2.4;
                        c.x += 0.05 * sin(t * 2 + h * 20);    // wobble as it rises
                        float r = 0.035 + 0.05 * Hash(h * 91);
                        float3 b = Bubble(p, c, r);
                        col = lerp(col, lerp(col, _Scum.rgb, 0.55), b.x);
                        col = lerp(col, _Ink.rgb, b.y * 0.85);
                        col = lerp(col, float3(1, 1, 1), b.z * 0.8);
                    }

                    // inked surface line
                    float inkLine = 1 - smoothstep(0.012, 0.024, depth);
                    col = lerp(col, _Ink.rgb, inkLine);
                    a = lerp(_Opacity, 1, max(scum, inkLine));
                }

                // surface blobs on top
                float blob = max(popFill, popInk);
                col = lerp(col, _Top.rgb * 1.1, popFill * step(depth, 0.02));
                col = lerp(col, _Ink.rgb, popInk);
                col = lerp(col, float3(1, 1, 1), popShine * 0.85 * popFill);
                col = lerp(col, _Scum.rgb, popRing);
                a = max(a, max(blob, popRing * 0.9));

                return fixed4(col, a * i.color.a);
            }
            ENDCG
        }
    }
}
