// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 2D 能量护盾 Shader - Light vs Decay
// 专为 SpriteRenderer 设计，与 HTML 演示效果一致
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Shader "LightVsDecay/EnergyShield2D"
{
    Properties
    {
        // SpriteRenderer 会自动使用这个作为 Sprite 纹理
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        
        [Header(Shield Textures)]
        _HexGridTex ("Hex Grid Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        
        [Header(Base Color)]
        [HDR]_BaseColor ("Base Color (RGBA)", Color) = (0, 1, 1, 0.3)
        [HDR]_EdgeColor ("Edge Glow Color", Color) = (0, 1, 1, 1)
        [HDR]_HitColor ("Hit Flash Color", Color) = (1, 1, 1, 1)
        
        [Header(Animation_Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0.1, 5)) = 1.5
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3
        
        [Header(Animation_Flow)]
        _FlowSpeed ("Flow Speed", Range(0.1, 5)) = 1.0
        _FlowIntensity ("Flow Intensity", Range(0, 1)) = 0.4
        
        [Header(Edge Glow_Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.5, 10)) = 2.5
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 1.0
        
        [Header(Grid Settings)]
        _GridTiling ("Grid Tiling", Range(1, 10)) = 3
        _GridAlpha ("Grid Alpha", Range(0, 1)) = 0.5
        
        [Header(Dynamic Control)]
        _HitFlash ("Hit Flash", Range(0, 1)) = 0
        _ShieldAlpha ("Shield Alpha", Range(0, 1)) = 1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Cull Back
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            // 移动端优化：降低精度
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // Sprite 纹理（用于遮罩圆形）
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            // 自定义纹理
            TEXTURE2D(_HexGridTex);
            SAMPLER(sampler_HexGridTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
                        // 属性
            CBUFFER_START(UnityPerMaterial)
                float4 _HexGridTex_ST;
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
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                // 位置变换
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                
                // UV
                output.uv = TRANSFORM_TEX(input.uv, _HexGridTex);
                output.color = input.color;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 0. 采样 Sprite 纹理作为圆形遮罩
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                half4 spriteTex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, input.uv);
                half circleMask = spriteTex.a; // 使用 Sprite 的 Alpha 作为遮罩
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 1. 计算 UV 中心距离（用于伪菲涅尔和径向效果）
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                float2 centeredUV = input.uv - 0.5; // UV 中心化 (-0.5 ~ 0.5)
                float distFromCenter = length(centeredUV) * 2.0; // 归一化到 0~1
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 2. 采样六边形网格纹理
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                float2 gridUV = input.uv * _GridTiling;
                half4 hexTex = SAMPLE_TEXTURE2D(_HexGridTex,sampler_HexGridTex, gridUV);
                half gridMask = hexTex.a; // 使用网格纹理的 Alpha

                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 3. 呼吸脉动效果 (Pulse)
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                half pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5; // 0~1 循环
                pulse = lerp(1.0, pulse, _PulseIntensity);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 4. 能量流动效果 (Flow) - 从边缘向中心的波纹
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 采样噪声（带时间偏移）
                float2 noiseUV = gridUV*0.3 + float2(0, _Time.y * _FlowSpeed );
                half noise = SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex, noiseUV).r;

                // 径向波纹：从外向内流动
                half flowWave = sin(distFromCenter * 10.0 - _Time.y * _FlowSpeed * 2.0 + noise * 3.14159);
                flowWave = flowWave * 0.5 + 0.5; // 归一化到 0~1
                half flowEffect = lerp(1.0, flowWave, _FlowIntensity);

                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 5. 伪菲涅尔边缘发光（基于距中心的距离）
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 越靠近边缘，distFromCenter 越接近 1
                half fresnel = pow(saturate(distFromCenter), _FresnelPower);
                fresnel *= _FresnelIntensity;

                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 6. 组合动画效果
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                half animatedAlpha = pulse * flowEffect;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 7. 计算基础颜色
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 基础填充（中心到边缘的渐变）
                half baseGradient = lerp(0.3, 1.0, distFromCenter * 0.7);
                half4 finalColor = _BaseColor;
                finalColor.a = _BaseColor.a * baseGradient;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 8. 叠加六边形网格
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 网格亮度受动画和边缘位置影响
                half gridBrightness = gridMask * _GridAlpha * animatedAlpha;
                // 边缘的网格更亮
                gridBrightness += gridMask * fresnel * 0.3;
                
                // 混合网格颜色
                finalColor.rgb = lerp(finalColor.rgb, _EdgeColor.rgb, gridBrightness);
                finalColor.a += gridBrightness * 0.5;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 9. 叠加菲涅尔边缘发光
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                finalColor.rgb = lerp(finalColor.rgb, _EdgeColor.rgb, fresnel);
                finalColor.a += fresnel * 0.3;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 10. 撞击闪烁效果
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                finalColor.rgb = lerp(finalColor.rgb, _HitColor.rgb, _HitFlash);
                finalColor.a += _HitFlash * 0.5;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 11. 应用圆形遮罩和总透明度
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                finalColor.a *= circleMask * _ShieldAlpha;
                
                // 应用顶点颜色（SpriteRenderer 的 Color）
                finalColor *= input.color;
                
                // 限制 Alpha 范围
                finalColor.a = saturate(finalColor.a* fresnel);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Sprites/Default"
}
