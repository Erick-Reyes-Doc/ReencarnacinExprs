using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tipos de marcador que aparecen sobre una unidad durante el combate.
public enum FeedbackKind
{
    Damage,       // daño directo (numero rojo)
    Critical,     // daño critico (numero naranja grande con "¡Crítico!")
    Periodic,     // daño por sangrado, veneno o quemadura (numero mas pequeño)
    Heal,         // curacion (numero verde con "+")
    Dodge,        // ataque esquivado
    Buff,         // mejora recibida (turquesa)
    Debuff        // estado negativo recibido (naranja rojizo)
}

// Punto de entrada estatico: los sistemas de combate avisan aqui y FloatingCombatText (solo existe en la escena de combate)
// dibuja el marcador. Nunca cambia nada del juego; si no hay escena de combate, los avisos simplemente se ignoran.
public static class CombatFeedback
{
    public static event Action<BattleUnit, string, FeedbackKind> Message;

    private static readonly HashSet<BattleUnit> critical = new HashSet<BattleUnit>();

    public static void Show(BattleUnit unit, string text, FeedbackKind kind)
    {
        if (unit != null)
            Message?.Invoke(unit, text, kind);
    }

    // El golpe que se va a entregar a 'unit' fue critico (lo consume el siguiente daño mostrado).
    public static void MarkCritical(BattleUnit unit)
    {
        if (unit != null)
            critical.Add(unit);
    }

    public static bool ConsumeCritical(BattleUnit unit)
    {
        return unit != null && critical.Remove(unit);
    }

    public static void ClearCritical(BattleUnit unit)
    {
        if (unit != null)
            critical.Remove(unit);
    }

    // Nombre del marcador de un estado y si es una mejora (true) o un estado negativo (false).
    public static string StatusText(StatusEffectType type, out bool isBuff)
    {
        isBuff = false;

        switch (type)
        {
            case StatusEffectType.Bleeding: return "¡Sangrado!";
            case StatusEffectType.Burn: return "¡Quemadura!";
            case StatusEffectType.Poison: return "¡Veneno!";
            case StatusEffectType.Mark: return "¡Marcado!";
            case StatusEffectType.Stun: return "¡Aturdido!";
            case StatusEffectType.Weaken: return "¡Debilitado!";
            case StatusEffectType.Vulnerability: return "¡Vulnerable!";
            case StatusEffectType.PriorityTarget: return "¡Objetivo!";
            case StatusEffectType.DefenseBoost: isBuff = true; return "¡Resistencia!";
            case StatusEffectType.Guard: isBuff = true; return "¡Guardia!";
            case StatusEffectType.EvasionBoost: isBuff = true; return "¡Evasión!";
            case StatusEffectType.Regeneration: isBuff = true; return "¡Regeneración!";
            default: return "";
        }
    }
}

// Marcadores flotantes de combate (numeros de daño y curacion, "¡Crítico!", "¡Esquivado!" y nombres de mejoras y estados).
// Se dibujan en un lienzo propio sobre toda la pantalla, siguen a la unidad en el campo, aparecen con un pequeño impulso, suben
// y se desvanecen. Varios avisos sobre la misma unidad se escalonan para no taparse.
public class FloatingCombatText : MonoBehaviour
{
    private const float Lifetime = 1.5f;
    private const float RiseDistance = 85f;
    private const float Stagger = 0.22f;

    private Canvas overlay;
    private RectTransform root;
    private Camera cam;
    private readonly Dictionary<BattleUnit, float> nextFree = new Dictionary<BattleUnit, float>();
    private readonly Dictionary<BattleUnit, int> stackIndex = new Dictionary<BattleUnit, int>();

    private void Awake()
    {
        GameObject go = new GameObject("MarcadoresCombate", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        overlay = go.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 500;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)go.transform;
    }

    private void OnEnable()
    {
        BattleUnit.DamageApplied += OnDamage;
        BattleUnit.HealApplied += OnHeal;
        CombatFeedback.Message += OnMessage;
    }

    private void OnDisable()
    {
        BattleUnit.DamageApplied -= OnDamage;
        BattleUnit.HealApplied -= OnHeal;
        CombatFeedback.Message -= OnMessage;
    }

    private void OnDamage(BattleUnit unit, int lost, bool periodic)
    {
        if (lost <= 0)
            return;

        bool crit = !periodic && CombatFeedback.ConsumeCritical(unit);

        Spawn(unit, crit ? "¡Crítico!\n" + lost : lost.ToString(),
            periodic ? FeedbackKind.Periodic : (crit ? FeedbackKind.Critical : FeedbackKind.Damage));
    }

    private void OnHeal(BattleUnit unit, int healed)
    {
        if (healed > 0)
            Spawn(unit, "+" + healed, FeedbackKind.Heal);
    }

