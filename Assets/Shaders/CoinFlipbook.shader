Shader "LightVsDecay/CoinFlipbook"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HDR][MainColor] _Color ("Color", Color) = (1,1,1,1)
        _Columns ("Columns", Float) = 2
        _Rows ("Rows", Float) = 2
        _FPS ("FPS", Float) = 8
        _FrameOffset ("Frame Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Columns;
                float _Rows;
                float _FPS;
                float _FrameOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float columns = max(_Columns, 1.0);
                float rows = max(_Rows, 1.0);
                float totalFrames = columns * rows;
                float frame = fmod(floor(_Time.y * _FPS + _FrameOffset), totalFrames);

                float column = fmod(frame, columns);
                float row = floor(frame / columns);

                float2 frameUV = input.uv;
                frameUV.x = (frameUV.x + column) / columns;
                frameUV.y = (frameUV.y + (rows - 1.0 - row)) / rows;

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, frameUV) * _Color;
                return color;
            }
            ENDHLSL
        }
    }
}
