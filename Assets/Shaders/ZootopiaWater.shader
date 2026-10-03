Shader "Zootopia/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.2, 0.65, 0.75, 1)
        _DeepColor ("Deep", Color) = (0.05, 0.25, 0.45, 1)
        _SkyColor ("Sky Reflection", Color) = (0.7, 0.82, 0.95, 1)
        _SpecColor ("Specular", Color) = (1, 1, 1, 1)
        _FoamTex ("Shore Foam Mask", 2D) = "black" {}
        _MapSize ("Map Size", Float) = 260
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
        fixed4 _SkyColor;
        sampler2D _FoamTex;
        float _MapSize;
        float _ShoreRadius;
        float _WaveHeight;
        float _WaveSpeed;
        half _Shininess;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
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
            float t = _Time.y * _WaveSpeed;

            // Fine ripples in the normal (cheap analytic noise, no texture).
            float2 p = IN.worldPos.xz;
            float r1 = sin(p.x * 1.3 + t * 2.1) * cos(p.y * 1.1 - t * 1.7);
            float r2 = sin((p.x + p.y) * 2.3 - t * 2.9);
            o.Normal = normalize(float3(r1 * 0.12, r2 * 0.12, 1.0));   // tangent space: z is up

            float dist = length(IN.worldPos.xz);
            float deep = saturate((dist - _ShoreRadius) / 40.0);
            fixed3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, deep);

            // Fresnel: grazing angles reflect the sky.
            float3 v = normalize(IN.viewDir);
            float fres = pow(1.0 - saturate(dot(v, o.Normal)), 4.0);
            water = lerp(water, _SkyColor.rgb, fres * 0.65);

            // Animated white foam where the sea meets the beach.
            float2 uv = IN.worldPos.xz / _MapSize + 0.5;
            float foamMask = tex2D(_FoamTex, uv).r;
            float bands = sin(foamMask * 18.0 - t * 2.2) * 0.5 + 0.5;
            float foam = saturate(foamMask * 1.6 - 0.25) * (0.55 + bands * 0.45);
            water = lerp(water, fixed3(0.95, 0.97, 0.98), foam);

            o.Albedo = water;
            o.Emission = _SkyColor.rgb * fres * 0.15;
            o.Specular = _Shininess;
            o.Gloss = 1.0 - foam;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
