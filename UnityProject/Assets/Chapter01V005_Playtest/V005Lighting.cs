using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace MakubeX.V005 {
 public sealed class V005Lighting:MonoBehaviour {
  public bool Applied{get;private set;}
  IEnumerator Start(){while(SceneManager.GetActiveScene()!=gameObject.scene)yield return null;Apply();}
  public void Apply(){
   RenderSettings.ambientMode=AmbientMode.Trilight;
   RenderSettings.ambientSkyColor=new Color(.13f,.18f,.24f);RenderSettings.ambientEquatorColor=new Color(.08f,.11f,.15f);RenderSettings.ambientGroundColor=new Color(.06f,.07f,.09f);
   var p=new SphericalHarmonicsL2();p.AddAmbientLight(new Color(.075f,.095f,.13f));p.AddDirectionalLight(Vector3.up,new Color(.12f,.15f,.21f),.4f);RenderSettings.ambientProbe=p;
   RenderSettings.reflectionIntensity=.14f;RenderSettings.fog=false;Applied=true;
  }
 }
}
