using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Workshop: latency and rollback")]
    [SerializeField] bool workshopExerciseEnabled = true;
    [SerializeField, Range(0f, 1.5f)] float simulatedServerDelay = 0.45f;

    // OPGAVE 1: zet dit op true zodat de eigenaar direct lokale feedback krijgt.
    static bool predictLocalShot = false;

    // OPGAVE 2: zet dit op true zodat fout voorspelde feedback wordt opgeruimd.
    static bool rollbackPredictedShot = false;

    Camera aimCamera;
    TankHealth health;
    float nextFireTime;
    int nextShotId = 1;
    readonly Dictionary<int, List<GameObject>> predictedShotObjects = new Dictionary<int, List<GameObject>>();

    static Material fxMaterial;
    static Material splatMaterial;
    static Material predictedSplatMaterial;
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
        int shotId = nextShotId++;

        if (workshopExerciseEnabled && predictLocalShot)
        {
            predictedShotObjects[shotId] =
                SpawnShotFx(targetNetworkObjectId, muzzlePosition, endPoint, hitNormal, didHit, true);
        }

        FireServerRpc(shotId, targetNetworkObjectId, muzzlePosition, endPoint, hitNormal, didHit);
    }

    [Rpc(SendTo.Server)]
    void FireServerRpc(int shotId, ulong targetNetworkObjectId, Vector3 muzzlePosition, Vector3 endPoint,
        Vector3 hitNormal, bool didHit, RpcParams rpcParams = default)
    {
        if (workshopExerciseEnabled && simulatedServerDelay > 0f)
        {
            StartCoroutine(FinishFireAfterDelay(shotId, targetNetworkObjectId, muzzlePosition, endPoint,
                hitNormal, didHit, rpcParams.Receive.SenderClientId));
            return;
        }

        FinishFire(shotId, targetNetworkObjectId, muzzlePosition, endPoint, hitNormal, didHit,
            rpcParams.Receive.SenderClientId);
    }

    IEnumerator FinishFireAfterDelay(int shotId, ulong targetNetworkObjectId, Vector3 muzzlePosition,
        Vector3 endPoint, Vector3 hitNormal, bool didHit, ulong senderClientId)
    {
        yield return new WaitForSeconds(simulatedServerDelay);
        FinishFire(shotId, targetNetworkObjectId, muzzlePosition, endPoint, hitNormal, didHit, senderClientId);
    }

    void FinishFire(int shotId, ulong predictedTargetNetworkObjectId, Vector3 muzzlePosition, Vector3 endPoint,
        Vector3 hitNormal, bool didHit, ulong senderClientId)
    {
        ulong authoritativeTargetNetworkObjectId = predictedTargetNetworkObjectId;
        Vector3 authoritativeEndPoint = endPoint;
        Vector3 authoritativeHitNormal = hitNormal;
        bool authoritativeDidHit = didHit;
        bool acceptedPrediction = true;

        if (workshopExerciseEnabled)
        {
            acceptedPrediction = ValidateShot(predictedTargetNetworkObjectId, muzzlePosition, endPoint,
                out authoritativeTargetNetworkObjectId, out authoritativeEndPoint, out authoritativeHitNormal,
                out authoritativeDidHit);
        }

        if (acceptedPrediction &&
            authoritativeTargetNetworkObjectId != 0 &&
            NetworkManager != null && NetworkManager.SpawnManager != null &&
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(authoritativeTargetNetworkObjectId, out var target) &&
            target.TryGetComponent<TankHealth>(out var targetHealth))
        {
            targetHealth.ApplyDamage(damage, senderClientId);
        }

        ShotFxClientRpc(shotId, authoritativeTargetNetworkObjectId, muzzlePosition, authoritativeEndPoint,
            authoritativeHitNormal, authoritativeDidHit);
    }

    bool ValidateShot(ulong predictedTargetNetworkObjectId, Vector3 muzzlePosition, Vector3 predictedEndPoint,
        out ulong authoritativeTargetNetworkObjectId, out Vector3 authoritativeEndPoint,
        out Vector3 authoritativeHitNormal, out bool authoritativeDidHit)
    {
        authoritativeTargetNetworkObjectId = 0;
        authoritativeEndPoint = predictedEndPoint;
        authoritativeHitNormal = Vector3.zero;
        authoritativeDidHit = false;

        Vector3 direction = predictedEndPoint - muzzlePosition;
        if (direction.sqrMagnitude < 0.0001f)
            direction = muzzle != null ? muzzle.forward : transform.forward;

        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        direction.Normalize();

        authoritativeEndPoint = muzzlePosition + direction * range;

        var hits = Physics.RaycastAll(muzzlePosition, direction, range, hitMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            ulong ownId = 0;
            var ownNetworkObject = GetComponentInParent<NetworkObject>();
            if (ownNetworkObject != null) ownId = ownNetworkObject.NetworkObjectId;

            foreach (var hit in hits)
            {
                var hitNetworkObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitNetworkObject != null && hitNetworkObject.NetworkObjectId == ownId)
                    continue;

                authoritativeDidHit = true;
                authoritativeEndPoint = hit.point;
                authoritativeHitNormal = hit.normal;

                if (hitNetworkObject != null && hitNetworkObject.IsSpawned)
                    authoritativeTargetNetworkObjectId = hitNetworkObject.NetworkObjectId;

                break;
            }
        }

        return predictedTargetNetworkObjectId == authoritativeTargetNetworkObjectId;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShotFxClientRpc(int shotId, ulong targetNetworkObjectId, Vector3 muzzlePosition, Vector3 endPoint,
        Vector3 hitNormal, bool didHit)
    {
        if (workshopExerciseEnabled && rollbackPredictedShot)
            RollbackPredictedShot(shotId);

        SpawnShotFx(targetNetworkObjectId, muzzlePosition, endPoint, hitNormal, didHit, false);
    }

    List<GameObject> SpawnShotFx(ulong targetNetworkObjectId, Vector3 muzzlePosition, Vector3 endPoint,
        Vector3 hitNormal, bool didHit, bool predicted)
    {
        var spawnedObjects = predicted ? new List<GameObject>() : null;

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

        AddIfTracked(spawnedObjects, SpawnTracer(muzzlePosition, tracerEnd));
        AddIfTracked(spawnedObjects, SpawnFlash(muzzlePosition));

        if (!didHit) return spawnedObjects;

        AddIfTracked(spawnedObjects, SpawnImpact(endPoint, hitNormal));

        if (targetNetworkObjectId != 0 &&
            NetworkManager != null && NetworkManager.SpawnManager != null &&
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var target))
        {
            AddIfTracked(spawnedObjects, SpawnSplat(target.transform, endPoint, hitNormal, predicted));
        }

        return spawnedObjects;
    }

    void RollbackPredictedShot(int shotId)
    {
        if (!predictedShotObjects.TryGetValue(shotId, out var objects))
            return;

        foreach (var go in objects)
        {
            if (go != null)
                Destroy(go);
        }

        predictedShotObjects.Remove(shotId);
    }

    static void AddIfTracked(List<GameObject> list, GameObject go)
    {
        if (list != null && go != null)
            list.Add(go);
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

    GameObject SpawnTracer(Vector3 from, Vector3 to)
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
        return go;
    }

    GameObject SpawnFlash(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        go.name = "ShotFlash";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.24f;
        go.GetComponent<Renderer>().sharedMaterial = GetFxMaterial();

        Destroy(go, flashTime);
        return go;
    }

    GameObject SpawnImpact(Vector3 point, Vector3 normal)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        go.name = "ShotImpact";
        go.transform.position = point + normal * 0.05f;
        go.transform.localScale = Vector3.one * 0.22f;
        go.GetComponent<Renderer>().sharedMaterial = GetFxMaterial();

        Destroy(go, impactTime);
        return go;
    }

    GameObject SpawnSplat(Transform parent, Vector3 point, Vector3 normal, bool predicted)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var collider = quad.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        quad.name = predicted ? "PredictedShotSplat" : "ShotSplat";

        if (parent != null)
            quad.transform.SetParent(parent, true);

        quad.transform.position = point + normal * 0.03f;
        quad.transform.rotation = Quaternion.LookRotation(normal);
        quad.transform.localScale = Vector3.one * 0.55f;

        var renderer = quad.GetComponent<Renderer>();
        if (predicted)
        {
            if (predictedSplatMaterial == null)
            {
                predictedSplatMaterial = CreateSplatMaterial(renderer.sharedMaterial, new Color(1f, 0.9f, 0.15f));
            }

            renderer.sharedMaterial = predictedSplatMaterial;
        }
        else
        {
            if (splatMaterial == null)
                splatMaterial = CreateSplatMaterial(renderer.sharedMaterial, new Color(1f, 0.25f, 0.65f));

            renderer.sharedMaterial = splatMaterial;
        }

        Destroy(quad, 5f);
        return quad;
    }

    Material CreateSplatMaterial(Material fallback, Color color)
    {
        Material template = null;
        var gunRenderer = GetComponentInParent<Renderer>();
        if (gunRenderer != null) template = gunRenderer.sharedMaterial;
        if (template == null) template = fallback;

        var material = new Material(template);
        material.color = color;
        return material;
    }

    void OnGUI()
    {
        if (IsOwner && workshopExerciseEnabled)
        {
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                hintStyle.normal.textColor = new Color(1f, 0.9f, 0.3f, 0.9f);
            }

            GUI.Label(new Rect(26f, 78f, 520f, 44f),
                $"Workshop latency: {simulatedServerDelay:0.00}s server delay | prediction {(predictLocalShot ? "ON" : "OFF")} | rollback {(rollbackPredictedShot ? "ON" : "OFF")}",
                hintStyle);
        }

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

    GUIStyle hintStyle;
}
