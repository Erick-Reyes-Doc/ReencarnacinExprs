using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Enfoque cinematografico de los ataques (referencia: Darkest Dungeon). Es solo PRESENTACION: nunca cambia reglas de daño,
// habilidades, estados ni turnos.
//
// Secuencia de un ataque (heroes y enemigos):
//   nombre de la habilidad -> zoom rapido al atacante y su objetivo (el resto se oscurece) -> animacion ofensiva ->
//   IMPACTO (destello + sacudida + pausa de 0,05 s + numeros de daño) -> regreso suave a la vista normal.
//
// El zoom afecta solo al escenario (la camara); el HUD no se redimensiona. Restaura siempre camara y colores, aunque el ataque
// falle o el objetivo muera.
//
// Dos formas de entrar (comparten todo el codigo):
//   1) Heroes: CombatHudController llama a Play(..., resolve, done). La habilidad se resuelve EN el impacto, asi daño, vida,
//      numeros y muerte coinciden con el golpe, y el turno termina al acabar (done).
//   2) Enemigos y ataques de area: la IA resuelve al momento, asi que se escucha AbilitySystem.AbilityStarting; los numeros se
//      retienen (CombatFeedback.BeginHold) hasta el impacto.
public class AttackCinematic : MonoBehaviour
{
    [Header("Zoom (solo escenario)")]
    [Tooltip("Acercamiento maximo. Se reduce solo si atacante y objetivo dejarian de verse completos.")]
    [Range(1f, 1.4f)] [SerializeField] private float zoom = 1.18f;
    [Min(0.01f)] [SerializeField] private float zoomInSeconds = 0.09f;
    [Min(0.01f)] [SerializeField] private float zoomOutSeconds = 0.28f;

    [Header("Oscurecer a los que no participan")]
    [Range(0.1f, 1f)] [SerializeField] private float dimFactor = 0.45f;
    [Tooltip("Oscuridad del velo que cubre el fondo y a los demas personajes durante el enfoque (0 = ninguno, 1 = negro).")]
    [Range(0f, 0.9f)] [SerializeField] private float shadeAlpha = 0.5f;

    [Header("Impacto")]
    [Tooltip("Segundos hasta el golpe cuando la habilidad no tiene 'Impact Time' o el atacante no tiene animacion.")]
    [Min(0.05f)] [SerializeField] private float defaultImpactSeconds = 0.30f;
    [Tooltip("Pausa congelada en el golpe.")]
    [Min(0f)] [SerializeField] private float hitStopSeconds = 0.05f;
    [Min(0f)] [SerializeField] private float holdAfterImpactSeconds = 0.30f;
    [Min(0f)] [SerializeField] private float shakeSeconds = 0.16f;
    [Tooltip("Intensidad de la sacudida como fraccion de la altura de la vista.")]
    [Range(0f, 0.05f)] [SerializeField] private float shakeAmount = 0.012f;
    [Min(0.01f)] [SerializeField] private float flashSeconds = 0.14f;
    [Tooltip("Tiron de zoom extra en el golpe (fraccion: 0,05 = +5 %) que se relaja enseguida.")]
    [Range(0f, 0.15f)] [SerializeField] private float punchZoom = 0.05f;
    [Min(0.01f)] [SerializeField] private float punchSeconds = 0.14f;
    [Min(0.01f)] [SerializeField] private float slashSeconds = 0.16f;

    [Header("Embestida del atacante")]
    [Tooltip("Cuanto avanza hacia el objetivo (unidades de mundo). Nunca mas de un tercio de la distancia que los separa.")]
    [Min(0f)] [SerializeField] private float lungeDistance = 0.9f;
    [Min(0.01f)] [SerializeField] private float lungeInSeconds = 0.10f;
    [Min(0.01f)] [SerializeField] private float lungeBackSeconds = 0.25f;

    [Header("Reaccion del objetivo")]
    [Tooltip("Cuanto retrocede al ser golpeado (unidades de mundo).")]
    [Min(0f)] [SerializeField] private float recoilDistance = 0.4f;
    [Min(0.01f)] [SerializeField] private float recoilBackSeconds = 0.25f;
    [SerializeField] private Color hitTint = new Color(1f, 0.38f, 0.32f, 1f);
    [Min(0.01f)] [SerializeField] private float hitTintSeconds = 0.16f;

    [Header("Sonido del golpe")]
    [Tooltip("Sonido por defecto de los ataques (cada habilidad puede traer el suyo en 'Hit Sound').")]
    [SerializeField] private AudioClip defaultHitSound;
    [Tooltip("Volumen de los efectos de habilidades (golpes, disparos, bloqueos). Bajo a proposito: deben quedar de fondo, por debajo de la musica de combate.")]
    [Range(0f, 1f)] [SerializeField] private float hitVolume = 0.12f;
    [Tooltip("Cuanto antes del impacto empieza el audio: el clip lleva el silbido del tajo y el golpe seco unas decimas despues.")]
    [Min(0f)] [SerializeField] private float hitSoundLeadSeconds = 0.25f;

    [Header("Defensa (el objetivo se cubre con el escudo)")]
    [Tooltip("Sonido al bloquear un golpe (choque metalico sobre el escudo).")]
    [SerializeField] private AudioClip blockSound;
    [Tooltip("Cuanto antes del impacto empieza a subir el escudo el defensor.")]
    [Min(0f)] [SerializeField] private float blockLeadSeconds = 0.10f;
    [Tooltip("Fraccion del retroceso normal que sufre quien bloquea (el escudo absorbe el golpe).")]
    [Range(0f, 1f)] [SerializeField] private float blockRecoilFactor = 0.4f;

    [Header("Aura de proteccion (habilidades de apoyo)")]
    [Min(0.2f)] [SerializeField] private float auraSeconds = 0.95f;
    [SerializeField] private Color auraColor = new Color(0.55f, 0.82f, 1f, 1f);

    [Header("Nombre de la habilidad")]
    [Min(0f)] [SerializeField] private float bannerSeconds = 1.0f;

    public static AttackCinematic Instance { get; private set; }

    // true mientras dura el enfoque: el HUD ignora acciones y el fin de turno espera.
    public static bool Busy => Instance != null && Instance.running;

    // Volumen de los efectos de habilidades (el mismo para los sonidos que se reproducen fuera de la cinematica).
    public static float AbilitySfxVolume => Instance != null ? Instance.hitVolume : 0.12f;

    // Segundos (a velocidad 1) hasta el impacto de una habilidad sin 'Impact Time' propio.
    public static float DefaultImpactSeconds => Instance != null ? Instance.defaultImpactSeconds : 0.30f;

    private bool running;
    private Coroutine routine;

    // Estado aplicado cada fotograma en LateUpdate.
    private Camera cam;
    private float baseSize;
    private Vector3 basePos;
    private float targetSize;
    private Vector3 targetPos;
    private float zoomK;
    private float dimK;
    private Vector3 shakeOffset;

    // Embestida / reaccion / tiron (se calculan en UpdateFx y se aplican en LateUpdate).
    private Transform attackerT;
    private Transform targetT;
    private SpriteRenderer targetSr;
    private Vector3 lungeOffset;
    private Vector3 recoilOffset;
    private Vector3 lungeApplied;
    private Vector3 recoilApplied;
    private Vector3 lungeLast;
    private Vector3 recoilLast;
    private float recoilScale = 1f;    // 1 = retroceso normal; menos si el objetivo bloquea
    private float currentPower = 1f;   // contundencia del golpe en curso (AbilityData.impactPower)
    private float currentStop;         // pausa de impacto en curso (ya escalada)
    private float punchK;
    private float clock;
    private float impactClock = -1f;
    private float impactAtTime;
    private float lungeWorld;
    private float awayDir;
    private Color tintOriginal;
    private Color tintApplied;
    private bool tintActive;
    private bool hitLanded;

    private readonly List<Dimmed> dimmed = new List<Dimmed>();
    private GameObject slashGo;
    private float slashAge;
    private float slashBase;
    private static Sprite slashSprite;
    private AnimatableFlash flash;
    private GameObject bannerRoot;
    private Text bannerText;
    private CanvasGroup bannerGroup;
    private Coroutine bannerRoutine;
    private static Sprite flashSprite;

    private class Dimmed
    {
        public SpriteRenderer renderer;
        public Color original;
        public Color applied;
    }

    private class AnimatableFlash
    {
        public GameObject go;
        public SpriteRenderer renderer;
        public float age;
        public float baseScale;
    }

    // ------------------------------------------------------------
    // Ciclo de vida y entradas
    // ------------------------------------------------------------

    private void OnEnable()
    {
        Instance = this;
        AbilitySystem.AbilityStarting += OnAbilityStarting;
        AbilitySystem.AbilityFailed += OnAbilityFailed;
    }

    private void OnDisable()
    {
        AbilitySystem.AbilityStarting -= OnAbilityStarting;
        AbilitySystem.AbilityFailed -= OnAbilityFailed;
        Cleanup();

        if (Instance == this)
            Instance = null;
    }

    // true si este ataque debe llevar enfoque (ofensivo, con objetivo distinto del usuario).
    public static bool Wants(AbilityData ability, BattleUnit user, BattleUnit target)
    {
        return Instance != null && Instance.isActiveAndEnabled && !Busy && Instance.Applies(ability, user, target);
    }

    private bool Applies(AbilityData ability, BattleUnit user, BattleUnit target)
    {
        if (ability == null || user == null || target == null)
            return false;

        // Ataques (enemigo o heroe) y apoyos de aura sobre un aliado (puede ser el propio usuario).
        bool attack = ability.abilityType == AbilityType.Attack;
        bool aura = ability.protectiveAura && target.Side == user.Side;

        if (target == user && !aura)
            return false;

        if (!attack && !aura)
            return false;

        Camera c = Camera.main;
        return c != null && c.orthographic;
    }

