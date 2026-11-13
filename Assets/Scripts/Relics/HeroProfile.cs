using System;
using UnityEngine;

// Datos de un protagonista para el mapa (donde no hay BattleUnit): sus datos base, su progresion y lo que muestra
// la ficha. Las estadisticas se calculan con las mismas formulas que BattleUnit (base + nivel + reliquias).
[Serializable]
public class HeroProfile
{
    [Tooltip("Nombre del GameObject del personaje en el combate (clave de GameSession.Party y de las reliquias).")]
    public string unitId;
    public string displayName;
    public UnitData unit;
    public CharacterProgressionData progression;

    [Header("Icono oficial (Assets/Sprites/Personajes/Iconos)")]
    [Tooltip("Retrato oficial del personaje. Si esta asignado sustituye al retrato provisional en la barra del mapa y en la ficha.")]
    public Sprite icon;
    [Tooltip("Icono que sustituye al oficial cuando ya se completo el evento indicado (p. ej. Noelle sin mascara tras La mascara rota).")]
    public Sprite iconAfterEvent;
    public string iconAfterEventId;

    [Header("Retrato provisional (recorte de la cabeza del sprite de combate, con su tinte)")]
    public Sprite bodySprite;
    [Tooltip("Sprite del cuerpo que sustituye a 'bodySprite' cuando ya se completo 'iconAfterEventId' (Noelle sin mascara).")]
    public Sprite bodyAfterEvent;
    public Color tint = Color.white;

    [Header("Animacion Idle del retrato grande de la ficha (opcional)")]
    [Tooltip("Fotogramas de la animacion en reposo. Si hay 2 o mas sustituyen al sprite estatico grande SOLO en la ventana de descripcion del personaje; no afectan al combate.")]
    public Sprite[] idleFrames = new Sprite[0];
    [Min(1f)] public float idleFps = 8f;

    [Tooltip("Fotogramas del Idle cuando ya se completo 'iconAfterEventId' (Noelle sin mascara). Vacio = tras el evento se muestra el sprite estatico de 'bodyAfterEvent'.")]
    public Sprite[] idleFramesAfterEvent = new Sprite[0];

    // Fotogramas del Idle que corresponden al aspecto actual (antes o despues del evento que cambia al personaje).
    public Sprite[] ActiveIdleFrames =>
        bodyAfterEvent != null && !string.IsNullOrEmpty(iconAfterEventId) && GameSession.IsEncounterCompleted(iconAfterEventId)
            ? idleFramesAfterEvent
            : idleFrames;

    public bool HasIdle
    {
        get
        {
            Sprite[] frames = ActiveIdleFrames;

            if (frames == null || frames.Length < 2)
                return false;

            foreach (Sprite frame in frames)
                if (frame == null)
                    return false;

            return true;
        }
    }

    // Retrato que se muestra en iconos: el oficial (sin tinte) o, si falta, el recorte provisional con su tinte.
    // Cuerpo completo para la ficha: el de despues del evento si ya se completo; si no, el base.
    public Sprite Body => bodyAfterEvent != null && !string.IsNullOrEmpty(iconAfterEventId) && GameSession.IsEncounterCompleted(iconAfterEventId) ? bodyAfterEvent : bodySprite;

    public Sprite Icon => iconAfterEvent != null && !string.IsNullOrEmpty(iconAfterEventId) && GameSession.IsEncounterCompleted(iconAfterEventId)
        ? iconAfterEvent
        : (icon != null ? icon : HeroPortraits.Get(bodySprite));
    public Color IconTint => icon != null || iconAfterEvent != null ? Color.white : tint;

    [Header("Habilidades (las del conjunto inicial y, si existe, el transformado)")]
    public AbilityData[] abilities = new AbilityData[0];
    public AbilityData[] transformedAbilities = new AbilityData[0];
}

public struct HeroStats
{
    public int level;
    public int experience;
    public int experienceRequired;   // 0 = nivel maximo
    public int maxHealth;
    public int health;
    public int damageMin;
    public int damageMax;
    public int protection;           // %
    public float evasion;            // %
    public int speed;
    public float crit;               // %
}

public static class CharacterStats
{
    public const int MaxLevel = 10;

    // Estadisticas del personaje con un conjunto de modificadores de reliquias (GetMods o PreviewMods).
    public static HeroStats Compute(HeroProfile hero, StatMods mods)
    {
        int level = 1;
        int experience = 0;
        int savedHealth = -1;

        if (GameSession.TryGetMember(hero.unitId, out GameSession.PartyMemberState state))
        {
            level = Mathf.Clamp(state.level, 1, MaxLevel);
            experience = state.experience;
            savedHealth = state.health;
        }

        CharacterProgressionData p = hero.progression;
        UnitData u = hero.unit;

        HeroStats stats = new HeroStats { level = level, experience = experience };
        stats.maxHealth = Mathf.Max(1, u.maxHealth + (p != null ? p.GetHealthBonus(level) : 0) + mods.health);
        int damageBonus = (p != null ? p.GetDamageBonus(level) : 0) + mods.damage;
        stats.damageMin = Mathf.Max(1, u.damageMin + damageBonus);
        stats.damageMax = Mathf.Max(stats.damageMin, u.damageMax + damageBonus);
        stats.protection = Mathf.Clamp(u.protectionPercent + mods.protection, BattleUnit.MinProtectionPercent, BattleUnit.MaxProtectionPercent);
        stats.evasion = Mathf.Clamp(u.evasionPercent + mods.evasion, 0f, BattleUnit.MaxEvasionPercent);
        stats.speed = Mathf.Max(0, u.speed + (p != null ? p.GetSpeedBonus(level) : 0) + mods.speed);
        stats.crit = Mathf.Clamp(u.criticalChance + mods.crit, 0f, 100f);
        stats.experienceRequired = p != null && level < MaxLevel ? p.GetExperienceRequired(level) : 0;
        stats.health = savedHealth < 0 ? stats.maxHealth : Mathf.Clamp(savedHealth, 0, stats.maxHealth);
        return stats;
    }

    // Estadisticas actuales (nivel + reliquias equipadas). Si la foto del equipo guardo otra vida maxima (p. ej. tras
    // equipar o quitar una reliquia de vida), se actualiza el maximo; la vida actual no sube sola, solo se recorta.
    public static HeroStats Current(HeroProfile hero)
    {
        HeroStats stats = Compute(hero, RelicInventory.GetMods(hero.unitId));

        if (GameSession.TryGetMember(hero.unitId, out GameSession.PartyMemberState state) &&
            state.maxHealth > 0 && state.maxHealth != stats.maxHealth)
        {
            GameSession.TryUpdateMemberMaxHealth(hero.unitId, stats.maxHealth);
            stats = Compute(hero, RelicInventory.GetMods(hero.unitId));
        }

        return stats;
    }
}
