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
        public bool InputBlocked { get; set; }
        public bool IsRunning { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
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
            bool look = !InputBlocked && Input.GetMouseButton(1) && !Input.GetKey(KeyCode.Escape);
            Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !look;
            if (look)
            {
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -82f, 82f);
                transform.Rotate(Vector3.up, Input.GetAxis("Mouse X") * lookSensitivity, Space.World);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            MoveInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), Input.GetKey(KeyCode.LeftShift), Time.deltaTime);
        }

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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
