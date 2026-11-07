using System;
using System.Collections.Generic;
using UnityEngine;

public enum RelicRarity
{
    Common,
    Uncommon,
    Rare,
    Epic
}

// Estadisticas que una reliquia puede modificar (las que el sistema de combate ya maneja).
public enum RelicStat
{
    Health,
    Damage,
    Protection,
    Speed,
    CritChance,
    Evasion      // se agrega al final para no alterar los valores ya serializados
}

[Serializable]
public struct RelicModifier
{
    public RelicStat stat;

    [Tooltip("Magnitud (positiva). En 'Penalizaciones' se resta; en 'Bonificaciones' se suma. Daño: suma a minimo y maximo. Proteccion, evasion y critico: puntos porcentuales.")]
    [Min(0)] public int amount;
}

// Definicion reutilizable de un tipo de reliquia. Cada copia que posee el jugador es una unidad distinta
// (ver RelicInventory); aqui solo viven los datos del tipo.
[CreateAssetMenu(fileName = "NewRelic", menuName = "Reencarnacion Express/Relic")]
public class RelicData : ScriptableObject
{
    [Tooltip("Identificador unico del tipo (no cambiar una vez usado).")]
    public string typeId;

    public string displayName;

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;
    public RelicRarity rarity;

    public List<RelicModifier> bonuses = new List<RelicModifier>();
    public List<RelicModifier> penalties = new List<RelicModifier>();

    [Tooltip("Identificador del personaje que puede equiparla (p. ej. PlayerArquera). Vacio = cualquiera.")]
    public string exclusiveHeroId = "";

    [Header("Tienda")]
    [Tooltip("Si es verdadero, la tienda del mapa vende esta reliquia. Las que solo se consiguen como recompensa de zona deben quedar en falso.")]
    public bool availableInShop = false;

    [Tooltip("Precio en Coronas cuando se vende en la tienda.")]
    [Min(0)] public int shopPrice = 45;

    [Tooltip("Reliquia unica: solo se puede poseer una (entre inventario y equipada). La tienda no vende otra copia.")]
    public bool uniqueOwnership = false;

    public bool IsExclusive => !string.IsNullOrEmpty(exclusiveHeroId);

    public bool CanBeUsedBy(string heroId)
    {
        return !IsExclusive || exclusiveHeroId == heroId;
    }

    // Efecto neto sobre una estadistica (bonificaciones - penalizaciones).
    public int NetModifier(RelicStat stat)
    {
        int total = 0;

        foreach (RelicModifier m in bonuses)
            if (m.stat == stat) total += m.amount;

        foreach (RelicModifier m in penalties)
            if (m.stat == stat) total -= m.amount;

        return total;
    }

    // Sufijo del valor: daño, vida y velocidad son puntos; proteccion, evasion y critico, puntos porcentuales.
    public static string Suffix(RelicStat stat)
    {
        return stat == RelicStat.CritChance || stat == RelicStat.Protection || stat == RelicStat.Evasion ? " %" : "";
    }

    public static string StatName(RelicStat stat)
    {
        switch (stat)
        {
            case RelicStat.Health: return "Vida";
            case RelicStat.Damage: return "Daño";
            case RelicStat.Protection: return "Protección";
            case RelicStat.Speed: return "Velocidad";
            case RelicStat.Evasion: return "Evasión";
            default: return "Crítico";
        }
    }

    public static string RarityName(RelicRarity rarity)
    {
        switch (rarity)
        {
            case RelicRarity.Uncommon: return "Poco común";
            case RelicRarity.Rare: return "Rara";
            case RelicRarity.Epic: return "Épica";
            default: return "Común";
        }
    }

    public static Color RarityColor(RelicRarity rarity)
    {
        switch (rarity)
        {
            case RelicRarity.Uncommon: return new Color32(0x6F, 0xB8, 0x7A, 0xFF);
            case RelicRarity.Rare: return new Color32(0x5E, 0x9B, 0xE0, 0xFF);
            case RelicRarity.Epic: return new Color32(0xB0, 0x78, 0xE0, 0xFF);
            default: return new Color32(0xB9, 0xB2, 0xA3, 0xFF);
        }
    }
}
