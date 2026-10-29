Shader "Traveller/Photographic Foliage"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Photographic colour", 2D) = "white" {}
        _AlphaMap ("Leaf silhouette", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 0.75
        _Cutoff ("Cutout", Range(0,1)) = 0.42
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
        _Wind ("Wind amplitude", Range(0,0.2)) = 0.035
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        AlphaToMask On
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert alphatest:_Cutoff
        #pragma target 3.0
        #pragma multi_compile_instancing
        #include "UnityCG.cginc"
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _AlphaMap, _BumpMap;
        fixed4 _Color;
        half _BumpScale, _Glossiness, _Wind;
        struct Input { float2 uv_MainTex; };
        void vert(inout appdata_full v)
        {
            float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
            float wind = sin(w.x * 0.47 + w.z * 0.68 + _Time.y * 1.4) * _Wind;
            // Keep the breeze in metres even for FBXs carrying a centimetre
            // conversion or a rotated import root.
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, float3(wind, 0, wind * 0.45));
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = tex2D(_AlphaMap, IN.uv_MainTex).r;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Metallic = 0; o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/Diffuse"
}
