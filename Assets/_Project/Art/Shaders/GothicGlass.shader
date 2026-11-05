Shader "Traveller/Gothic Glass"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct Vertex
            {
                float4 position : POSITION;
                fixed4 colour : COLOR;
            };
            struct Fragment
            {
                float4 position : SV_POSITION;
                fixed4 colour : COLOR;
                UNITY_FOG_COORDS(0)
            };
            Fragment vert(Vertex input)
            {
                Fragment output;
                output.position = UnityObjectToClipPos(input.position);
                output.colour = input.colour;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }
            fixed4 frag(Fragment input) : SV_Target
            {
                fixed4 colour = input.colour;
                UNITY_APPLY_FOG(input.fogCoord, colour);
                return colour;
            }
            ENDCG
        }
    }
}
