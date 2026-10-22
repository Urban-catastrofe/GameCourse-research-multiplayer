using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class TurretController : NetworkBehaviour
{
    [SerializeField] float rotationSpeed = 100f;

    // Owner writes the turret yaw; everyone else reads it.
    readonly NetworkVariable<float> turretYaw = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            turretYaw.Value = transform.localEulerAngles.y;

        ApplyYaw();
    }

    void Update()
    {
        if (!IsOwner)
        {
            ApplyYaw();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        float rotationInput = 0f;

        if (keyboard.zKey.isPressed)
            rotationInput -= 1f;

        if (keyboard.xKey.isPressed)
            rotationInput += 1f;

        if (rotationInput != 0f)
        {
            transform.Rotate(0f, rotationInput * rotationSpeed * Time.deltaTime, 0f, Space.Self);
            turretYaw.Value = transform.localEulerAngles.y;
        }
    }

    void ApplyYaw()
    {
        var e = transform.localEulerAngles;
        transform.localRotation = Quaternion.Euler(e.x, turretYaw.Value, e.z);
    }
}
