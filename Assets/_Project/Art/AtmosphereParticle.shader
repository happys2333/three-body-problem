Shader "Traveller/Atmosphere Particle"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            float4 _Color;
            v2f vert(appdata i) { v2f o; o.position=UnityObjectToClipPos(i.vertex);o.color=i.color*_Color;o.uv=i.uv;return o; }
            float4 frag(v2f i):SV_Target { float falloff=saturate(1-length(i.uv-.5)*2);return float4(i.color.rgb,i.color.a*falloff*falloff); }
            ENDCG
        }
    }
}
