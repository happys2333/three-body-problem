using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TravellerPlayer : MonoBehaviour
    {
        public Camera viewCamera;
        public float walkSpeed = 3f;
        public float runMultiplier = 1.8f;
        public float lookSensitivity = 2f;
        public float eyeHeight = 1.65f;
        public TravellerActor bodyActor;
        private CharacterController controller;
        private float pitch;
        private float verticalSpeed;
        private bool hasFocus;
        private int ignoreLookFrames;
        public bool InputBlocked { get; set; }
        public bool PointerReleased { get; set; }
        public bool IsRunning { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            hasFocus = Application.isFocused;
            viewCamera.transform.SetParent(transform, false);
            viewCamera.transform.localPosition = Vector3.up * eyeHeight;
            SynchronizeView();
        }

        public void Teleport(Vector3 eyePosition, Vector3 lookAt)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = eyePosition - Vector3.up * eyeHeight;
            var rotation = Quaternion.LookRotation(lookAt - eyePosition);
            transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            viewCamera.transform.localRotation = Quaternion.Euler(rotation.eulerAngles.x, 0f, 0f);
            verticalSpeed = 0f;
            controller.enabled = true;
            SynchronizeView();
            if (bodyActor != null) bodyActor.ResetMotionTracking();
        }

        private void SynchronizeView()
        {
            pitch = viewCamera.transform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }

        private void Update()
        {
            UpdateMovement();
        }

        public void FaceTarget(Vector3 target)
        {
            Vector3 direction = target - viewCamera.transform.position;
            if (direction.sqrMagnitude < 0.01f) return;
            var rotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            viewCamera.transform.localRotation = Quaternion.Euler(rotation.eulerAngles.x, 0f, 0f);
            SynchronizeView();
        }

        private void UpdateMovement()
        {
            bool look = ShouldCaptureMouse(InputBlocked, PointerReleased, hasFocus);
            var desired = look ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != desired)
            {
                Cursor.lockState = desired; ignoreLookFrames = 2;
            }
            Cursor.visible = !look;
            if (look && ignoreLookFrames == 0) LookInput(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")));
            if (ignoreLookFrames > 0) ignoreLookFrames--;
            if (hasFocus) MoveInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), Input.GetKey(KeyCode.LeftShift), Time.deltaTime);
            else IsRunning = false;
        }

        public static bool ShouldCaptureMouse(bool inputBlocked, bool pointerReleased, bool focused) =>
            !inputBlocked && !pointerReleased && focused;

        public void LookInput(Vector2 delta)
        {
            if (InputBlocked || PointerReleased) return;
            var preferences = TravellerUserSettings.Current;
            float sensitivity = lookSensitivity * preferences.mouseSensitivity / 2f;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity * (preferences.invertMouseY ? -1 : 1), -82f, 82f);
            transform.Rotate(Vector3.up, delta.x * sensitivity, Space.World);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused; ignoreLookFrames = 2;
            if (!focused) ReleasePointer();
        }

        public void ReleasePointer()
        { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        public void MoveInput(Vector2 input, bool running, float deltaTime)
        {
            IsRunning = !InputBlocked && running;
            if (!InputBlocked)
            {
                Vector3 direction = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
                Vector3 movement = transform.TransformDirection(direction) * (walkSpeed * (IsRunning ? runMultiplier : 1f));
                if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
                verticalSpeed += Physics.gravity.y * deltaTime;
                movement.y = verticalSpeed;
                controller.Move(movement * deltaTime);
                if (transform.position.y < -25f)
                {
                    var stage = GetComponentInParent<TravellerStage>();
                    if (stage != null) stage.ResetView();
                }
            }
        }

        private void OnDisable()
        {
            ReleasePointer();
        }
    }
}
