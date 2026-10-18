Shader "TheLabyrinth/Arcane Torch Palette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlameColor ("Flame Palette", Color) = (0.1,0.95,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment ArcaneFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            fixed4 _FlameColor;
            fixed4 ArcaneFrag(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                // Change bright warm flame pixels; retain dark wood and white-hot cores.
                half warm = saturate((color.r - color.b - 0.04h) * 5.0h);
                warm *= smoothstep(0.25h, 0.7h, color.r);
                half brightness = max(color.r, max(color.g, color.b));
                color.rgb = lerp(color.rgb, _FlameColor.rgb * brightness, warm);
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
