using UnityEngine;

// Presentacion de habilidades: lanza el trigger del Animator de ESTA unidad (p. ej. "Corte" -> Valerian_CorteDelBastion, que
// vuelve solo al Idle). No altera ninguna regla de combate.
//
// Dos formas de uso:
//  - AttackCinematic llama a PlayAttack para los ataques (con velocidad) y espera a IsPlayingAttack.
//  - Para el resto de habilidades con 'animationTrigger' escucha AbilitySystem.AbilityExecuted.
//
// Un heroe caido (UnitDownedVisual) no se anima: se apaga el Animator mientras esta tumbado y se reanuda al levantarse.
[RequireComponent(typeof(BattleUnit))]
public class UnitAnimationDriver : MonoBehaviour
{
    private BattleUnit unit;
    private Animator animator;

    private bool playing;
    private int idleHash;
    private int attackHash;
    private float playStart;
    private float playSpeed = 1f;

    [Tooltip("Trigger del Animator para la defensa cuando la unidad recibe un ataque (vacio = no tiene animacion de defensa).")]
    [SerializeField] private string defendTrigger = "Defensa";

    public bool IsPlayingAttack => playing;
    public Animator Animator => animator;

    // true si el Animator tiene animacion de defensa y la unidad puede usarla ahora.
    public bool CanDefend
    {
        get
        {
            if (string.IsNullOrEmpty(defendTrigger) || animator == null || animator.runtimeAnimatorController == null || !animator.enabled)
                return false;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == defendTrigger)
                    return true;
            }

