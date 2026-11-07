// Ters kabuk kenar çizgisi, normal boyunca şişirme (07.11). Cüzdandaki Same/Outline (Shader Graph) kutunun merkezinden
// sign(pos) ile şişiriyor, bu sadece kutu gibi dışbükey modellerde doğru çalışıyor. Ayna çerçevesi gibi oyuklu/eğri
// modeller için bu sürüm her köşeyi dünya uzayında kendi normali boyunca _OutlineWidth metre dışarı iter.
// Toplamalı saydam, arka yüzler: renk siyahken görünmez, beyazken modelin çevresinde parlak bir kenar kalır (bloom yayar).
Shader "Same/OutlineNormal"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width (m)", Float) = 0.006
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.positionCS = TransformWorldToHClip(positionWS + normalWS * _OutlineWidth);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(_OutlineColor.rgb, 1); }
            ENDHLSL
        }
    }
}
