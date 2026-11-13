using System.Collections;
using UnityEngine;

// Efecto visual de muerte de una unidad (heroe o enemigo): el sprite se oscurece hasta quedar negro, aparece un craneo rojo
// con su aro sobre el cuerpo y despues todo se desvanece. Solo presentacion: PositionManager llama a Play en lugar de ocultar
// el sprite de golpe; la logica de combate (turnos, formacion) no espera a que termine. Si la unidad revive durante el efecto
// (recuperacion poscombate), Cancel lo detiene y restaura el sprite.
public class UnitDeathEffect : MonoBehaviour
{
    private const float DarkenSeconds = 0.35f;
    private const float SkullDelay = 0.12f;
    private const float HoldUntil = 0.95f;
    private const float FadeSeconds = 0.6f;
    private const float SkullFadeDelay = 0.2f;

    // Duracion total del efecto (oscurecer, craneo, desvanecer); PositionManager espera esto antes de mover a los supervivientes.
    public const float TotalSeconds = HoldUntil + FadeSeconds + SkullFadeDelay;

    private static Sprite ringSprite;
    private static Sprite skullSprite;

    private SpriteRenderer body;
    private Color originalColor;
    private Coroutine routine;
    private GameObject skullRoot;

    // Inicia el efecto sobre 'unit'. Si no hay sprite o el icono falta, el sprite se oculta como antes.
    public static void Play(BattleUnit unit, SpriteRenderer sprite)
    {
        if (unit == null || sprite == null)
            return;

        UnitDeathEffect effect = unit.GetComponent<UnitDeathEffect>();

        if (effect == null)
            effect = unit.gameObject.AddComponent<UnitDeathEffect>();

        // Unidad con animacion de derrota propia (el Capitan): la hace, se queda en su ultima pose y despues se desvanece
        // (sin oscurecer ni craneo). Dice su ultima frase.
        UnitAnimationDriver driver = unit.GetComponent<UnitAnimationDriver>();

        if (unit.gameObject.activeInHierarchy && driver != null && driver.PlayDefeat())
        {
            effect.BeginDefeatAnimation(sprite, driver.DefeatSeconds);

            // Sonido de la derrota (armadura que se derrumba), junto con la animacion.
            if (unit.Data != null)
                CombatCueSounds.Play(unit.Data.defeatSound, AttackCinematic.AbilitySfxVolume * 1.5f);

            if (unit.Data != null && !string.IsNullOrEmpty(unit.Data.defeatLine))
                BossSpeechBanner.Show(unit, unit.Data.defeatLine, null);

            return;
        }

        if (!unit.gameObject.activeInHierarchy || !effect.Begin(sprite))
            sprite.enabled = false;
    }

    // Segundos que dura la derrota animada mas reciente (animacion + pausa + desvanecido): la victoria espera esto.
    public static float LastDefeatAnimationSeconds { get; private set; }

    private const float DefeatHoldSeconds = 1.2f;
    private const float DefeatFadeSeconds = 0.7f;

    private void BeginDefeatAnimation(SpriteRenderer sprite, float clipSeconds)
    {
        Stop(true);
        body = sprite;
        originalColor = sprite.color;
        LastDefeatAnimationSeconds = clipSeconds + DefeatHoldSeconds + DefeatFadeSeconds;
        routine = StartCoroutine(RunDefeat(clipSeconds));
    }

    private IEnumerator RunDefeat(float clipSeconds)
    {
        yield return new WaitForSeconds(clipSeconds + DefeatHoldSeconds);

        for (float t = 0f; t < DefeatFadeSeconds; t += Time.deltaTime)
        {
            if (body == null)
                yield break;

            Color c = originalColor;
            c.a = originalColor.a * (1f - t / DefeatFadeSeconds);
            body.color = c;
            yield return null;
        }

        if (body != null)
            body.enabled = false;

        routine = null;
    }

    // Detiene el efecto y deja el sprite como estaba (lo reactiva quien revive a la unidad).
    public static void Cancel(BattleUnit unit)
    {
        UnitDeathEffect effect = unit != null ? unit.GetComponent<UnitDeathEffect>() : null;

        if (effect != null)
            effect.Stop(true);
    }

    private bool Begin(SpriteRenderer sprite)
    {
        EnsureSprites();

        if (skullSprite == null)
            return false;

        Stop(true);
        body = sprite;
        originalColor = sprite.color;
        routine = StartCoroutine(Run());
        return true;
    }

    private void Stop(bool restore)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (skullRoot != null)
        {
            Destroy(skullRoot);
            skullRoot = null;
        }

