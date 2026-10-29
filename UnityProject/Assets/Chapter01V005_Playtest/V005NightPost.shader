// Upgrade NOTE: replaced '_V005CameraToWorld' with 'unity_V005CameraToWorld'

Shader "Hidden/MakubeX/V005NightPost" {
 Properties { _MainTex("Source",2D)="black"{} _BloomTex("Bloom",2D)="black"{} }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  CGINCLUDE
  #include "UnityCG.cginc"
  sampler2D _MainTex,_BloomTex;
  UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
  float4x4 unity_V005CameraToWorld,_V005InverseProjection;
  float segment(float3 o,float3 d,float3 lo,float3 hi,float limit){
   float3 safe=sign(d)*max(abs(d),.00001);safe+=float3(d.x==0,d.y==0,d.z==0)*.00001;
   float3 a=(lo-o)/safe,b=(hi-o)/safe;float3 near=min(a,b),far=max(a,b);
   return max(0,min(min(far.x,min(far.y,far.z)),limit)-max(0,max(near.x,max(near.y,near.z))));
  }
  float3 fogScene(float3 c,float2 uv){
   float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));
   float4 clip=float4(uv*2-1,1,1);float3 view=mul(_V005InverseProjection,clip).xyz;
   float3 direction=mul((float3x3)unity_V005CameraToWorld,normalize(view));float distance=depth/max(.001,abs(normalize(view).z));
   float3 origin=_WorldSpaceCameraPos;
   float optical=segment(origin,direction,float3(-165,-55,-245),float3(115,115,-25),distance)*.0028;
   optical+=segment(origin,direction,float3(-50,-31,-110.5),float3(80,-7,-5.5),distance)*.010;
   optical+=segment(origin,direction,float3(-125,-23.5,-205),float3(45,1.5,-107),distance)*.012;
   float mist=pow(saturate((distance-28)/230),2)*.36;
   float weight=1-exp(-optical)*(1-mist);
   return lerp(c,float3(.026,.047,.078),weight);
  }
  float3 SceneLinear(float3 c){
   #ifdef UNITY_COLORSPACE_GAMMA
    return GammaToLinearSpace(max(c,0));
   #else
    return max(c,0);
   #endif
  }
  float4 _BlurDirection;
  float _Threshold,_BloomIntensity,_Exposure,_ShadowLift;
  float4 extract(v2f_img i):SV_Target {
   float3 c=SceneLinear(tex2D(_MainTex,i.uv).rgb);
   float brightness=max(max(c.r,c.g),c.b);
   float soft=saturate((brightness-_Threshold+.35)/.7);soft=soft*soft*.35;
   float contribution=max(brightness-_Threshold,soft)/max(brightness,.0001);
   return float4(c*contribution,1);
  }
  float4 blur(v2f_img i):SV_Target {
   float2 d=_BlurDirection.xy;
   float3 c=tex2D(_MainTex,i.uv).rgb*.227027;
   c+=(tex2D(_MainTex,i.uv+d*1.384615).rgb+tex2D(_MainTex,i.uv-d*1.384615).rgb)*.316216;
   c+=(tex2D(_MainTex,i.uv+d*3.230769).rgb+tex2D(_MainTex,i.uv-d*3.230769).rgb)*.070270;
   return float4(c,1);
  }
  float4 composite(v2f_img i):SV_Target {
   float3 c=fogScene(SceneLinear(tex2D(_MainTex,i.uv).rgb),i.uv)+tex2D(_BloomTex,i.uv).rgb*_BloomIntensity;
   // Compress HDR light sources rather than increasing the entire night image.
   c*=_Exposure;
   float luminance=dot(c,float3(.2126,.7152,.0722));
   c=lerp(c,luminance.xxx,saturate(luminance*.8)*.6);
   // Filmic toe keeps the original night background dark while preserving
   // bright cloud volumes and the full 7/16-unit warm source radiance.
   c=saturate((c*(2.51*c+.03))/(c*(2.43*c+.59)+.14));
   float shadow=1-smoothstep(.08,.45,dot(c,float3(.2126,.7152,.0722)));
   c+=float3(.65,.8,1)*_ShadowLift*shadow;
   
   #ifdef UNITY_COLORSPACE_GAMMA
    c=LinearToGammaSpace(c);
   #endif
   c*=1-.08*smoothstep(.2,.72,length(i.uv-.5));
   return float4(c,1);
  }
  ENDCG
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment extract
   #pragma target 3.0
   ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment blur
   #pragma target 3.0
   ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment composite
   #pragma target 3.0
   ENDCG }
 }
 Fallback Off
}
