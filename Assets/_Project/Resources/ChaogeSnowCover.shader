Shader "Traveller/Chaoge Snow Cover"
{
    Properties
    {
        _Deposits ("Water, dry ice, air ice", Vector) = (0,0,0,0)
        _Bounds ("Outdoor extent", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float4 _Deposits, _Bounds;

            struct Attributes { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 ground : TEXCOORD0;
                half up : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.ground = mul(unity_ObjectToWorld, input.vertex).xz;
                output.up = UnityObjectToWorldNormal(input.normal).y;
                UNITY_TRANSFER_FOG(output, output.pos);
                return output;
            }

            float hash(float2 cell)
            {
                float3 p = frac(float3(cell.xyx) * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise(float2 p)
            {
                float2 cell = floor(p), t = frac(p);
                t = t * t * (3 - 2 * t);
                return lerp(lerp(hash(cell), hash(cell + float2(1, 0)), t.x),
                            lerp(hash(cell + float2(0, 1)), hash(cell + 1), t.x), t.y);
            }

            float spread(float amount, float pattern)
            {
                return smoothstep(.06 + pattern * .76, .19 + pattern * .76, amount);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 distanceToEdge = min(input.ground - _Bounds.xy, _Bounds.zw - input.ground);
                float edge = smoothstep(0, 13, min(distanceToEdge.x, distanceToEdge.y));
                float grain = noise(input.ground * 9);
                float patches = .65 * noise(input.ground * .48) + .35 * noise(input.ground * 2.1);
                float water = spread(_Deposits.x, patches);
                float dryIce = spread(_Deposits.y, noise(input.ground * 1.35 + 25));
                float airIce = spread(_Deposits.z, noise(input.ground * .92 - 39));

                half3 color = lerp(half3(.24, .28, .33), half3(.31, .33, .36), dryIce * .85);
                color = lerp(color, half3(.17, .30, .42), airIce * .87);
                color *= .78 + .24 * grain + .16 * patches;
                half4 result = half4(color, water * edge * smoothstep(.58, .91, input.up) * .94);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
}
