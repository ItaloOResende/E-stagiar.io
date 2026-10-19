Shader "Supervisor/Model Glow"
{
 Properties { _Strength("Strength", Float) = 0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+11" "RenderType"="Transparent" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend One One
   ZWrite Off
   ZTest LEqual
   Cull Back
   Offset -1, -1
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
   struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
   CBUFFER_START(UnityPerMaterial)
   float4 _GlowColor;
   float _Strength, _Size, _Power;
   CBUFFER_END
   V vert(A v) { V o; o.normal=TransformObjectToWorldNormal(v.normalOS); o.world=TransformObjectToWorld(v.positionOS.xyz)+o.normal*_Size; o.positionCS=TransformWorldToHClip(o.world); return o; }
   half4 frag(V i):SV_Target
   {
    float rim=pow(saturate(1-abs(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.world)))),max(0.1,_Power));
    return half4(_GlowColor.rgb*rim*_Strength,0);
   }
   ENDHLSL
  }
 }
}
