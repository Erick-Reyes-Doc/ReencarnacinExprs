using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla "Victory": se muestra tras ganar un combate con los datos de VictorySummary (XP y niveles por heroe, Coronas y
// reliquia). Solo PRESENTA: no entrega recompensas, asi que se puede abrir o cerrar sin duplicar nada. Entrada con fundido,
// titulo que aparece, barras de XP que se llenan (con realce si el heroe sube de nivel) y realce discreto de la reliquia.
// Sin resumen pendiente (p. ej. al abrir la escena sola) muestra el estado actual del equipo sin recompensas.
public class VictoryController : MonoBehaviour
{
    [System.Serializable]
    public class HeroRow
    {
        public string heroId;
        public CanvasGroup group;
        public TMP_Text levelText;
        public TMP_Text xpText;
        public TMP_Text gainText;
        public RectTransform barFill;
        public TMP_Text levelUpTag;
        public Image highlight;
    }

    [Header("Fondo y pantalla")]
    [SerializeField] private Image background;
    [SerializeField] private Color fallbackBackground = new Color(0.05f, 0.07f, 0.11f, 1f);
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private CanvasGroup progressHeader;
    [SerializeField] private CanvasGroup rewardsHeader;
    [SerializeField] private CanvasGroup rewardsBox;
    [SerializeField] private Image fade;

    [Header("Equipo")]
    [SerializeField] private List<HeroRow> rows = new List<HeroRow>();

    [Header("Recompensas")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private RectTransform relicGroup;
    [SerializeField] private Image relicIcon;
    [SerializeField] private TMP_Text relicName;
    [SerializeField] private Image relicChip;
    [SerializeField] private TMP_Text relicRarity;
    [SerializeField] private Image relicGlow;
    [SerializeField] private TMP_Text noRelicText;

    [Header("Continuar")]
    [SerializeField] private CanvasGroup continueGroup;
    [SerializeField] private Button continueButton;
    [SerializeField] private string nextScene = "Map";
    [Tooltip("Escena tras vencer al jefe final (epilogo y creditos). Vacio = vuelve al mapa como siempre.")]
    [SerializeField] private string endingScene = "FinalDialogo";

    [Header("Sonidos")]
    [SerializeField] private AudioClip victoryFanfare;
    [SerializeField] private AudioClip xpFillLoop;
    [SerializeField] private AudioClip levelUpSound;
    [SerializeField] private AudioClip relicSound;
    [SerializeField] private AudioClip relicRareSound;
    [SerializeField, Range(0f, 1f)] private float fanfareVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float xpLoopVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float levelUpVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float relicVolume = 0.6f;

    private AudioSource sfxSource;
    private AudioSource loopSource;
    private int fillsRunning;
    private float lastLevelUpSoundTime = -10f;

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        sfxSource.PlayOneShot(clip, volume);
    }

    // Bucle de llenado de XP: suena mientras haya alguna barra llenandose y se apaga con un fundido corto.
    private void BeginFillSound()
    {
        fillsRunning++;

        if (xpFillLoop == null || fillsRunning != 1)
            return;

        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.spatialBlend = 0f;
            loopSource.loop = true;
            loopSource.clip = xpFillLoop;
        }

        loopSource.volume = xpLoopVolume;

        if (!loopSource.isPlaying)
            loopSource.Play();
    }

    private void EndFillSound()
    {
        fillsRunning = Mathf.Max(0, fillsRunning - 1);

        if (fillsRunning == 0 && loopSource != null && loopSource.isPlaying)
            StartCoroutine(FadeOutLoop());
    }

    private IEnumerator FadeOutLoop()
    {
        AudioSource source = loopSource;
        float start = source.volume;

        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            if (fillsRunning > 0 || source == null)
                yield break;

            source.volume = Mathf.Lerp(start, 0f, t / 0.25f);
            yield return null;
        }

        if (fillsRunning == 0 && source != null)
            source.Stop();
    }

    private static readonly Color Gold = new Color(0.96f, 0.86f, 0.58f, 1f);
    private static readonly Color Ivory = new Color(0.93f, 0.90f, 0.82f, 1f);

    private VictorySummary summary;
    private bool leaving;
    private bool animationsDone;

    private void Start()
    {
        Time.timeScale = 1f;
        summary = VictorySummary.Current;

        Populate();

        if (continueButton != null)
            continueButton.onClick.AddListener(Continue);

        StartCoroutine(Intro());
    }

    private void Update()
    {
        // Enter / Espacio tambien continuan (cuando la animacion ya termino, para no saltarla por accidente).
        Keyboard keyboard = Keyboard.current;

        if (animationsDone && !leaving && keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            Continue();
    }

    // ------------------------------------------------------------
    // Datos
    // ------------------------------------------------------------

    // Tarjeta con la descripcion de la reliquia al pasar el cursor por encima (se construye en tiempo de ejecucion).
    private RectTransform tooltipRoot;

    private void SetupRelicTooltip(RelicData relic)
    {
        if (relicGroup == null || relicName == null)
            return;

        // Area sensible: imagen transparente que recibe el puntero.
        Image hit = relicGroup.GetComponent<Image>();

        if (hit == null)
        {
            hit = relicGroup.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            hit.transform.SetAsFirstSibling();
        }

        RelicHover hover = relicGroup.GetComponent<RelicHover>();

        if (hover == null)
            hover = relicGroup.gameObject.AddComponent<RelicHover>();

        hover.owner = this;

        // Mismo diseño que la tarjeta de la tienda: borde dorado fino (1,5 px), fondo azul casi negro, titulo 25 en negrita y cuerpo 21.
        if (tooltipRoot == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            GameObject root = new GameObject("TarjetaReliquia", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            tooltipRoot = (RectTransform)root.transform;
            tooltipRoot.anchorMin = tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRoot.pivot = new Vector2(0.5f, 0f);
            Image border = root.GetComponent<Image>();
            border.color = new Color(0.831f, 0.686f, 0.416f, 1f);
            border.raycastTarget = false;
            root.GetComponent<CanvasGroup>().blocksRaycasts = false;

            GameObject back = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(tooltipRoot, false);
            RectTransform br = (RectTransform)back.transform;
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.one;
            br.offsetMin = new Vector2(1.5f, 1.5f);
            br.offsetMax = new Vector2(-1.5f, -1.5f);
            Image bi = back.GetComponent<Image>();
            bi.color = new Color(0.063f, 0.082f, 0.118f, 0.98f);
            bi.raycastTarget = false;

            tipTitle = MakeTipText("Titulo", relicName.font, 25f);
            tipBody = MakeTipText("Texto", noRelicText != null ? noRelicText.font : relicName.font, 21f);
            root.SetActive(false);
        }

        // Contenido: nombre y rareza en el color de la rareza; efecto, ventajas (verde) y desventajas (rojo).
        string rarityHex = ColorUtility.ToHtmlStringRGB(RarityColor(relic));
        string title = "<color=#" + rarityHex + ">" + relic.displayName + "</color>";
        string body = "<color=#" + rarityHex + ">" + (relic.IsExclusive ? "Exclusiva" : RelicData.RarityName(relic.rarity)) + "</color>";

        if (relic.uniqueOwnership)
            body += "  ·  Única";

        if (!string.IsNullOrEmpty(relic.description))
            body += "\n" + relic.description;

        foreach (RelicModifier m in relic.bonuses)
            body += "\n<color=#7FD08F>+" + m.amount + RelicData.Suffix(m.stat) + " " + RelicData.StatName(m.stat) + "</color>";

        foreach (RelicModifier m in relic.penalties)
            body += "\n<color=#E56B5E>−" + m.amount + RelicData.Suffix(m.stat) + " " + RelicData.StatName(m.stat) + "</color>";

        const float width = 420f, pad = 14f;
        tipTitle.text = title;
        tipBody.text = body;
        float titleH = tipTitle.GetPreferredValues(title, width - 2f * pad, 0f).y;
        float bodyH = tipBody.GetPreferredValues(body, width - 2f * pad, 0f).y;
        tipTitle.rectTransform.sizeDelta = new Vector2(width - 2f * pad, titleH);
        tipTitle.rectTransform.anchoredPosition = new Vector2(pad, -10f);
        tipBody.rectTransform.sizeDelta = new Vector2(width - 2f * pad, bodyH);
        tipBody.rectTransform.anchoredPosition = new Vector2(pad, -10f - titleH - 4f);
        tooltipRoot.sizeDelta = new Vector2(width, titleH + bodyH + 28f);
    }

    private TextMeshProUGUI tipTitle, tipBody;

    private TextMeshProUGUI MakeTipText(string name, TMP_FontAsset font, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(tooltipRoot, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = Color.white;
        t.richText = true;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        return t;
    }

    internal void ShowRelicTooltip(bool show)
    {
        if (tooltipRoot == null)
            return;

        if (show)
        {
            tooltipRoot.SetAsLastSibling();
            tooltipRoot.position = relicGroup.position + new Vector3(0f, relicGroup.rect.height * 0.5f * relicGroup.lossyScale.y + 12f, 0f);
        }

        tooltipRoot.gameObject.SetActive(show);
    }

    private class RelicHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public VictoryController owner;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (owner != null) owner.ShowRelicTooltip(true); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (owner != null) owner.ShowRelicTooltip(false); }
        private void OnDisable() { if (owner != null) owner.ShowRelicTooltip(false); }
    }

    private void Populate()
    {
        // Fondo: el del encuentro ganado (discreto: la oscuridad la pone el velo de la escena).
        Sprite sprite = summary != null && summary.encounter != null ? summary.encounter.background : null;

        if (background != null)
        {
            background.sprite = sprite;
            background.color = sprite != null ? Color.white : fallbackBackground;
        }

        // Coronas.
        int coins = summary != null ? summary.coins : 0;
        SetText(coinsText, "+" + coins + " Coronas");

        if (coinsText != null)
            coinsText.color = coins > 0 ? Ivory : new Color(Ivory.r, Ivory.g, Ivory.b, 0.6f);

        // Reliquia (o "Sin reliquia", conservando la distribucion).
        RelicData relic = summary != null ? summary.relic : null;

        if (relicGroup != null)
            relicGroup.gameObject.SetActive(relic != null);

        if (noRelicText != null)
            noRelicText.gameObject.SetActive(relic == null);

        if (relic != null)
            SetupRelicTooltip(relic);

        if (relic != null)
        {
            if (relicIcon != null)
            {
                relicIcon.sprite = relic.icon;
                relicIcon.enabled = relic.icon != null;
            }

            Color rarity = RarityColor(relic);
            SetText(relicName, relic.displayName);
            SetText(relicRarity, relic.IsExclusive ? "Exclusiva" : RelicData.RarityName(relic.rarity));

            if (relicChip != null)
                relicChip.color = new Color(rarity.r * 0.35f, rarity.g * 0.35f, rarity.b * 0.35f, 0.95f);

            if (relicRarity != null)
                relicRarity.color = relic.IsExclusive ? Gold : Color.Lerp(rarity, Color.white, 0.25f);

            if (relicName != null)
                relicName.color = relic.IsExclusive ? Gold : Ivory;

            if (relicGlow != null)
                relicGlow.color = new Color(rarity.r, rarity.g, rarity.b, 0f);
        }

        // Filas del equipo con su estado de partida (antes de ganar la XP de este combate).
        foreach (HeroRow row in rows)
        {
            VictorySummary.HeroEntry entry = summary != null ? summary.Find(row.heroId) : null;
            int level, xp, required;

            if (entry != null)
            {
                level = entry.levelBefore;
                xp = entry.xpBefore;
                required = entry.Required(level);
                SetText(row.gainText, "+" + entry.xpGained + " XP");
            }
            else
            {
                // Sin resumen: estado actual del equipo guardado en la sesion, sin XP ganada.
                level = 1;
                xp = 0;

                if (GameSession.TryGetMember(row.heroId, out GameSession.PartyMemberState state))
                {
                    level = state.level;
                    xp = state.experience;
                }

                required = 0;
                SetText(row.gainText, "+0 XP");
            }

            ShowRowState(row, level, xp, required, entry);

            if (row.levelUpTag != null)
                row.levelUpTag.gameObject.SetActive(false);

            if (row.highlight != null)
                row.highlight.color = new Color(Gold.r, Gold.g, Gold.b, 0f);
        }
    }

    private static Color RarityColor(RelicData relic)
    {
        if (relic.IsExclusive)
            return new Color(0.69f, 0.47f, 0.88f, 1f);   // morado (con detalles dorados)

        if (relic.rarity == RelicRarity.Rare)
            return new Color(0.37f, 0.61f, 0.88f, 1f);   // azul

        return new Color(0.72f, 0.70f, 0.66f, 1f);       // comun: gris
    }

    // Texto y barra de una fila para (nivel, xp). Sin XP requerida (nivel maximo o sin datos) la barra queda llena.
    private void ShowRowState(HeroRow row, int level, int xp, int required, VictorySummary.HeroEntry entry)
    {
        SetText(row.levelText, "Nv. " + level);
        bool maxed = entry != null && level >= entry.maxLevel;

        if (required > 0)
        {
            SetText(row.xpText, xp + " / " + required + " XP");
            SetBar(row, Mathf.Clamp01(xp / (float)required));
        }
        else
        {
            SetText(row.xpText, maxed ? "NIVEL MÁXIMO" : "");
            SetBar(row, maxed ? 1f : 0f);
        }
    }

    private static void SetBar(HeroRow row, float fraction)
    {
        if (row.barFill == null)
            return;

        row.barFill.anchorMin = new Vector2(0f, 0f);
        row.barFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        row.barFill.offsetMin = row.barFill.offsetMax = Vector2.zero;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    // ------------------------------------------------------------
    // Animaciones (discretas, con tiempo no escalado)
    // ------------------------------------------------------------

    private IEnumerator Intro()
    {
        SetAlpha(titleGroup, 0f);
        SetAlpha(progressHeader, 0f);
        SetAlpha(rewardsHeader, 0f);
        SetAlpha(rewardsBox, 0f);
        SetAlpha(continueGroup, 0f);

        foreach (HeroRow row in rows)
            SetAlpha(row.group, 0f);

        if (continueButton != null)
            continueButton.interactable = false;

        // 1. Entrada de la pantalla: fundido desde negro.
        if (fade != null)
        {
            fade.gameObject.SetActive(true);
            yield return Tween(0.7f, k => SetFade(1f - k));
            fade.gameObject.SetActive(false);
        }

        // 2. Aparece el titulo (fundido + un pequeno crecimiento): suena la fanfarria de victoria.
        PlaySfx(victoryFanfare, fanfareVolume);

        if (titleRect != null)
            titleRect.localScale = Vector3.one * 0.9f;

        yield return Tween(0.5f, k =>
        {
            SetAlpha(titleGroup, k);

            if (titleRect != null)
                titleRect.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, k);
        });

        // 3. Cabecera y filas, una a una.
        yield return FadeIn(progressHeader, 0.25f);

        foreach (HeroRow row in rows)
        {
            StartCoroutine(FadeIn(row.group, 0.3f));
            yield return new WaitForSecondsRealtime(0.12f);
        }

        yield return new WaitForSecondsRealtime(0.25f);

        // 4. Barras de experiencia (todas a la vez; cada una resuelve sus subidas de nivel).
        int running = 0;

        foreach (HeroRow row in rows)
        {
            VictorySummary.HeroEntry entry = summary != null ? summary.Find(row.heroId) : null;

            if (entry == null || (entry.xpGained <= 0 && !entry.LeveledUp))
                continue;

            running++;
            StartCoroutine(RunBar(row, entry, () => running--));
        }

        while (running > 0)
            yield return null;

        // 5. Recompensas.
        yield return FadeIn(rewardsHeader, 0.25f);
        yield return FadeIn(rewardsBox, 0.35f);

        if (summary != null && summary.relic != null)
            yield return RelicHighlight();

        // 6. Continuar.
        yield return FadeIn(continueGroup, 0.3f);

        if (continueButton != null)
            continueButton.interactable = true;

        animationsDone = true;
    }

    // Llena la barra desde el estado previo hasta el final; si sube de nivel, la completa, cambia de nivel y sigue.
    private IEnumerator RunBar(HeroRow row, VictorySummary.HeroEntry entry, System.Action done)
    {
        int level = entry.levelBefore;
        float xp = entry.xpBefore;

        while (level < entry.levelAfter)
        {
            int required = entry.Required(level);
            yield return FillTo(row, level, xp, required, required);   // hasta llenar el nivel
            level++;
            xp = 0f;
            SetText(row.levelText, "Nv. " + level);
            yield return LevelUpHighlight(row, level, entry);
            SetBar(row, 0f);
        }

        int target = entry.xpAfter;
        int needed = entry.Required(level);

        if (needed > 0)
            yield return FillTo(row, level, xp, target, needed);
        else
            ShowRowState(row, level, 0, 0, entry);

        done?.Invoke();
    }

    private IEnumerator FillTo(HeroRow row, int level, float fromXp, float toXp, int required)
    {
        if (required <= 0)
            yield break;

        float span = Mathf.Abs(toXp - fromXp) / required;
        float seconds = Mathf.Clamp(0.4f + span * 1.1f, 0.4f, 1.5f);

        BeginFillSound();

        yield return Tween(seconds, k =>
        {
            float value = Mathf.Lerp(fromXp, toXp, Mathf.SmoothStep(0f, 1f, k));
            SetBar(row, Mathf.Clamp01(value / required));
            SetText(row.xpText, Mathf.RoundToInt(value) + " / " + required + " XP");
        });

        SetBar(row, Mathf.Clamp01(toXp / required));
        SetText(row.xpText, Mathf.RoundToInt(toXp) + " / " + required + " XP");
        EndFillSound();
    }

    // Subida de nivel: el texto del nivel crece un poco y la fila se ilumina en dorado una sola vez.
    private IEnumerator LevelUpHighlight(HeroRow row, int newLevel, VictorySummary.HeroEntry entry)
    {
        // Mejoras de estadisticas desde el nivel con que empezo el combate, en texto pequeño y verde bajo la XP ganada.
        if (row.levelUpTag != null)
        {
            string gains = StatGains(entry, newLevel);
            bool had = row.levelUpTag.gameObject.activeSelf;
            row.levelUpTag.text = gains;
            row.levelUpTag.gameObject.SetActive(gains.Length > 0);

            if (!had && gains.Length > 0 && row.gainText != null)
                row.gainText.rectTransform.anchoredPosition += new Vector2(0f, 14f);
        }

        Color original = row.levelText != null ? row.levelText.color : Ivory;

        if (row.levelText != null)
            row.levelText.color = Gold;

        // Sonido de subida de nivel justo al empezar el realce (una sola vez si varios heroes suben a la vez).
        if (Time.unscaledTime - lastLevelUpSoundTime > 0.35f)
        {
            lastLevelUpSoundTime = Time.unscaledTime;
            PlaySfx(levelUpSound, levelUpVolume);
        }

        yield return Tween(0.6f, k =>
        {
            float pulse = Mathf.Sin(k * Mathf.PI);

            if (row.highlight != null)
                row.highlight.color = new Color(Gold.r, Gold.g, Gold.b, 0.22f * pulse);

            if (row.levelText != null)
                row.levelText.rectTransform.localScale = Vector3.one * (1f + 0.22f * pulse);
        });

        if (row.levelText != null)
            row.levelText.rectTransform.localScale = Vector3.one;

        // El texto dorado y la etiqueta quedan para indicar que subio de nivel.
        if (row.levelText != null)
            row.levelText.color = Gold;
    }

    private static string StatGains(VictorySummary.HeroEntry entry, int newLevel)
    {
        CharacterProgressionData p = entry.progression;

        if (p == null)
            return "";

        int from = entry.levelBefore;
        int hp = p.GetHealthBonus(newLevel) - p.GetHealthBonus(from);
        int dmg = p.GetDamageBonus(newLevel) - p.GetDamageBonus(from);
        int spd = p.GetSpeedBonus(newLevel) - p.GetSpeedBonus(from);
        string s = "";

        if (hp > 0) s += "+" + hp + " Vida";
        if (dmg > 0) s += (s != "" ? "  " : "") + "+" + dmg + " Daño";
        if (spd > 0) s += (s != "" ? "  " : "") + "+" + spd + " Velocidad";
        return s;
    }

    // Reliquia obtenida: un solo pulso suave del icono y un resplandor tenue del color de su rareza.
    private IEnumerator RelicHighlight()
    {
        Color glow = relicGlow != null ? relicGlow.color : Color.clear;

        // Reliquia rara (azul) o exclusiva (morada): sonido mas rico; comun: sonido sencillo.
        RelicData relic = summary != null ? summary.relic : null;
        bool special = relic != null && (relic.IsExclusive || relic.rarity == RelicRarity.Rare);
        PlaySfx(special && relicRareSound != null ? relicRareSound : relicSound, relicVolume);

        yield return Tween(0.8f, k =>
        {
            float pulse = Mathf.Sin(k * Mathf.PI);

            if (relicGroup != null)
                relicGroup.localScale = Vector3.one * (1f + 0.07f * pulse);

            if (relicGlow != null)
                relicGlow.color = new Color(glow.r, glow.g, glow.b, 0.45f * pulse);
        });

        if (relicGroup != null)
            relicGroup.localScale = Vector3.one;

        if (relicGlow != null)
            relicGlow.color = new Color(glow.r, glow.g, glow.b, 0f);
    }

    private static IEnumerator FadeIn(CanvasGroup group, float seconds)
    {
        yield return Tween(seconds, k => SetAlpha(group, k));
        SetAlpha(group, 1f);
    }

    private static IEnumerator Tween(float seconds, System.Action<float> apply)
    {
        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            apply(Mathf.Clamp01(t / seconds));
            yield return null;
        }

        apply(1f);
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
            group.alpha = alpha;
    }

    private void SetFade(float alpha)
    {
        if (fade == null)
            return;

        Color c = fade.color;
        c.a = alpha;
        fade.color = c;
    }

    // ------------------------------------------------------------
    // Continuar
    // ------------------------------------------------------------

    private void Continue()
    {
        if (leaving)
            return;

        leaving = true;

        if (continueButton != null)
            continueButton.interactable = false;

        StartCoroutine(LeaveRoutine());
    }

    // Fundido a negro y vuelta al mapa (que aparece con su fundido de entrada). El resumen se descarta: no se muestra dos veces.
    private IEnumerator LeaveRoutine()
    {
        if (fade != null)
        {
            fade.gameObject.SetActive(true);
            yield return Tween(0.6f, SetFade);
        }

        // Tras vencer al Capitan (jefe final): epilogo y creditos en lugar de volver al mapa.
        bool bossDefeated = summary != null && summary.encounterId == "BOSS" && !string.IsNullOrEmpty(endingScene) &&
                            GameSession.IsSceneInBuild(endingScene);

        VictorySummary.Clear();

        if (bossDefeated)
        {
            SceneManager.LoadScene(endingScene);
            yield break;
        }

        GameSession.RequestMapFadeIn();

        if (GameSession.IsSceneInBuild(nextScene))
            SceneManager.LoadScene(nextScene);
    }
}
