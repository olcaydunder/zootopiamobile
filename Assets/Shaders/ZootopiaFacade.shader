Shader "Zootopia/Facade"
{
    // Building walls: photo-scanned plaster (or metal) tinted per building, with windows from a
    // mask texture (A = window/frame/sill, RGB their colour, B > 0.5 in the mask's G... see below).
    // Glass reflects the sky / city (reflection probe) and gets a sharp highlight.
    Properties
    {
        _BaseTex ("Wall (plaster / metal)", 2D) = "white" {}
        _BaseNrm ("Wall Normal", 2D) = "bump" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _WindowTex ("Windows (RGB colour, A mask)", 2D) = "black" {}
        _GlassTex ("Glass mask (R)", 2D) = "black" {}
        _BaseScale ("Wall texture scale (per bay, per storey)", Vector) = (1.4, 1.3, 0, 0)
        _Reflect ("Glass reflection", Float) = 0.55
        _SpecColor ("Specular", Color) = (0.9, 0.9, 0.9, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf BlinnPhong
        #pragma target 3.0

        sampler2D _BaseTex, _BaseNrm, _WindowTex, _GlassTex;
        fixed4 _Color;
        float4 _BaseScale;
        float _Reflect;

        struct Input
        {
            float2 uv_WindowTex;
            float3 worldRefl;
            INTERNAL_DATA
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            float2 buv = IN.uv_WindowTex * _BaseScale.xy;
            fixed3 wall = tex2D(_BaseTex, buv).rgb * _Color.rgb * 1.15;
            fixed4 win = tex2D(_WindowTex, IN.uv_WindowTex);
            fixed glass = tex2D(_GlassTex, IN.uv_WindowTex).r;
            o.Albedo = lerp(wall, win.rgb, win.a);

            float3 n = UnpackNormal(tex2D(_BaseNrm, buv));
            o.Normal = normalize(lerp(n, float3(0, 0, 1), win.a));

            float3 r = WorldReflectionVector(IN, o.Normal);
            half3 env = DecodeHDR(UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, r), unity_SpecCube0_HDR);
            o.Emission = env * glass * _Reflect;
            o.Specular = lerp(0.08, 0.9, glass);
            o.Gloss = lerp(0.05, 1.0, glass);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
