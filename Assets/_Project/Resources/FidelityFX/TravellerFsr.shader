Shader "Hidden/ThreeBodyProblem/FidelityFX FSR"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "EASU"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma require samplelod
            #pragma vertex vert_img
            #pragma fragment Easu
            #include "UnityCG.cginc"
            #define A_GPU 1
            #define A_HLSL 1
            #define FSR_EASU_F 1
            #include "ffx_a.h"
            #include "ffx_fsr1.h"

            Texture2D<float4> _MainTex;
            SamplerState sampler_MainTex;
            float4 _FsrEasu0, _FsrEasu1, _FsrEasu2, _FsrEasu3, _FsrOutputSize;

            // FSR 1 uses perceptual values in [0, 1]. The intermediate holds
            // those values, so each filter works in the same color space.
            float4 Perceptual(float4 sample)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                return sample;
                #else
                return sqrt(saturate(sample));
                #endif
            }

            AF4 FsrEasuRF(AF2 p) { return Perceptual(_MainTex.GatherRed(sampler_MainTex, p)); }
            AF4 FsrEasuGF(AF2 p) { return Perceptual(_MainTex.GatherGreen(sampler_MainTex, p)); }
            AF4 FsrEasuBF(AF2 p) { return Perceptual(_MainTex.GatherBlue(sampler_MainTex, p)); }

            float4 Easu(v2f_img i) : SV_Target
            {
                uint2 pixel = min((uint2)(i.uv * _FsrOutputSize.xy), (uint2)_FsrOutputSize.xy - 1);
                float3 color;
                FsrEasuF(color, pixel, asuint(_FsrEasu0), asuint(_FsrEasu1), asuint(_FsrEasu2), asuint(_FsrEasu3));
                return float4(saturate(color), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "RCAS"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment Rcas
            #include "UnityCG.cginc"
            #define A_GPU 1
            #define A_HLSL 1
            #define FSR_RCAS_F 1
            #include "ffx_a.h"
            #include "ffx_fsr1.h"

            Texture2D<float4> _MainTex;
            float4 _FsrOutputSize, _FsrRcas;

            AF4 FsrRcasLoadF(ASU2 p)
            {
                return _MainTex.Load(int3(clamp(p, int2(0, 0), (int2)_FsrOutputSize.xy - 1), 0));
            }
            void FsrRcasInputF(inout AF1 r, inout AF1 g, inout AF1 b) { }

            float4 Rcas(v2f_img i) : SV_Target
            {
                uint2 pixel = min((uint2)(i.uv * _FsrOutputSize.xy), (uint2)_FsrOutputSize.xy - 1);
                float3 color;
                FsrRcasF(color.r, color.g, color.b, pixel, asuint(_FsrRcas));
                color = saturate(color);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                color *= color;
                #endif
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
