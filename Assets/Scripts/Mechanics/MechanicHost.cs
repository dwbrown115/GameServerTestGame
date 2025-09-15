using System.Collections.Generic;
using UnityEngine;

/// Attach to a GameObject and add mechanics components to the same object.
/// The host will wire a shared context and drive Tick order.
[DisallowMultipleComponent]
public class MechanicHost : MonoBehaviour
{
    [Header("Context")]
    public Transform payload; // optional; defaults to this.transform
    public Transform target; // optional

    [Header("Auto Payload (optional)")]
    [Tooltip("If true and no payload is assigned, a child payload will be created at runtime.")]
    public bool autoCreatePayload = true;
    public string payloadName = "Payload";

    [Tooltip("Number of payload objects to auto-spawn when none is assigned.")]
    [Min(1)]
    public int payloadCount = 1;

    // External sprite disabled for now
    // public Sprite payloadSprite; // not used yet
    public Color spriteColor = Color.white; // tint for generated circle sprite

    [Min(0f)]
    public float colliderRadius = 0.15f;
    public bool addRigidbody2D = true;
    public bool triggerCollider = true;

    // [Tooltip("Sorting order for SpriteRenderer on the payload.")]
    // public int payloadSortingOrder = 0; // optional, not needed now

    [Tooltip("Optional layer to assign to the payload (set -1 to keep current).")]
    public int payloadLayer = -1;

    [Header("Visuals (generated circle)")]
    [Tooltip("World radius of the generated circle sprite. If 0, uses colliderRadius.")]
    [Min(0f)]
    public float visualRadius = 0f;

    [Header("Mechanic Selection (Editor only)")]
    [Tooltip("Primary mechanic to use (server-driven later)")]
    public MechanicKind primaryMechanic = MechanicKind.None;

    [Tooltip("Secondary mechanic to combine (server-driven later)")]
    public MechanicKind secondaryMechanic = MechanicKind.None;

    [Header("Mechanic Overrides")]
    // Deprecated: per-mechanic radius overrides removed in favor of a single override applied to the first radius-capable mechanic

    [Tooltip("If true, sets OrbitMechanic.angularSpeedDeg on attached Orbit mechanics")]
    public bool overrideOrbitSpeed = false;

    [Min(0f)]
    public float orbitSpeedDeg = 90f;

    [Tooltip(
        "If true, overrides the radius on the first mechanic that supports a radius (Orbit/Aura/Drain)"
    )]
    public bool overrideRadius = false;

    [Min(0f)]
    public float mechanicRadius = 2f;

    [Tooltip("If true, applies Aura visualization toggle to attached AuraMechanic components")]
    public bool overrideAuraShowVisualization = false;
    public bool auraShowVisualization = true;

    [Tooltip("If true, overrides AuraMechanic.interval on attached Aura mechanics")]
    public bool overrideAuraInterval = false;

    [Min(0.01f)]
    public float auraInterval = 0.5f;

    [Tooltip("If true, overrides damage on supported mechanics (Projectile/Aura).")]
    public bool overrideDamage = false;
    public int damageAmount = 10;

    [Tooltip("If true, overrides ProjectileMechanic.destroyOnHit on attached Projectile mechanics")]
    public bool overrideProjectileDestroyOnHit = false;
    public bool projectileDestroyOnHit = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private readonly List<IMechanic> _mechanics = new();
    private readonly List<Transform> _payloads = new();
    private MechanicContext _ctx;