            return false;
        }
    }

    // Lanza la animacion de defensa (al recibir un ataque). Comparte el seguimiento de fin con los ataques.
    public bool PlayDefense()
    {
        return CanDefend && PlayAttack(defendTrigger, 1f);
    }

    // ------------------------------------------------------------
    // Jefe: hablar, entrada a la segunda fase y derrota
    // ------------------------------------------------------------

    [Header("Jefe (solo si su Animator lo tiene)")]
    [SerializeField] private string talkTrigger = "Hablar";
    [SerializeField] private string phaseTrigger = "EntradaFase2";
    [SerializeField] private string phaseBool = "Fase2";
    [SerializeField] private string defeatTrigger = "Derrota";

    [Header("Sonidos de la fase 2 del jefe")]
    [Tooltip("Suena al empezar la animacion de entrada a la fase 2 (estallido del aura).")]
    [SerializeField] private AudioClip phaseEntrySound;
    [Tooltip("Aura de la fase 2: bucle continuo que sube al terminar la animacion de entrada y se apaga al caer el jefe.")]
    [SerializeField] private AudioClip phaseLoopSound;
    [SerializeField, Range(0f, 1f)] private float phaseLoopVolume = 0.08f;
    [SerializeField, Min(0.1f)] private float phaseLoopFadeSeconds = 1.5f;

    private AudioSource phaseLoopSource;
    private bool phaseLoopWanted;

    private string pendingTrigger;      // animacion que espera a que el jefe quede libre (entrada a la fase 2)
    private bool pendingPhaseBool;
    private bool defeated;

    private bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(name) || animator == null || animator.runtimeAnimatorController == null || !animator.enabled)
            return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.type == type && p.name == name)
                return true;

        return false;
    }

    // Gesto de hablar (mientras el jefe dice una frase). No interrumpe otra animacion en curso.
    public bool PlayTalk()
    {
        if (defeated || playing || pendingTrigger != null || !HasParameter(talkTrigger, AnimatorControllerParameterType.Trigger))
            return false;

        return PlayAttack(talkTrigger, 1f);
    }

    // Derrota del jefe: pose de derrota que se queda en su ultimo fotograma. Devuelve false si no tiene esa animacion.
    public bool PlayDefeat()
    {
        if (!HasParameter(defeatTrigger, AnimatorControllerParameterType.Trigger))
            return false;

        defeated = true;
        phaseLoopWanted = false;   // el aura se apaga al caer
        pendingTrigger = null;
        Finish();
        animator.ResetTrigger(defeatTrigger);
        animator.SetTrigger(defeatTrigger);
        return true;
    }

    // Duracion del clip de derrota (para saber cuanto esperar antes de desvanecer al jefe).
    public float DefeatSeconds
    {
        get
        {
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null && clip.name.EndsWith(defeatTrigger))
                        return clip.length;

            return 1.8f;
        }
    }

    // Cambio de fase del jefe: la animacion de entrada espera a que termine el golpe/animacion en curso.
    private void OnPhaseChanged(BossPhaseController controller, BossPhaseData.Phase previous, BossPhaseData.Phase next)
    {
        if (controller == null || controller.Unit != unit || controller.CurrentIndex < 1 ||
            !HasParameter(phaseTrigger, AnimatorControllerParameterType.Trigger))
            return;

        pendingTrigger = phaseTrigger;
        pendingPhaseBool = true;
    }

    private void UpdatePending()
    {
        if (pendingTrigger == null || playing || defeated || AttackCinematic.Busy)
            return;

        string trigger = pendingTrigger;
        pendingTrigger = null;

        if (pendingPhaseBool && HasParameter(phaseBool, AnimatorControllerParameterType.Bool))
            animator.SetBool(phaseBool, true);

        pendingPhaseBool = false;
        phaseRunning = PlayAttack(trigger, 1f);

        // Estallido del aura al entrar en la fase 2 (de fondo, igual que los efectos de habilidades).
        if (phaseRunning)
            CombatCueSounds.Play(phaseEntrySound, AttackCinematic.AbilitySfxVolume * 1.5f);
    }

    // Aura de la fase 2: bucle continuo con entrada y salida suaves.
    private void StartPhaseLoop()
    {
        if (phaseLoopSound == null || defeated)
            return;

        if (phaseLoopSource == null)
        {
            phaseLoopSource = gameObject.AddComponent<AudioSource>();
            phaseLoopSource.clip = phaseLoopSound;
            phaseLoopSource.loop = true;
            phaseLoopSource.playOnAwake = false;
            phaseLoopSource.spatialBlend = 0f;
            phaseLoopSource.volume = 0f;
        }

        phaseLoopWanted = true;

        if (!phaseLoopSource.isPlaying)
            phaseLoopSource.Play();
    }

    private void TickPhaseLoop()
    {
        if (phaseLoopSource == null)
            return;

        float target = phaseLoopWanted && !defeated ? phaseLoopVolume : 0f;
        phaseLoopSource.volume = Mathf.MoveTowards(phaseLoopSource.volume, target, phaseLoopVolume / phaseLoopFadeSeconds * Time.unscaledDeltaTime);

        if (target <= 0f && phaseLoopSource.volume <= 0.001f && phaseLoopSource.isPlaying)
            phaseLoopSource.Stop();
    }

    private bool phaseRunning;

    // true desde que el jefe cambia de fase hasta que termina su animacion de entrada (TurnManager pausa los turnos mientras tanto).
    public bool PhaseEntryBusy => !defeated && (pendingPhaseBool || phaseRunning);

    [Tooltip("Trigger del Animator para la caida cuando el heroe llega a vida 0 (queda en pose de derrota en su sitio). Vacio = sin animacion de caida.")]
    [SerializeField] private string downedTrigger = "Caido";
    [Tooltip("Trigger del Animator para levantarse cuando se le cura (vuelve al Idle).")]
    [SerializeField] private string standTrigger = "Levantar";

    private bool downedByAnimation;   // pose de derrota dibujada por el Animator

    // true si el Animator tiene animacion de caida y puede usarse ahora.
    public bool HasDownedAnimation
    {
        get
        {
            if (string.IsNullOrEmpty(downedTrigger) || animator == null || animator.runtimeAnimatorController == null || !animator.enabled)
                return false;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == downedTrigger)
                    return true;
            }

            return false;
        }
    }

    // Caida: el heroe pasa a su pose de derrota (en el sitio) y se queda ahi hasta que se levante. true si hay animacion.
    public bool PlayDowned()
    {
        if (!HasDownedAnimation)
            return false;

        downedByAnimation = true;
        Finish();
        animator.ResetTrigger(standTrigger);
        animator.ResetTrigger(downedTrigger);
        animator.SetTrigger(downedTrigger);
        return true;
    }

    // Se levanta (curacion): vuelve al Idle.
    public void StandUp()
    {
        if (!downedByAnimation || animator == null)
            return;

        downedByAnimation = false;

        if (animator.runtimeAnimatorController != null && !string.IsNullOrEmpty(standTrigger))
        {
            animator.ResetTrigger(downedTrigger);
            animator.SetTrigger(standTrigger);
        }
    }

    private void Awake()
    {
        unit = GetComponent<BattleUnit>();
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        AbilitySystem.AbilityExecuted += OnAbilityExecuted;
        BossPhaseController.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        AbilitySystem.AbilityExecuted -= OnAbilityExecuted;
        BossPhaseController.PhaseChanged -= OnPhaseChanged;
        Finish();
    }

    // Lanza 'trigger' a la velocidad indicada. Devuelve false si no se pudo (sin Animator, sin trigger o apagado).
    public bool PlayAttack(string trigger, float speed)
    {
        if (!CanPlay(trigger))
            return false;

        playSpeed = Mathf.Max(0.1f, speed);
        idleHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        attackHash = 0;
        playStart = Time.unscaledTime;
        playing = true;

        animator.speed = playSpeed;
        animator.ResetTrigger(trigger);
        animator.SetTrigger(trigger);
        RaiseForAction();
        return true;
    }

    // Orden de dibujo durante la accion: mientras la unidad hace una animacion (ataque, apoyo, defensa) se dibuja por encima de
    // las demas unidades, para que su animacion y sus efectos nunca queden tapados por un vecino. Con el enfoque cinematografico
    // activo es el propio enfoque quien eleva a los participantes; esto cubre las habilidades sin enfoque (apoyos, curaciones,
    // mejoras) y el tramo de animacion que dura mas que el enfoque. Al terminar (Finish) recupera su orden normal.
    private SpriteRenderer actionRenderer;
    private int actionOriginal;
    private int actionApplied;
    private bool actionRaised;

    private void RaiseForAction()
    {
        if (actionRaised || unit == null || AttackCinematic.Busy)
            return;

        SpriteRenderer own = unit.GetComponentInChildren<SpriteRenderer>();

        if (own == null)
            return;

        int top = int.MinValue;

        foreach (BattleUnit other in FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude))
        {
            if (other == null || other == unit)
                continue;

            SpriteRenderer sr = other.GetComponentInChildren<SpriteRenderer>();

            if (sr != null && sr.sortingOrder > top)
                top = sr.sortingOrder;
        }

        if (top == int.MinValue || own.sortingOrder > top)
            return;   // ya esta delante de todos

        actionRenderer = own;
        actionOriginal = own.sortingOrder;
        actionApplied = top + 1;
        own.sortingOrder = actionApplied;
        actionRaised = true;
    }

    private void LowerAfterAction()
    {
        if (!actionRaised)
            return;

        if (actionRenderer != null && actionRenderer.sortingOrder == actionApplied)
            actionRenderer.sortingOrder = actionOriginal;

        actionRaised = false;
        actionRenderer = null;
    }

    // Pausa o reanuda la animacion (pausa de impacto). Reanudar vuelve a la velocidad del ataque.
    public void Freeze(bool frozen)
    {
        if (animator != null && playing)
            animator.speed = frozen ? 0f : playSpeed;
    }

    private void OnAbilityExecuted(BattleUnit user, BattleUnit target, AbilityData ability)
    {
        if (user != unit || ability == null || string.IsNullOrEmpty(ability.animationTrigger))
            return;

        // Ya la lanzo el enfoque cinematografico (ataques).
        if (playing)
            return;

        PlayAttack(ability.animationTrigger, ability.animationSpeed);
    }

    private bool CanPlay(string trigger)
    {
        if (animator == null || animator.runtimeAnimatorController == null || !animator.enabled || downedByAnimation || defeated)
            return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)
                return true;
        }

        Debug.LogWarning("UnitAnimationDriver: el Animator de " + name + " no tiene el trigger '" + trigger + "'.");
        return false;
    }

    private void Finish()
    {
        bool phaseJustEnded = phaseRunning;
        LowerAfterAction();
        playing = false;
        phaseRunning = false;

        // Termino la animacion de entrada a la fase 2: el aura empieza a sonar de fondo.
        if (phaseJustEnded && !defeated)
            StartPhaseLoop();

        if (animator != null)
            animator.speed = 1f;
    }

    [Header("Respiracion sincronizada (enemigos)")]
    [Tooltip("Todas las unidades con esto activo respiran a la vez: el Idle se alinea con un reloj comun (como los enemigos del video de referencia). " +
             "Los clips de Idle de estas unidades deben durar lo mismo.")]
    [SerializeField] private bool syncIdle = false;

    private int syncHash;

    // Mantiene el Idle en fase con el reloj comun mientras la unidad no hace otra animacion.
    private void SyncIdle(bool downed)
    {
        if (!syncIdle || downed || downedByAnimation || !animator.enabled || animator.runtimeAnimatorController == null || animator.IsInTransition(0))
            return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        // El primer estado visto en reposo es el Idle de la unidad.
        if (syncHash == 0)
            syncHash = info.shortNameHash;

        if (info.shortNameHash != syncHash || info.length <= 0.01f)
            return;

        float wanted = (Time.time / info.length) % 1f;
        float current = info.normalizedTime % 1f;
        float diff = Mathf.Abs(wanted - current);
        diff = Mathf.Min(diff, 1f - diff);

        if (diff > 0.02f)
            animator.Play(syncHash, 0, wanted);
    }

    private void Update()
    {
        TickPhaseLoop();

        if (animator == null)
            return;

        bool downed = UnitDownedVisual.IsActive(unit);

        // Con animacion de caida el Animator sigue encendido (es el que dibuja la pose de derrota).
        if (downed && !downedByAnimation && animator.enabled)
        {
            animator.enabled = false;
            Finish();
        }
        else if (!downed && !animator.enabled)
        {
            animator.enabled = true;
        }

        // La caida del jefe (derrota animada) tambien se dibuja por delante de los demas hasta que se desvanece.
        if (defeated && !actionRaised && !AttackCinematic.Busy)
            RaiseForAction();

        if (!playing)
        {
            UpdatePending();

            if (!playing)
                SyncIdle(downed);

            return;
        }

        // Si el enfoque cinematografico termino antes que la animacion, la unidad sigue por delante de las demas hasta acabar.
        if (!actionRaised && !AttackCinematic.Busy)
            RaiseForAction();

        // Seguimiento del ataque: espera a que entre en el estado de ataque y a que vuelva al Idle (o termine su clip).
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (attackHash == 0)
        {
            if (info.shortNameHash != idleHash && !animator.IsInTransition(0))
                attackHash = info.shortNameHash;
            else if (Time.unscaledTime - playStart > 0.6f)
                Finish();   // el trigger no llego a cambiar de estado
        }
        else if (info.shortNameHash != attackHash || info.normalizedTime >= 0.999f)
        {
            Finish();
        }
    }
}
