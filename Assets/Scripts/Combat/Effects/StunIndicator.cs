using System.Collections.Generic;
using UnityEngine;

// Indicador visual de aturdimiento: mientras una unidad tiene el estado Aturdimiento, tres estrellas amarillas giran sobre su
// cabeza (heroes y enemigos, venga de la habilidad que venga). Solo presentacion: lee el estado, nunca lo cambia.
//
// Va en la escena de combate (junto a AttackCinematic). Las estrellas siguen al sprite de la unidad, asi que acompanan sus
// desplazamientos y sus animaciones.
public class StunIndicator : MonoBehaviour
{
    [Header("Estrellas")]
    [Range(1, 5)] [SerializeField] private int starCount = 3;
    [SerializeField] private Color starColor = new Color(1f, 0.9f, 0.3f, 1f);
    [Tooltip("Tamano de cada estrella como fraccion de la altura del personaje.")]
    [Range(0.03f, 0.15f)] [SerializeField] private float starSize = 0.07f;
    [Tooltip("Radio horizontal de la orbita como fraccion de la altura del personaje.")]
    [Range(0.05f, 0.4f)] [SerializeField] private float orbitRadius = 0.15f;
    [Tooltip("Vueltas por segundo.")]
    [Min(0.1f)] [SerializeField] private float turnsPerSecond = 1.0f;
    [Tooltip("Segundos que tarda en aparecer al empezar el aturdimiento (con un pequeno salto de escala).")]
    [Min(0.01f)] [SerializeField] private float popSeconds = 0.18f;

    private class Marker
    {
        public GameObject root;
        public SpriteRenderer[] stars;
        public float age;
    }

    private readonly Dictionary<BattleUnit, Marker> markers = new Dictionary<BattleUnit, Marker>();
    private readonly List<BattleUnit> toRemove = new List<BattleUnit>();
    private BattleUnit[] units = new BattleUnit[0];
    private float nextRefresh;
    private int topOrder;
    private static Sprite starSprite;

    private void OnDisable()
    {
        foreach (Marker m in markers.Values)
        {
            if (m.root != null)
                Destroy(m.root);
        }

        markers.Clear();
    }

    private void Update()
    {
        // La lista de unidades se refresca de vez en cuando (hay pocas y no aparecen a cada fotograma).
        if (Time.unscaledTime >= nextRefresh)
        {
            units = FindObjectsByType<BattleUnit>(FindObjectsInactive.Exclude);
            topOrder = AttackCinematic.FxBase();
            nextRefresh = Time.unscaledTime + 0.4f;
        }

        foreach (BattleUnit unit in units)
        {
            if (unit == null)
                continue;

            if (IsStunned(unit, out SpriteRenderer sr))
                Show(unit, sr);
            else
                Hide(unit);
        }

        // Unidades destruidas o ya fuera de la lista.
        toRemove.Clear();

        foreach (KeyValuePair<BattleUnit, Marker> pair in markers)
        {
            if (pair.Key == null || pair.Value.root == null)
                toRemove.Add(pair.Key);
        }

        foreach (BattleUnit dead in toRemove)
            Hide(dead);
    }

    private static bool IsStunned(BattleUnit unit, out SpriteRenderer sr)
    {
        sr = null;

        if (!unit.IsAlive || UnitDownedVisual.IsActive(unit))
            return false;

        StatusEffectController statuses = unit.GetComponent<StatusEffectController>();

        if (statuses == null || !statuses.HasEffect(StatusEffectType.Stun))
            return false;

        sr = unit.GetComponentInChildren<SpriteRenderer>();
        return sr != null && sr.enabled && sr.sprite != null;
    }

    private void Show(BattleUnit unit, SpriteRenderer sr)
    {
        if (starSprite == null)
            starSprite = BuildStarSprite();

        if (!markers.TryGetValue(unit, out Marker marker) || marker.root == null)
        {
            marker = CreateMarker(unit);
            markers[unit] = marker;
        }

        marker.age += Time.unscaledDeltaTime;

        Bounds b = sr.bounds;
        float h = Mathf.Max(b.size.y, 1f);
        Vector3 center = new Vector3(b.center.x, Mathf.Lerp(b.center.y, b.max.y, 0.88f), unit.transform.position.z);
        float pop = Mathf.Clamp01(marker.age / popSeconds);
        float scale = h * starSize / starSprite.bounds.size.x * Mathf.Lerp(2f, 1f, pop);
        float spin = marker.age * turnsPerSecond * Mathf.PI * 2f;

        for (int i = 0; i < marker.stars.Length; i++)
        {
            float angle = spin + i * Mathf.PI * 2f / marker.stars.Length;
            SpriteRenderer star = marker.stars[i];
            star.transform.position = center + new Vector3(Mathf.Cos(angle) * h * orbitRadius, Mathf.Sin(angle) * h * orbitRadius * 0.3f, 0f);
            star.transform.localScale = Vector3.one * scale;
            star.transform.rotation = Quaternion.Euler(0f, 0f, marker.age * 200f + i * 40f);
            star.sortingLayerID = sr.sortingLayerID;
            star.sortingOrder = Mathf.Max(sr.sortingOrder + 12, topOrder);

            // Las que pasan por detras de la cabeza (seno negativo) se ven algo mas pequenas y tenues.
            float depth = 0.75f + 0.25f * Mathf.Sin(angle);
            star.transform.localScale *= depth;
            star.color = new Color(starColor.r, starColor.g, starColor.b, pop * Mathf.Lerp(0.6f, 1f, depth));
        }
    }

    private Marker CreateMarker(BattleUnit unit)
    {
        GameObject root = new GameObject("StunStars_" + unit.name);
        root.transform.SetParent(transform, false);

        Marker marker = new Marker { root = root, stars = new SpriteRenderer[starCount], age = 0f };

        for (int i = 0; i < starCount; i++)
        {
            GameObject go = new GameObject("Star" + i);
            go.transform.SetParent(root.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = starSprite;
            sr.color = new Color(starColor.r, starColor.g, starColor.b, 0f);
            marker.stars[i] = sr;
        }

        return marker;
    }

    private void Hide(BattleUnit unit)
    {
        if (unit == null)
        {
            // Unidad destruida: se limpian los marcadores huerfanos.
            List<BattleUnit> orphans = new List<BattleUnit>();

            foreach (KeyValuePair<BattleUnit, Marker> pair in markers)
            {
                if (pair.Key == null)
                {
                    if (pair.Value.root != null)
                        Destroy(pair.Value.root);

                    orphans.Add(pair.Key);
                }
            }

            foreach (BattleUnit key in orphans)
                markers.Remove(key);

            return;
        }

        if (markers.TryGetValue(unit, out Marker marker))
        {
            if (marker.root != null)
                Destroy(marker.root);

            markers.Remove(unit);
        }
    }

    // Estrella de cinco puntas.
    private static Sprite BuildStarSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                // Radio del contorno de la estrella segun el angulo (5 puntas).
                float edge = 0.55f + 0.4f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * (ang - Mathf.PI * 0.5f))), 1.4f);
                float a = Mathf.Clamp01((edge - r) / 0.06f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
