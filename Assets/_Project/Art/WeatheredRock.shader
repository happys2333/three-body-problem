Shader "Traveller/Weathered Rock"
{
    Properties
    {
        _Color ("Stone tint", Color) = (0.75,0.68,0.55,1)
        _MainTex ("Rock surface", 2D) = "white" {}
        [Normal] _BumpMap ("Rock detail", 2D) = "bump" {}
        _Scale ("Texture size in metres", Float) = 3
        _Glossiness ("Smoothness", Range(0,1)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color;
        half _Glossiness, _Scale;
        struct Input { float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = abs(WorldNormalVector(IN,float3(0,0,1)));
            float3 weight = pow(n,4); weight /= max(dot(weight,1),0.0001);
            float3 p = IN.worldPos / max(_Scale,0.01);
            o.Albedo = _Color.rgb * (tex2D(_MainTex,p.yz).rgb*weight.x + tex2D(_MainTex,p.xz).rgb*weight.y + tex2D(_MainTex,p.xy).rgb*weight.z);
            half3 bump = UnpackNormal(tex2D(_BumpMap,p.yz))*weight.x + UnpackNormal(tex2D(_BumpMap,p.xz))*weight.y + UnpackNormal(tex2D(_BumpMap,p.xy))*weight.z;
            o.Normal = normalize(half3(bump.xy*0.7,1));
            o.Metallic = 0; o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
