using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MakubeX
{
    public sealed class ChapterNodeGesture : MonoBehaviour, IPointerClickHandler
    {
        MakubeXApp app;int index;
        float previousClick=float.NegativeInfinity;Vector2 previousPosition;
        public void Initialize(MakubeXApp owner,int chapter){app=owner;index=chapter;}
        public void OnPointerClick(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left || !app)return;
            float now=Time.unscaledTime;
            bool doubleClick=data.clickCount==2 || now-previousClick<=.48f && (data.position-previousPosition).sqrMagnitude<=196;
            previousClick=doubleClick ? float.NegativeInfinity : now;previousPosition=data.position;
            if(doubleClick)app.ActivateChapter(index);
        }
    }

    // The highlight itself is round; transparent square corners are never drawn.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CircularChapterHighlight : MaskableGraphic
    {
        const int Segments=64;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;var centre=rect.center;
            float radius=Mathf.Min(rect.width,rect.height)*.5f;
            var c=color;c.a*=.12f;mesh.AddVert(centre,c,Vector2.one*.5f);
            float[] radii={.84f,.94f,1f};float[] alpha={.12f,.8f,0};
            for(int ring=0;ring<radii.Length;ring++)for(int i=0;i<Segments;i++) {
                float angle=i*Mathf.PI*2/Segments;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                c=color;c.a*=alpha[ring];mesh.AddVert(centre+direction*(radius*radii[ring]),c,Vector2.one*.5f+direction*.5f);
            }
            for(int i=0;i<Segments;i++) {
                int next=(i+1)%Segments;mesh.AddTriangle(0,1+i,1+next);
                for(int ring=0;ring<2;ring++) {
                    int inner=1+ring*Segments,outer=inner+Segments;
                    mesh.AddTriangle(inner+i,outer+i,outer+next);mesh.AddTriangle(inner+i,outer+next,inner+next);
                }
            }
        }
    }
}
