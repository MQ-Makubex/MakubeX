using UnityEngine;

namespace MakubeX.Chapter01Integration
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class Chapter01WalkController : MonoBehaviour
    {
        public Camera view;
        public bool showHelp = true;
        public Vector3 spawn;
        public float walkSpeed = 2.5f, sprintSpeed = 4f;
        [Range(.5f, 10f)] public float lookSensitivity = 3.5f;
        [System.NonSerialized] public bool verificationMode;
        // Opted into by v004 story only; older integration scenes keep their controls.
        [System.NonSerialized] public bool clickToLook;
        public CollisionFlags LastFlags { get; private set; }
        public int Respawns { get; private set; }
        public float VerticalSpeed { get; private set; }
        public CharacterController Body => GetComponent<CharacterController>();
        float pitch;
        bool captureRequested;
        bool previousCapture;
        bool Paused => MakubeXApp.Instance && MakubeXApp.Instance.IsPaused;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int MakubeX_IsLookCaptured();
#endif
        public bool LookCaptured {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                return clickToLook && MakubeX_IsLookCaptured()!=0;
#else
                return captureRequested && (Application.isBatchMode || Cursor.lockState==CursorLockMode.Locked);
#endif
            }
        }

        void Update()
        {
            if (verificationMode || Paused){previousCapture=false;return;}
            if (!clickToLook && Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Input.GetKeyDown(KeyCode.R)) ResetToSpawn();
#if !UNITY_WEBGL || UNITY_EDITOR
            if(clickToLook && Input.GetMouseButtonDown(0))CaptureLook();
#endif
            bool looking=clickToLook ? LookCaptured : Input.GetMouseButton(1);
            if (looking && (!clickToLook || previousCapture))
                ApplyLookDelta(new Vector2(Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y")));
            previousCapture=looking;
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Step(input, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift), Time.deltaTime);
            if (transform.position.y < -8f) ResetToSpawn();
        }

        // The automated traversal calls the same movement and CharacterController.Move as keyboard input.
        public void Step(Vector2 input, bool sprint, float deltaTime)
        {
            if(Paused)return;
            float dt = Mathf.Clamp(deltaTime, 0f, .05f);
            input = Vector2.ClampMagnitude(input, 1f);
            if (Body.isGrounded && VerticalSpeed < 0) VerticalSpeed = -2f;
            VerticalSpeed = Mathf.Max(VerticalSpeed - 20f * dt, -30f);
            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * (sprint ? sprintSpeed : walkSpeed);
            LastFlags = Body.Move((velocity + Vector3.up * VerticalSpeed) * dt);
        }
        public void ApplyLookDelta(Vector2 delta)
        {
            if(Paused || verificationMode || !view)return;
            transform.Rotate(0,delta.x*lookSensitivity,0);
            pitch=Mathf.Clamp(pitch-delta.y*lookSensitivity,-80,80);
            view.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }
        public void CaptureLook()
        {
            if(!clickToLook || Paused || verificationMode)return;
            captureRequested=true;
#if !UNITY_WEBGL || UNITY_EDITOR
            if(!Application.isBatchMode){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
#endif
        }
        public void ReleaseLook(){captureRequested=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OnApplicationFocus(bool focused){if(!focused && clickToLook)ReleaseLook();}
        void OnDisable(){if(clickToLook)ReleaseLook();}

        public void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        public void ResetToSpawn()
        {
            Body.enabled = false;
            transform.position = spawn;
            Body.enabled = true;
            VerticalSpeed = 0;
            Respawns++;
        }

        void OnGUI()
        {
            if (verificationMode || !showHelp) return;
            GUI.Box(new Rect(12, 12, 550, 58), "Chapter 01 - bridge to hut integration test\nWASD / Arrows: walk | Shift: run | Hold RMB: look | R: reset");
        }
    }
}
