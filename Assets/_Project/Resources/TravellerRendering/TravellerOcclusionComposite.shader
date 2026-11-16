Shader "Hidden/ThreeBodyProblem/Ray Traced Occlusion Composite"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Composite
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _Occlusion;
            float4 Composite(v2f_img i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                color.rgb *= 1.0 - 0.32 * tex2D(_Occlusion, i.uv).r;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
