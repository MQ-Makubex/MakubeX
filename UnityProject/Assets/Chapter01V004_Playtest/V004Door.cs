using UnityEngine;
using MakubeX.Chapter01Integration;

namespace MakubeX.V004
{
    public sealed class V004Door : MonoBehaviour
    {
        public Transform hinge, openPose;
        public Chapter01WalkController player;
        public bool showHelp = true;
        Quaternion closedRotation;
        public bool IsOpen { get; private set; }
        public event System.Action<bool> OpenStateChanged;
        void Awake() { closedRotation=hinge.rotation; }
        public void Toggle() { if(MakubeXApp.Instance && MakubeXApp.Instance.IsPaused)return;IsOpen=!IsOpen;OpenStateChanged?.Invoke(IsOpen); }
        void Update()
        {
            if(MakubeXApp.Instance && MakubeXApp.Instance.IsPaused)return;
            if(Vector3.Distance(player.transform.position,hinge.position)<3f && Input.GetKeyDown(KeyCode.E))Toggle();
            hinge.rotation=Quaternion.RotateTowards(hinge.rotation,IsOpen?openPose.rotation:closedRotation,160f*Time.deltaTime);
        }
        void OnGUI()
        {
            if(!showHelp)return;
            GUI.Box(new Rect(12,76,550,42),"V004 revised hut | E near door: open / close\nExpanded hut, Tripo door, revised corner streetlamps");
        }
    }
}