        if (restore && body != null)
            body.color = originalColor;
    }

    private void OnDisable()
    {
        // Si el objeto se desactiva a medias (cambio de escena, limpieza de enemigos) no queda nada a medio hacer.
        if (skullRoot != null)
            Destroy(skullRoot);

        routine = null;
    }

    private IEnumerator Run()
    {
        Bounds bounds = body.bounds;
        float height = Mathf.Max(0.5f, bounds.size.y);

        skullRoot = BuildSkull(bounds, height, out SpriteRenderer ring, out SpriteRenderer skull);
        Color ringColor = ring.color;
        Color skullColor = skull.color;
        skullRoot.transform.localScale = skullRoot.transform.localScale * 0.01f;
        Vector3 fullScale = skullRoot.transform.localScale * 100f;

        for (float t = 0f; t < HoldUntil + FadeSeconds + SkullFadeDelay; t += Time.deltaTime)
        {
            if (body == null)
                yield break;

            // Oscurece el sprite (conserva su transparencia).
            float dark = Mathf.Clamp01(t / DarkenSeconds);
            Color tint = Color.Lerp(originalColor, new Color(0f, 0f, 0f, originalColor.a), dark);

            // Desvanece el cuerpo al final.
            float bodyFade = t > HoldUntil ? Mathf.Clamp01((t - HoldUntil) / FadeSeconds) : 0f;
            tint.a = originalColor.a * (1f - bodyFade);
            body.color = tint;

            // Craneo: pequeño impulso al aparecer y desvanecimiento un poco despues del cuerpo.
            if (skullRoot != null)
            {
                float appear = Mathf.Clamp01((t - SkullDelay) / 0.25f);
                float pop = appear < 0.6f ? Mathf.Lerp(0.5f, 1.15f, appear / 0.6f) : Mathf.Lerp(1.15f, 1f, (appear - 0.6f) / 0.4f);
                skullRoot.transform.localScale = fullScale * (appear <= 0f ? 0.01f : pop);

                float skullFade = t > HoldUntil + SkullFadeDelay ? Mathf.Clamp01((t - HoldUntil - SkullFadeDelay) / FadeSeconds) : 0f;
                float alpha = appear * (1f - skullFade);
                ring.color = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * alpha);
                skull.color = new Color(skullColor.r, skullColor.g, skullColor.b, skullColor.a * alpha);
            }

            yield return null;
        }

        if (skullRoot != null)
        {
            Destroy(skullRoot);
            skullRoot = null;
        }

        if (body != null)
        {
            body.enabled = false;        // oculto como antes (PositionManager lo reactiva al revivir)
            body.color = originalColor;  // y con su color original para cuando vuelva
        }

        routine = null;
    }

    private GameObject BuildSkull(Bounds bounds, float height, out SpriteRenderer ring, out SpriteRenderer skull)
    {
        GameObject root = new GameObject("IconoMuerte");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(bounds.center.x, bounds.center.y - height * 0.05f, transform.position.z);

        // Diametro del aro: la mitad de la altura del sprite (compensa la escala del padre).
        float parentScale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float s = height * 0.5f / ringSprite.bounds.size.x / parentScale;
        root.transform.localScale = new Vector3(s, s, 1f);

        ring = root.AddComponent<SpriteRenderer>();
        ring.sprite = ringSprite;
        ring.sortingLayerID = body.sortingLayerID;
        ring.sortingOrder = body.sortingOrder + 5;

        GameObject skullGo = new GameObject("Craneo");
        skullGo.transform.SetParent(root.transform, false);
        float k = 0.62f * ringSprite.bounds.size.x / skullSprite.bounds.size.x;
        skullGo.transform.localScale = new Vector3(k, k, 1f);
        skull = skullGo.AddComponent<SpriteRenderer>();
        skull.sprite = skullSprite;
        skull.sortingLayerID = body.sortingLayerID;
        skull.sortingOrder = body.sortingOrder + 6;
        return root;
    }

    // El craneo viene de Resources/Combate/IconoMuerte; el aro se genera una sola vez (disco oscuro con borde rojo).
    private static void EnsureSprites()
    {
        if (skullSprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>("Combate/IconoMuerte");

            if (texture != null)
                skullSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        if (ringSprite == null)
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color ringRed = new Color(0.72f, 0.08f, 0.16f, 1f);
            Color disc = new Color(0.03f, 0.02f, 0.05f, 0.7f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01((1f - r) * size * 0.5f);   // borde exterior suave
                    Color c;

                    if (r > 0.86f)
                        c = new Color(ringRed.r, ringRed.g, ringRed.b, edge);
                    else
                        c = new Color(disc.r, disc.g, disc.b, disc.a * Mathf.Clamp01((0.86f - r) * size * 0.5f + 0.5f) + (r > 0.83f ? 0f : 0f));

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
