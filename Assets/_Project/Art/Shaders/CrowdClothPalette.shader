Shader "Traveller/Crowd Cloth Palette"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _ClothColor ("Cloth palette", Color) = (0.4,0.45,0.55,1)
        _TintStrength ("Cloth variation", Range(0,1)) = 0.75
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap;
        fixed4 _ClothColor;
        half _TintStrength, _Glossiness;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; fixed4 color : COLOR; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 baseColor = tex2D(_MainTex, IN.uv_MainTex).rgb;
            // Bone-derived vertex alpha protects head, hands, forearms and footwear.
            half skin = IN.color.b * step(0.15, baseColor.g) * step(0.035, baseColor.r-baseColor.g) * step(0.02, baseColor.g-baseColor.b);
            half cloth = IN.color.a * _TintStrength * (1-skin);
            half value = dot(baseColor, half3(0.299,0.587,0.114));
            o.Albedo = lerp(baseColor, value * _ClothColor.rgb * 2, cloth);
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Metallic = 0; o.Smoothness = _Glossiness; o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
