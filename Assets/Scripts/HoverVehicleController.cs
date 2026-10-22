using TankDemo.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ClientNetworkTransform))]
public class HoverVehicleController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 26f;   // forward/back acceleration (m/s^2)
    [SerializeField] float turnSpeed = 110f;  // yaw speed (deg/s)
    [SerializeField] float maxSpeed = 18f;    // horizontal speed clamp (m/s)
    [SerializeField] float lateralGrip = 6f;  // sideways velocity bleed (1/s) so the hull follows its nose

    [Header("Hover springs")]
    [SerializeField] float hoverHeight = 1.2f;
    [SerializeField] float springStrength = 7000f;
    [SerializeField] float springDamper = 900f;
    [SerializeField] float maxSpringForce = 6000f;
    [SerializeField] float rayLength = 3.5f;
    [SerializeField] LayerMask groundMask;
    [SerializeField] Vector2 cornerXZ = new Vector2(1.1f, 1.5f);

    [Header("Aim")]
    // Shots are fired horizontally along the hull/turret forward; the camera only
    // provides the view, it does not control the shot direction.

    Rigidbody rb;
    TankHealth health;
    Vector3 groundNormal = Vector3.up;
    Camera viewCamera;
    float moveInput;
    float turnInput;
    float yaw;   // authoritative hull yaw, advanced by input only

#if UNITY_EDITOR
    // Temporary hooks so MCP tooling can drive the tank without a keyboard.
    public static float DebugMove;
    public static float DebugTurn;
#endif

    /// <summary>
    /// Single source of truth for the spawn ring, shared with the server's
    /// connection approval so both sides place players identically.
    /// </summary>
    public static void GetSpawnPose(ulong clientId, out Vector3 position, out Quaternion rotation)
    {
        SpawnSlots.Pose((int)(clientId % (ulong)SpawnSlots.Count), out position, out rotation);
    }

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<TankHealth>();
        rb.isKinematic = !IsOwner; // remote tanks are driven by ClientNetworkTransform
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        viewCamera = GetComponentInChildren<Camera>(true);
        if (viewCamera != null)
            viewCamera.gameObject.SetActive(IsOwner);

        if (IsOwner)
        {
            TeleportToSpawn();
            StartCoroutine(FixSpawnAfterInitialSync());
        }
    }

    /// <summary>
    /// NGO applies the NetworkTransform's initial state (prefab pose) right after
    /// spawn, which teleports the owner. Re-apply the ring slot for a short window
    /// so a late-arriving initial state can never leave the player out of position.
    /// </summary>
    System.Collections.IEnumerator FixSpawnAfterInitialSync()
    {
        for (int i = 0; i < 20; i++)
        {
            yield return null;
            if (!IsOwner)
                yield break;

            TeleportToSpawn();
        }
    }

    public void TeleportToSpawn()
    {
        GetSpawnPose(OwnerClientId, out var position, out var rotation);
        transform.SetPositionAndRotation(position, rotation);
        yaw = rotation.eulerAngles.y;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (health != null && health.IsDead)
        {
            moveInput = 0f;
            turnInput = 0f;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        moveInput = 0f;
        turnInput = 0f;

        if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
            moveInput += 1f;

        if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
            moveInput -= 1f;

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
            turnInput += 1f;

        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
            turnInput -= 1f;

#if UNITY_EDITOR
        if (DebugMove != 0f || DebugTurn != 0f)
        {
            moveInput = DebugMove;
            turnInput = DebugTurn;
        }
#endif
    }

    void LateUpdate()
    {
        if (!IsOwner || viewCamera == null) return;
        if (!viewCamera.gameObject.activeInHierarchy) return;

        // The camera hangs below the Turret, which has a non-uniform scale; that
        // shears its world rotation (the horizon rolls as the tank turns). Rebuild
        // the view rotation as level yaw + the authored pitch.
        var camTransform = viewCamera.transform;
        Vector3 forward = camTransform.forward;
        float pitch = Mathf.Asin(Mathf.Clamp(-forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = transform.forward;
        flatForward.Normalize();

        camTransform.rotation =
            Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
    }

    void FixedUpdate()
    {
        if (!IsOwner || rb == null) return;
        if (health != null && health.IsDead) return;

        // Yaw: impose a level, yaw-only rotation every step, advanced only by
        // input. Writing angular velocity directly let spring/contact torques
        // tumble the hull, and re-reading the body's yaw let bumps accumulate
        // into a slow drift-spin, so the yaw is tracked here instead.
        yaw += turnInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        rb.angularVelocity = Vector3.zero;

        // Thrust along the hull, projected onto the ground plane
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        rb.AddForce(flatForward * (moveInput * moveSpeed), ForceMode.Acceleration);

        // Arcade grip: bleed off sideways velocity so the tank does not ice-skate
        Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
        float sideSpeed = Vector3.Dot(rb.linearVelocity, flatRight);
        rb.AddForce(-flatRight * (sideSpeed * lateralGrip), ForceMode.Acceleration);

        // Four hover springs
        Vector3 avgNormal = Vector3.zero;
        int hits = 0;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 origin = transform.TransformPoint(
                    new Vector3(cornerXZ.x * sx, -0.4f, cornerXZ.y * sz));

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength,
                        groundMask, QueryTriggerInteraction.Ignore))
                {
                    hits++;
                    avgNormal += hit.normal;

                    float error = hoverHeight - hit.distance; // >0 -> too low
                    float velUp = Vector3.Dot(rb.GetPointVelocity(hit.point), Vector3.up);
                    float force = Mathf.Clamp(error * springStrength - velUp * springDamper,
                                              0f, maxSpringForce);

                    rb.AddForceAtPosition(Vector3.up * force, hit.point);
                }
            }
        }

        groundNormal = hits > 0 ? (avgNormal / hits).normalized : Vector3.up;

        // Speed clamp
        Vector3 flatVel = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        if (flatVel.magnitude > maxSpeed)
        {
            flatVel = flatVel.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(flatVel.x, rb.linearVelocity.y, flatVel.z);
        }
    }
}
