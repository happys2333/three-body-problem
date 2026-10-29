Shader "Hidden/Traveller/Sun Damage"
{
    Properties{_MainTex("Scene",2D)="white"{}}
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _Pulse,_Injury,_Blackout;
            fixed4 frag(v2f_img i):SV_Target
            {
                float2 p=i.uv*2-1;float rim=smoothstep(.35,1.05,max(abs(p.x),abs(p.y)));
                float hit=saturate(_Pulse*.50+_Injury*.12)*rim;
                float3 colour=tex2D(_MainTex,i.uv).rgb;
                colour=lerp(colour,dot(colour,float3(.299,.587,.114)).xxx,saturate(_Injury-.65)*.5);
                colour=lerp(colour,float3(.58,.035,.008),hit);
                return float4(colour*(1-_Blackout),1);
            }
            ENDCG
        }
    }
}
