using System.Collections;
using UnityEngine;

// Sonidos del combate que NO salen de la cinematica de ataque. Vive en el objeto persistente UiHoverSound y escucha eventos estaticos.
//  - Curacion y mejora (buff) sin habilidad con sonido propio: pociones, regeneracion al inicio del turno, curaciones de enemigos,
//    levantar a un caido, efectos de reliquias.
//  - Habilidades con sonido propio que NO pasan por la cinematica (apoyos y curas de enemigos, invocaciones del jefe...): su 'Hit Sound'
//    se reproduce aqui, retrasado para que su golpe caiga en el impacto de la animacion (mismo ajuste 'Hit Sound Lead' que en la cinematica).
//  - Reproductor estatico CombatCueSounds.Play(...) para anuncios, entrada a fase 2, derrota del jefe, etc.
// Si hay una habilidad con enfoque cinematografico en curso (AttackCinematic.Busy) NO suenan los de curacion / mejora genericos: esa
// habilidad ya trae su sonido en el momento exacto del impacto, y asi no se duplica.
public class CombatCueSounds : MonoBehaviour
{
    [SerializeField] private AudioClip healClip;
    [SerializeField] private AudioClip buffClip;
    [SerializeField, Range(0f, 1f)] private float healVolume = 0.15f;   // de fondo, por debajo de la musica de combate
    [SerializeField, Range(0f, 1f)] private float buffVolume = 0.15f;
    [Tooltip("Tiempo minimo (s) entre dos sonidos del mismo tipo, para que varias curaciones o mejoras simultaneas no se acumulen.")]
    [SerializeField, Min(0f)] private float minInterval = 0.25f;
    [Tooltip("Adelanto (s) por defecto de 'Hit Sound' cuando la habilidad no define el suyo (sin cinematica).")]
    [SerializeField, Min(0f)] private float defaultHitLead = 0.10f;

    public static CombatCueSounds Instance { get; private set; }

    private AudioSource source;
    private float lastHeal = -10f;
    private float lastBuff = -10f;
    private float suppressGenericUntil = -10f;

    private void Awake()
    {
        if (healClip != null) healClip.LoadAudioData();
        if (buffClip != null) buffClip.LoadAudioData();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        Instance = this;
        BattleUnit.HealApplied += OnHeal;
        CombatFeedback.Message += OnMessage;
        AbilitySystem.AbilityExecuted += OnAbilityExecuted;
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        BattleUnit.HealApplied -= OnHeal;
        CombatFeedback.Message -= OnMessage;
        AbilitySystem.AbilityExecuted -= OnAbilityExecuted;
    }

    // Reproduce 'clip' (si existe) con el volumen indicado, opcionalmente tras 'delay' segundos. No hace nada si no hay reproductor.
    public static void Play(AudioClip clip, float volume, float delay = 0f)
    {
        if (clip == null || Instance == null || !Instance.isActiveAndEnabled)
            return;

        if (delay <= 0.001f)
            Instance.source.PlayOneShot(clip, volume);
        else
            Instance.StartCoroutine(Instance.PlayLater(clip, volume, delay));
    }

    private IEnumerator PlayLater(AudioClip clip, float volume, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        source.PlayOneShot(clip, volume);
    }

    // Habilidad ejecutada sin cinematica: su sonido se programa para el impacto de la animacion.
    private void OnAbilityExecuted(BattleUnit user, BattleUnit target, AbilityData ability)
    {
        if (ability == null || ability.hitSound == null || AttackCinematic.Busy)
            return;

        float speed = Mathf.Max(0.1f, ability.animationSpeed);
        float impactAt = ability.impactTime > 0f ? ability.impactTime / speed : AttackCinematic.DefaultImpactSeconds;
        float lead = ability.hitSoundLead >= 0f ? ability.hitSoundLead : defaultHitLead;
        float delay = Mathf.Max(0f, impactAt - lead);

        Play(ability.hitSound, AttackCinematic.AbilitySfxVolume, delay);

        // Mientras suena este, los de curacion / mejora genericos se callan (la propia habilidad ya trae el suyo).
        suppressGenericUntil = Time.unscaledTime + Mathf.Max(0.6f, delay + 0.4f);
    }

    private void OnHeal(BattleUnit unit, int amount)
    {
        if (amount <= 0 || healClip == null || AttackCinematic.Busy || Time.unscaledTime < suppressGenericUntil)
            return;

        if (Time.unscaledTime - lastHeal < minInterval)
            return;

        lastHeal = Time.unscaledTime;
        source.PlayOneShot(healClip, healVolume);
    }

    private void OnMessage(BattleUnit unit, string text, FeedbackKind kind)
    {
        if (kind != FeedbackKind.Buff || buffClip == null || AttackCinematic.Busy || Time.unscaledTime < suppressGenericUntil || !IsStatusBuff(text))
            return;

        if (Time.unscaledTime - lastBuff < minInterval)
            return;

        lastBuff = Time.unscaledTime;
        source.PlayOneShot(buffClip, buffVolume);
    }

    // Solo las mejoras de estado (Resistencia, Guardia, Evasion, Regeneracion); no "¡Convocado!" ni "¡De pie!".
    private static bool IsStatusBuff(string text)
    {
        foreach (StatusEffectType type in new[] { StatusEffectType.DefenseBoost, StatusEffectType.Guard, StatusEffectType.EvasionBoost, StatusEffectType.Regeneration })
            if (CombatFeedback.StatusText(type, out bool _) == text)
                return true;

        return false;
    }
}
