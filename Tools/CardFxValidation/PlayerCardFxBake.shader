Shader "Hidden/Baseball/PlayerCardFxBake"
{
    Properties
    {
        _MainTex ("고정 원화 시트", 2D) = "white" {}
        _SweepStrength ("광택 강도", Range(0,1)) = 0.22
        _SparkleStrength ("반짝임 강도", Range(0,1)) = 0.38
        _SweepWidth ("광택 폭", Range(0.01,1)) = 0.18
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _SweepStrength, _SparkleStrength, _SweepWidth;

            float Glint(float2 uv, float2 center)
            {
                float2 d = abs(uv - center);
                return exp(-dot(d / float2(.038, .004), d / float2(.038, .004)))
                    + exp(-dot(d / float2(.004, .026), d / float2(.004, .026)));
            }

            float4 frag(v2f_img input) : SV_Target
            {
                float2 cell = floor(input.uv * 4);
                float2 uv = frac(input.uv * 4);
                float frame = (3 - cell.y) * 4 + cell.x;
                float phase = frame / 15;
                // 모든 프레임은 정확히 같은 첫 칸의 UV를 읽는다. 원화의 이동·확대·회전을 금지한다.
                float4 source = tex2D(_MainTex, float2(uv.x * .25, .75 + uv.y * .25));
                float pulse = sin(UNITY_PI * phase);
                float envelope = pulse * pulse;
                float distance = (uv.x - lerp(-.2, 1.2, smoothstep(0, 1, phase))) / _SweepWidth;
                float sweep = exp(-distance * distance) * _SweepStrength;
                // 반짝임의 중심도 고정하고 광량만 바꾼다.
                float4 pulses = sin(UNITY_PI * (phase + float4(0, .25, .55, .75)));
                pulses *= pulses;
                float sparkle = Glint(uv, float2(.59, .84)) * pulses.x
                    + Glint(uv, float2(.89, .58)) * pulses.y
                    + Glint(uv, float2(.11, .44)) * pulses.z
                    + Glint(uv, float2(.48, .19)) * pulses.w;
                float light = saturate(envelope * (sweep + sparkle * _SparkleStrength));
                return float4(lerp(source.rgb, float3(1, 1, 1), light), 1);
            }
            ENDCG
        }
    }
}
