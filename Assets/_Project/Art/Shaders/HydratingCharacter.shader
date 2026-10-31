Shader "Traveller/Hydrating Character"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _ClothColor ("Cloth palette", Color) = (.4,.45,.55,1)
        _TintStrength ("Cloth variation", Range(0,1)) = .75
        _Glossiness ("Smoothness", Range(0,1)) = .2
        _Hydration ("Body volume", Range(0,1)) = 0
        _Wetness ("Water absorption", Range(0,1)) = 0
        _Bottom ("Lowest body point", Float) = 0
        _BodyDepth ("Prone body thickness", Float) = .4
        _PaperThickness ("Dry paper thickness", Float) = .012
        _Elapsed ("Soaking time", Float) = 0
        _Seed ("Variation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap;
        fixed4 _ClothColor;
        half _TintStrength, _Glossiness, _Hydration, _Wetness;
        float _Bottom, _BodyDepth, _PaperThickness, _Elapsed, _Seed;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; fixed4 color : COLOR; };
        void vert(inout appdata_full v)
        {
            float thickness = lerp(_PaperThickness / max(.01,_BodyDepth), 1, _Hydration);
            float wrinkle = sin(v.vertex.x * 17 + v.vertex.z * 12 + _Seed) * .004 * (1-_Hydration);
            float floating = sin(_Elapsed*1.8+_Seed+v.vertex.z*2) * .003 * _Wetness * (1-_Hydration);
            v.vertex.y = _Bottom + (v.vertex.y-_Bottom)*thickness + wrinkle + floating;
            v.normal = normalize(float3(v.normal.x,v.normal.y / max(thickness,.001),v.normal.z));
            v.tangent.xyz = normalize(float3(v.tangent.x,v.tangent.y*thickness,v.tangent.z));
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 baseColor=tex2D(_MainTex,IN.uv_MainTex).rgb;
            half skin=IN.color.b*step(.15,baseColor.g)*step(.035,baseColor.r-baseColor.g)*step(.02,baseColor.g-baseColor.b);
            half cloth=IN.color.a*_TintStrength*(1-skin);
            half value=dot(baseColor,half3(.299,.587,.114));
            fixed3 natural=lerp(baseColor,value*_ClothColor.rgb*2,cloth);
            fixed3 dry=lerp(natural,fixed3(.58,.38,.18)*max(.28,value*1.8),.65);
            o.Albedo=lerp(dry*(1-.22*_Wetness),natural,_Hydration);
            o.Normal=UnpackNormal(tex2D(_BumpMap,IN.uv_BumpMap));
            o.Smoothness=lerp(.03,.34,_Wetness)*(1-_Hydration)+_Glossiness*_Hydration;
            o.Metallic=0;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
