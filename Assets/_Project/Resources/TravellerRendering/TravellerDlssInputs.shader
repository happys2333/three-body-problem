Shader "Hidden/ThreeBodyProblem/DLSS Inputs"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment CopyDepth
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float CopyDepth(v2f_img i) : SV_Target
            {
                return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
            }
            ENDHLSL
        }
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment CopyMotion
            #include "UnityCG.cginc"
            sampler2D _CameraMotionVectorsTexture;
            float2 CopyMotion(v2f_img i) : SV_Target
            {
                return tex2D(_CameraMotionVectorsTexture, i.uv).rg;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
