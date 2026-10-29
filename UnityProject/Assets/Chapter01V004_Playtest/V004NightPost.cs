using UnityEngine;

namespace MakubeX.V004
{
    [ExecuteAlways,RequireComponent(typeof(Camera))]
    public sealed class V004NightPost : MonoBehaviour
    {
        public Material filterMaterial;
        [Range(0,1)] public float bloomIntensity=.22f;
        [Range(.1f,8)] public float threshold=1.05f;
        [Range(.1f,3)] public float exposure=1.05f;
        [Range(0,.08f)] public float shadowLift;
        Material renderMaterial,sourceMaterial;
        void ReleaseMaterial()
        {
            if(renderMaterial!=null){if(Application.isPlaying)Destroy(renderMaterial);else DestroyImmediate(renderMaterial);}
            renderMaterial=null;sourceMaterial=null;
        }
        void OnDisable(){ReleaseMaterial();}
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(filterMaterial==null || !filterMaterial.shader.isSupported){Graphics.Blit(source,destination);return;}
            if(renderMaterial==null || sourceMaterial!=filterMaterial || renderMaterial.shader!=filterMaterial.shader)
            {
                ReleaseMaterial();sourceMaterial=filterMaterial;
                renderMaterial=new Material(filterMaterial){hideFlags=HideFlags.HideAndDontSave};
            }
            int width=Mathf.Max(1,source.width/4),height=Mathf.Max(1,source.height/4);
            var a=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGBHalf);
            var b=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGBHalf);
            a.filterMode=b.filterMode=FilterMode.Bilinear;
            try
            {
                renderMaterial.SetFloat("_Threshold",threshold);renderMaterial.SetFloat("_BloomIntensity",bloomIntensity);renderMaterial.SetFloat("_Exposure",exposure);
                renderMaterial.SetFloat("_ShadowLift",shadowLift);
                Graphics.Blit(source,a,renderMaterial,0);
                for(int i=0;i<2;i++)
                {
                    renderMaterial.SetVector("_BlurDirection",new Vector4(1f/width,0,0,0));Graphics.Blit(a,b,renderMaterial,1);
                    renderMaterial.SetVector("_BlurDirection",new Vector4(0,1f/height,0,0));Graphics.Blit(b,a,renderMaterial,1);
                }
                renderMaterial.SetTexture("_BloomTex",a);Graphics.Blit(source,destination,renderMaterial,2);
            }
            finally {renderMaterial.SetTexture("_BloomTex",null);RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
    }
}
