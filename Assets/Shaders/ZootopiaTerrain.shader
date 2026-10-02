Shader "Zootopia/Terrain"
{
    Properties
    {
        _MainTex ("Color Map", 2D) = "white" {}
        _DetailTex ("Detail Noise", 2D) = "gray" {}
        _DetailScale ("Detail Scale", Float) = 0.35
        _DetailStrength ("Detail Strength", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _DetailTex;
        float _DetailScale;
        float _DetailStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed3 c = tex2D(_MainTex, IN.uv_MainTex).rgb;
            fixed fine = tex2D(_DetailTex, IN.worldPos.xz * _DetailScale).r;
            fixed coarse = tex2D(_DetailTex, IN.worldPos.xz * (_DetailScale * 0.13)).r;
            fixed d = (fine * 0.65 + coarse * 0.35) - 0.5;
            o.Albedo = c * (1.0 + d * _DetailStrength * 2.0);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
