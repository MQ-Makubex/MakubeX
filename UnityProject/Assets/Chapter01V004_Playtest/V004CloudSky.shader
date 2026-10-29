Shader "MakubeX/V004 Baked Cloud Sky" {
 Properties {
  _PosX("Positive X",2D)="black"{} _NegX("Negative X",2D)="black"{}
  _PosY("Positive Y",2D)="black"{} _NegY("Negative Y",2D)="black"{}
  _PosZ("Positive Z",2D)="black"{} _NegZ("Negative Z",2D)="black"{}
  _Exposure("Exposure",Float)=1
 }
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _PosX,_NegX,_PosY,_NegY,_PosZ,_NegZ;
   float _Exposure;
   struct v2f {float4 pos:SV_POSITION;float3 direction:TEXCOORD0;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
   float4 frag(v2f i):SV_Target {
    float3 d=normalize(i.direction),a=abs(d);float3 c;float2 uv;
    if(a.x>=a.y && a.x>=a.z) {
     if(d.x>0){uv=float2(-d.z,d.y)/a.x*.5+.5;c=tex2D(_PosX,uv).rgb;}
     else{uv=float2(d.z,d.y)/a.x*.5+.5;c=tex2D(_NegX,uv).rgb;}
    } else if(a.y>=a.z) {
     if(d.y>0){uv=float2(d.x,-d.z)/a.y*.5+.5;c=tex2D(_PosY,uv).rgb;}
     else{uv=float2(d.x,d.z)/a.y*.5+.5;c=tex2D(_NegY,uv).rgb;}
    } else {
     if(d.z>0){uv=float2(d.x,d.y)/a.z*.5+.5;c=tex2D(_PosZ,uv).rgb;}
     else{uv=float2(-d.x,d.y)/a.z*.5+.5;c=tex2D(_NegZ,uv).rgb;}
    }
    c*=_Exposure;
    return float4(c,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
