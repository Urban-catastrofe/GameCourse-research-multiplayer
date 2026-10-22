using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Health, kills, death/respawn and the floating HP bar above the tank.
/// </summary>
public class TankHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] int maxHealth = 100;
    [SerializeField] float respawnDelay = 3f;

    [Header("Health bar")]
    [SerializeField] float barHeight = 2.6f;
    [SerializeField] Vector2 barSize = new Vector2(140f, 16f);
    [SerializeField] float barScale = 0.01f;

    readonly NetworkVariable<int> health = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<int> kills = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> dead = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<int> respawnTick = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    readonly List<Renderer> cachedRenderers = new List<Renderer>();
    readonly List<Collider> cachedColliders = new List<Collider>();

    GameObject barRoot;
    Image fill;
    GUIStyle hudStyle;
    GUIStyle hintStyle;

    public bool IsDead => dead.Value;
    public int Health => health.Value;
    public int Kills => kills.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            health.Value = maxHealth;
            dead.Value = false;
        }

        health.OnValueChanged += OnHealthChanged;
        dead.OnValueChanged += OnDeadChanged;
        respawnTick.OnValueChanged += OnRespawnTickChanged;

        CacheParts();
        BuildBar();
        UpdateBar();
        ApplyDeadState(dead.Value);
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnHealthChanged;
        dead.OnValueChanged -= OnDeadChanged;
        respawnTick.OnValueChanged -= OnRespawnTickChanged;
    }

    void CacheParts()
    {
        cachedRenderers.Clear();
        cachedColliders.Clear();
        GetComponentsInChildren(true, cachedRenderers);
        GetComponentsInChildren(true, cachedColliders);
    }

    void OnHealthChanged(int previous, int current) => UpdateBar();
    void OnDeadChanged(bool previous, bool current) => ApplyDeadState(current);

    void OnRespawnTickChanged(int previous, int current)
    {
        if (!IsOwner) return;

        var mover = GetComponent<HoverVehicleController>();
        if (mover != null)
            mover.TeleportToSpawn();

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>Server only.</summary>
    public void ApplyDamage(int amount, ulong attackerId)
    {
        if (!IsServer || dead.Value) return;
        if (attackerId == OwnerClientId) return; // no self-damage

        health.Value = Mathf.Max(0, health.Value - amount);

        if (health.Value <= 0)
        {
            dead.Value = true;
            AddKill(attackerId);
            StartCoroutine(RespawnAfterDelay());
        }
    }

    void AddKill(ulong attackerId)
    {
        if (NetworkManager == null) return;
        if (!NetworkManager.ConnectedClients.TryGetValue(attackerId, out var client)) return;
        if (client.PlayerObject == null) return;
        if (client.PlayerObject.TryGetComponent<TankHealth>(out var attackerHealth))
            attackerHealth.kills.Value++;
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        if (!IsServer) yield break;

        health.Value = maxHealth;
        dead.Value = false;
        respawnTick.Value++;
    }

    void ApplyDeadState(bool isDead)
    {
        foreach (var r in cachedRenderers)
            if (r != null)
                r.enabled = !isDead;

        foreach (var c in cachedColliders)
            if (c != null)
                c.enabled = !isDead;

        if (barRoot != null)
            barRoot.SetActive(!isDead);

        if (isDead)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    void BuildBar()
    {
        barRoot = new GameObject("HealthBar");
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = new Vector3(0f, barHeight, 0f);
        barRoot.transform.localRotation = Quaternion.identity;
        barRoot.transform.localScale = Vector3.one * barScale;

        var canvas = barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = barRoot.GetComponent<RectTransform>();
        rt.sizeDelta = barSize;

        var bgGo = new GameObject("BG");
        bgGo.transform.SetParent(barRoot.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(barRoot.transform, false);
        fill = fillGo.AddComponent<Image>();
        fill.raycastTarget = false;
        var fillRt = fill.rectTransform;
        fillRt.anchorMin = new Vector2(0.02f, 0.15f);
        fillRt.anchorMax = new Vector2(0.98f, 0.85f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
    }

    void UpdateBar()
    {
        if (fill == null) return;

        float f = Mathf.Clamp01(health.Value / (float)Mathf.Max(1, maxHealth));
        var rt = fill.rectTransform;
        rt.anchorMax = new Vector2(Mathf.Lerp(0.02f, 0.98f, f), 0.85f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fill.color = f > 0.5f
            ? Color.Lerp(new Color(1f, 0.8f, 0.1f), new Color(0.2f, 1f, 0.3f), (f - 0.5f) / 0.5f)
            : Color.Lerp(new Color(1f, 0.15f, 0.1f), new Color(1f, 0.8f, 0.1f), f / 0.5f);
    }

    void LateUpdate()
    {
        if (barRoot == null || !barRoot.activeSelf) return;

        var cam = Camera.main;
        if (cam == null) return;

        barRoot.transform.rotation = cam.transform.rotation;
    }

    void OnGUI()
    {
        if (!IsOwner) return;

        if (hudStyle == null)
            hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        }

        GUI.Box(new Rect(12f, 12f, 430f, 64f), GUIContent.none);
        GUI.Label(new Rect(26f, 17f, 400f, 26f), $"HP {health.Value}/{maxHealth}    Kills {kills.Value}", hudStyle);
        GUI.Label(new Rect(26f, 45f, 420f, 22f),
            "WASD rijden · Z/X toren · klik/space = schieten", hintStyle);
    }
}
