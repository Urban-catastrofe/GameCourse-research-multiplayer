using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hitscan weapon: shots damage other tanks (server applies the damage)
/// and leave a paint splat where they hit. Hitting the arena does nothing
/// beyond a small impact spark.
/// </summary>
public class PaintGun : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] Transform muzzle;

    [Header("Combat")]
    [SerializeField] float range = 60f;
    [SerializeField] float cooldown = 0.35f;
    [SerializeField] int damage = 10;
    [SerializeField] LayerMask hitMask;

    [Header("Shot FX")]
    [SerializeField] float tracerTime = 0.12f;
    [SerializeField] float tracerWidth = 0.12f;
    [SerializeField] float flashTime = 0.07f;
    [SerializeField] float impactTime = 0.25f;
    [SerializeField] Color shotColor = new Color(1f, 0.35f, 0.7f);

    Camera aimCamera;
    TankHealth health;
    float nextFireTime;

    static Material fxMaterial;
    static Material splatMaterial;
    static Texture2D crosshair;

    public override void OnNetworkSpawn()
    {
        health = GetComponentInParent<TankHealth>();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        var root = transform.root;
        aimCamera = root != null ? root.GetComponentInChildren<Camera>(true) : null;
    }

    void Update()
    {
        if (health != null && health.IsDead) return;

        bool held =
            (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
            (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);

        if (!held || Time.time < nextFireTime) return;

        nextFireTime = Time.time + cooldown;
        Fire();
    }

    void Fire()
    {
        // Shots are always horizontal, fired from the barrel along the turret's
        // facing. The camera only provides the view, never the shot direction.
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector3 reference = muzzle != null ? muzzle.forward : transform.forward;
        Vector3 direction = Vector3.ProjectOnPlane(reference, Vector3.up);

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        direction.Normalize();

        ulong targetNetworkObjectId = 0;
        Vector3 endPoint = origin + direction * range;
        Vector3 hitNormal = Vector3.zero;
        bool didHit = false;

        var hits = Physics.RaycastAll(origin, direction, range, hitMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            ulong ownId = 0;
            var ownNetworkObject = GetComponentInParent<NetworkObject>();
            if (ownNetworkObject != null) ownId = ownNetworkObject.NetworkObjectId;

            foreach (var hit in hits)
            {
                var hitNetworkObject = hit.collider.GetComponentInParent<NetworkObject>();

                // Never shoot through / hit your own tank.
                if (hitNetworkObject != null && hitNetworkObject.NetworkObjectId == ownId)
                    continue;

                didHit = true;
                endPoint = hit.point;
                hitNormal = hit.normal;

                if (hitNetworkObject != null && hitNetworkObject.IsSpawned)
                    targetNetworkObjectId = hitNetworkObject.NetworkObjectId;

                break; // first non-self hit stops the shot
            }
        }

        Vector3 muzzlePosition = muzzle != null ? muzzle.position : origin;
        FireServerRpc(targetNetworkObjectId, muzzlePosition, endPoint, hitNormal, didHit);
    }

    [Rpc(SendTo.Server)]
    void FireServerRpc(ulong targetNetworkObjectId, Vector3 muzzlePosition, Vector3 endPoint,
        Vector3 hitNormal, bool didHit, RpcParams rpcParams = default)
    {
        if (targetNetworkObjectId != 0 &&
            NetworkManager != null && NetworkManager.SpawnManager != null &&
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var target) &&
            target.TryGetComponent<TankHealth>(out var targetHealth))
        {
            targetHealth.ApplyDamage(damage, rpcParams.Receive.SenderClientId);
        }

        ShotFxClientRpc(targetNetworkObjectId, muzzlePosition, endPoint, hitNormal, didHit);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShotFxClientRpc(ulong targetNetworkObjectId, Vector3 muzzlePosition, Vector3 endPoint,
        Vector3 hitNormal, bool didHit)
    {
        // The camera sits behind the tank, so when a target is very close the
        // ray can hit in front of the muzzle - stretch the tracer so it never
        // points backwards.
        Vector3 tracerEnd = endPoint;
        Vector3 delta = tracerEnd - muzzlePosition;
        if (delta.sqrMagnitude < 1f)
        {
            Vector3 fallback = muzzle != null ? muzzle.forward : transform.forward;
            tracerEnd = muzzlePosition + (delta.sqrMagnitude > 0.0001f ? delta.normalized : fallback) * 6f;
        }

        SpawnTracer(muzzlePosition, tracerEnd);
        SpawnFlash(muzzlePosition);

        if (!didHit) return;

        SpawnImpact(endPoint, hitNormal);

        if (targetNetworkObjectId != 0 &&
            NetworkManager != null && NetworkManager.SpawnManager != null &&
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var target))
        {
            SpawnSplat(target.transform, endPoint, hitNormal);
        }
    }

    Material GetFxMaterial()
    {
        if (fxMaterial != null) return fxMaterial;

        // Clone a material we know ships with the build (the gun's own mesh).
        Material template = null;
        var gunRenderer = GetComponentInParent<Renderer>();
        if (gunRenderer != null) template = gunRenderer.sharedMaterial;
        if (template == null) template = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        fxMaterial = new Material(template);
        fxMaterial.color = shotColor;

        if (fxMaterial.HasProperty("_EmissionColor"))
        {
            fxMaterial.EnableKeyword("_EMISSION");
            fxMaterial.SetColor("_EmissionColor", shotColor * 2f);
        }

        return fxMaterial;
    }

    void SpawnTracer(Vector3 from, Vector3 to)
    {
        var go = new GameObject("ShotTracer");
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = tracerWidth;
        line.endWidth = tracerWidth * 0.35f;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetFxMaterial();

        Destroy(go, tracerTime);
    }

    void SpawnFlash(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        go.name = "ShotFlash";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.24f;
        go.GetComponent<Renderer>().sharedMaterial = GetFxMaterial();

        Destroy(go, flashTime);
    }

    void SpawnImpact(Vector3 point, Vector3 normal)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        go.name = "ShotImpact";
        go.transform.position = point + normal * 0.05f;
        go.transform.localScale = Vector3.one * 0.22f;
        go.GetComponent<Renderer>().sharedMaterial = GetFxMaterial();

        Destroy(go, impactTime);
    }

    void SpawnSplat(Transform parent, Vector3 point, Vector3 normal)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var collider = quad.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        if (parent != null)
            quad.transform.SetParent(parent, true);

        quad.transform.position = point + normal * 0.03f;
        quad.transform.rotation = Quaternion.LookRotation(normal);
        quad.transform.localScale = Vector3.one * 0.55f;

        var renderer = quad.GetComponent<Renderer>();
        if (splatMaterial == null)
        {
            Material template = null;
            var gunRenderer = GetComponentInParent<Renderer>();
            if (gunRenderer != null) template = gunRenderer.sharedMaterial;
            if (template == null) template = renderer.sharedMaterial;

            splatMaterial = new Material(template);
            splatMaterial.color = new Color(1f, 0.25f, 0.65f);
        }
        renderer.sharedMaterial = splatMaterial;

        Destroy(quad, 5f);
    }

    void OnGUI()
    {
        if (health != null && health.IsDead) return;
        if (aimCamera == null) return;

        if (crosshair == null)
        {
            crosshair = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            crosshair.SetPixel(0, 0, Color.white);
            crosshair.Apply();
        }

        // Draw the reticle on the point the horizontal shot would actually hit,
        // so it always marks the shot line instead of the camera centre.
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector3 reference = muzzle != null ? muzzle.forward : transform.forward;
        Vector3 direction = Vector3.ProjectOnPlane(reference, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        Vector3 point = origin + direction * range;
        if (Physics.Raycast(origin, direction, out var hit, range, hitMask, QueryTriggerInteraction.Ignore))
            point = hit.point;

        Vector3 viewport = aimCamera.WorldToViewportPoint(point);
        if (viewport.z <= 0f) return;

        float x = viewport.x * Screen.width;
        float y = (1f - viewport.y) * Screen.height;

        var previous = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.4f);
        GUI.DrawTexture(new Rect(x - 6f, y - 6f, 12f, 12f), crosshair);

        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(x - 3f, y - 3f, 6f, 6f), crosshair);

        GUI.color = previous;
    }
}
