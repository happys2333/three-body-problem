Shader "Traveller/Torch Flame"
{
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha One ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            float4 frag(v2f i):SV_Target
            {
                float y=i.uv.y;float x=(i.uv.x-.5)*2;
                x+=sin(y*13-_Time.y*11)*(.03+y*.14)+sin(y*29-_Time.y*17)*y*.055;
                float width=pow(saturate(1-y),.72)*(.7+.10*sin(_Time.y*15+y*8));
                float edge=1-smoothstep(width*.78,width,abs(x));
                float core=1-smoothstep(width*.1,width*.55,abs(x));
                float3 colour=lerp(float3(1,.12,.005),float3(1,.68,.08),core);
                colour=lerp(colour,float3(1,.95,.50),core*saturate(1-y*2));
                float alpha=edge*smoothstep(0,.09,y)*saturate((1-y)*6);
                return float4(colour,alpha*.9);
            }
            ENDCG
        }
    }
}
