Shader "Baseball/UI/MatchUniform"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _UniformColor ("Uniform Color", Color) = (0.16,0.23,0.46,1)
        _UniformMask ("Uniform Region", 2D) = "white" {}
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
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; };
            sampler2D _MainTex;
            sampler2D _UniformMask;
            fixed4 _Color, _UniformColor, _TextureSampleAdd;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o;
                o.world=v.vertex;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.color=v.color*_Color;
                o.uv=v.uv;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 source=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
                // 원화의 청색 의상만 선택한다. 흰 천·피부·눈·글러브와 투명도는 원본을 유지한다.
                float mask=smoothstep(0.047,0.16,source.b-source.r)*smoothstep(0.020,0.08,source.b-source.g);
                mask*=tex2D(_UniformMask,i.uv).r;
                // 카드 유니폼 변환기와 같은 휘도/기준 명도로 음영과 흰 반사광을 보존한다.
                float light=dot(source.rgb,float3(0.2126,0.7152,0.0722))/0.40;
                float3 dyed=light<=1 ? _UniformColor.rgb*light : lerp(_UniformColor.rgb,1,min(0.65,(light-1)*0.45));
                source.rgb=lerp(source.rgb,dyed,mask);
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
