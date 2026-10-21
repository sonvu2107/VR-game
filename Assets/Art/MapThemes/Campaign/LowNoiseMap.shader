Shader "TheLabyrinth/Low Noise Pixel Map"
{
    Properties
    {
        _MainTex ("Original atlas", 2D) = "white" {}
        _PixelsPerCell ("Logical pixels per atlas cell", Float) = 48
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _PixelsPerCell;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Output vert(Input v)
            {
                Output o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            fixed4 frag(Output input) : SV_Target
            {
                // Atlas cell 3 is the large floor emblem, stretched over 3x3 world cells.
                // Keep its original detail; the 48-pixel prefilter is only for small repeating tiles.
                if (input.uv.x >= .75 && input.uv.y >= .75)
                    return tex2D(_MainTex, input.uv);
                float density = max(16, _PixelsPerCell) * 4;
                float2 cellMin = floor(input.uv * 4) * .25;
                float2 lo = cellMin + _MainTex_TexelSize.xy * 3;
                float2 hi = cellMin + .25 - _MainTex_TexelSize.xy * 3;
                float2 center = (floor(input.uv * density) + .5) / density;
                // Lọc trước khi lấy mẫu thưa: không chỉ pixelate bằng cách bỏ ngẫu nhiên texel.
                // Chặn trong cell để không kéo nền/alpha từ ô atlas bên cạnh.
                float4 sum = 0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 uv = clamp(center + float2(x,y) / (density * 3), lo, hi);
                    float4 texel = tex2D(_MainTex, uv);
                    sum.rgb += texel.rgb * texel.a;
                    sum.a += texel.a;
                }
                return float4(sum.rgb / max(.0001, sum.a), sum.a / 9);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
