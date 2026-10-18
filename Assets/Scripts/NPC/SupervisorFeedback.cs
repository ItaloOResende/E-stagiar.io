using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(SupervisorPatrol))]
public class SupervisorFeedback : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform modelRoot;
    [Tooltip("Vazio: procura MeshRenderer e SkinnedMeshRenderer no Model Root.")]
    [SerializeField] private Renderer[] modelRenderers;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;
    [Header("Infracao: gradiente Multiply no modelo")]
    [SerializeField] private bool effectEnabled = true;
    [Tooltip("Arraste SupervisorMultiply.shader aqui para incluir no build.")]
    [SerializeField] private Shader multiplyShader;
    [Tooltip("Arraste SupervisorGlow.shader aqui para incluir no build.")]
    [SerializeField] private Shader glowShader;
    [SerializeField] private Color gradientStart = Color.red;
    [SerializeField] private Color gradientEnd = Color.white;
    [Tooltip("Direcao local ao Model Root: Y = de baixo para cima.")]
    [SerializeField] private Vector3 gradientDirection = Vector3.up;
    [SerializeField, Min(0.01f)] private float gradientLength = 1.8f;
    [SerializeField] private float gradientOffset;
    [SerializeField, Range(0f, 1f)] private float multiplyIntensity = 0.9f;
    [SerializeField, ColorUsage(false, true)] private Color glowColor = new Color(3f, 0f, 0f, 1f);
    [SerializeField, Min(0f)] private float glowIntensity = 0.7f;
    [SerializeField, Range(0f, 0.1f)] private float glowSize = 0.015f;
    [SerializeField, Min(0.1f)] private float glowPower = 2f;
    [SerializeField, Min(0f)] private float fadeIn = 0.12f;
    [SerializeField, Min(0f)] private float holdTime = 3f;
    [SerializeField, Min(0f)] private float fadeOut = 0.8f;
    [Header("Camera do player")]
    [SerializeField] private bool lookAtSupervisor = true;
    [SerializeField] private Transform lookTarget;
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField, Min(0.01f)] private float lookDuration = 0.8f;
    [SerializeField, Min(1f)] private float lookSpeed = 240f;
    [Header("Empurrar por proximidade")]
    [SerializeField] private bool shoveEnabled = true;
    [SerializeField, Min(0.1f)] private float shoveRadius = 1.2f;
    [SerializeField, Min(0f)] private float rearmMargin = 0.4f;
    [SerializeField, Min(0f)] private float maximumHeightDifference = 1.3f;
    [SerializeField, Min(0f)] private float shoveCooldown = 3f;
    [SerializeField, Min(0.1f)] private float shoveDuration = 1.1f;
    [SerializeField, Min(0f)] private float shoveSpeed = 9f;
    [SerializeField, Range(0.05f, 1f)] private float minimumControl = 0.15f;
    [SerializeField, Min(0.01f)] private float shoveBlendIn = 0.08f;
    [SerializeField] private bool spinOnShove;
    [SerializeField, Min(0f)] private float spinSpeed = 360f;
    [SerializeField] private bool verticalImpulse;
    [SerializeField, Min(0f)] private float upwardSpeed = 3f;
    [SerializeField] private LayerMask shoveOccluders = ~0;
    [SerializeField] private float shoveSightHeight = 0.8f;
    [Header("Sons posicionais")]
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField, Min(0.01f)] private float soundMinDistance = 1f;
    [SerializeField, Min(0.1f)] private float soundMaxDistance = 18f;
    [System.Serializable]
    public class Sound
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public Vector2 pitch = new Vector2(0.95f, 1.05f);
        [System.NonSerialized] public AudioSource source;
    }
    [SerializeField] private Sound infractionSound = new Sound();
    [SerializeField] private Sound walkingSound = new Sound();
    [SerializeField] private Sound periodicSound = new Sound();
    [SerializeField] private Sound shoveSound1 = new Sound();
    [SerializeField] private Sound shoveSound2 = new Sound();
    [SerializeField, Min(0f)] private float infractionSoundCooldown = 0.4f;
    [SerializeField, Min(0.1f)] private float periodicInterval = 7f;
    [Tooltip("Distancia real percorrida entre passos.")]
    [SerializeField, Min(0.05f)] private float stepDistance = 0.65f;
    [SerializeField, Min(0f)] private float secondShoveSoundDelay;

    private struct Overlay { public Renderer source, copy; }
    private readonly List<Overlay> overlays = new List<Overlay>();
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private Material multiplyMaterial, glowMaterial;
    private SupervisorPatrol patrol;
    private Vector3 lastPosition;
    private float stepTravel, nextPeriodic, nextInfractionSound, nextShove, effectAge = -1f;
    private bool armed = true;

    private void Awake()
    {
        patrol = GetComponent<SupervisorPatrol>();
        if (playerMovement == null) playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerLook == null && playerMovement != null) playerLook = playerMovement.GetComponent<PlayerLook>();
        if (modelRoot == null) modelRoot = transform;
        if (modelRenderers == null || modelRenderers.Length == 0) modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        if (multiplyShader == null) multiplyShader = Shader.Find("Supervisor/Model Multiply");
        if (glowShader == null) glowShader = Shader.Find("Supervisor/Model Glow");
        if (multiplyShader != null) multiplyMaterial = new Material(multiplyShader);
        if (glowShader != null) glowMaterial = new Material(glowShader);
        if (multiplyMaterial == null || glowMaterial == null) Debug.LogWarning("SupervisorFeedback: atribua os dois shaders para o efeito no modelo.", this);
        foreach (Renderer r in modelRenderers)
        {
            if (r == null) continue;
            if (multiplyMaterial != null) AddOverlay(r, multiplyMaterial);
            if (glowMaterial != null) AddOverlay(r, glowMaterial);
        }
        foreach (Sound s in new[] {infractionSound, walkingSound, periodicSound, shoveSound1, shoveSound2})
        {
            if (s == null) continue;
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.playOnAwake = false; s.source.spatialBlend = 1f;
            s.source.outputAudioMixerGroup = mixerGroup;
            s.source.minDistance = soundMinDistance; s.source.maxDistance = Mathf.Max(soundMinDistance, soundMaxDistance);
        }
    }
    private void OnEnable() { lastPosition = transform.position; nextPeriodic = Time.time + periodicInterval; }
    private void AddOverlay(Renderer source, Material material)
    {
        Mesh mesh = null;
        if (source is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
        else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;
        if (mesh == null) return;
        GameObject go = new GameObject("Supervisor effect (runtime)");
        go.layer = source.gameObject.layer; go.transform.SetParent(source.transform, false);
        Renderer copy;
        if (source is SkinnedMeshRenderer skin)
        {
            var s = go.AddComponent<SkinnedMeshRenderer>();
            s.sharedMesh = mesh; s.bones = skin.bones; s.rootBone = skin.rootBone;
            s.localBounds = skin.localBounds; s.updateWhenOffscreen = skin.updateWhenOffscreen;
            copy = s;
        }
        else { go.AddComponent<MeshFilter>().sharedMesh = mesh; copy = go.AddComponent<MeshRenderer>(); }
        Material[] materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        copy.sharedMaterials = materials; copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false; copy.enabled = false;
        overlays.Add(new Overlay {source = source, copy = copy});
    }
    public void NotifyInfraction()
    {
        if (!isActiveAndEnabled) return;
        effectAge = 0f;
        if (Time.time >= nextInfractionSound) { Play(infractionSound); nextInfractionSound = Time.time + infractionSoundCooldown; }
        if (lookAtSupervisor && playerLook != null)
            playerLook.FocusOn(lookTarget != null ? lookTarget : transform, lookOffset, lookDuration, lookSpeed);
    }
    private void Update()
    {
        if (Time.deltaTime <= 0f) return;
        CheckShove();
        if (Time.time >= nextPeriodic) { Play(periodicSound); nextPeriodic = Time.time + Mathf.Max(0.1f, periodicInterval); }
        Vector3 travel = transform.position - lastPosition; travel.y = 0f; lastPosition = transform.position;
        if (travel.magnitude > 2f || travel.sqrMagnitude < 0.000001f) stepTravel = 0f;
        else
        {
            stepTravel += travel.magnitude;
            if (stepTravel >= Mathf.Max(0.05f, stepDistance)) { stepTravel = 0f; Play(walkingSound); }
        }
    }
    private void CheckShove()
    {
        if (!shoveEnabled || playerMovement == null) return;
        Vector3 d = playerMovement.transform.position - transform.position;
        float height = Mathf.Abs(d.y); d.y = 0f;
        if (d.magnitude > shoveRadius + rearmMargin) armed = true;
        if (!armed || Time.time < nextShove || d.sqrMagnitude > shoveRadius * shoveRadius || height > maximumHeightDifference) return;
        Vector3 origin = transform.position + Vector3.up * shoveSightHeight;
        Vector3 ray = playerMovement.transform.position + Vector3.up * shoveSightHeight - origin;
        int count = Physics.RaycastNonAlloc(origin, ray.normalized, hits, ray.magnitude, shoveOccluders, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return;
        for (int i = 0; i < count; i++)
            if (!hits[i].transform.IsChildOf(transform) && !hits[i].transform.IsChildOf(playerMovement.transform)) return;
        if (d.sqrMagnitude < 0.0001f) d = transform.forward;
        float spin = spinOnShove ? spinSpeed * (Random.value < 0.5f ? -1f : 1f) : 0f;
        if (!playerMovement.TryStartShove(d, shoveDuration, shoveSpeed, spin, minimumControl, shoveBlendIn, verticalImpulse ? upwardSpeed : 0f)) return;
        armed = false; nextShove = Time.time + shoveCooldown;
        Play(shoveSound1);
        if (secondShoveSoundDelay <= 0f) Play(shoveSound2); else StartCoroutine(DelayedSound());
    }
    private IEnumerator DelayedSound() { yield return new WaitForSeconds(secondShoveSoundDelay); Play(shoveSound2); }
    private void Play(Sound s)
    {
        if (s == null || s.clip == null || s.source == null) return;
        s.source.pitch = Random.Range(Mathf.Max(0.01f, Mathf.Min(s.pitch.x, s.pitch.y)), Mathf.Max(0.01f, Mathf.Max(s.pitch.x, s.pitch.y)));
        s.source.PlayOneShot(s.clip, s.volume);
    }
    private void LateUpdate()
    {
        float weight = 0f;
        if (effectAge >= 0f)
        {
            effectAge += Time.deltaTime;
            if (fadeIn > 0f && effectAge < fadeIn) weight = Mathf.SmoothStep(0f, 1f, effectAge / fadeIn);
            else if (effectAge <= fadeIn + holdTime) weight = 1f;
            else if (fadeOut > 0f) weight = 1f - Mathf.SmoothStep(0f, 1f, (effectAge - fadeIn - holdTime) / fadeOut);
            if (effectAge > fadeIn + holdTime + fadeOut) effectAge = -1f;
        }
        if (!effectEnabled) weight = 0f;
        Vector3 axis = modelRoot.TransformDirection(gradientDirection.sqrMagnitude > 0.0001f ? gradientDirection.normalized : Vector3.up);
        if (multiplyMaterial != null)
        {
            multiplyMaterial.SetColor("_StartColor", gradientStart); multiplyMaterial.SetColor("_EndColor", gradientEnd);
            multiplyMaterial.SetVector("_Origin", modelRoot.position); multiplyMaterial.SetVector("_Axis", axis);
            multiplyMaterial.SetFloat("_Length", Mathf.Max(0.01f, gradientLength)); multiplyMaterial.SetFloat("_Offset", gradientOffset);
            multiplyMaterial.SetFloat("_Strength", weight * multiplyIntensity);
        }
        if (glowMaterial != null)
        {
            glowMaterial.SetColor("_GlowColor", glowColor); glowMaterial.SetFloat("_Strength", weight * glowIntensity);
            glowMaterial.SetFloat("_Size", glowSize); glowMaterial.SetFloat("_Power", glowPower);
        }
        foreach (Overlay o in overlays)
        {
            if (o.copy == null || o.source == null) continue;
            o.copy.enabled = weight > 0f && o.source.enabled;
            if (o.copy.enabled && o.source is SkinnedMeshRenderer src && o.copy is SkinnedMeshRenderer dst && src.sharedMesh != null)
                for (int i = 0; i < src.sharedMesh.blendShapeCount; i++) dst.SetBlendShapeWeight(i, src.GetBlendShapeWeight(i));
        }
    }
    private void OnDisable()
    {
        StopAllCoroutines(); effectAge = -1f; stepTravel = 0f;
        foreach (Overlay o in overlays) if (o.copy != null) o.copy.enabled = false;
        foreach (Sound s in new[] {infractionSound, walkingSound, periodicSound, shoveSound1, shoveSound2}) if (s?.source != null) s.source.Stop();
    }
    private void OnDestroy()
    {
        foreach (Overlay o in overlays) if (o.copy != null) Destroy(o.copy.gameObject);
        if (multiplyMaterial != null) Destroy(multiplyMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
        foreach (Sound s in new[] {infractionSound, walkingSound, periodicSound, shoveSound1, shoveSound2}) if (s?.source != null) Destroy(s.source);
    }
    private void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, shoveRadius); }
}
