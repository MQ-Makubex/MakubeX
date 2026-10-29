Shader "MakubeX/V005 Weathered Palette" {
 Properties {
  _Color("Tint",Color)=(1,1,1,1)
  _Dark("Dark grain",Color)=(.02,.02,.02,1)
  _Light("Light grain",Color)=(.2,.15,.1,1)
  _Grain("Grain direction",Vector)=(1,1,1,0)
  _Metallic("Metallic",Range(0,1))=0
  _Glossiness("Smoothness",Range(0,1))=.2
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  LOD 200
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  fixed4 _Color,_Dark,_Light;
  float4 _Grain;
  half _Metallic,_Glossiness;
  struct Input { float3 worldPos; };
  float hash3(float3 p) {p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
  float noise3(float3 p) {
   float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
   return lerp(lerp(lerp(hash3(i),hash3(i+float3(1,0,0)),f.x),lerp(hash3(i+float3(0,1,0)),hash3(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash3(i+float3(0,0,1)),hash3(i+float3(1,0,1)),f.x),lerp(hash3(i+float3(0,1,1)),hash3(i+float3(1,1,1)),f.x),f.y),f.z);
  }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float3 p=float3(-IN.worldPos.x,-IN.worldPos.z,IN.worldPos.y);
   float broad=noise3(p*1.8)*.65+noise3(p*3.6)*.25+noise3(p*7.2)*.1;
   float3 g=p*_Grain.xyz*7;float grain=noise3(g)*.65+noise3(g*2)*.25+noise3(g*4)*.10;
   float ramp=smoothstep(.24,.73,broad);
   float wood=step(2,max(_Grain.x,max(_Grain.y,_Grain.z)));
   ramp=lerp(ramp,smoothstep(.23,.77,grain),wood);
   float3 patch=float3(.045,.032,.020);
   #ifdef UNITY_COLORSPACE_GAMMA
   patch=LinearToGammaSpace(patch);
   #endif
   float3 base=lerp(_Dark.rgb,_Light.rgb,ramp)*lerp(.78,1.0,grain);
   base=lerp(base,patch,smoothstep(.35,.75,noise3(p*2.6))*.55*wood);
   o.Albedo=base*_Color.rgb;
   o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Standard"
}
