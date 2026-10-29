Shader "Hidden/Traveller/Heat Distortion"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} _Heat ("Heat", Float) = 0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Heat;
            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv=input.uv;
                float strength=_Heat*smoothstep(.9,.1,uv.y);
                uv.x+=sin(uv.y*155+_Time.y*3.2)*.0018*strength;
                uv.y+=sin(uv.x*110+_Time.y*2.1)*.0009*strength;
                fixed4 result=tex2D(_MainTex,uv);
                float vignette=saturate(length(input.uv-.5)*1.4);
                result.rgb=lerp(result.rgb,result.rgb*float3(1.08,.97,.89),vignette*_Heat*.35);
                return result;
            }
            ENDCG
        }
    }
}
