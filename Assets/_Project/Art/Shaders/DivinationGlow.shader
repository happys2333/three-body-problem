Shader "Traveller/Divination Glow"
{
    Properties
    {
        _Color ("Rune light", Color) = (1,.73,.27,1)
        _Reveal ("Stroke reveal", Range(0,1)) = 0
        _Intensity ("Ritual intensity", Float) = 0
        _EffectTime ("Paused presentation clock", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color; float _Reveal, _Intensity, _EffectTime;
            struct v2f { float4 vertex : SV_POSITION; float3 local : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata_full v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz; o.color=v.color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float a=frac(atan2(i.local.x,i.local.z)/6.2831853+1);
                float stroke=saturate((_Reveal-a)*36+.5);
                float sweep=pow(saturate(1-abs(frac(a-_EffectTime*.10)*2-1)),14);
                float shimmer=.82+.18*sin(_EffectTime*2.4-length(i.local.xz)*3);
                fixed3 tint=lerp(_Color.rgb, fixed3(.26,.75,.78),i.color.b*.42);
                return fixed4(tint*(.6+sweep*.7),stroke*_Intensity*shimmer*i.color.a);
            }
            ENDCG
        }
    }
}
