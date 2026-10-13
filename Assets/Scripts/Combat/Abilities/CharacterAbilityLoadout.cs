using System;
using System.Collections.Generic;
using UnityEngine;

// Conjunto activo de habilidades de un personaje (hasta 4).
// Si una unidad lleva este componente, AbilitySystem solo le permite
// usar las habilidades de su conjunto activo; las unidades sin el
// componente no tienen restricciones (habilidades de prueba).
//
// El conjunto puede sustituirse una unica vez por el alternativo
// (por ejemplo, Noelle enmascarada -> liberada) mediante
// UnlockTransformedForm(). No hay ningun desbloqueo automatico: la
// transformacion solo ocurre cuando otro sistema (evento narrativo o
// una prueba manual) llama al metodo.
//
// Regla de cooldowns al transformar: cada habilidad nueva hereda el
// cooldown pendiente de la habilidad que ocupaba su misma ranura
// (si es mayor que el suyo). Asi cambiar de etapa no sirve para
// reutilizar de inmediato una habilidad equivalente.
public class CharacterAbilityLoadout : MonoBehaviour
{
    public const int MaxAbilities = 4;

    [Header("Conjunto inicial (ej. Noelle enmascarada)")]
    [SerializeField] private AbilityData[] initialAbilities =
        new AbilityData[MaxAbilities];

    [Header("Conjunto tras la transformacion (ej. Noelle liberada)")]
    [SerializeField] private AbilityData[] transformedAbilities =
        new AbilityData[MaxAbilities];

    private bool isTransformed;

    // Se dispara una sola vez, al transformarse.
    public event Action<CharacterAbilityLoadout> OnTransformed;

    public bool IsTransformed => isTransformed;

    // true si aun no se transformo y existe un conjunto alternativo.
    public bool CanTransform =>
        !isTransformed && Build(transformedAbilities).Count > 0;

    // Equivalentes para Noelle.
    public bool IsMasked => !isTransformed;
    public bool IsLiberated => isTransformed;

    // Habilidades del conjunto activo (ranuras vacias omitidas).
    public IReadOnlyList<AbilityData> Abilities => Build(ActiveSet);

    private AbilityData[] ActiveSet =>
        isTransformed ? transformedAbilities : initialAbilities;

    private static List<AbilityData> Build(AbilityData[] set)
    {
        List<AbilityData> list = new List<AbilityData>();

        if (set == null)
            return list;

        foreach (AbilityData ability in set)
        {
            if (ability != null)
                list.Add(ability);
        }

        return list;
    }

    public bool HasAbility(AbilityData ability)
    {
        if (ability == null)
            return false;

        AbilityData[] set = ActiveSet;

        if (set == null)
            return false;

        foreach (AbilityData candidate in set)
        {
            if (candidate == ability)
                return true;
        }

        return false;
    }

    // Asigna los conjuntos desde codigo (pruebas o carga de datos).
    public void SetAbilities(
        AbilityData[] initial,
        AbilityData[] transformed
    )
    {
        initialAbilities = initial;
        transformedAbilities = transformed;
        isTransformed = false;
    }

    // Sustituye el conjunto activo por el alternativo. Solo ocurre una
    // vez; no modifica nivel, XP, vida, posicion ni equipo.
    public bool UnlockTransformedForm()
    {
        if (isTransformed)
        {
            Debug.Log("La transformacion ya se habia realizado.");
            return false;
        }

        if (Build(transformedAbilities).Count == 0)
        {
            Debug.LogWarning(
                "No hay habilidades configuradas para la transformacion."
            );
            return false;
        }

        AbilityCooldownController cooldowns =
            GetComponent<AbilityCooldownController>();

        if (cooldowns != null)
        {
            // Herencia de cooldown por ranura.
            for (int i = 0; i < MaxAbilities; i++)
            {
                AbilityData from = i < initialAbilities.Length
                    ? initialAbilities[i]
                    : null;

                AbilityData to = i < transformedAbilities.Length
                    ? transformedAbilities[i]
                    : null;

                cooldowns.InheritCooldown(from, to);
            }
        }

        isTransformed = true;

        BattleUnit unit = GetComponent<BattleUnit>();

        Debug.Log(
            (unit != null ? unit.Data.unitName : name) +
            " se transforma: nuevo conjunto de habilidades activo."
        );

        OnTransformed?.Invoke(this);

        return true;
    }

    // Nombre pedido para Noelle: libera su forma divina.
    public bool UnlockNoelleDivineForm()
    {
        return UnlockTransformedForm();
    }
}
