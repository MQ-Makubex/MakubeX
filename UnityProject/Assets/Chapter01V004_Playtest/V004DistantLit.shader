Shader "MakubeX/V004 Distant Vertex Lit" {
 Properties {
  _Color("Base",Color)=(.2,.25,.35,1)
  _Metallic("Metallic",Range(0,1))=.7
  _Glossiness("Smoothness",Range(0,1))=.4
  _SkyDir("Sky direction",Vector)=(0,1,0,0)
  _SkyColor("Sky color",Color)=(.04,.08,.2,1)
  _RimDir("Rim direction",Vector)=(0,1,0,0)
  _RimColor("Rim color",Color)=(.06,.1,.3,1)
 }
 SubShader {
  Tags {"RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Tags {"LightMode"="ForwardBase"}
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fwdbase
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   float4 _Color,_SkyDir,_SkyColor,_RimDir,_RimColor;
   float _Metallic,_Glossiness;
   struct v2f {float4 pos:SV_POSITION;float3 color:TEXCOORD0;UNITY_FOG_COORDS(1)};
   v2f vert(appdata_base v) {
    v2f o;o.pos=UnityObjectToClipPos(v.vertex);
    float3 p=mul(unity_ObjectToWorld,v.vertex).xyz,n=UnityObjectToWorldNormal(v.normal);
    float3 view=normalize(_WorldSpaceCameraPos-p),sun=normalize(UnityWorldSpaceLightDir(p));
    float3 illumination=float3(.04,.055,.085);
    illumination+=_LightColor0.rgb*saturate(dot(n,sun));
    illumination+=_SkyColor.rgb*saturate(dot(n,normalize(_SkyDir.xyz)));
    illumination+=_RimColor.rgb*saturate(dot(n,normalize(_RimDir.xyz)));
    #ifdef VERTEXLIGHT_ON
     illumination+=Shade4PointLights(unity_4LightPosX0,unity_4LightPosY0,unity_4LightPosZ0,unity_LightColor[0].rgb,unity_LightColor[1].rgb,unity_LightColor[2].rgb,unity_LightColor[3].rgb,unity_4LightAtten0,p,n);
    #endif
    float glint=pow(saturate(dot(n,normalize(view+sun))),lerp(8,64,_Glossiness))*.2*_Metallic;
    float edge=pow(1-saturate(dot(n,view)),3)*.02*_Metallic;
    o.color=_Color.rgb*illumination+_LightColor0.rgb*glint+float3(.22,.4,1)*edge;
    UNITY_TRANSFER_FOG(o,o.pos);return o;
   }
   float4 frag(v2f i):SV_Target {float4 c=float4(i.color,1);
 #ifdef UNITY_COLORSPACE_GAMMA
 c.rgb=LinearToGammaSpace(c.rgb);
 #endif
 UNITY_APPLY_FOG(i.fogCoord,c);return c;}
   ENDCG
  }
 }
 Fallback Off
}
