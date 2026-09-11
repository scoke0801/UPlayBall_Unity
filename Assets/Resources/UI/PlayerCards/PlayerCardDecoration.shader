Shader "Baseball/UI/PlayerCardDecoration"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
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
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 mode:TEXCOORD1; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float mode:TEXCOORD2; };
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o;
                o.world=v.vertex;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.color=v.color*_Color;
                o.uv=v.uv;
                o.mode=v.mode.x;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 source=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
                // 문장 사각형의 배경색이 선수 위에 네모로 남지 않도록 금속색만 전경으로 쓴다.
                // 외곽·명찰(mode=0)은 원화 그대로 유지한다.
                if(i.mode>0.5 && i.mode<1.5)
                {
                    float warm=smoothstep(0.07,0.16,source.r-source.b)*smoothstep(0.42,0.62,source.g/max(source.r,0.001));
                    float highlight=smoothstep(0.74,0.88,min(source.r,source.g));
                    source.a*=max(warm,highlight);
                }
                else if(i.mode>2.5)
                {
                    // 레어의 은색 배지를 재배치할 때 주황색 사진 배경을 제외한다.
                    source.a*=1-smoothstep(0.08,0.2,source.r-source.g);
                }
                else if(i.mode>1.5)
                {
                    // 올스타는 은색 리본·분홍 별을 보존하고 청색 사진 배경만 제거한다.
                    source.a*=1-smoothstep(0.025,0.08,source.b-source.r);
                }
                fixed4 result=source*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
