Shader "MakubeX/V004 Story Powered CRT" {
 Properties {
 _Color("Base tint",Color)=(1,1,1,1) _MainTex("Base",2D)="white"{}
 _BumpMap("Normal",2D)="bump"{} _BumpScale("Normal strength",Float)=1
 _MetallicGlossMap("Metallic smoothness",2D)="white"{} _Metallic("Metallic",Range(0,1))=0 _Glossiness("Smoothness",Range(0,1))=.4 _GlossMapScale("Gloss map scale",Float)=1
 [HDR] _EmissionColor("Emission",Color)=(1,1,1,1) _EmissionMap("Original screen mask",2D)="black"{} _Powered("Power",Range(0,1))=0
 }
 SubShader {Tags {"RenderType"="Opaque"} LOD 200
 CGPROGRAM
 #include "UnityStandardUtils.cginc"
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 #pragma shader_feature_local _NORMALMAP
 #pragma shader_feature_local _METALLICGLOSSMAP
 sampler2D _MainTex,_BumpMap,_MetallicGlossMap,_EmissionMap;half4 _Color,_EmissionColor;half _Metallic,_Glossiness,_GlossMapScale,_Powered,_BumpScale;
 struct Input {float2 uv_MainTex;};
 void surf(Input i,inout SurfaceOutputStandard o){
  float3 emitter=tex2D(_EmissionMap,i.uv_MainTex).rgb;
  float mask=saturate(max(emitter.r,max(emitter.g,emitter.b))*12);
  o.Albedo=tex2D(_MainTex,i.uv_MainTex).rgb*_Color.rgb*lerp(1,.025,(1-_Powered)*mask);
  o.Metallic=_Metallic;o.Smoothness=_Glossiness;
  #ifdef _NORMALMAP
  o.Normal=UnpackScaleNormal(tex2D(_BumpMap,i.uv_MainTex),_BumpScale);
  #endif
  #ifdef _METALLICGLOSSMAP
  float4 packed=tex2D(_MetallicGlossMap,i.uv_MainTex);o.Metallic=packed.r;o.Smoothness=packed.a*_GlossMapScale;
  #endif
  o.Emission=emitter*_EmissionColor.rgb*_Powered;o.Alpha=1;
 }
 ENDCG
 } Fallback "Standard"
}
