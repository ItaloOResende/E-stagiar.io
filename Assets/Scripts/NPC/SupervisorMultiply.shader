Shader "Supervisor/Model Multiply"
{
 Properties { _Strength("Strength", Float) = 0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend DstColor Zero
   ZWrite Off
   ZTest LEqual
   Cull Back
   Offset -1, -1
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A { float4 positionOS:POSITION; };
   struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
   CBUFFER_START(UnityPerMaterial)
   float4 _StartColor, _EndColor, _Origin, _Axis;
   float _Length, _Offset, _Strength;
   CBUFFER_END
   V vert(A v) { V o; o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); return o; }
   half4 frag(V i):SV_Target
   {
    float t=saturate((dot(i.world-_Origin.xyz,_Axis.xyz)-_Offset)/max(_Length,0.01));
    return half4(lerp(float3(1,1,1),lerp(_StartColor.rgb,_EndColor.rgb,t),saturate(_Strength)),1);
   }
   ENDHLSL
  }
 }
}
