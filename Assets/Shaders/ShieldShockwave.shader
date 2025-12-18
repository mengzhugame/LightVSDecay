// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 护盾恢复冲击波 Shader
// 简单的环形扩散效果
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Shader "LightVsDecay/ShieldShockwave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0, 1, 1, 1)
        _RingWidth ("Ring Width", Range(0.01, 0.5)) = 0.1
        _Softness ("Edge Softness", Range(0.01, 0.3)) = 0.05
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color:COLOR;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color:TEXCOORD1;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _RingWidth;
                half _Softness;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 spriteTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, input.uv);
                // 计算到中心的距离
                float2 centeredUV = input.uv - 0.5;
                float dist = length(centeredUV) * 2.0; // 归一化到 0-1
                
                // 创建环形
                float outerEdge = 1.0;
                float innerEdge = 1.0 - _RingWidth;
                
                // 软边缘
                float ring = smoothstep(innerEdge - _Softness, innerEdge, dist);
                ring *= smoothstep(outerEdge + _Softness, outerEdge, dist);
                
                // 应用颜色和透明度
                half4 finalColor = _BaseColor*input.color*spriteTex;
                finalColor.a *= ring;
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Transparent/Diffuse"
}
