Shader "Hidden/MakubeX/V004NightPost" {
 Properties { _MainTex("Source",2D)="black"{} _BloomTex("Bloom",2D)="black"{} }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  CGINCLUDE
  #include "UnityCG.cginc"
  sampler2D _MainTex,_BloomTex;
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
   float3 c=SceneLinear(tex2D(_MainTex,i.uv).rgb)+tex2D(_BloomTex,i.uv).rgb*_BloomIntensity;
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
