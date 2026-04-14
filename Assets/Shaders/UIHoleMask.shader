Shader "UI/HoleMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 0, 0, 0.8)
        _HoleCenter ("Hole Center (Screen Pixels)", Vector) = (540, 960, 0, 0)
        _HoleSize   ("Hole Size (Screen Pixels)",   Vector) = (200, 100, 0, 0)
        _CornerRadius ("Corner Radius (Pixels)", Float) = 20

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                float2 texcoord  : TEXCOORD0;
                float4 color     : COLOR;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4    _Color;
            float4    _HoleCenter;
            float4    _HoleSize;
            float     _CornerRadius;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex    = UnityObjectToClipPos(v.vertex);
                o.texcoord  = v.texcoord;
                o.color     = v.color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            float roundedBoxSDF(float2 centerPos, float2 size, float radius)
            {
                float2 d = max(abs(centerPos) - size + radius, 0.0);
                return length(d) - radius;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 转为屏幕像素坐标（_ScreenParams.xy = 屏幕宽高像素数）
                // 像素坐标系下 cornerRadius 单位与 C# 端完全一致，圆角/圆形在任意分辨率都正确
                float2 screenUV   = i.screenPos.xy / i.screenPos.w;
                float2 screenPixel = screenUV * _ScreenParams.xy;
                float2 diff = screenPixel - _HoleCenter.xy;
                float dist = roundedBoxSDF(diff, _HoleSize.xy * 0.5, _CornerRadius);
                // 2px 软边抗锯齿（像素空间下等价于之前的 smoothstep(-0.001,0.001,dist)）
                float alpha = smoothstep(-1.0, 1.0, dist);
                fixed4 col = _Color;
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
}
