Shader "Traveller/Revival Ripple"
{
    Properties { _Color ("Ripple", Color) = (.5,.8,.8,1) _Opacity ("Opacity", Float) = 0 _Elapsed ("Time", Float) = 0 _Seed ("Variation", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            fixed4 _Color;float _Opacity,_Elapsed,_Seed;
            v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float radius=length(i.uv*2-1);
                float rings=pow(saturate(sin(radius*25-_Elapsed*3+_Seed)),16);
                return fixed4(_Color.rgb,rings*saturate(1-radius)*_Opacity);
            }
            ENDCG
        }
    }
}