    // Heroes: 'resolve' ejecuta la habilidad en el instante del impacto (devuelve si tuvo exito); 'done' se llama al terminar
    // (con ese resultado) para que el HUD consuma el turno.
    public static void Play(BattleUnit user, BattleUnit target, AbilityData ability, Func<bool> resolve, Action<bool> done)
    {
        if (Instance == null)
        {
            bool ok = resolve != null && resolve();
            done?.Invoke(ok);
            return;
        }

        Instance.Begin(user, target, ability, resolve, done);
    }

    // Enemigos y ataques de area (ya validados, a punto de resolverse): se retienen los numeros hasta el impacto.
    private void OnAbilityStarting(BattleUnit user, BattleUnit target, AbilityData ability)
    {
        if (running || !Applies(ability, user, target))
            return;

        // Begin limpia cualquier retencion anterior, asi que la retencion se activa despues.
        Begin(user, target, ability, null, null);
        CombatFeedback.BeginHold();
    }

    // La habilidad no tuvo efecto: se cancela el enfoque al momento (la IA puede probar otra).
    private void OnAbilityFailed()
    {
        if (running && currentResolve == null)
            Cleanup();
    }

    private Func<bool> currentResolve;

    private void Begin(BattleUnit user, BattleUnit target, AbilityData ability, Func<bool> resolve, Action<bool> done)
    {
        Cleanup();
        currentResolve = resolve;
        running = true;
        routine = StartCoroutine(Run(user, target, ability, resolve, done));
    }

    // ------------------------------------------------------------
    // Secuencia
    // ------------------------------------------------------------

