using System;
using System.Collections.Generic;
using UnityEngine;

// Iconos de habilidades para el HUD. La asociacion se hace por la
// identidad de la habilidad (el nombre del asset AbilityData, p. ej.
// "Valerian_CortePreciso"), no por el nombre visible ni por la posicion
// del boton, asi que no hace falta tocar los AbilityData. Es solo
// presentacion: el HUD consulta este conjunto y nunca lo modifica.
//
// Si un AbilityData ya trae su propio abilityIcon, ese tiene prioridad.
[CreateAssetMenu(
    fileName = "AbilityIconSet",
    menuName = "Reencarnacion Express/UI/Ability Icon Set"
)]
public class AbilityIconSet : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Nombre del asset AbilityData (identidad estable de la habilidad).")]
        public string abilityAsset;
        public Sprite icon;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, Sprite> lookup;

    public IReadOnlyList<Entry> Entries => entries;

    public void SetEntries(List<Entry> newEntries)
    {
        entries = newEntries;
        lookup = null;
    }

    // Devuelve el icono de la habilidad o null si no hay ninguno.
    public Sprite Get(AbilityData ability)
    {
        if (ability == null)
            return null;

        if (ability.abilityIcon != null)
            return ability.abilityIcon;

        if (lookup == null)
        {
            lookup = new Dictionary<string, Sprite>();

            foreach (Entry entry in entries)
                if (entry != null && !string.IsNullOrEmpty(entry.abilityAsset) && entry.icon != null)
                    lookup[entry.abilityAsset] = entry.icon;
        }

        lookup.TryGetValue(ability.name, out Sprite sprite);
        return sprite;
    }
}
