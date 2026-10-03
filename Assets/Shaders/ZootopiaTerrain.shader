Shader "Zootopia/Terrain"
{
    // Ground of the city map. High quality (LOD 300): five photo-scanned layers (grass, forest floor,
    // dirt, paving, asphalt) blended by a splat map, each with its own normal map, plus macro
    // variation. The colour map's alpha marks places that keep the painted colour (sand, sea floor,
    // pools, rock slopes). Low quality (LOD 100): painted colour map with procedural detail only.
    Properties
    {
        _MainTex ("Color Map (A = use colour)", 2D) = "white" {}
        _Splat ("Splat (R grass, G forest, B dirt, A paving; rest asphalt)", 2D) = "black" {}
        _GrassTex ("Grass", 2D) = "white" {}
        _GrassNrm ("Grass Normal", 2D) = "bump" {}
        _ForestTex ("Forest floor", 2D) = "white" {}
        _ForestNrm ("Forest Normal", 2D) = "bump" {}
        _DirtTex ("Dirt", 2D) = "white" {}
        _DirtNrm ("Dirt Normal", 2D) = "bump" {}
        _PavingTex ("Paving", 2D) = "white" {}
        _PavingNrm ("Paving Normal", 2D) = "bump" {}
        _AsphaltTex ("Asphalt", 2D) = "white" {}
        _AsphaltNrm ("Asphalt Normal", 2D) = "bump" {}
        _Tiling ("Tiling (grass, forest, dirt, paving)", Vector) = (0.22, 0.25, 0.25, 0.4)
        _AsphaltTiling ("Asphalt Tiling", Float) = 0.2
        _DetailTex ("Detail Noise", 2D) = "gray" {}
        _DetailScale ("Detail Scale", Float) = 0.35
        _DetailStrength ("Detail Strength", Float) = 0.45
        _DetailNormal ("Detail Normal", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Float) = 0.6
        _HasLayers ("Has Layers", Float) = 0
        _SpecColor ("Specular", Color) = (0.6, 0.6, 0.6, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf BlinnPhong
        #pragma target 3.0

        sampler2D _MainTex, _Splat, _DetailTex;
        sampler2D _GrassTex, _GrassNrm, _ForestTex, _ForestNrm, _DirtTex, _DirtNrm, _PavingTex, _PavingNrm, _AsphaltTex, _AsphaltNrm;
        float4 _Tiling;
        float _AsphaltTiling, _DetailScale, _DetailStrength, _HasLayers;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 cm = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 s = tex2D(_Splat, IN.uv_MainTex);
            float2 w = IN.worldPos.xz;

            // Macro variation breaks up visible tiling.
            fixed macro = tex2D(_DetailTex, w * (_DetailScale * 0.05)).r;
            fixed micro = tex2D(_DetailTex, w * _DetailScale).r;
            float wa = saturate(1.0 - (s.r + s.g + s.b + s.a));

            // Two scales per layer for the soft ground, so close-ups and distance both look right.
            fixed3 g = tex2D(_GrassTex, w * _Tiling.x).rgb * 0.6 + tex2D(_GrassTex, w * (_Tiling.x * 0.23)).rgb * 0.4;
            fixed3 f = tex2D(_ForestTex, w * _Tiling.y).rgb;
            fixed3 d = tex2D(_DirtTex, w * _Tiling.z).rgb;
            fixed3 p = tex2D(_PavingTex, w * _Tiling.w).rgb;
            fixed3 a = tex2D(_AsphaltTex, w * _AsphaltTiling).rgb;
            fixed3 layers = g * s.r + f * s.g + d * s.b + p * s.a + a * wa;

            // Grass gets the painted green tint of the colour map so parks look lush.
            fixed3 tinted = lerp(layers, layers * cm.rgb * 2.2, s.r * 0.45);
            fixed3 albedo = lerp(tinted, cm.rgb, cm.a);
            albedo *= 0.88 + macro * 0.24 + (micro - 0.5) * 0.08;
            o.Albedo = lerp(cm.rgb, albedo, _HasLayers);

            float3 n = UnpackNormal(tex2D(_GrassNrm, w * _Tiling.x)) * s.r
                     + UnpackNormal(tex2D(_ForestNrm, w * _Tiling.y)) * s.g
                     + UnpackNormal(tex2D(_DirtNrm, w * _Tiling.z)) * s.b
                     + UnpackNormal(tex2D(_PavingNrm, w * _Tiling.w)) * s.a
                     + UnpackNormal(tex2D(_AsphaltNrm, w * _AsphaltTiling)) * wa;
            n = lerp(float3(0, 0, 1), n, (1.0 - cm.a) * _HasLayers);
            o.Normal = normalize(float3(n.xy, max(0.2, n.z)));

            // A little sheen on asphalt and paving.
            o.Specular = 0.35;
            o.Gloss = (wa * 0.35 + s.a * 0.2) * _HasLayers;
        }
        ENDCG
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _DetailTex;
        sampler2D _DetailNormal;
        float _DetailScale;
        float _DetailStrength;
        float _NormalStrength;

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

            // Runtime-generated normal map: plain RGB encoding (not DXT5nm), unpacked by hand.
            float3 n1 = tex2D(_DetailNormal, IN.worldPos.xz * _DetailScale).rgb * 2.0 - 1.0;
            float3 n2 = tex2D(_DetailNormal, IN.worldPos.xz * (_DetailScale * 0.21)).rgb * 2.0 - 1.0;
            float2 xy = (n1.xy * 0.6 + n2.xy * 0.4) * _NormalStrength;
            o.Normal = normalize(float3(xy, 1.0));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