    private void OnMessage(BattleUnit unit, string text, FeedbackKind kind)
    {
        if (!string.IsNullOrEmpty(text))
            Spawn(unit, text, kind);
    }

    // ------------------------------------------------------------
    // Creacion y animacion
    // ------------------------------------------------------------

    private void Spawn(BattleUnit unit, string text, FeedbackKind kind)
    {
        if (unit == null || root == null)
            return;

        // Escalonado: cada aviso nuevo sobre la misma unidad espera un instante y sube un poco mas arriba.
        float now = Time.time;
        float start = nextFree.TryGetValue(unit, out float free) ? Mathf.Max(now, free) : now;
        nextFree[unit] = start + Stagger;

        int index = stackIndex.TryGetValue(unit, out int s) && start - now < 0.01f ? s + 1 : 0;
        stackIndex[unit] = start - now > 0.01f ? 0 : index;

        StartCoroutine(Animate(unit, text, kind, start - now, index % 3));
    }

    private static void Style(FeedbackKind kind, out Color color, out int size, out bool outlineStrong)
    {
        outlineStrong = true;

        switch (kind)
        {
            case FeedbackKind.Critical: color = new Color(1f, 0.62f, 0.12f); size = 88; break;
            case FeedbackKind.Periodic: color = new Color(0.78f, 0.25f, 0.36f); size = 50; break;
            case FeedbackKind.Heal: color = new Color(0.42f, 0.92f, 0.52f); size = 70; break;
            case FeedbackKind.Dodge: color = new Color(0.82f, 0.88f, 0.95f); size = 50; break;
            case FeedbackKind.Buff: color = new Color(0.40f, 0.88f, 0.92f); size = 50; break;
            case FeedbackKind.Debuff: color = new Color(1f, 0.52f, 0.30f); size = 50; break;
            default: color = new Color(0.95f, 0.22f, 0.18f); size = 76; break;
        }
    }

    private Vector2 AnchorOf(BattleUnit unit)
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null || unit == null)
            return Vector2.zero;

        SpriteRenderer sr = unit.GetComponentInChildren<SpriteRenderer>();
        Vector3 world = unit.transform.position;

        if (sr != null)
            world = new Vector3(sr.bounds.center.x, Mathf.Lerp(sr.bounds.center.y, sr.bounds.max.y, 0.55f), 0f);

        Vector3 screen = cam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        return local;
    }

    private IEnumerator Animate(BattleUnit unit, string text, FeedbackKind kind, float delay, int lane)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (unit == null)
            yield break;

        Style(kind, out Color color, out int size, out bool _);

        GameObject go = new GameObject("Marcador", typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(root, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(520f, 160f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Text label = go.GetComponent<Text>();
        label.font = GameFonts.Bold;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.supportRichText = false;
        label.text = text;
        label.color = color;

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(0.04f, 0.02f, 0.03f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);

        float jitter = kind == FeedbackKind.Damage || kind == FeedbackKind.Periodic || kind == FeedbackKind.Heal
            ? UnityEngine.Random.Range(-24f, 24f) : (lane - 1) * 10f;
        float laneOffset = lane * 46f;
        Vector2 last = AnchorOf(unit);

        for (float t = 0f; t < Lifetime; t += Time.deltaTime)
        {
            if (go == null)
                yield break;

            float k = t / Lifetime;

            if (unit != null)
                last = AnchorOf(unit);

            // Sube con frenada suave; el golpe critico tiembla un instante.
            float rise = RiseDistance * (1f - (1f - k) * (1f - k));
            float shake = kind == FeedbackKind.Critical && k < 0.18f ? Mathf.Sin(t * 90f) * 7f : 0f;
            Vector2 pos = last + new Vector2(jitter + shake, 40f + laneOffset + rise);
            pos.x = Mathf.Clamp(pos.x, -820f, 820f);   // los de las unidades de los bordes no se salen de la pantalla
            rt.anchoredPosition = pos;

            // Impulso de entrada: pequeño, un poco mas grande que el final y asienta.
            float pop = k < 0.12f ? Mathf.Lerp(0.55f, 1.25f, k / 0.12f)
                      : k < 0.24f ? Mathf.Lerp(1.25f, 1f, (k - 0.12f) / 0.12f) : 1f;
            rt.localScale = Vector3.one * pop;

            float alpha = k < 0.68f ? 1f : 1f - (k - 0.68f) / 0.32f;
            label.color = new Color(color.r, color.g, color.b, alpha);
            outline.effectColor = new Color(0.04f, 0.02f, 0.03f, 0.95f * alpha);

            yield return null;
        }

        if (go != null)
            Destroy(go);
    }
}
