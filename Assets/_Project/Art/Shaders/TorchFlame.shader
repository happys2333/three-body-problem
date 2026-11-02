Shader "Traveller/Torch Flame"
{
    Properties
    {
        _MainTex ("Flame flipbook", 2D) = "black" {}
        _Grid ("Columns, rows", Vector) = (15,4,0,0)
        _FrameRate ("Frames per second", Float) = 30
        _Intensity ("Emission", Range(0,4)) = 1.5
        _Phase ("Instance phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Grid;
            float _FrameRate, _Intensity, _Phase;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
            float2 FrameUV(float2 uv, float frame)
            {
                // Trim the simulation's empty lower border down to the wick.
                uv.y=lerp(.039,.98,uv.y);
                float2 inset=.5*_MainTex_TexelSize.xy*_Grid.xy;
                uv=clamp(uv,inset,1-inset);
                float column=fmod(frame,_Grid.x);
                float row=_Grid.y-1-floor(frame/_Grid.x);
                return (uv+float2(column,row))/_Grid.xy;
            }
            float4 frag(v2f i):SV_Target
            {
                float frames=_Grid.x*_Grid.y;
                float time=fmod(_Time.y*_FrameRate+_Phase,frames);
                float frame=floor(time);
                float4 a=tex2D(_MainTex,FrameUV(i.uv,frame));
                float4 b=tex2D(_MainTex,FrameUV(i.uv,fmod(frame+1,frames)));
                float4 fire=lerp(a,b,frac(time));
                fire.rgb*=_Intensity;
                fire.a*=smoothstep(0,.018,i.uv.y);
                return fire;
            }
            ENDCG
        }
    }
}