    private IEnumerator Run(BattleUnit user, BattleUnit target, AbilityData ability, Func<bool> resolve, Action<bool> done)
    {
        bool success = true;
        bool finished = false;
        bool support = ability.abilityType != AbilityType.Attack;   // apoyo con aura: sin embestida, golpe, sacudida ni pausa
        currentPower = Mathf.Max(0.5f, ability.impactPower);
        currentStop = support ? 0f : hitStopSeconds * currentPower;
        UnitAnimationDriver driver = user != null ? user.GetComponent<UnitAnimationDriver>() : null;

        // Si el objetivo es un rival con animacion de defensa (Valerian al ser atacado), se cubre con el escudo.
        UnitAnimationDriver targetDriver = !support && target != null && user != null && target.Side != user.Side
            ? target.GetComponent<UnitAnimationDriver>() : null;
        bool blocking = targetDriver != null && targetDriver.CanDefend;
        bool blockStarted = false;
        recoilScale = blocking ? blockRecoilFactor : 1f;

        // Proyectil (flechas): sale 'flight' segundos antes del impacto y vuela hasta el objetivo; el arquero no embiste.
        float flight = ability.projectileFlightSeconds;
        bool projectile = !support && flight > 0.01f;
        bool arrowFired = false;

        // Ataques de area (lluvia de meteoritos) y sello de erupcion (se anuncia en el suelo antes de estallar).
        bool area = !support && ability.targetType == AbilityTargetType.AllEnemies;
        bool meteors = area && ability.impactEffect == ImpactEffect.MeteorRain;
        bool eruption = !support && ability.impactEffect == ImpactEffect.Eruption;
        bool runeShown = false;
        List<BattleUnit> areaTargets = new List<BattleUnit>();

        try
        {
            cam = Camera.main;
            baseSize = cam.orthographicSize;
            basePos = cam.transform.position;

            Bounds focus = BoundsOf(user);
            focus.Encapsulate(BoundsOf(target));

            // Ataque de area: todos los enemigos vivos entran en el plano (el zoom baja solo para que quepan).
            // Apoyo de grupo (Amparo Divino, Bendicion Continua): todos los aliados vivos entran en el plano y reciben el aura.
            bool groupAllies = support && ability.targetType == AbilityTargetType.AllAllies;

            if ((area || groupAllies) && target != null)
            {
                foreach (BattleUnit candidate in FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude))
                {
                    if (candidate != null && candidate.Side == target.Side && candidate.IsAlive)
                    {
                        areaTargets.Add(candidate);
                        focus.Encapsulate(BoundsOf(candidate));
                    }
                }
            }
            targetSize = baseSize / ComputeZoom(focus, out targetPos);
            zoomK = 0f;
            dimK = 0f;
            shakeOffset = Vector3.zero;

            CollectDimmed(user, target, ability);
            BeginFocus(user, target, areaTargets);
            ShowBanner(ability.abilityName);

            // Animacion ofensiva del atacante (a la velocidad de la habilidad).
            float speed = Mathf.Max(0.1f, ability.animationSpeed);
            bool animated = !string.IsNullOrEmpty(ability.animationTrigger) && driver != null &&
                            driver.PlayAttack(ability.animationTrigger, speed);
            float impactAt = animated && ability.impactTime > 0f ? ability.impactTime / speed : defaultImpactSeconds;

            // Embestida (atacante hacia el objetivo) y retroceso del objetivo: solo en horizontal.
            attackerT = user != null ? user.transform : null;
            targetT = target != null ? target.transform : null;
            targetSr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
            float dx = targetT != null && attackerT != null ? targetT.position.x - attackerT.position.x : 1f;
            awayDir = dx >= 0f ? 1f : -1f;
            lungeWorld = support || projectile ? 0f : Mathf.Min(lungeDistance, Mathf.Abs(dx) * 0.33f);
            clock = 0f;
            impactClock = -1f;
            impactAtTime = impactAt;

            // Zoom rapido + oscurecer hasta el golpe (la embestida llega justo al impacto).
            // El golpe del clip debe caer en el impacto: cada habilidad puede traer su propio adelanto (hitSoundLead).
            float lead = ability.hitSoundLead >= 0f ? ability.hitSoundLead : hitSoundLeadSeconds;
            float soundAt = Mathf.Max(0f, impactAt - lead);
            bool soundPlayed = false;
            bool shotPlayed = false;

            while (clock < impactAt)
            {
                if (!soundPlayed && clock >= soundAt)
                {
                    soundPlayed = true;
                    PlayHitSound(ability);
                }

                // El defensor sube el escudo justo antes de que llegue el golpe.
                if (blocking && !blockStarted && clock >= impactAt - blockLeadSeconds)
                {
                    blockStarted = true;
                    targetDriver.PlayDefense();
                }

                // Sonido de la suelta (cuerda del arco / lanzamiento): empieza un poco antes para que su golpe caiga al soltar.
                if (projectile && !shotPlayed && clock >= impactAt - flight - ability.shotSoundLead)
                {
                    shotPlayed = true;
                    PlayClip(ability.shotSound, hitVolume);
                }

                // La flecha sale del arco y vuela hasta el objetivo.
                if (projectile && !arrowFired && clock >= impactAt - flight)
                {
                    arrowFired = true;
                    float fly = Mathf.Min(flight, Mathf.Max(0.04f, impactAt - clock));

                    if (meteors)
                        SpawnMeteors(areaTargets, target, ability, fly);
                    else
                        SpawnArrow(user, target, ability, fly);
                }

                // El sello de erupcion aparece bajo el objetivo antes de estallar.
                if (eruption && !runeShown && clock >= impactAt - 0.45f)
                {
                    runeShown = true;
                    SpawnRune(target, ability.projectileColor);
                }

                TickArrows();
                clock += Time.unscaledDeltaTime;
                zoomK = Smooth(clock / zoomInSeconds);
                dimK = zoomK;
                ApplyDim();
                UpdateFx();
                yield return null;
            }

            zoomK = 1f;
            dimK = 1f;

            // IMPACTO: la habilidad se resuelve ahora (heroes) y salen los numeros retenidos.
            if (driver != null)
                driver.Freeze(true);

            // Antes de resolver: si el objetivo ardia, la erupcion sale mas grande (la quemadura se consume al resolver).
            StatusEffectController targetStatuses = target != null ? target.GetComponent<StatusEffectController>() : null;
            bool wasBurning = targetStatuses != null && targetStatuses.HasEffect(StatusEffectType.Burn);

            if (resolve != null)
            {
                CombatFeedback.BeginHold();
                success = resolve();
            }

            CombatFeedback.ReleaseHold();

            impactClock = clock;
            // La marca no hiere: sin retroceso ni tinte, solo la mira que se estampa.
            bool markOnly = !support && ability.impactEffect == ImpactEffect.Mark;
            hitLanded = success && !support && !markOnly;

            if (success)
            {
                if (support)
                {
                    if (ability.targetType == AbilityTargetType.AllAllies && areaTargets.Count > 0)
                    {
                        for (int a = 0; a < areaTargets.Count; a++)
                            SpawnAura(areaTargets[a], ability, a == 0);
                    }
                    else
                    {
                        SpawnAura(target, ability);
                    }
                }
                else if (blocking)
                {
                    // Bloqueado: sin tinte rojo ni tajo; chispas metalicas sobre el escudo y su sonido.
                    SpawnShockwave(target, 0.55f, new Color(0.85f, 0.93f, 1f, 1f), 10);
                    PlayClip(blockSound, hitVolume);
                }
                else
                {
                    switch (ability.impactEffect)
                    {
                        case ImpactEffect.Shockwave:
                            BeginHitTint();
                            SpawnFlash(target);
                            SpawnShockwave(target);
                            break;

                        case ImpactEffect.ArrowHit:
                            BeginHitTint();
                            SpawnShockwave(target, 0.35f, Color.Lerp(Color.white, ability.projectileColor, 0.5f), 8);
                            break;

                        case ImpactEffect.Pierce:
                            BeginHitTint();
                            SpawnSlash(target, 0f);
                            SpawnShockwave(target, 0.5f, new Color(0.75f, 0.88f, 1f, 1f), 12);
                            break;

                        case ImpactEffect.Mark:
                            SpawnMarkStamp(target, ability.projectileColor);
                            break;

                        case ImpactEffect.Burst:
                            BeginHitTint();
                            SpawnFlash(target, ability.projectileColor);
                            SpawnShockwave(target, 0.6f, ability.projectileColor, 12);
                            break;

                        case ImpactEffect.Fire:
                            BeginHitTint();
                            SpawnFlash(target, ability.projectileColor);
                            SpawnShockwave(target, 0.6f, ability.projectileColor, 10);
                            SpawnEmbers(target, Color.Lerp(ability.projectileColor, new Color(1f, 0.95f, 0.6f, 1f), 0.4f), 16);
                            break;

                        case ImpactEffect.Eruption:
                            BeginHitTint();
                            SpawnEruption(target, ability.projectileColor, wasBurning);
                            break;

                        case ImpactEffect.MeteorRain:
                            // Cada meteorito explota al llegar (SpawnMeteors); aqui solo se tine el primer objetivo.
                            BeginHitTint();
                            break;

                        default:
                            BeginHitTint();
                            SpawnFlash(target);
                            SpawnSlash(target, ability.impactSlashAngle);
                            break;
                    }
                }
            }

            if (blocking && targetDriver != null)
                targetDriver.Freeze(true);

            // Pausa de impacto (la camara y el atacante quedan congelados).
            float stop = 0f;

            while (stop < currentStop)
            {
                float dt = Time.unscaledDeltaTime;
                stop += dt;
                clock += dt;
                UpdateFx();
                yield return null;
            }

            if (driver != null)
                driver.Freeze(false);

            if (blocking && targetDriver != null)
                targetDriver.Freeze(false);

            // Sacudida + mantener el plano un instante.
            float hold = 0f;
            float lastHold = support ? auraSeconds : Mathf.Max(holdAfterImpactSeconds, shakeSeconds);

            while (hold < lastHold)
            {
                float dt = Time.unscaledDeltaTime;
                hold += dt;
                clock += dt;
                shakeOffset = success && !support && hold < shakeSeconds ? Shake((1f - hold / shakeSeconds) * currentPower) : Vector3.zero;
                UpdateFx();
                TickFlash();
                yield return null;
            }

            shakeOffset = Vector3.zero;

            // Regreso suave a la vista normal.
            float back = 0f;

            while (back < zoomOutSeconds)
            {
                float dt = Time.unscaledDeltaTime;
                back += dt;
                clock += dt;
                zoomK = 1f - Smooth(back / zoomOutSeconds);
                dimK = zoomK;
                ApplyDim();
                UpdateFx();
                TickFlash();
                yield return null;
            }

            zoomK = 0f;
            dimK = 0f;

            // Las reacciones (regreso del atacante y del objetivo) terminan antes de continuar.
            float settle = Mathf.Max(lungeBackSeconds, recoilBackSeconds) + currentStop + holdAfterImpactSeconds;

            while (impactClock >= 0f && clock - impactClock < settle)
            {
                clock += Time.unscaledDeltaTime;
                UpdateFx();
                TickFlash();
                yield return null;
            }

            // El turno solo continua cuando termina la animacion del atacante (con tope de seguridad).
            float wait = 0f;

            while (driver != null && driver.IsPlayingAttack && wait < 4f)
            {
                wait += Time.unscaledDeltaTime;
                clock += Time.unscaledDeltaTime;
                UpdateFx();
                TickFlash();
                yield return null;
            }

            finished = true;
        }
        finally
        {
            // Siempre se restaura la vista normal. 'done' solo si la secuencia llego a su fin (no si se aborto).
            Restore();

            if (finished)
                done?.Invoke(success);
        }
    }

    private AudioSource audioSource;
    private PositionManager positionManager;

    // Orden de dibujo base de TODOS los efectos (flechas, hechizos, tajos, impactos, auras): justo por encima del sprite mas
    // alto de la escena, asi nunca se ocultan tras ningun personaje (ni del objetivo ni de los demas). Queda por debajo del HUD.
    public static int FxBase()
    {
        int top = 0;

        foreach (BattleUnit unit in FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude))
        {
            SpriteRenderer sr = unit != null ? unit.GetComponentInChildren<SpriteRenderer>() : null;

            if (sr != null && sr.sortingOrder > top)
                top = sr.sortingOrder;
        }

        return Mathf.Min(top + 5, 60);
    }

    // Sonido del golpe: el de la habilidad o, si no tiene, el por defecto. Se reproduce en 2D (no depende de la camara).
    private void PlayHitSound(AbilityData ability)
    {
        if (ability == null || (ability.abilityType != AbilityType.Attack && ability.hitSound == null))
            return;   // un apoyo solo suena si trae su propio sonido

        AudioClip clip = ability.hitSound != null ? ability.hitSound : defaultHitSound;

        PlayClip(clip, hitVolume);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        audioSource.PlayOneShot(clip, volume);
    }

    // Aborta cualquier secuencia en curso y deja todo como estaba. Seguro de llamar varias veces.
    private void Cleanup()
    {
        Coroutine r = routine;
        routine = null;

        if (r != null)
            StopCoroutine(r);

        Restore();
    }

    // Deja camara, colores y retenciones como estaban.
    private void Restore()
    {
        routine = null;
        running = false;
        currentResolve = null;

        // Desplazamientos, tinte y tajo: se devuelven a su estado original (en un fin normal ya estan a cero).
        // Solo se resta lo aplicado si nadie movio la unidad despues (si no, ese desplazamiento ya no esta incluido).
        if (attackerT != null && (attackerT.position - lungeLast).sqrMagnitude < 1e-6f)
            attackerT.position -= lungeApplied;

        if (targetT != null && (targetT.position - recoilLast).sqrMagnitude < 1e-6f)
            targetT.position -= recoilApplied;

        if (tintActive && targetSr != null && Approximately(targetSr.color, tintApplied))
            targetSr.color = tintOriginal;

        // Red de seguridad: todas las unidades vuelven exactamente a su casilla (sin restos de embestida o retroceso).
        if (attackerT != null || targetT != null)
        {
            if (positionManager == null)
                positionManager = FindAnyObjectByType<PositionManager>();

            if (positionManager != null)
                positionManager.UpdateVisualPositions();
        }

        attackerT = null;
        targetT = null;
        targetSr = null;
        lungeApplied = Vector3.zero;
        recoilApplied = Vector3.zero;
        lungeOffset = Vector3.zero;
        recoilOffset = Vector3.zero;
        tintActive = false;
        hitLanded = false;
        impactClock = -1f;
        punchK = 0f;

        if (slashGo != null)
            Destroy(slashGo);

        slashGo = null;
        ClearAura();
        ClearArrows();
        EndFocus();

        if (cam != null)
        {
            cam.orthographicSize = baseSize;
            cam.transform.position = basePos;
        }

        zoomK = 0f;
        dimK = 0f;
        shakeOffset = Vector3.zero;

        foreach (Dimmed d in dimmed)
        {
            if (d.renderer != null && Approximately(d.renderer.color, d.applied))
                d.renderer.color = d.original;
        }

        dimmed.Clear();
        CombatFeedback.ReleaseHold();

        if (flash != null && flash.go != null)
            Destroy(flash.go);

        flash = null;
    }

    private void LateUpdate()
    {
        if (!running || cam == null)
            return;

        // Embestida del atacante y retroceso del objetivo (solo desplazamientos horizontales sobre su casilla).
        ApplyOffset(attackerT, ref lungeApplied, ref lungeLast, lungeOffset);
        ApplyOffset(targetT, ref recoilApplied, ref recoilLast, recoilOffset);

        float size = Mathf.Lerp(baseSize, targetSize, zoomK) / (1f + punchZoom * currentPower * punchK);
        cam.orthographicSize = size;
        Vector3 pos = Vector3.Lerp(basePos, targetPos, zoomK) + shakeOffset;

        // La vista nunca sale del encuadre normal (ni con la sacudida): el fondo siempre la cubre.
        float maxDx = (baseSize - size) * cam.aspect;
        float maxDy = baseSize - size;
        pos.x = Mathf.Clamp(pos.x, basePos.x - maxDx, basePos.x + maxDx);
        pos.y = Mathf.Clamp(pos.y, basePos.y - maxDy, basePos.y + maxDy);
        cam.transform.position = new Vector3(pos.x, pos.y, basePos.z);

        // Velo oscuro que cubre toda la vista (fondo y demas personajes), por debajo de los protagonistas de la accion.
        if (shade != null)
        {
            shade.transform.position = new Vector3(pos.x, pos.y, 0f);
            shade.transform.localScale = new Vector3(size * 2f * cam.aspect * 1.4f, size * 2f * 1.4f, 1f);
            shade.color = new Color(0.02f, 0.02f, 0.05f, shadeAlpha * dimK);
        }
    }

    // ------------------------------------------------------------
    // Enfoque (estilo Darkest Dungeon): solo el atacante y su objetivo quedan por encima de todo; el resto se funde al fondo
    // ------------------------------------------------------------

    private class Raised { public SpriteRenderer renderer; public int original; public int applied; }

    private readonly List<Raised> raised = new List<Raised>();
    private SpriteRenderer shade;
    private static Sprite solidSprite;

    private void RaiseForFocus(SpriteRenderer sr, int order)
    {
        if (sr == null)
            return;

        foreach (Raised r in raised)
            if (r.renderer == sr)
                return;

        raised.Add(new Raised { renderer = sr, original = sr.sortingOrder, applied = order });
        sr.sortingOrder = order;
    }

    private void BeginFocus(BattleUnit user, BattleUnit target, List<BattleUnit> group)
    {
        int top = FxBase() - 5;   // sprite mas alto antes de elevar a nadie

        if (solidSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            solidSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }

        SpriteRenderer userSr = user != null ? user.GetComponentInChildren<SpriteRenderer>() : null;

        GameObject go = new GameObject("FocusShade");
        shade = go.AddComponent<SpriteRenderer>();
        shade.sprite = solidSprite;
        shade.sortingLayerID = userSr != null ? userSr.sortingLayerID : 0;
        shade.sortingOrder = top + 9;
        shade.color = new Color(0.02f, 0.02f, 0.05f, 0f);

        // Participantes: el objetivo (o todo el grupo afectado) y, encima de todos, el que actua.
        if (group != null && group.Count > 0)
        {
            foreach (BattleUnit u in group)
                RaiseForFocus(u != null ? u.GetComponentInChildren<SpriteRenderer>() : null, top + 10);
        }

        if (target != null && target != user)
            RaiseForFocus(target.GetComponentInChildren<SpriteRenderer>(), top + 10);

        RaiseForFocus(userSr, top + 11);
    }

    private void EndFocus()
    {
        foreach (Raised r in raised)
        {
            if (r.renderer != null && r.renderer.sortingOrder == r.applied)
                r.renderer.sortingOrder = r.original;
        }

        raised.Clear();

        if (shade != null)
            Destroy(shade.gameObject);

        shade = null;
    }

    // ------------------------------------------------------------
    // Camara
    // ------------------------------------------------------------

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // Mayor zoom (hasta 'zoom') con el que 'focus' queda visible; la camara no sale nunca del encuadre normal (el fondo
    // siempre la cubre). Con zoom 1 no se mueve.
    private float ComputeZoom(Bounds focus, out Vector3 pos)
    {
        float aspect = cam.aspect;

        for (float z = zoom; z > 1.001f; z -= 0.01f)
        {
            float halfH = baseSize / z;
            float halfW = halfH * aspect;
            float maxDx = baseSize * aspect - halfW;
            float maxDy = baseSize - halfH;

            // Horizontal: centrado en el par. Vertical: el zoom conserva la altura a la que ya se ven (los paneles del HUD
            // tapan la parte baja de la pantalla).
            float cx = Mathf.Clamp(focus.center.x, basePos.x - maxDx, basePos.x + maxDx);
            float fy = focus.center.y;
            float cy = Mathf.Clamp(fy - (fy - basePos.y) / z, basePos.y - maxDy, basePos.y + maxDy);

            float mx = halfW * 0.06f;
            float my = halfH * 0.04f;

            if (focus.min.x >= cx - halfW + mx && focus.max.x <= cx + halfW - mx &&
                focus.min.y >= cy - halfH + my && focus.max.y <= cy + halfH - my)
            {
                pos = new Vector3(cx, cy, basePos.z);
                return z;
            }
        }

        pos = basePos;
        return 1f;
    }

    private Vector3 Shake(float strength)
    {
        Vector2 r = UnityEngine.Random.insideUnitCircle * (shakeAmount * baseSize * 2f * strength);
        return new Vector3(r.x, r.y, 0f);
    }

    private static Bounds BoundsOf(BattleUnit unit)
    {
        SpriteRenderer sr = unit != null ? unit.GetComponentInChildren<SpriteRenderer>() : null;

        if (sr != null && sr.sprite != null)
            return sr.bounds;

        Vector3 p = unit != null ? unit.transform.position : Vector3.zero;
        return new Bounds(p, new Vector3(1f, 1.5f, 0f));
    }

    // ------------------------------------------------------------
    // Oscurecer a los que no participan
    // ------------------------------------------------------------

    private void CollectDimmed(BattleUnit user, BattleUnit target, AbilityData ability)
    {
        dimmed.Clear();
        bool area = ability.targetType == AbilityTargetType.AllEnemies;

        foreach (BattleUnit unit in FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude))
        {
            if (unit == user || unit == target)
                continue;

            // Un ataque de area golpea a todo el bando: ninguno se oscurece.
            if (area && target != null && unit.Side == target.Side)
                continue;

            if (UnitDownedVisual.IsActive(unit))
                continue;

            SpriteRenderer sr = unit.GetComponentInChildren<SpriteRenderer>();

            if (sr == null || !sr.enabled || sr.sprite == null)
                continue;

            dimmed.Add(new Dimmed { renderer = sr, original = sr.color, applied = sr.color });
        }
    }

    private void ApplyDim()
    {
        for (int i = dimmed.Count - 1; i >= 0; i--)
        {
            Dimmed d = dimmed[i];

            // Si otro sistema cambio su color (p. ej. se desvanece al morir), se deja de controlar.
            if (d.renderer == null || !Approximately(d.renderer.color, d.applied))
            {
                dimmed.RemoveAt(i);
                continue;
            }

            Color dark = new Color(d.original.r * dimFactor, d.original.g * dimFactor, d.original.b * dimFactor, d.original.a);
            d.applied = Color.Lerp(d.original, dark, dimK);
            d.renderer.color = d.applied;
        }
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f &&
               Mathf.Abs(a.b - b.b) < 0.004f && Mathf.Abs(a.a - b.a) < 0.004f;
    }

    // ------------------------------------------------------------
    // Destello de impacto
    // ------------------------------------------------------------

    private void SpawnFlash(BattleUnit target, Color? tint = null)
    {
        if (flash != null && flash.go != null)
            Destroy(flash.go);

        if (flashSprite == null)
            flashSprite = BuildFlashSprite();

        Bounds b = BoundsOf(target);
        SpriteRenderer targetRenderer = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;

        GameObject go = new GameObject("ImpactFlash");
        go.transform.position = new Vector3(b.center.x, Mathf.Lerp(b.center.y, b.max.y, 0.3f), target != null ? target.transform.position.z : 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = flashSprite;
        sr.sortingLayerID = targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        sr.sortingOrder = FxBase() + 10;
        Color flashColor = tint ?? new Color(1f, 0.94f, 0.78f, 1f);
        sr.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0.9f);

        float size = Mathf.Max(b.size.y, 1f) * 0.75f;
        float spriteSize = flashSprite.bounds.size.x;
        float baseScale = size / spriteSize;
        go.transform.localScale = Vector3.one * baseScale * 0.5f;

        flash = new AnimatableFlash { go = go, renderer = sr, age = 0f, baseScale = baseScale };
    }

    // ------------------------------------------------------------
    // Embestida, reaccion del objetivo, tiron de zoom y tajo
    // ------------------------------------------------------------

    // Suma el desplazamiento deseado a la posicion de la unidad. Si otro sistema movio la unidad desde la ultima vez (p. ej. el
    // cambio de casilla de una habilidad), el desplazamiento anterior ya no esta incluido y se parte de cero.
    private static void ApplyOffset(Transform t, ref Vector3 applied, ref Vector3 last, Vector3 wanted)
    {
        if (t == null)
        {
            applied = Vector3.zero;
            return;
        }

        if (applied != Vector3.zero && (t.position - last).sqrMagnitude > 1e-6f)
            applied = Vector3.zero;

        t.position += wanted - applied;
        applied = wanted;
        last = t.position;
    }

    // Calcula (segun el reloj de la secuencia) la embestida, el retroceso, el tinte y el tiron; LateUpdate los aplica.
    private void UpdateFx()
    {
        float since = impactClock >= 0f ? clock - impactClock : -1f;

        // Embestida: llega al objetivo justo en el impacto, se mantiene durante la pausa y vuelve a su casilla.
        float lunge;

        if (since < 0f)
        {
            lunge = Smooth((clock - (impactAtTime - lungeInSeconds)) / lungeInSeconds);
        }
        else
        {
            float holdEnd = currentStop + holdAfterImpactSeconds * 0.7f;
            lunge = 1f - Smooth((since - holdEnd) / lungeBackSeconds);
        }

        lungeOffset = new Vector3(awayDir * lungeWorld * lunge, 0f, 0f);

        // Retroceso del objetivo: sale disparado al terminar la pausa de impacto y vuelve.
        float recoil = 0f;

        if (hitLanded && since >= 0f)
        {
            float s = since - currentStop;

            if (s > 0f)
                recoil = s < 0.05f ? Smooth(s / 0.05f) : 1f - Smooth((s - 0.05f) / recoilBackSeconds);
        }

        recoilOffset = new Vector3(awayDir * recoilDistance * currentPower * recoilScale * recoil, 0f, 0f);

        // Tiron de zoom: salta en el golpe y se relaja justo despues de la pausa.
        punchK = 0f;

        if (hitLanded && since >= 0f)
            punchK = since < currentStop ? 1f : 1f - Smooth((since - currentStop) / punchSeconds);

        // Tinte de golpe en el objetivo.
        if (tintActive)
        {
            if (targetSr == null || !Approximately(targetSr.color, tintApplied))
            {
                tintActive = false;   // otro sistema tomo el color (se desvanece al morir, cae...): se deja de controlar
            }
            else
            {
                float k = since < hitTintSeconds ? 1f : 1f - Smooth((since - hitTintSeconds) / 0.10f);
                Color hit = new Color(tintOriginal.r * hitTint.r, tintOriginal.g * hitTint.g, tintOriginal.b * hitTint.b, tintOriginal.a);
                tintApplied = Color.Lerp(tintOriginal, hit, k);
                targetSr.color = tintApplied;

                if (k <= 0f)
                {
                    targetSr.color = tintOriginal;
                    tintActive = false;
                }
            }
        }
    }

    private void BeginHitTint()
    {
        if (targetSr == null || !targetSr.enabled || targetSr.sprite == null)
            return;

        tintOriginal = targetSr.color;
        tintApplied = tintOriginal;
        tintActive = true;
    }

    private void SpawnSlash(BattleUnit target, float angle)
    {
        if (slashGo != null)
            Destroy(slashGo);

        if (slashSprite == null)
            slashSprite = BuildSlashSprite();

        Bounds b = BoundsOf(target);
        SpriteRenderer sr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;

        slashGo = new GameObject("ImpactSlash");
        slashGo.transform.position = new Vector3(b.center.x, Mathf.Lerp(b.center.y, b.max.y, 0.25f), target != null ? target.transform.position.z : 0f);
        slashGo.transform.rotation = Quaternion.Euler(0f, 0f, angle * awayDir);

        SpriteRenderer slash = slashGo.AddComponent<SpriteRenderer>();
        slash.sprite = slashSprite;
        slash.sortingLayerID = sr != null ? sr.sortingLayerID : 0;
        slash.sortingOrder = FxBase() + 11;
        slash.color = Color.white;

        slashBase = Mathf.Max(b.size.y, 1f) * 1.25f / slashSprite.bounds.size.x;
        slashAge = 0f;
        slashGo.transform.localScale = Vector3.one * slashBase * 0.3f;
    }

    private void TickSlash()
    {
        if (slashGo == null)
            return;

        slashAge += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(slashAge / slashSeconds);

        if (k >= 1f)
        {
            Destroy(slashGo);
            slashGo = null;
            return;
        }

        // Crece muy rapido (golpe seco) y se desvanece.
        float grow = 1f - (1f - Mathf.Clamp01(k / 0.35f)) * (1f - Mathf.Clamp01(k / 0.35f));
        slashGo.transform.localScale = Vector3.one * slashBase * Mathf.Lerp(0.3f, 1f, grow);
        SpriteRenderer sr = slashGo.GetComponent<SpriteRenderer>();
        sr.color = new Color(1f, 1f, 1f, 1f - Smooth((k - 0.4f) / 0.6f));
    }

    // Franja diagonal afilada: nucleo blanco, borde rojo y puntas finas (sin arte externo).
    private static Sprite BuildSlashSprite()
    {
        const int w = 256;
        const int h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);

            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float taper = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.7f);
                float edge = Mathf.Clamp01(1f - v / taper);
                float alpha = Mathf.Pow(edge, 1.2f);
                float core = Mathf.Pow(edge, 5f);
                Color c = Color.Lerp(new Color(1f, 0.22f, 0.18f), Color.white, core);
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w * 0.5f);
    }

    // ------------------------------------------------------------
    // Aura de proteccion (apoyos): resplandor tras el aliado, dos anillos que se expanden y chispas que suben.
    // ------------------------------------------------------------

    private class AuraPiece
    {
        public GameObject go;
        public SpriteRenderer renderer;
        public float delay;
        public float life;
        public float age;
        public Vector3 start;
        public Vector3 end;
        public Vector3 velocity;
        public float alpha;
        public float fadeIn;
    }

    private readonly List<AuraPiece> auraPieces = new List<AuraPiece>();
    private static Sprite ringSprite;

    private AuraPiece AddAuraPiece(Sprite sprite, Vector3 position, int layerId, int order, float delay, float life,
        Vector3 startScale, Vector3 endScale, Vector3 velocity, float alpha, float fadeIn, Color? color = null)
    {
        Color tint = color ?? auraColor;
        GameObject go = new GameObject("AuraFx");
        go.transform.position = position;
        go.transform.localScale = startScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = layerId;
        sr.sortingOrder = order;
        sr.color = new Color(tint.r, tint.g, tint.b, 0f);

        AuraPiece piece = new AuraPiece
        {
            go = go, renderer = sr, delay = delay, life = life, start = startScale, end = endScale,
            velocity = velocity, alpha = alpha, fadeIn = fadeIn
        };

        auraPieces.Add(piece);
        return piece;
    }

    private void SpawnAura(BattleUnit target, AbilityData ability, bool clearPrevious = true)
    {
        if (clearPrevious)
            ClearAura();

        Color tint = ability != null ? ability.auraColor : auraColor;
        int ringCount = ability != null ? ability.auraRings : 2;
        int sparkCount = ability != null ? ability.auraSparks : 14;

        if (flashSprite == null)
            flashSprite = BuildFlashSprite();

        if (ringSprite == null)
            ringSprite = BuildRingSprite();

        Bounds b = BoundsOf(target);
        SpriteRenderer tsr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        int layer = tsr != null ? tsr.sortingLayerID : 0;
        int order = FxBase();
        float z = target != null ? target.transform.position.z : 0f;
        float h = Mathf.Max(b.size.y, 1f);
        float glowUnit = flashSprite.bounds.size.x;
        float ringUnit = ringSprite.bounds.size.x;
        Vector3 chest = new Vector3(b.center.x, Mathf.Lerp(b.center.y, b.max.y, 0.2f), z);

        // Resplandor detras del aliado (se enciende rapido y se apaga suave).
        AddAuraPiece(flashSprite, new Vector3(b.center.x, b.center.y, z), layer, order - 1, 0f, auraSeconds,
            new Vector3(h * 0.55f / glowUnit, h * 0.95f / glowUnit, 1f), new Vector3(h * 0.75f / glowUnit, h * 1.2f / glowUnit, 1f),
            Vector3.zero, 0.5f, 0.12f, tint);

        // Anillos de potenciacion que se expanden desde el pecho.
        for (int i = 0; i < ringCount; i++)
        {
            AddAuraPiece(ringSprite, chest, layer, order + 6, 0.16f * i, 0.55f,
                Vector3.one * (h * 0.30f / ringUnit), Vector3.one * (h * 0.95f / ringUnit), Vector3.zero, 0.95f, 0.05f, tint);
        }

        // Chispas que suben alrededor del aliado.
        for (int i = 0; i < sparkCount; i++)
        {
            float x = b.center.x + UnityEngine.Random.Range(-0.28f, 0.28f) * h * 0.55f;
            float y = b.min.y + UnityEngine.Random.Range(0.02f, 0.45f) * h;
            float size = UnityEngine.Random.Range(0.035f, 0.075f) * h / glowUnit;
            AddAuraPiece(flashSprite, new Vector3(x, y, z), layer, order + 7, UnityEngine.Random.Range(0f, 0.35f),
                UnityEngine.Random.Range(0.55f, 0.85f), Vector3.one * size, Vector3.one * size * 0.4f,
                new Vector3(0f, UnityEngine.Random.Range(0.45f, 0.9f) * h, 0f), 0.95f, 0.06f, tint);
        }
    }

    // Golpe contundente (embate de escudo): dos anillos de choque y chispas que salen disparadas desde el punto de impacto.
    private void SpawnShockwave(BattleUnit target, float scale = 1f, Color? tint = null, int sparkCount = 16)
    {
        if (flashSprite == null)
            flashSprite = BuildFlashSprite();

        if (ringSprite == null)
            ringSprite = BuildRingSprite();

        Bounds b = BoundsOf(target);
        SpriteRenderer tsr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        int layer = tsr != null ? tsr.sortingLayerID : 0;
        int order = FxBase();
        float z = target != null ? target.transform.position.z : 0f;
        float h = Mathf.Max(b.size.y, 1f);
        float ringUnit = ringSprite.bounds.size.x;
        float glowUnit = flashSprite.bounds.size.x;
        Color warm = tint ?? new Color(1f, 0.93f, 0.72f, 1f);

        // El punto de impacto es el costado del objetivo que mira al atacante.
        Vector3 hit = new Vector3(b.center.x - awayDir * b.extents.x * 0.35f, Mathf.Lerp(b.center.y, b.max.y, 0.1f), z);

        for (int i = 0; i < 2; i++)
        {
            AddAuraPiece(ringSprite, hit, layer, order + 9, 0.05f * i, 0.38f,
                Vector3.one * (h * 0.15f * scale / ringUnit), Vector3.one * (h * (0.85f + 0.3f * i) * scale / ringUnit), Vector3.zero, 1f, 0.02f, warm);
        }

        for (int i = 0; i < sparkCount; i++)
        {
            float a = UnityEngine.Random.Range(-70f, 70f) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(0.9f, 2.0f) * h * scale;
            Vector3 v = new Vector3(Mathf.Cos(a) * awayDir * speed, Mathf.Sin(a) * speed, 0f);
            float size = UnityEngine.Random.Range(0.03f, 0.07f) * h / glowUnit;
            AddAuraPiece(flashSprite, hit, layer, order + 10, 0f, UnityEngine.Random.Range(0.3f, 0.5f),
                Vector3.one * size, Vector3.one * size * 0.3f, v, 1f, 0.01f, warm);
        }
    }

    private void TickAura()
    {
        for (int i = auraPieces.Count - 1; i >= 0; i--)
        {
            AuraPiece p = auraPieces[i];

            if (p.go == null)
            {
                auraPieces.RemoveAt(i);
                continue;
            }

            p.age += Time.unscaledDeltaTime;
            float t = p.age - p.delay;

            if (t < 0f)
                continue;

            float k = Mathf.Clamp01(t / p.life);

            if (k >= 1f)
            {
                Destroy(p.go);
                auraPieces.RemoveAt(i);
                continue;
            }

            p.go.transform.localScale = Vector3.Lerp(p.start, p.end, 1f - (1f - k) * (1f - k));
            p.go.transform.position += p.velocity * Time.unscaledDeltaTime;

            float fadeIn = p.fadeIn > 0f ? Mathf.Clamp01(t / p.fadeIn) : 1f;
            float fadeOut = 1f - Smooth((k - 0.45f) / 0.55f);
            Color c = p.renderer.color;
            c.a = p.alpha * Mathf.Min(fadeIn, fadeOut);
            p.renderer.color = c;
        }
    }

    private void ClearAura()
    {
        foreach (AuraPiece p in auraPieces)
        {
            if (p.go != null)
                Destroy(p.go);
        }

        auraPieces.Clear();
    }

    // Anillo luminoso fino (con un halo suave a los lados).
    private static Sprite BuildRingSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float line = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.045f, 2f));
                float halo = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.14f, 2f)) * 0.35f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(line + halo)));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ------------------------------------------------------------
    // Flechas: vuelan del arquero al objetivo, se clavan (o lo atraviesan) y se desvanecen. Marca: mira que se estampa.
    // ------------------------------------------------------------

    private class ArrowPiece
    {
        public GameObject go;
        public SpriteRenderer arrow;
        public SpriteRenderer trail;
        public Vector3 start;
        public Vector3 end;
        public Vector3 dir;
        public float length;
        public float flight;
        public float age;
        public bool arrived;
        public float arrivedAge;
        public bool pierce;
        public Transform target;
        public Color color;
        public float delay;          // espera antes de empezar a volar (meteoritos escalonados)
        public bool orb;             // orbe / bola de fuego / meteorito: sin asta, desaparece al llegar
        public float orbScale;
        public SpriteRenderer core;  // nucleo claro del orbe
        public Action onArrive;      // al llegar (explosion del meteorito)
    }

    private readonly List<ArrowPiece> arrows = new List<ArrowPiece>();
    private static Sprite arrowSprite;
    private static Sprite crosshairSprite;

    private void SpawnArrow(BattleUnit user, BattleUnit target, AbilityData ability, float flight)
    {
        if (arrowSprite == null)
            arrowSprite = BuildArrowSprite();

        if (slashSprite == null)
            slashSprite = BuildSlashSprite();

        Bounds ab = BoundsOf(user);
        Bounds tb = BoundsOf(target);
        SpriteRenderer usr = user != null ? user.GetComponentInChildren<SpriteRenderer>() : null;
        float z = user != null ? user.transform.position.z : 0f;
        float h = Mathf.Max(ab.size.y, 1f);

        // Sale de la mano del arco (el costado del arquero que mira al objetivo) y llega al pecho del objetivo.
        Vector3 start = new Vector3(awayDir > 0f ? Mathf.Lerp(ab.center.x, ab.max.x, 0.8f) : Mathf.Lerp(ab.center.x, ab.min.x, 0.8f),
            ab.min.y + ab.size.y * 0.64f, z);
        Vector3 end = new Vector3(tb.center.x - awayDir * tb.extents.x * 0.15f, tb.min.y + tb.size.y * 0.58f, z);
        Vector3 dir = (end - start).normalized;
        float length = h * 0.14f;

        GameObject go = new GameObject("ArrowFx");
        go.transform.position = start;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        int layer = usr != null ? usr.sortingLayerID : 0;
        int order = FxBase();

        // Estela (detras de la flecha) y flecha.
        GameObject trailGo = new GameObject("Trail");
        trailGo.transform.SetParent(go.transform, false);
        SpriteRenderer trail = trailGo.AddComponent<SpriteRenderer>();
        trail.sprite = slashSprite;
        trail.sortingLayerID = layer;
        trail.sortingOrder = order + 13;

        GameObject arrowGo = new GameObject("Arrow");
        arrowGo.transform.SetParent(go.transform, false);
        SpriteRenderer sr = arrowGo.AddComponent<SpriteRenderer>();
        sr.sprite = arrowSprite;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order + 14;

        float arrowScale = length / arrowSprite.bounds.size.x;
        arrowGo.transform.localScale = Vector3.one * arrowScale;

        // Orbe o bola de fuego: esfera luminosa con nucleo claro (en lugar de la flecha).
        bool orb = ability.projectileKind != ProjectileKind.Arrow;
        float orbScale = 0f;
        SpriteRenderer core = null;

        if (orb)
        {
            if (flashSprite == null)
                flashSprite = BuildFlashSprite();

            orbScale = h * (ability.projectileKind == ProjectileKind.Fireball ? 0.13f : 0.10f) / flashSprite.bounds.size.x;
            sr.sprite = flashSprite;
            arrowGo.transform.localScale = Vector3.one * orbScale;
            core = AddOrbCore(arrowGo.transform, layer, order + 15);
            length = h * 0.16f;
        }

        ArrowPiece piece = new ArrowPiece
        {
            go = go, arrow = sr, trail = trail, start = start, end = end, dir = dir, length = length,
            flight = Mathf.Max(0.04f, flight), age = 0f, pierce = ability.impactEffect == ImpactEffect.Pierce,
            target = target != null ? target.transform : null, color = ability.projectileColor,
            orb = orb, orbScale = orbScale, core = core
        };

        arrows.Add(piece);
        UpdateArrow(piece, 0f);
    }

    // Nucleo blanco pequeno que se dibuja sobre el resplandor del orbe.
    private SpriteRenderer AddOrbCore(Transform parent, int layer, int order)
    {
        GameObject go = new GameObject("Core");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 0.45f;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = flashSprite;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        sr.color = new Color(1f, 1f, 0.92f, 1f);
        return sr;
    }

    // Lluvia de meteoritos: uno por enemigo (mas los extra de la habilidad) cae del cielo escalonado; al llegar, cada uno explota.
    private void SpawnMeteors(List<BattleUnit> victims, BattleUnit anchor, AbilityData ability, float fall)
    {
        if (flashSprite == null)
            flashSprite = BuildFlashSprite();

        if (slashSprite == null)
            slashSprite = BuildSlashSprite();

        if (victims.Count == 0 && anchor != null)
            victims.Add(anchor);

        SpriteRenderer asr = anchor != null ? anchor.GetComponentInChildren<SpriteRenderer>() : null;
        int layer = asr != null ? asr.sortingLayerID : 0;
        int order = FxBase();
        float z = anchor != null ? anchor.transform.position.z : 0f;
        float skyY = basePos.y + baseSize + 2.5f;
        Color color = ability.projectileColor;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float groundY = 0f;
        int index = 0;

        foreach (BattleUnit victim in victims)
        {
            Bounds b = BoundsOf(victim);
            minX = Mathf.Min(minX, b.min.x);
            maxX = Mathf.Max(maxX, b.max.x);
            groundY += b.min.y;
            Vector3 hit = new Vector3(b.center.x, b.min.y + b.size.y * 0.25f, z);
            AddMeteor(hit, skyY, layer, order, fall, 0.07f * index, victim, ability, color, b.size.y);
            index++;
        }

        groundY /= Mathf.Max(1, victims.Count);

        // Meteoritos extra entre los enemigos (solo visuales, sin objetivo propio).
        for (int i = 0; i < ability.extraProjectiles; i++)
        {
            float x = UnityEngine.Random.Range(minX, maxX);
            Vector3 hit = new Vector3(x, groundY + UnityEngine.Random.Range(0.3f, 1.6f), z);
            AddMeteor(hit, skyY, layer, order, fall * UnityEngine.Random.Range(0.9f, 1.15f), UnityEngine.Random.Range(0.02f, 0.3f), null, ability, color, 6f);
        }
    }

    private void AddMeteor(Vector3 hit, float skyY, int layer, int order, float fall, float delay, BattleUnit victim, AbilityData ability, Color color, float h)
    {
        float drop = skyY - hit.y;
        Vector3 start = new Vector3(hit.x - awayDir * drop * 0.45f, skyY, hit.z);
        Vector3 dir = (hit - start).normalized;

        GameObject go = new GameObject("MeteorFx");
        go.transform.position = start;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        GameObject trailGo = new GameObject("Trail");
        trailGo.transform.SetParent(go.transform, false);
        SpriteRenderer trail = trailGo.AddComponent<SpriteRenderer>();
        trail.sprite = slashSprite;
        trail.sortingLayerID = layer;
        trail.sortingOrder = order + 13;

        GameObject rockGo = new GameObject("Rock");
        rockGo.transform.SetParent(go.transform, false);
        SpriteRenderer rock = rockGo.AddComponent<SpriteRenderer>();
        rock.sprite = flashSprite;
        rock.sortingLayerID = layer;
        rock.sortingOrder = order + 14;
        float size = Mathf.Max(h, 4f) * 0.2f / flashSprite.bounds.size.x;
        rockGo.transform.localScale = Vector3.one * size;

        SpriteRenderer core = AddOrbCore(rockGo.transform, layer, order + 15);
        Vector3 impactPoint = hit;
        BattleUnit target = victim;

        ArrowPiece piece = new ArrowPiece
        {
            go = go, arrow = rock, trail = trail, start = start, end = hit, dir = dir, length = Mathf.Max(h, 4f) * 0.24f,
            flight = Mathf.Max(0.08f, fall), age = 0f, pierce = false, target = null, color = color,
            delay = delay, orb = true, orbScale = size, core = core,
            onArrive = () => SpawnBlast(impactPoint, target, ability, layer, order)
        };

        arrows.Add(piece);
        UpdateArrow(piece, 0f);
    }

    // Explosion de un meteorito: destello, onda de choque plana en el suelo, columna de llamas y brasas.
    private void SpawnBlast(Vector3 pos, BattleUnit victim, AbilityData ability, int layer, int order)
    {
        if (flashSprite == null)
            flashSprite = BuildFlashSprite();

        if (ringSprite == null)
            ringSprite = BuildRingSprite();

        float h = victim != null ? Mathf.Max(BoundsOf(victim).size.y, 4f) : 6f;
        Color color = ability.projectileColor;
        Color hot = Color.Lerp(color, new Color(1f, 0.95f, 0.6f, 1f), 0.5f);
        float glowUnit = flashSprite.bounds.size.x;
        float ringUnit = ringSprite.bounds.size.x;

        AddAuraPiece(flashSprite, pos, layer, order + 12, 0f, 0.35f,
            Vector3.one * (h * 0.35f / glowUnit), Vector3.one * (h * 1.0f / glowUnit), Vector3.zero, 0.95f, 0.02f, hot);
        AddAuraPiece(ringSprite, pos, layer, order + 11, 0f, 0.45f,
            new Vector3(h * 0.2f / ringUnit, h * 0.08f / ringUnit, 1f), new Vector3(h * 1.1f / ringUnit, h * 0.4f / ringUnit, 1f), Vector3.zero, 0.9f, 0.02f, color);
        AddAuraPiece(flashSprite, pos + Vector3.up * (h * 0.25f), layer, order + 10, 0.03f, 0.5f,
            new Vector3(h * 0.18f / glowUnit, h * 0.3f / glowUnit, 1f), new Vector3(h * 0.4f / glowUnit, h * 1.1f / glowUnit, 1f),
            new Vector3(0f, h * 0.35f, 0f), 0.8f, 0.03f, color);

        for (int i = 0; i < 10; i++)
        {
            float a = UnityEngine.Random.Range(35f, 145f) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(0.7f, 1.7f) * h;
            float size = UnityEngine.Random.Range(0.025f, 0.06f) * h / glowUnit;
            AddAuraPiece(flashSprite, pos, layer, order + 13, 0f, UnityEngine.Random.Range(0.35f, 0.7f),
                Vector3.one * size, Vector3.one * size * 0.3f, new Vector3(Mathf.Cos(a) * speed, Mathf.Sin(a) * speed, 0f), 1f, 0.01f, hot);
        }
    }

    // Brasas que suben desde el objetivo (llamas).
    private void SpawnEmbers(BattleUnit target, Color color, int count)
    {
        if (flashSprite == null)
            flashSprite = BuildFlashSprite();

        Bounds b = BoundsOf(target);
        SpriteRenderer tsr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        int layer = tsr != null ? tsr.sortingLayerID : 0;
        int order = FxBase();
        float z = target != null ? target.transform.position.z : 0f;
        float h = Mathf.Max(b.size.y, 1f);
        float glowUnit = flashSprite.bounds.size.x;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = new Vector3(b.center.x + UnityEngine.Random.Range(-0.2f, 0.2f) * h, b.min.y + UnityEngine.Random.Range(0.15f, 0.7f) * h, z);
            float size = UnityEngine.Random.Range(0.03f, 0.07f) * h / glowUnit;
            AddAuraPiece(flashSprite, p, layer, order + 12, UnityEngine.Random.Range(0f, 0.12f), UnityEngine.Random.Range(0.5f, 0.9f),
                Vector3.one * size, Vector3.one * size * 0.25f, new Vector3(UnityEngine.Random.Range(-0.1f, 0.1f) * h, UnityEngine.Random.Range(0.4f, 0.9f) * h, 0f), 1f, 0.02f, color);
        }
    }

    // Sello magico en el suelo bajo el objetivo (anuncia la erupcion): circulo con runas que gira y se intensifica.
    private void SpawnRune(BattleUnit target, Color color)
    {
        if (runeSprite == null)
            runeSprite = BuildRuneSprite();

        Bounds b = BoundsOf(target);
        SpriteRenderer tsr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        int layer = tsr != null ? tsr.sortingLayerID : 0;
        int order = FxBase();
        float z = target != null ? target.transform.position.z : 0f;
        float h = Mathf.Max(b.size.y, 1f);
        Vector3 feet = new Vector3(b.center.x, b.min.y + h * 0.02f, z);
        float unit = runeSprite.bounds.size.x;

        AddAuraPiece(runeSprite, feet, layer, order - 1, 0f, 0.8f,
            new Vector3(h * 0.35f / unit, h * 0.35f * 0.3f / unit, 1f), new Vector3(h * 0.75f / unit, h * 0.75f * 0.3f / unit, 1f),
            Vector3.zero, 0.95f, 0.12f, color);
    }

    // Erupcion: columna de fuego que sube del sello, onda de choque y chispas (mayor si el objetivo ardia).
    private void SpawnEruption(BattleUnit target, Color color, bool bigger)
    {
        if (flashSprite == null)
            flashSprite = BuildFlashSprite();

        if (ringSprite == null)
            ringSprite = BuildRingSprite();

        Bounds b = BoundsOf(target);
        SpriteRenderer tsr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        int layer = tsr != null ? tsr.sortingLayerID : 0;
        int order = FxBase();
        float z = target != null ? target.transform.position.z : 0f;
        float h = Mathf.Max(b.size.y, 1f);
        float k = bigger ? 1.35f : 1f;
        Vector3 feet = new Vector3(b.center.x, b.min.y + h * 0.02f, z);
        float glowUnit = flashSprite.bounds.size.x;
        float ringUnit = ringSprite.bounds.size.x;
        Color hot = Color.Lerp(color, new Color(1f, 0.95f, 0.6f, 1f), 0.55f);

        // Columna: se estira hacia arriba muy rapido y se desvanece.
        AddAuraPiece(flashSprite, feet + Vector3.up * (h * 0.45f * k), layer, order + 10, 0f, 0.55f,
            new Vector3(h * 0.25f * k / glowUnit, h * 0.3f / glowUnit, 1f), new Vector3(h * 0.42f * k / glowUnit, h * 1.5f * k / glowUnit, 1f),
            Vector3.zero, 0.9f, 0.04f, color);
        AddAuraPiece(flashSprite, feet + Vector3.up * (h * 0.4f * k), layer, order + 11, 0f, 0.4f,
            new Vector3(h * 0.12f * k / glowUnit, h * 0.3f / glowUnit, 1f), new Vector3(h * 0.22f * k / glowUnit, h * 1.2f * k / glowUnit, 1f),
            Vector3.zero, 1f, 0.03f, hot);

        for (int i = 0; i < 2; i++)
        {
            AddAuraPiece(ringSprite, feet, layer, order + 9, 0.06f * i, 0.45f,
                new Vector3(h * 0.2f / ringUnit, h * 0.08f / ringUnit, 1f), new Vector3(h * (0.9f + 0.3f * i) * k / ringUnit, h * 0.34f * k / ringUnit, 1f),
                Vector3.zero, 0.95f, 0.02f, color);
        }

        int sparks = bigger ? 22 : 14;

        for (int i = 0; i < sparks; i++)
        {
            float x = UnityEngine.Random.Range(-0.18f, 0.18f) * h;
            float size = UnityEngine.Random.Range(0.025f, 0.06f) * h / glowUnit;
            AddAuraPiece(flashSprite, feet + new Vector3(x, 0f, 0f), layer, order + 12, UnityEngine.Random.Range(0f, 0.1f), UnityEngine.Random.Range(0.45f, 0.85f),
                Vector3.one * size, Vector3.one * size * 0.25f, new Vector3(x * 1.5f, UnityEngine.Random.Range(0.8f, 1.8f) * h, 0f), 1f, 0.01f, hot);
        }
    }

    private static Sprite runeSprite;

    // Circulo magico: dos aros y marcas de runa alrededor.
    private static Sprite BuildRuneSprite()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float outer = Mathf.Exp(-Mathf.Pow((d - 0.94f) / 0.025f, 2f));
                float inner = Mathf.Exp(-Mathf.Pow((d - 0.62f) / 0.02f, 2f));
                // Marcas de runa entre los dos aros (8 trazos).
                float tick = Mathf.Pow(Mathf.Abs(Mathf.Cos(4f * ang)), 18f);
                float runes = d > 0.68f && d < 0.88f ? tick * 0.9f : 0f;
                // Triangulo central tenue.
                float tri = d < 0.55f ? Mathf.Exp(-Mathf.Pow((Mathf.Abs(Mathf.Cos(1.5f * ang)) * d - 0.28f) / 0.02f, 2f)) * 0.7f : 0f;
                float glow = Mathf.Exp(-Mathf.Pow((d - 0.94f) / 0.1f, 2f)) * 0.25f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(outer + inner + runes + tri + glow)));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
    }

    private void TickArrows()
    {
        for (int i = arrows.Count - 1; i >= 0; i--)
        {
            ArrowPiece p = arrows[i];

            if (p.go == null)
            {
                arrows.RemoveAt(i);
                continue;
            }

            if (UpdateArrow(p, Time.unscaledDeltaTime))
            {
                Destroy(p.go);
                arrows.RemoveAt(i);
            }
        }
    }

    // Avanza la flecha; devuelve true cuando ya debe destruirse.
    private bool UpdateArrow(ArrowPiece p, float dt)
    {
        p.age += dt;
        float t = p.age - p.delay;

        // Meteoritos escalonados: invisibles hasta que les toca caer.
        if (t < 0f)
        {
            p.arrow.color = new Color(1f, 1f, 1f, 0f);
            p.trail.color = new Color(1f, 1f, 1f, 0f);

            if (p.core != null)
                p.core.color = new Color(1f, 1f, 1f, 0f);

            return false;
        }

        if (!p.arrived)
        {
            float k = Mathf.Clamp01(t / p.flight);
            float eased = Mathf.Pow(k, p.orb ? 1.6f : 1.35f);   // sale rapido y acelera hacia el objetivo
            p.go.transform.position = Vector3.Lerp(p.start, p.end, eased);

            // Estela larga y brillante mientras vuela.
            float trailLen = p.orb ? p.length * Mathf.Lerp(2.0f, 4.5f, k) : Mathf.Lerp(p.length * 1.2f, p.length * 3.2f, Mathf.Sin(k * Mathf.PI));
            SetTrail(p, trailLen, 0.75f);

            if (p.orb)
            {
                // Esfera de energia que parpadea; el nucleo es casi blanco.
                float flicker = 1f + 0.14f * Mathf.Sin(t * 45f);
                p.arrow.transform.localScale = Vector3.one * p.orbScale * flicker;
                p.arrow.color = new Color(p.color.r, p.color.g, p.color.b, 1f);

                if (p.core != null)
                    p.core.color = new Color(1f, 1f, 0.92f, 1f);
            }
            else
            {
                p.arrow.color = Color.white;
            }

            if (k >= 1f)
            {
                p.arrived = true;
                p.arrivedAge = 0f;
                p.onArrive?.Invoke();

                // Los orbes y meteoritos estallan al llegar (su efecto lo pone el impacto): desaparecen.
                if (p.orb)
                    return true;

                // Se clava: se queda pegada al objetivo y lo acompana en su retroceso (salvo la perforante, que lo atraviesa).
                if (!p.pierce && p.target != null)
                    p.go.transform.SetParent(p.target, true);
            }

            return false;
        }

        p.arrivedAge += dt;

        if (p.pierce)
        {
            // Atraviesa al objetivo y sigue de largo mientras se desvanece.
            p.go.transform.position += p.dir * (p.length * 7f * dt);
            float a = 1f - Mathf.Clamp01(p.arrivedAge / 0.28f);
            p.arrow.color = new Color(1f, 1f, 1f, a);
            SetTrail(p, p.length * 3.4f, 0.8f * a);
            return p.arrivedAge >= 0.28f;
        }

        // Clavada: temblor breve al impactar y se desvanece despues de un rato visible.
        float shiver = p.arrivedAge < 0.18f ? Mathf.Sin(p.arrivedAge * 90f) * 0.02f * p.length * (1f - p.arrivedAge / 0.18f) : 0f;
        p.arrow.transform.localPosition = new Vector3(0f, shiver, 0f);
        float fade = 1f - Smooth((p.arrivedAge - 0.55f) / 0.45f);
        p.arrow.color = new Color(1f, 1f, 1f, fade);
        SetTrail(p, p.length * 1.2f, 0.5f * Mathf.Clamp01(1f - p.arrivedAge / 0.25f) * fade);
        return p.arrivedAge >= 1.0f;
    }

    // La estela es un trazo (tajo) a lo largo del eje de la flecha, por detras de ella, con el color del proyectil.
    private static void SetTrail(ArrowPiece p, float length, float alpha)
    {
        float unit = slashSprite != null ? slashSprite.bounds.size.x : 2f;
        float thickness = p.orb ? 0.34f : 0.05f;   // la estela de un orbe es ancha; la de una flecha, un hilo
        p.trail.transform.localScale = new Vector3(length / unit, p.length * thickness / (slashSprite != null ? slashSprite.bounds.size.y : 0.25f), 1f);
        p.trail.transform.localPosition = new Vector3(-length * 0.5f, 0f, 0f);
        p.trail.color = new Color(p.color.r, p.color.g, p.color.b, Mathf.Clamp01(alpha));
    }

    private void ClearArrows()
    {
        foreach (ArrowPiece p in arrows)
        {
            if (p.go != null)
                Destroy(p.go);
        }

        arrows.Clear();
    }

    // Mira roja que se estampa sobre el objetivo (marca del cazador): empieza grande y se cierra sobre el cuerpo.
    private void SpawnMarkStamp(BattleUnit target, Color color)
    {
        if (flashSprite == null)
            flashSprite = BuildFlashSprite();

        if (ringSprite == null)
            ringSprite = BuildRingSprite();

        if (crosshairSprite == null)
            crosshairSprite = BuildCrosshairSprite();

        Bounds b = BoundsOf(target);
        SpriteRenderer tsr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        int layer = tsr != null ? tsr.sortingLayerID : 0;
        int order = FxBase();
        float z = target != null ? target.transform.position.z : 0f;
        float h = Mathf.Max(b.size.y, 1f);
        Vector3 center = new Vector3(b.center.x, b.min.y + b.size.y * 0.55f, z);
        float unit = crosshairSprite.bounds.size.x;
        Color red = new Color(1f, 0.22f, 0.18f, 1f);

        AddAuraPiece(crosshairSprite, center, layer, order + 15, 0f, 0.95f,
            Vector3.one * (h * 0.9f / unit), Vector3.one * (h * 0.34f / unit), Vector3.zero, 1f, 0.04f, red);
        AddAuraPiece(ringSprite, center, layer, order + 14, 0.05f, 0.45f,
            Vector3.one * (h * 0.2f / ringSprite.bounds.size.x), Vector3.one * (h * 0.8f / ringSprite.bounds.size.x), Vector3.zero, 0.9f, 0.03f, red);
        AddAuraPiece(flashSprite, center, layer, order + 13, 0f, 0.35f,
            Vector3.one * (h * 0.3f / flashSprite.bounds.size.x), Vector3.one * (h * 0.7f / flashSprite.bounds.size.x), Vector3.zero, 0.8f, 0.03f, red);
    }

    // Flecha de perfil: asta con punta y plumas, apuntando a +X.
    private static Sprite BuildArrowSprite()
    {
        const int w = 128;
        const int hgt = 16;
        Texture2D tex = new Texture2D(w, hgt, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color shaft = new Color(0.72f, 0.55f, 0.35f, 1f);
        Color head = new Color(0.86f, 0.9f, 0.95f, 1f);
        Color feather = new Color(0.95f, 0.95f, 0.9f, 1f);

        for (int y = 0; y < hgt; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float v = (y + 0.5f - hgt * 0.5f) / (hgt * 0.5f);   // -1..1
                Color c = clear;

                // Asta fina.
                if (Mathf.Abs(v) < 0.14f && x > 10 && x < w - 20)
                    c = shaft;

                // Punta triangular.
                if (x >= w - 26 && Mathf.Abs(v) < (w - x) / 26f * 0.62f)
                    c = head;

                // Plumas (dos aletas inclinadas en la cola).
                if (x < 28 && Mathf.Abs(Mathf.Abs(v) - (28 - x) / 28f * 0.8f) < 0.14f)
                    c = feather;

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, hgt), new Vector2(0.5f, 0.5f), w * 0.5f);
    }

    // Mira (circulo con cruz).
    private static Sprite BuildCrosshairSprite()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.62f) / 0.05f, 2f));
                float cross = 0f;

                // Cruz con hueco en el centro y brazos que sobresalen del circulo.
                bool onAxis = Mathf.Abs(dx) < 0.045f || Mathf.Abs(dy) < 0.045f;

                if (onAxis && d > 0.18f && d < 0.95f)
                    cross = 1f;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ring + cross)));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void TickFlash()
    {
        TickArrows();
        TickAura();
        TickSlash();

        if (flash == null || flash.go == null)
            return;

        flash.age += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(flash.age / flashSeconds);

        if (k >= 1f)
        {
            Destroy(flash.go);
            flash = null;
            return;
        }

        flash.go.transform.localScale = Vector3.one * flash.baseScale * Mathf.Lerp(0.5f, 1.15f, 1f - (1f - k) * (1f - k));
        Color c = flash.renderer.color;
        c.a = 0.9f * (1f - k);
        flash.renderer.color = c;
    }

    private static Sprite BuildFlashSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ------------------------------------------------------------
    // Nombre de la habilidad (capa propia: no toca el HUD)
    // ------------------------------------------------------------

    private void ShowBanner(string text)
    {
        if (bannerSeconds <= 0f || string.IsNullOrEmpty(text))
            return;

        EnsureBanner();
        bannerText.text = text;

        if (bannerRoutine != null)
            StopCoroutine(bannerRoutine);

        bannerRoutine = StartCoroutine(BannerRoutine());
    }

    private IEnumerator BannerRoutine()
    {
        bannerRoot.SetActive(true);
        float fadeIn = 0.08f;
        float fadeOut = 0.22f;
        float t = 0f;

        while (t < bannerSeconds)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Min(Mathf.Clamp01(t / fadeIn), Mathf.Clamp01((bannerSeconds - t) / fadeOut));
            bannerGroup.alpha = a;
            yield return null;
        }

        bannerGroup.alpha = 0f;
        bannerRoot.SetActive(false);
        bannerRoutine = null;
    }

    private void EnsureBanner()
    {
        if (bannerRoot != null)
            return;

        bannerRoot = new GameObject("AttackBanner", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        bannerRoot.transform.SetParent(transform, false);

        Canvas canvas = bannerRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = bannerRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        bannerGroup = bannerRoot.GetComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;

        GameObject textGo = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(Outline));
        textGo.transform.SetParent(bannerRoot.transform, false);
        RectTransform rect = (RectTransform)textGo.transform;
        rect.anchorMin = new Vector2(0.52f, 0.885f);
        rect.anchorMax = new Vector2(0.97f, 0.96f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        bannerText = textGo.GetComponent<Text>();
        bannerText.alignment = TextAnchor.MiddleRight;
        bannerText.fontSize = 46;
        bannerText.color = new Color(0.96f, 0.90f, 0.76f, 1f);
        bannerText.raycastTarget = false;
        bannerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        bannerText.verticalOverflow = VerticalWrapMode.Overflow;
        GameFonts.Apply(bannerText);

        Outline outline = textGo.GetComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.02f, 0.02f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        bannerRoot.SetActive(false);
    }
}