    private void Awake()
    {
        Log(
            $"Awake. payload={(payload ? payload.name : "<null>")}, autoCreatePayload={autoCreatePayload}, visualRadius={visualRadius}, colliderRadius={colliderRadius}"
        );

        // Auto-create payload if requested and none provided
        if (payload == null && autoCreatePayload)
        {
            Log("No payload assigned. Auto-creating payload child(ren).");

            // Determine if we intend to orbit and the initial radius for placement
            // Prefer explicit override, else existing orbit component, else fallback if orbit is intended
            bool intendOrbit =
                (primaryMechanic == MechanicKind.Orbit)
                || (secondaryMechanic == MechanicKind.Orbit)
                || overrideOrbitSpeed;
            float initialR = 0f;
            var orbitComp = GetComponent<Mechanics.Neuteral.OrbitMechanic>();
            if (orbitComp != null)
            {
                initialR = Mathf.Max(0f, orbitComp.radius);
                Log($"Initial placement using existing OrbitMechanic.radius={initialR}.");
            }
            else if (intendOrbit)
            {
                // Use a sensible default (matches OrbitMechanic default)
                initialR = 2f;
                Log($"Initial placement infers orbit intent; using default radius={initialR}.");
            }
            // Apply unified radius override to initial placement only when orbiting; keep center for aura-style
            if (overrideRadius && intendOrbit)
            {
                initialR = Mathf.Max(0f, mechanicRadius);
                Log($"Initial placement applying overrideRadius (orbit intent) => r={initialR}.");
            }

            int count = Mathf.Max(1, payloadCount);
            Transform parent = transform;
            if (count > 1)
            {
                var group = new GameObject(
                    string.IsNullOrWhiteSpace(payloadName) ? "Payloads" : payloadName + "s"
                );
                group.transform.SetParent(transform, false);
                group.transform.localPosition = Vector3.zero;
                parent = group.transform;
            }

            float visualR = overrideRadius
                ? 1f
                : (visualRadius > 0f ? visualRadius : colliderRadius);
            Vector3 center = transform.position;
            _payloads.Clear();
            for (int i = 0; i < count; i++)
            {
                string name =
                    count > 1
                        ? (
                            string.IsNullOrWhiteSpace(payloadName)
                                ? $"Payload_{i}"
                                : $"{payloadName}_{i}"
                        )
                        : (string.IsNullOrWhiteSpace(payloadName) ? "Payload" : payloadName);
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                if (overrideRadius)
                    go.transform.localScale = new Vector3(mechanicRadius, mechanicRadius, 1f);
                // Assign layer: explicit if valid, otherwise match owner's layer
                if (payloadLayer >= 0 && payloadLayer <= 31)
                    go.layer = payloadLayer;
                else
                    go.layer = gameObject.layer;

                // Visual
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GenerateCircleSprite(visualR);
                sr.color = spriteColor;

                // Physics
                Rigidbody2D prb = null;
                if (addRigidbody2D)
                {
                    prb = go.AddComponent<Rigidbody2D>();
                    prb.gravityScale = 0f;
                    prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    prb.interpolation = RigidbodyInterpolation2D.Interpolate;
                    prb.bodyType = RigidbodyType2D.Kinematic;
                    prb.constraints = RigidbodyConstraints2D.FreezeRotation;
                }
                var cc = go.AddComponent<CircleCollider2D>();
                // With override, encode size via transform scale and keep collider at 0.5
                if (overrideRadius)
                    cc.radius = 0.5f;
                else
                    cc.radius = Mathf.Max(0.0001f, colliderRadius);
                cc.isTrigger = triggerCollider;

                // Relay for trigger events
                go.AddComponent<PayloadTriggerRelay>();

                // Equal angular spacing placement at desired radius
                Vector3 worldPos = center;
                if (initialR > 0f)
                {
                    float angle = (count > 1) ? (Mathf.PI * 2f * i / count) : 0f; // radians
                    float x = Mathf.Cos(angle) * initialR;
                    float y = Mathf.Sin(angle) * initialR;
                    worldPos = center + new Vector3(x, y, 0f);
                }

                if (prb != null)
                    prb.position = worldPos;
                go.transform.position = worldPos;

                // Assign first as the main payload for compatibility
                if (i == 0)
                    payload = go.transform;

                _payloads.Add(go.transform);
            }

            Log($"Created {count} payload(s). First assigned as primary payload: {payload.name}.");
        }
        else
        {
            Log("Payload creation skipped (either already assigned or autoCreatePayload=false).");
        }

        _ctx = new MechanicContext
        {
            Owner = transform,
            Payload = payload != null ? payload : transform,
            Target = target,
            OwnerRb2D = GetComponent<Rigidbody2D>(),
            PayloadRb2D = (
                payload != null ? payload.GetComponent<Rigidbody2D>() : GetComponent<Rigidbody2D>()
            ),
        };
        Log(
            $"Context ready. Owner={name}, Payload={_ctx.Payload.name}, Target={(target ? target.name : "<null>")}, OwnerRb2D={(_ctx.OwnerRb2D ? "yes" : "no")}, PayloadRb2D={(_ctx.PayloadRb2D ? "yes" : "no")}."
        );

        // Ensure an OrbitMechanic exists if requested via selection or orbit speed override
        var existingOrbit = GetComponent<Mechanics.Neuteral.OrbitMechanic>();
        bool wantOrbit =
            (primaryMechanic == MechanicKind.Orbit)
            || (secondaryMechanic == MechanicKind.Orbit)
            || overrideOrbitSpeed;
        if (existingOrbit == null && wantOrbit)
        {
            existingOrbit = gameObject.AddComponent<Mechanics.Neuteral.OrbitMechanic>();
            Log("Auto-added OrbitMechanic based on host configuration.");
        }

        // Ensure a ProjectileMechanic exists if selected
        bool wantProjectile =
            (primaryMechanic == MechanicKind.Projectile)
            || (secondaryMechanic == MechanicKind.Projectile);
        var existingProjectile = GetComponent<Mechanics.Neuteral.ProjectileMechanic>();
        if (existingProjectile == null && wantProjectile)
        {
            existingProjectile = gameObject.AddComponent<Mechanics.Neuteral.ProjectileMechanic>();
            existingProjectile.debugLogs = debugLogs;
            if (overrideDamage)
            {
                existingProjectile.damage = Mathf.Max(0, damageAmount);
            }
            if (overrideProjectileDestroyOnHit)
            {
                existingProjectile.destroyOnHit = projectileDestroyOnHit;
            }
            Log("Auto-added ProjectileMechanic based on host configuration.");
        }

        // Ensure a DrainMechanic exists if selected; attach it to the generated payload when available
        bool wantDrain =
            (primaryMechanic == MechanicKind.Drain) || (secondaryMechanic == MechanicKind.Drain);
        if (wantDrain)
        {
            Mechanics.Corruption.DrainMechanic existingDrain = null;
            if (payload != null)
                existingDrain = payload.GetComponent<Mechanics.Corruption.DrainMechanic>();
            if (existingDrain == null)
                existingDrain = GetComponent<Mechanics.Corruption.DrainMechanic>();
            if (existingDrain == null)
            {
                if (payload != null)
                {
                    existingDrain =
                        payload.gameObject.AddComponent<Mechanics.Corruption.DrainMechanic>();
                    existingDrain.debugLogs = debugLogs;
                    Log("Auto-added DrainMechanic to payload based on host configuration.");
                }
                else
                {
                    existingDrain = gameObject.AddComponent<Mechanics.Corruption.DrainMechanic>();
                    existingDrain.debugLogs = debugLogs;
                    Log("Auto-added DrainMechanic to host (no payload available).");
                }
            }
        }

        // Collect mechanics on host and payload(s), and prepare initialization list with appropriate contexts
        var mechInits = new List<(IMechanic mech, MechanicContext ctx)>();
        bool radiusApplied = false;
        // Host-attached mechanics use the primary payload by default
        foreach (var comp in GetComponents<MonoBehaviour>())
        {
            Log($"Found component: {comp.GetType().Name}.");
            if (comp is IMechanic mech)
            {
                _mechanics.Add(mech);
                mechInits.Add((mech, _ctx));
                // Apply host-controlled overrides to known mechanics
                // Propagate debug flag to known mechanics for consistent logging
                if (comp is Mechanics.Neuteral.OrbitMechanic omDbg)
                    omDbg.debugLogs = debugLogs;
                // Single radius override: apply to the first radius-capable mechanic we encounter
                if (overrideRadius && !radiusApplied)
                {
                    if (comp is Mechanics.Neuteral.OrbitMechanic om)
                    {
                        om.radius = mechanicRadius;
                        radiusApplied = true;
                        Log($"Applied override to OrbitMechanic.radius={mechanicRadius}.");
                    }
                    else if (comp is Mechanics.Neuteral.AuraMechanic am)
                    {
                        am.radius = mechanicRadius;
                        radiusApplied = true;
                        Log($"Applied override to AuraMechanic.radius={mechanicRadius}.");
                    }
                    else if (comp is Mechanics.Corruption.DrainMechanic dm)
                    {
                        dm.radius = mechanicRadius;
                        radiusApplied = true;
                        Log($"Applied override to DrainMechanic.radius={mechanicRadius}.");
                    }
                }
                if (overrideAuraShowVisualization && comp is Mechanics.Neuteral.AuraMechanic amViz)
                {
                    amViz.showVisualization = auraShowVisualization;
                    Log(
                        $"Applied override to AuraMechanic.showVisualization={auraShowVisualization}."
                    );
                }
                if (overrideOrbitSpeed && comp is Mechanics.Neuteral.OrbitMechanic os)
                {
                    float speed = orbitSpeedDeg;
                    if (speed <= 0f)
                    {
                        speed = 90f;
                        Log("orbitSpeedDeg was <= 0; defaulting to 90 deg/s.");
                    }
                    os.angularSpeedDeg = speed;
                    Log($"Applied override to OrbitMechanic.angularSpeedDeg={speed}.");
                }
                if (overrideAuraInterval && comp is Mechanics.Neuteral.AuraMechanic amInterval)
                {
                    amInterval.interval = Mathf.Max(0.01f, auraInterval);
                    Log($"Applied override to AuraMechanic.interval={amInterval.interval}.");
                }
                if (overrideAuraInterval && comp is Mechanics.Corruption.DrainMechanic dmInterval)
                {
                    dmInterval.interval = Mathf.Max(0.01f, auraInterval);
                    Log($"Applied override to DrainMechanic.interval={dmInterval.interval}.");
                }

                if (overrideDamage)
                {
                    if (comp is Mechanics.Neuteral.ProjectileMechanic pm)
                    {
                        pm.debugLogs = debugLogs;
                        pm.damage = Mathf.Max(0, damageAmount);
                        Log($"Applied override to ProjectileMechanic.damage={pm.damage}.");
                    }
                    else if (comp is Mechanics.Neuteral.AuraMechanic aam)
                    {
                        aam.debugLogs = debugLogs;
                        aam.damagePerInterval = Mathf.Max(0, damageAmount);
                        Log(
                            $"Applied override to AuraMechanic.damagePerInterval={aam.damagePerInterval}."
                        );
                    }
                    else if (comp is Mechanics.Corruption.DrainMechanic dmDamage)
                    {
                        dmDamage.debugLogs = debugLogs;
                        dmDamage.damagePerInterval = Mathf.Max(0, damageAmount);
                        Log(
                            $"Applied override to DrainMechanic.damagePerInterval={dmDamage.damagePerInterval}."
                        );
                    }
                }
                else
                {
                    if (comp is Mechanics.Neuteral.ProjectileMechanic pmDbg)
                        pmDbg.debugLogs = debugLogs;
                    if (comp is Mechanics.Neuteral.AuraMechanic amDbg)
                        amDbg.debugLogs = debugLogs;
                    if (comp is Mechanics.Corruption.DrainMechanic dmDbg)
                        dmDbg.debugLogs = debugLogs;
                }

                // Apply projectile destroyOnHit override if requested
                if (
                    overrideProjectileDestroyOnHit
                    && comp is Mechanics.Neuteral.ProjectileMechanic pmDestroy
                )
                {
                    pmDestroy.destroyOnHit = projectileDestroyOnHit;
                    Log(
                        $"Applied override to ProjectileMechanic.destroyOnHit={projectileDestroyOnHit}."
                    );
                }
            }
        }

        // Payload-attached mechanics: initialize with per-payload contexts
        if (_payloads != null && _payloads.Count > 0)
        {
            foreach (var p in _payloads)
            {
                if (p == null)
                    continue;
                foreach (var comp in p.GetComponents<MonoBehaviour>())
                {
                    Log($"Found payload component on '{p.name}': {comp.GetType().Name}.");
                    if (comp is IMechanic mech)
                    {
                        _mechanics.Add(mech);
                        var ctxP = new MechanicContext
                        {
                            Owner = transform,
                            Payload = p,
                            Target = target,
                            OwnerRb2D = _ctx.OwnerRb2D,
                            PayloadRb2D = p.GetComponent<Rigidbody2D>(),
                        };
                        mechInits.Add((mech, ctxP));

                        // Apply unified radius override to the first radius-capable mechanic (including payload mechanics)
                        if (overrideRadius && !radiusApplied)
                        {
                            if (comp is Mechanics.Neuteral.OrbitMechanic om)
                            {
                                om.radius = mechanicRadius;
                                radiusApplied = true;
                                Log(
                                    $"Applied override to OrbitMechanic.radius={mechanicRadius} (payload '{p.name}')."
                                );
                            }
                            else if (comp is Mechanics.Neuteral.AuraMechanic am)
                            {
                                am.radius = mechanicRadius;
                                radiusApplied = true;
                                Log(
                                    $"Applied override to AuraMechanic.radius={mechanicRadius} (payload '{p.name}')."
                                );
                            }
                            else if (comp is Mechanics.Corruption.DrainMechanic dm)
                            {
                                dm.radius = mechanicRadius;
                                radiusApplied = true;
                                Log(
                                    $"Applied override to DrainMechanic.radius={mechanicRadius} (payload '{p.name}')."
                                );
                            }
                        }

                        // Apply interval override
                        if (
                            overrideAuraInterval
                            && comp is Mechanics.Corruption.DrainMechanic dmIntervalP
                        )
                        {
                            dmIntervalP.interval = Mathf.Max(0.01f, auraInterval);
                            Log(
                                $"Applied override to DrainMechanic.interval={dmIntervalP.interval} (payload '{p.name}')."
                            );
                        }

                        // Apply damage override
                        if (overrideDamage && comp is Mechanics.Corruption.DrainMechanic dmDamageP)
                        {
                            dmDamageP.damagePerInterval = Mathf.Max(0, damageAmount);
                            Log(
                                $"Applied override to DrainMechanic.damagePerInterval={dmDamageP.damagePerInterval} (payload '{p.name}')."
                            );
                        }
                    }
                }
            }
        }

        // For additional payloads beyond the primary, add dedicated OrbitMechanics so they also orbit
        if (wantOrbit && _payloads != null && _payloads.Count > 1)
        {
            for (int i = 1; i < _payloads.Count; i++)
            {
                var p = _payloads[i];
                var extraOrbit = gameObject.AddComponent<Mechanics.Neuteral.OrbitMechanic>();
                // Apply overrides
                if (overrideRadius)
                    extraOrbit.radius = mechanicRadius;
                if (overrideOrbitSpeed)
                {
                    float speed = orbitSpeedDeg;
                    if (speed <= 0f)
                        speed = 90f;
                    extraOrbit.angularSpeedDeg = speed;
                }
                _mechanics.Add(extraOrbit);

                // Create a dedicated context for this payload
                var ctxExtra = new MechanicContext
                {
                    Owner = transform,
                    Payload = p,
                    Target = target,
                    OwnerRb2D = _ctx.OwnerRb2D,
                    PayloadRb2D = p != null ? p.GetComponent<Rigidbody2D>() : null,
                };
                mechInits.Add((extraOrbit, ctxExtra));
                Log($"Added extra OrbitMechanic for payload '{p.name}'.");
            }
        }

        // Initialize each mechanic with its corresponding context
        foreach (var entry in mechInits)
        {
            Log($"Initializing mechanic: {entry.mech.GetType().Name}.");
            entry.mech.Initialize(entry.ctx);
        }

        // If an OrbitMechanic exists, log post-init distance for the primary payload
        var orbit = GetComponent<Mechanics.Neuteral.OrbitMechanic>();
        if (orbit != null && payload != null)
        {
            var centerPost = (target != null ? target.position : transform.position);
            float dist = Vector2.Distance(centerPost, payload.position);
            Log(
                $"Post-init primary payload distance from center: {dist:F2} (expected ~{(overrideRadius ? mechanicRadius : orbit.radius):F2})."
            );
        }

        // Align payload collider(s) and visual sprites to the chosen radius from the first radius-capable mechanic
        var auraComp = GetComponent<Mechanics.Neuteral.AuraMechanic>();
        var drainComp = GetComponent<Mechanics.Corruption.DrainMechanic>();
        float auraR = 0f;
        if (auraComp != null)
            auraR = Mathf.Max(0f, auraComp.radius);
        else if (orbit != null)
            auraR = Mathf.Max(0f, orbit.radius);
        else if (drainComp != null)
            auraR = Mathf.Max(0f, drainComp.radius);

        if (overrideRadius)
        {
            if (_payloads != null && _payloads.Count > 0)
            {
                for (int i = 0; i < _payloads.Count; i++)
                {
                    var p = _payloads[i];
                    if (p == null)
                        continue;
                    p.localScale = new Vector3(mechanicRadius, mechanicRadius, 1f);
                    var cc = p.GetComponent<CircleCollider2D>();
                    if (cc != null)
                        cc.radius = 0.5f;
                    var sr = p.GetComponent<SpriteRenderer>();
                    if (sr != null)
                        sr.sprite = GenerateCircleSprite(visualRadius > 0f ? visualRadius : 1f);
                }
                Log(
                    $"Scaled payloads to {mechanicRadius}, set colliders=0.5 and unit sprites on {_payloads.Count} payload(s)."
                );
            }
            else if (payload != null)
            {
                payload.localScale = new Vector3(mechanicRadius, mechanicRadius, 1f);
                var cc = payload.GetComponent<CircleCollider2D>();
                if (cc != null)
                    cc.radius = 0.5f;
                var sr = payload.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.sprite = GenerateCircleSprite(visualRadius > 0f ? visualRadius : 1f);
                Log(
                    $"Scaled primary payload to {mechanicRadius}, set collider=0.5 and unit sprite."
                );
            }
        }
        else if (auraR > 0f)
        {
            if (_payloads != null && _payloads.Count > 0)
            {
                for (int i = 0; i < _payloads.Count; i++)
                {
                    var p = _payloads[i];
                    if (p == null)
                        continue;
                    var cc = p.GetComponent<CircleCollider2D>();
                    if (cc != null)
                        cc.radius = Mathf.Max(0.0001f, auraR);
                    var sr = p.GetComponent<SpriteRenderer>();
                    if (sr != null)
                        sr.sprite = GenerateCircleSprite(visualRadius > 0f ? visualRadius : auraR);
                }
                Log(
                    $"Aligned payload colliders and sprites to radius {auraR} on {_payloads.Count} payload(s)."
                );
            }
            else if (payload != null)
            {
                var cc = payload.GetComponent<CircleCollider2D>();
                if (cc != null)
                    cc.radius = Mathf.Max(0.0001f, auraR);
                var sr = payload.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.sprite = GenerateCircleSprite(visualRadius > 0f ? visualRadius : auraR);
                Log($"Aligned primary payload collider and sprite to radius {auraR}.");
            }
        }
    }

