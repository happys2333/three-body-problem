Shader "Traveller/Celestial Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.09,0.17,0.25,1)
        _Horizon ("Horizon", Color) = (0.8,0.5,0.25,1)
        _Ground ("Ground", Color) = (0.12,0.09,0.06,1)
        _SunA ("Sun A", Vector) = (0.4,0.2,1,0)
        _SunB ("Sun B", Vector) = (-0.3,0.4,1,0)
        _SunC ("Sun C", Vector) = (0.1,0.6,1,0)
        _SunRadii ("Angular radii", Vector) = (0.035,0.035,0.035,0)
        _SunCount ("Visible suns", Float) = 1
        _Stars ("Stars", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            float4 _Zenith, _Horizon, _Ground, _SunA, _SunB, _SunC, _SunRadii;
            float _SunCount, _Stars;
            v2f vert(float4 vertex : POSITION) { v2f o; o.position=UnityObjectToClipPos(vertex); o.direction=vertex.xyz; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float3 solar(float3 ray,float3 direction,float radius,float3 tint)
            {
                float cosine=dot(ray,normalize(direction));
                float disc=smoothstep(cos(radius*1.04),cos(radius*.96),cosine);
                float halo=pow(saturate(cosine),max(5.0,1.5/(radius*radius)));
                return disc*tint*3.0 + halo*tint*.55;
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 ray=normalize(i.direction);
                float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(ray.y),.55));
                color=lerp(_Ground.rgb,color,smoothstep(-.1,.025,ray.y));
                float2 skyUV=float2(atan2(ray.x,ray.z),asin(ray.y))*float2(190,150);
                float2 cell=floor(skyUV);
                float brightness=hash(cell);
                float starlight=pow(saturate(1.0-length(frac(skyUV)-.5)*2),14);
                color+=step(.985,brightness)*starlight*_Stars*float3(.6,.76,1)*2*smoothstep(0,.2,ray.y);
                if(_SunCount>.5) color+=solar(ray,_SunA.xyz,_SunRadii.x,float3(1,.67,.26));
                if(_SunCount>1.5) color+=solar(ray,_SunB.xyz,_SunRadii.y,float3(1,.87,.57));
                if(_SunCount>2.5) color+=solar(ray,_SunC.xyz,_SunRadii.z,float3(1,.45,.18));
                return float4(color,1);
            }
            ENDCG
        }
    }
}
