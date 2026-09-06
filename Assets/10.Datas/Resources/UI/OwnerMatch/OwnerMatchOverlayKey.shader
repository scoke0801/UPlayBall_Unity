Shader "Baseball/UI/OwnerMatchOverlayKey"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            v2f vert(appdata input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                fixed3 keyColor = color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                // 키 임계값은 원본 PNG의 sRGB 픽셀값을 기준으로 잡았다. Linear 프로젝트에서도
                // 같은 체크무늬만 제거되도록 판정용 색만 sRGB로 되돌리고 출력색은 변경하지 않는다.
                keyColor = LinearToGammaSpace(keyColor);
                #endif
                float maximum = max(keyColor.r, max(keyColor.g, keyColor.b));
                float minimum = min(keyColor.r, min(keyColor.g, keyColor.b));
                float chroma = maximum - minimum;
                float luminance = dot(keyColor, float3(0.299, 0.587, 0.114));

                // ImageGen이 굽는 무채색 체크무늬만 제거하고 유니폼의 푸른 음영은 보존한다.
                float neutral = 1.0 - smoothstep(0.018, 0.055, chroma);
                float pale = smoothstep(0.90, 0.945, luminance);
                color.a *= 1.0 - neutral * pale;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
