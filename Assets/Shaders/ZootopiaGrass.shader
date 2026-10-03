Shader "Zootopia/Grass"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.16, 0.32, 0.1, 1)
        _TipColor ("Tip", Color) = (0.55, 0.68, 0.28, 1)
        _DryColor ("Dry Tip", Color) = (0.72, 0.68, 0.36, 1)
        _WindStrength ("Wind Strength", Float) = 0.12
        _WindSpeed ("Wind Speed", Float) = 1.6
        _FadeStart ("Fade Start", Float) = 28
        _FadeEnd ("Fade End", Float) = 38
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert nolightmap nodynlightmap nodirlightmap nometa
        #pragma multi_compile_instancing
        #pragma target 3.0

        fixed4 _BaseColor;
        fixed4 _TipColor;
        fixed4 _DryColor;
        float _WindStrength;
        float _WindSpeed;
        float _FadeStart;
        float _FadeEnd;

        struct Input
        {
            float3 worldPos;
            float height;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = v.texcoord.y;                 // 0 at the root, 1 at the tip
            float t = _Time.y * _WindSpeed;
            float sway = sin(t + wp.x * 0.35 + wp.z * 0.2) * 0.6 + sin(t * 1.7 + wp.z * 0.5) * 0.4;
            float3 offset = float3(sway, 0, sway * 0.6) * _WindStrength * h * h;

            // Shrink into the ground near the edge of the draw radius so it doesn't pop.
            float dist = distance(wp.xz, _WorldSpaceCameraPos.xz);
            float fade = 1.0 - saturate((dist - _FadeStart) / max(0.01, _FadeEnd - _FadeStart));
            wp.y -= (1.0 - fade) * h * 0.9;
            wp += offset;

            v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1)).xyz;
            v.normal = float3(0, 1, 0);             // lit like the ground: no dark back faces
            o.height = h;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float n = frac(sin(dot(floor(IN.worldPos.xz * 0.35), float2(12.9898, 78.233))) * 43758.5453);
            fixed3 tip = lerp(_TipColor.rgb, _DryColor.rgb, n * n * 0.7);
            o.Albedo = lerp(_BaseColor.rgb, tip, IN.height);
        }
        ENDCG
    }
    FallBack Off
}
