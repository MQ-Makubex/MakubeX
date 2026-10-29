Shader "MakubeX/V004 Two Sided Emission" {
 Properties {
  _Color("Base tint",Color)=(1,1,1,1)
  _MainTex("Base",2D)="white"{}
  [HDR] _EmissionColor("Emission radiance",Color)=(1,1,1,1)
  _EmissionMap("Emission mask",2D)="white"{}
 }
 SubShader {
  Tags {"RenderType"="Opaque" "Queue"="Geometry"}
  Cull Off ZWrite On
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   sampler2D _MainTex,_EmissionMap;
   float4 _Color,_EmissionColor;
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1)};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;UNITY_TRANSFER_FOG(o,o.pos);return o;}
   float4 frag(v2f i):SV_Target {
    float4 c=float4(tex2D(_EmissionMap,i.uv).rgb*_EmissionColor.rgb+tex2D(_MainTex,i.uv).rgb*_Color.rgb*.025,1);
    #ifdef UNITY_COLORSPACE_GAMMA
     c.rgb=LinearToGammaSpace(c.rgb);
    #endif
    UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
   ENDCG
  }
 }
 Fallback Off
}