    // Optional: log relay receipt on host for diagnostics
    private void OnPayloadTriggerEnter2D(Collider2D other)
    {
        if (debugLogs && other != null)
            Log(
                $"Host received ENTER from payload with {other.name} tag={other.tag} layer={other.gameObject.layer}"
            );
    }

    private void OnPayloadTriggerStay2D(Collider2D other)
    {
        if (debugLogs && other != null)
            Log(
                $"Host received STAY from payload with {other.name} tag={other.tag} layer={other.gameObject.layer}"
            );
    }

    private void OnPayloadTriggerExit2D(Collider2D other)
    {
        if (debugLogs && other != null)
            Log(
                $"Host received EXIT from payload with {other.name} tag={other.tag} layer={other.gameObject.layer}"
            );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // In editor, propagate overrides to attached components for immediate feedback
        var comps = GetComponents<MonoBehaviour>();
        bool radiusAppliedEditor = false;
        foreach (var comp in comps)
        {
            // Apply unified radius override to the first radius-capable mechanic
            if (overrideRadius && !radiusAppliedEditor)
            {
                if (comp is Mechanics.Neuteral.OrbitMechanic om)
                {
                    om.radius = Mathf.Max(0f, mechanicRadius);
                    radiusAppliedEditor = true;
                }
                else if (comp is Mechanics.Neuteral.AuraMechanic am)
                {
                    am.radius = Mathf.Max(0f, mechanicRadius);
                    radiusAppliedEditor = true;
                }
                else if (comp is Mechanics.Corruption.DrainMechanic dm)
                {
                    dm.radius = Mathf.Max(0f, mechanicRadius);
                    radiusAppliedEditor = true;
                }
            }
            if (overrideAuraShowVisualization && comp is Mechanics.Neuteral.AuraMechanic amViz)
                amViz.showVisualization = auraShowVisualization;
            if (overrideAuraInterval && comp is Mechanics.Neuteral.AuraMechanic amInterval)
                amInterval.interval = Mathf.Max(0.01f, auraInterval);
            if (overrideOrbitSpeed && comp is Mechanics.Neuteral.OrbitMechanic os)
                os.angularSpeedDeg = Mathf.Max(0.0001f, orbitSpeedDeg <= 0f ? 90f : orbitSpeedDeg);
            if (overrideDamage)
            {
                if (comp is Mechanics.Neuteral.ProjectileMechanic pm)
                    pm.damage = Mathf.Max(0, damageAmount);
                if (comp is Mechanics.Neuteral.AuraMechanic aam)
                    aam.damagePerInterval = Mathf.Max(0, damageAmount);
            }
            if (
                overrideProjectileDestroyOnHit
                && comp is Mechanics.Neuteral.ProjectileMechanic pmDestroy
            )
                pmDestroy.destroyOnHit = projectileDestroyOnHit;
        }
        // Align payload collider radius and visuals to chosen radius
        float chosenRadius = Mathf.Max(0f, colliderRadius);
        var relaysForEdit = GetComponentsInChildren<PayloadTriggerRelay>(true);
        if (relaysForEdit != null && relaysForEdit.Length > 0)
        {
            foreach (var r in relaysForEdit)
            {
                if (r == null)
                    continue;
                var cc = r.GetComponent<CircleCollider2D>();
                if (overrideRadius)
                {
                    if (r.transform != null)
                        r.transform.localScale = new Vector3(mechanicRadius, mechanicRadius, 1f);
                    if (cc != null)
                        cc.radius = 0.5f;
                }
                else
                {
                    if (cc != null)
                        cc.radius = Mathf.Max(0.0001f, chosenRadius);
                }
                var sr = r.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.sprite = GenerateCircleSprite(
                        visualRadius > 0f ? visualRadius : (overrideRadius ? 1f : chosenRadius)
                    );
            }
        }
        else if (payload != null)
        {
            var cc = payload.GetComponent<CircleCollider2D>();
            if (overrideRadius)
            {
                payload.localScale = new Vector3(mechanicRadius, mechanicRadius, 1f);
                if (cc != null)
                    cc.radius = 0.5f;
            }
            else
            {
                if (cc != null)
                    cc.radius = Mathf.Max(0.0001f, chosenRadius);
            }
            var sr = payload.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = GenerateCircleSprite(
                    visualRadius > 0f ? visualRadius : (overrideRadius ? 1f : chosenRadius)
                );
        }
    }
#endif

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _mechanics.Count; i++)
            _mechanics[i].Tick(dt);
    }

    // Allow runtime target assignment from other components (e.g., MechanicTargetSetter)
    public void SetTarget(Transform t)
    {
        target = t;
        if (_ctx != null)
            _ctx.Target = t;
    }

    // Creates a filled white circle sprite sized to match the given world radius
    private Sprite GenerateCircleSprite(float worldRadius)
    {
        // Fallback guard
        float r = Mathf.Max(0.01f, worldRadius);
        int size = 64; // texture size in pixels
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        int cx = size / 2;
        int cy = size / 2;
        float radPx = size * 0.5f - 0.5f;
        float rSqr = radPx * radPx;
        var colors = new Color[size * size];
        Color on = Color.white; // use SpriteRenderer.color to tint
        Color off = new Color(1, 1, 1, 0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx + 0.5f);
                float dy = (y - cy + 0.5f);
                int idx = y * size + x;
                colors[idx] = (dx * dx + dy * dy) <= rSqr ? on : off;
            }
        }
        tex.SetPixels(colors);
        tex.Apply(false, false);

        float pixelsPerUnit = size / (2f * r); // makes sprite diameter = 2*worldRadius units
        var rect = new Rect(0, 0, size, size);
        var pivot = new Vector2(0.5f, 0.5f);
        var sprite = Sprite.Create(tex, rect, pivot, pixelsPerUnit);
        return sprite;
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[MechanicHost] {name}: {message}", this);
    }
}
