Shader "TheLabyrinth/Pixel Dungeon Ambience"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Palette", Color) = (1,1,1,1)
        _Frames ("Loop frames", Float) = 1
        _FPS ("Frames per second", Float) = 5
        _Bob ("Soul float height", Float) = 0
        _Tint ("Palette strength", Range(0,1)) = 1
        [HideInInspector] _PreviewTime ("Validation time (-1 = live)", Float) = -1
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
            float4 _Color;
            float _Frames, _FPS, _Bob, _Tint, _PreviewTime;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            Output vert(Input input)
            {
                Output output;
                // Scaled Unity time: Pause freezes both loop and floating motion.
                float clock = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                input.vertex.y += sin(clock * 1.7) * _Bob;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }
            fixed4 frag(Output input) : SV_Target
            {
                float clock = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float count = max(1, _Frames);
                float stepTime = clock * _FPS;
                float frame = fmod(floor(stepTime), count);
                float2 animatedUv = input.uv + float2(frame * 0.25, 0);
                fixed4 color = tex2D(_MainTex, animatedUv);
                // Hàng đầu là chậu lửa: chân/rim luôn lấy frame 0 để không rung theo hình vẽ khác nhau.
                if (_Frames > 1 && input.uv.y > 0.75 && _Bob < 0.001)
                {
                    float nextFrame = fmod(frame + 1, count);
                    fixed4 nextColor = tex2D(_MainTex, input.uv + float2(nextFrame * 0.25, 0));
                    // Trộn màu trong lưới pixel hiện có; không dịch vị trí hoặc đổi tỷ lệ thân chậu.
                    color = lerp(color, nextColor, smoothstep(0, 1, frac(stepTime)));
                    fixed4 stationary = tex2D(_MainTex, input.uv);
                    float flameMask = smoothstep(0.40, 0.55, (input.uv.y - 0.75) * 4);
                    color = lerp(stationary, color, flameMask);
                }
                float brightness = max(color.r, max(color.g, color.b));
                // Giữ chân sắt tối; chỉ chuyển sắc phần sáng của lửa/linh hồn.
                float mask = smoothstep(0.18, 0.7, brightness) * _Tint;
                color.rgb = lerp(color.rgb, _Color.rgb * brightness, mask);
                color.a *= _Color.a;
                return color;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
