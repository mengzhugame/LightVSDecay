// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 能量护盾 Shader - Light vs Decay
// 目标平台：移动端 + 微信小游戏
// 特性：六边形网格、能量流动、呼吸脉动、菲涅尔边缘、撞击闪烁
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Shader "LightVsDecay/EnergyShield"
{
    Properties
    {
        [Header(Base Settings)]
        _MainTex ("Hex Grid Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        
        [Header(Color Settings)]
        _BaseColor ("Base Color", Color) = (0, 1, 1, 0.3)
        _EdgeColor ("Edge Glow Color", Color) = (0, 1, 1, 1)
        _HitColor ("Hit Flash Color", Color) = (1, 1, 1, 1)
        
        [Header(Animation)]
        _PulseSpeed ("Pulse Speed (呼吸)", Range(0.1, 5)) = 1.5
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3
        _FlowSpeed ("Flow Speed (流动)", Range(0.1, 5)) = 1.0
        _FlowIntensity ("Flow Intensity", Range(0, 1)) = 0.4
        
        [Header(Fresnel Edge)]
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.5
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 1.0
        
        [Header(Dynamic Effects)]
        _HitFlash ("Hit Flash", Range(0, 1)) = 0
        _ShieldAlpha ("Shield Alpha", Range(0, 1)) = 1
        
        [Header(Grid Settings)]
        _GridTiling ("Grid Tiling", Range(1, 10)) = 3
        _GridAlpha ("Grid Alpha", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        
        Pass
        {
            Name "EnergyShield"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            // 移动端优化：降低精度
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 screenPos : TEXCOORD3;
            };
            
            // 贴图
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            
            // 属性
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _HitColor;
                half _PulseSpeed;
                half _PulseIntensity;
                half _FlowSpeed;
                half _FlowIntensity;
                half _FresnelPower;
                half _FresnelIntensity;
                half _HitFlash;
                half _ShieldAlpha;
                half _GridTiling;
                half _GridAlpha;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 位置变换
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                
                // UV
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                // 法线和视角方向（用于菲涅尔）
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                
                // 屏幕坐标（用于噪声采样）
                output.screenPos = posInputs.positionNDC.xy;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 1. 基础UV和时间
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                float2 gridUV = input.uv * _GridTiling;
                float time = _Time.y;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 2. 采样六边形网格
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                half4 gridTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, gridUV);
                half gridMask = gridTex.a;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 3. 呼吸脉动效果 (D模式)
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                half pulse = sin(time * _PulseSpeed) * 0.5 + 0.5;
                pulse = lerp(1.0, pulse, _PulseIntensity);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 4. 能量流动效果 (C模式)
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 从边缘向中心流动的波纹
                float2 centeredUV = input.uv - 0.5;
                float distFromCenter = length(centeredUV);
                
                // 采样噪声（带时间偏移实现流动）
                float2 noiseUV = gridUV + float2(0, time * _FlowSpeed * 0.5);
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                
                // 径向波纹
                half flowWave = sin(distFromCenter * 10.0 - time * _FlowSpeed * 2.0 + noise * 3.14159);
                flowWave = flowWave * 0.5 + 0.5;
                half flowEffect = lerp(1.0, flowWave, _FlowIntensity);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 5. 菲涅尔边缘发光
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                fresnel *= _FresnelIntensity;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 6. 组合颜色
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 基础颜色
                half4 finalColor = _BaseColor;
                
                // 应用呼吸和流动效果
                half animatedAlpha = pulse * flowEffect;
                
                // 网格叠加
                half gridContribution = gridMask * _GridAlpha * animatedAlpha;
                finalColor.rgb = lerp(finalColor.rgb, _EdgeColor.rgb, gridContribution);
                finalColor.a += gridContribution * 0.5;
                
                // 边缘发光叠加
                finalColor.rgb = lerp(finalColor.rgb, _EdgeColor.rgb, fresnel);
                finalColor.a += fresnel * 0.3;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 7. 撞击闪烁效果
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                finalColor.rgb = lerp(finalColor.rgb, _HitColor.rgb, _HitFlash);
                finalColor.a += _HitFlash * 0.5;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 8. 应用总透明度（用于渐隐/渐显）
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                finalColor.a *= _ShieldAlpha;
                
                // 限制Alpha范围
                finalColor.a = saturate(finalColor.a);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Transparent/Diffuse"
}
