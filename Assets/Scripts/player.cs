using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour
{
    public float speed = 10f;
    public float jumpForce = 5f;
    public float mouseSensitivity = 0.1f;

    public Camera playerCamera;

    private Rigidbody body;

    private Vector2 movementInput;
    private Vector2 lookInput;

    private float cameraRotationX;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            playerCamera.gameObject.SetActive(false);
            return;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (!IsOwner) return;
        // Player links/rechts draaien
        transform.Rotate(
            0f,
            lookInput.x * mouseSensitivity,
            0f
        );

        // Camera omhoog/omlaag draaien
        cameraRotationX -= lookInput.y * mouseSensitivity;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -90f, 90f);

        playerCamera.transform.localRotation =
            Quaternion.Euler(cameraRotationX, 0f, 0f);
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        Vector3 direction =
            transform.right * movementInput.x +
            transform.forward * movementInput.y;

        body.linearVelocity = new Vector3(
            direction.x * speed,
            body.linearVelocity.y,
            direction.z * speed
        );
    }

    void OnMove(InputValue value)
    {
        movementInput = value.Get<Vector2>();

        Debug.Log("Move: " + movementInput);
    }

    void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    void OnJump()
    {
        if (!IsOwner) return;

        body.AddForce(
            Vector3.up * jumpForce,
            ForceMode.Impulse
        );
    }
}