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

    [Header("Retrato provisional (recorte de la cabeza del sprite de combate, con su tinte)")]
    public Sprite bodySprite;
    public Color tint = Color.white;

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
        stats.health = savedHealth < 0 ? stats.maxHealth : Mathf.Clamp(savedHealth, 1, stats.maxHealth);
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
