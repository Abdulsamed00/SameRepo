// Solma (10.11): eski Shader Graph'ın (Solma.shadergraph) yerine elle yazılmış tam ekran shader'ı. Graf, build'i durduran
// geçersiz bir "Blit" geçişi üretiyordu. Davranış aynı: renk doygunluğu 0.08'e kadar iner, miktar global _Solma (0-1).
Shader "Same/SolmaFullscreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Solma"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Solma;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0);
                half luma = dot(c.rgb, half3(0.2126729h, 0.7151522h, 0.0721750h));
                half3 faded = luma + 0.08h * (c.rgb - luma);
                c.rgb = lerp(c.rgb, faded, saturate(_Solma));
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
