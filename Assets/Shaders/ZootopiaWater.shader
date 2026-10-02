Shader "Zootopia/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.2, 0.65, 0.75, 1)
        _DeepColor ("Deep", Color) = (0.05, 0.25, 0.45, 1)
        _SpecColor ("Specular", Color) = (1, 1, 1, 1)
        _ShoreRadius ("Shore Radius", Float) = 90
        _WaveHeight ("Wave Height", Float) = 0.35
        _WaveSpeed ("Wave Speed", Float) = 1
        _Shininess ("Shininess", Range(0.03, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf BlinnPhong vertex:vert
        #pragma target 3.0

        fixed4 _ShallowColor;
        fixed4 _DeepColor;
        float _ShoreRadius;
        float _WaveHeight;
        float _WaveSpeed;
        half _Shininess;

        struct Input
        {
            float3 worldPos;
        };

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y * _WaveSpeed;
            float a = wp.x * 0.15 + t;
            float b = wp.z * 0.2 + t * 1.3;
            float c = (wp.x + wp.z) * 0.35 + t * 1.7;
            float w = sin(a) * 0.5 + sin(b) * 0.35 + sin(c) * 0.15;
            v.vertex.y += w * _WaveHeight;
            float dx = (cos(a) * 0.5 * 0.15 + cos(c) * 0.15 * 0.35) * _WaveHeight;
            float dz = (cos(b) * 0.35 * 0.2 + cos(c) * 0.15 * 0.35) * _WaveHeight;
            v.normal = normalize(float3(-dx, 1.0, -dz));
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float dist = length(IN.worldPos.xz);
            float deep = saturate((dist - _ShoreRadius) / 40.0);
            o.Albedo = lerp(_ShallowColor.rgb, _DeepColor.rgb, deep);
            o.Specular = _Shininess;
            o.Gloss = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
