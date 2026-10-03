Shader "Zootopia/Road"
{
    // Road strips: photo-scanned asphalt mapped in world space (seamless between roads and the
    // asphalt ground under them) with painted markings and kerbs from a mask (RGB colour, A mask)
    // laid out along the road (u across 0..1, v along).
    Properties
    {
        _AsphaltTex ("Asphalt", 2D) = "white" {}
        _AsphaltNrm ("Asphalt Normal", 2D) = "bump" {}
        _MainTex ("Markings (RGB colour, A mask)", 2D) = "black" {}
        _AsphaltTiling ("Asphalt Tiling", Float) = 0.2
        _SpecColor ("Specular", Color) = (0.6, 0.6, 0.6, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf BlinnPhong nolightmap nodynlightmap nodirlightmap noforwardadd exclude_path:deferred exclude_path:prepass
        #pragma target 3.0

        sampler2D _AsphaltTex, _AsphaltNrm, _MainTex;
        float _AsphaltTiling;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            float2 w = IN.worldPos.xz * _AsphaltTiling;
            fixed3 a = tex2D(_AsphaltTex, w).rgb;
            fixed4 m = tex2D(_MainTex, IN.uv_MainTex);
            // Paint is a bit worn: let the asphalt grain show through.
            o.Albedo = lerp(a, m.rgb * (0.85 + a * 0.3), m.a);
            o.Normal = UnpackNormal(tex2D(_AsphaltNrm, w));
            o.Specular = 0.4;
            o.Gloss = 0.3 + m.a * 0.2;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
