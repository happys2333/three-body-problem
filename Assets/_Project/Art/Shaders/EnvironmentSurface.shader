Shader "Traveller/Environment Surface"
{
    Properties
    {
        _Color ("Surface tint", Color) = (1,1,1,1)
        _MainTex ("Photographic albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Photographic normal", 2D) = "bump" {}
        _SurfaceMap ("R: roughness G: occlusion", 2D) = "white" {}
        _DetailMetres ("Texture repeat in metres", Float) = 2
        _NormalStrength ("Normal strength", Range(0,2)) = 0.8
        _RoughnessBias ("Roughness bias", Range(-0.3,0.3)) = 0
        _UseUV ("Use authored surface UV", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        #pragma multi_compile_instancing
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap, _SurfaceMap;
        fixed4 _Color;
        half _DetailMetres, _NormalStrength, _RoughnessBias, _UseUV;
        struct Input { float2 uv_MainTex; float3 localPos; float3 localNormal; float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.localPos=v.vertex.xyz; o.localNormal=v.normal;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            if(_UseUV>.5)
            {
                o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb*_Color.rgb;
                o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_NormalStrength);
                o.Metallic=0;
                half2 surface=tex2D(_SurfaceMap,IN.uv_MainTex).rg;
                o.Smoothness=saturate(1-surface.r-_RoughnessBias);
                o.Occlusion=lerp(1,surface.g,.45);o.Alpha=1;
                return;
            }
            float3 n=normalize(IN.localNormal);
            float3 weights=pow(abs(n),4); weights/=max(dot(weights,1),0.0001);
            float3 p=IN.localPos/max(_DetailMetres,0.01);
            float3 direction=step(0,n)*2-1;
            float2 xuv=float2(p.z*direction.x,p.y);
            float2 yuv=float2(p.x,p.z*direction.y);
            float2 zuv=float2(-p.x*direction.z,p.y);
            fixed3 albedo=tex2D(_MainTex,xuv).rgb*weights.x+tex2D(_MainTex,yuv).rgb*weights.y+tex2D(_MainTex,zuv).rgb*weights.z;
            half3 nx=UnpackScaleNormal(tex2D(_BumpMap,xuv),_NormalStrength);
            half3 ny=UnpackScaleNormal(tex2D(_BumpMap,yuv),_NormalStrength);
            half3 nz=UnpackScaleNormal(tex2D(_BumpMap,zuv),_NormalStrength);
            // Blend each axis perturbation about the actual surface normal.
            float3 bump=float3(0,nx.y,nx.x*direction.x)*weights.x
                       +float3(ny.x,0,ny.y*direction.y)*weights.y
                       +float3(-nz.x*direction.z,nz.y,0)*weights.z;
            float3 mapped=UnityObjectToWorldNormal(normalize(n+bump));
            float3 tangent=WorldNormalVector(IN,float3(1,0,0));
            float3 bitangent=WorldNormalVector(IN,float3(0,1,0));
            float3 normal=WorldNormalVector(IN,float3(0,0,1));
            o.Normal=normalize(float3(dot(mapped,tangent),dot(mapped,bitangent),dot(mapped,normal)));
            half2 surface=tex2D(_SurfaceMap,xuv).rg*weights.x+tex2D(_SurfaceMap,yuv).rg*weights.y+tex2D(_SurfaceMap,zuv).rg*weights.z;
            o.Albedo=albedo*_Color.rgb;
            o.Metallic=0; o.Smoothness=saturate(1-surface.r-_RoughnessBias);
            o.Occlusion=lerp(1,surface.g,.45); o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
