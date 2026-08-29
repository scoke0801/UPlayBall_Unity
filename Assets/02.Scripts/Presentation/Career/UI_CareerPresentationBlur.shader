Shader "Baseball/UI/CareerPresentationBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurSize ("Blur Size", Range(0, 8)) = 3.5
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
            Name "CareerPresentationBlur"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            float _BlurSize;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _BlurSize;
                fixed4 color = tex2D(_MainTex, input.texcoord) * 0.20;
                color += tex2D(_MainTex, input.texcoord + float2(offset.x, 0)) * 0.12;
                color += tex2D(_MainTex, input.texcoord - float2(offset.x, 0)) * 0.12;
                color += tex2D(_MainTex, input.texcoord + float2(0, offset.y)) * 0.12;
                color += tex2D(_MainTex, input.texcoord - float2(0, offset.y)) * 0.12;
                color += tex2D(_MainTex, input.texcoord + offset) * 0.08;
                color += tex2D(_MainTex, input.texcoord - offset) * 0.08;
                color += tex2D(_MainTex, input.texcoord + float2(offset.x, -offset.y)) * 0.08;
                color += tex2D(_MainTex, input.texcoord + float2(-offset.x, offset.y)) * 0.08;
                color *= input.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
