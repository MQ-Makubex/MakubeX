using UnityEngine;
using MakubeX.Chapter01Integration;

namespace MakubeX.V003
{
    public sealed class V003Door : MonoBehaviour
    {
        public Transform hinge, openPose;
        public Chapter01WalkController player;
        Quaternion closedRotation;
        public bool IsOpen { get; private set; }
        void Awake() { closedRotation=hinge.rotation; }
        public void Toggle() { IsOpen=!IsOpen; }
        void Update()
        {
            if(Vector3.Distance(player.transform.position,hinge.position)<3f && Input.GetKeyDown(KeyCode.E))Toggle();
            hinge.rotation=Quaternion.RotateTowards(hinge.rotation,IsOpen?openPose.rotation:closedRotation,160f*Time.deltaTime);
        }
        void OnGUI()
        {
            GUI.Box(new Rect(12,76,550,42),"V003 revised hut | E near door: open / close\nWorn wood, original props, four corner streetlamps");
        }
    }
}
