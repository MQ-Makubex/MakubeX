using System;
using UnityEngine;
using UnityEngine.UI;

namespace MakubeX
{
    // A small subdivided UI mesh lets the original artwork bend without editing the PNG.
    public sealed class MemoryMapWaveMesh : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive())return;
            const int columns=64,rows=36;var rect=graphic.rectTransform.rect;
            mesh.Clear();
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++) {
                var uv=new Vector2((float)x/columns,(float)y/rows);
                mesh.AddVert(new Vector3(rect.x+rect.width*uv.x,rect.y+rect.height*uv.y),graphic.color,uv);
            }
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++) {
                int a=y*(columns+1)+x,b=a+1,c=a+columns+1,d=c+1;
                mesh.AddTriangle(a,c,b);mesh.AddTriangle(b,c,d);
            }
        }
    }

    public sealed class MemoryMapEntrance : MonoBehaviour
    {
        public const float Duration=3.4f;
        public float Progress{get;private set;}
        public bool IsPlaying{get;private set;}
        Material artwork,fog;RawImage fogImage;CanvasGroup controls;Action finished;
        float started;
        public void Begin(Material map,CanvasGroup interaction,Action onFinished)
        {
            artwork=map;controls=interaction;finished=onFinished;started=Time.unscaledTime;
            Progress=0;IsPlaying=true;artwork.SetFloat("_Entrance",0);
            fogImage=CreateLoadingFog((RectTransform)transform);fog=fogImage.material;
            controls.alpha=0;controls.interactable=false;controls.blocksRaycasts=false;
            SetProgress(0);
        }
        public static RawImage CreateLoadingFog(RectTransform parent)
        {
            var go=new GameObject("Memory fog",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage),typeof(FogMaterialLifetime));
            var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.SetAsFirstSibling();
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=go.GetComponent<RawImage>();image.raycastTarget=false;
            var shader=Resources.Load<Shader>("Shaders/MemoryMapFog");
            if(!shader)throw new InvalidOperationException("Missing memory-map fog shader");
            image.material=new Material(shader);image.material.SetFloat("_Entrance",0);
            go.GetComponent<FogMaterialLifetime>().owned=image.material;return image;
        }
        void Update()
        {
            if(!IsPlaying)return;
            float value=Mathf.Clamp01((Time.unscaledTime-started)/Duration);SetProgress(value);
            if(value<1)return;
            IsPlaying=false;controls.alpha=1;controls.interactable=true;controls.blocksRaycasts=true;
            if(fogImage)Destroy(fogImage.gameObject);
            var callback=finished;finished=null;callback?.Invoke();
        }
        void SetProgress(float value)
        {
            Progress=value;artwork.SetFloat("_Entrance",value);
            if(fog)fog.SetFloat("_Entrance",value);
            controls.alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1,value));
        }
    }
    public sealed class FogMaterialLifetime : MonoBehaviour
    {
        public Material owned;
        void OnDestroy(){if(owned)Destroy(owned);}
    }
}
