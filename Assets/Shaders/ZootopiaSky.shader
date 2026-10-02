Shader "Zootopia/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.25, 0.5, 0.9, 1)
        _HorizonColor ("Horizon", Color) = (0.75, 0.86, 0.95, 1)
        _BottomColor ("Bottom", Color) = (0.45, 0.6, 0.72, 1)
        _SunColor ("Sun", Color) = (1, 0.92, 0.75, 1)
        _SunDir ("Sun Direction", Vector) = (0.4, 0.55, 0.3, 0)
        _SunSize ("Sun Sharpness", Float) = 400
        _SunGlow ("Sun Glow", Float) = 0.35
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

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _BottomColor;
            fixed4 _SunColor;
            float4 _SunDir;
            float _SunSize;
            float _SunGlow;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = saturate(d.y);
                float down = saturate(-d.y);
                fixed3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(up, 0.45));
                col = lerp(col, _BottomColor.rgb, pow(down, 0.35));
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _SunColor.rgb * (pow(s, _SunSize) * 2.0 + pow(s, 8.0) * _SunGlow);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
