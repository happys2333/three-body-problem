Shader "Traveller/Palace Craft Surface"
{
    Properties
    {
        _Color ("Base surface", Color) = (.25,.16,.065,1)
        _Secondary ("Patina or hide spots", Color) = (.075,.16,.13,1)
        _Metallic ("Metallic", Range(0,1)) = .85
        _Smoothness ("Smoothness", Range(0,1)) = .28
        _PatternScale ("Surface frequency", Float) = 18
        _Hide ("Hide pattern", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color, _Secondary;
        half _Metallic, _Smoothness, _PatternScale, _Hide;
        struct Input { float3 worldPos; };
        float hash(float3 p) { p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z); }
        float noise(float3 p)
        {
            float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float grain=noise(IN.worldPos*_PatternScale);
            float broad=noise(IN.worldPos*3.3+7);
            float pattern=_Hide>.5?smoothstep(.50,.72,noise(IN.worldPos*11)):smoothstep(.58,.79,broad)*.72;
            o.Albedo=lerp(_Color.rgb,_Secondary.rgb,pattern)*lerp(.79,1.12,grain);
            o.Metallic=_Metallic*(1-pattern*.7);
            o.Smoothness=saturate(_Smoothness+(grain-.5)*.14-pattern*.15);
            o.Occlusion=1; o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
