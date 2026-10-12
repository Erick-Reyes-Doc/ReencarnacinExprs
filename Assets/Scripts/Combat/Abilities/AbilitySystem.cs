
using UnityEngine;

public class AbilitySystem : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;

    // Comprueba si una habilidad puede utilizarse.
    public bool CanUseAbility(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability
    )
    {
        // Comprobar referencias.
        if (user == null || target == null || ability == null)
        {
            Debug.LogWarning(
                "Faltan datos para ejecutar la habilidad."
            );
            return false;
        }

        // Comprobar que ambos personajes esten vivos.
        if (!user.IsAlive || !target.IsAlive)
        {
            Debug.LogWarning(
                "El usuario o el objetivo esta derrotado."
            );
            return false;
        }

        // Comprobar que sea el turno del usuario.
        if (turnManager == null ||
            turnManager.CurrentUnit != user)
        {
            Debug.LogWarning(
                "No es el turno de este personaje."
            );
            return false;
        }

        // Validar posicion del usuario.
        if (!ability.CanUseFromPosition(user.Position))
        {
            Debug.Log(
                user.Data.unitName +
                " no puede usar " +
                ability.abilityName +
                " desde la posicion " +
                user.Position
            );

            return false;
        }

        // Validar posicion del objetivo.
        if (!ability.CanTargetPosition(target.Position))
        {
            Debug.Log(
                "La posicion " +
                target.Position +
                " no es un objetivo valido."
            );

            return false;
        }

        // Validar el equipo objetivo.
        if (ability.abilityType == AbilityType.Attack)
        {
            if (user.Side == target.Side)
            {
                Debug.LogWarning(
                    "No puedes atacar a un aliado."
                );
                return false;
            }
        }
        else
        {
            // Curacion y apoyo: solo aliados.
            if (user.Side != target.Side)
            {
                Debug.LogWarning(
                    "Esta habilidad solo puede usarse en aliados."
                );
                return false;
            }
        }

        return true;
    }

    // Ejecuta una habilidad previamente validada.
    public bool ExecuteAbility(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability
    )
    {
        if (!CanUseAbility(user, target, ability))
        {
            return false;
        }

        Debug.Log(
            user.Data.unitName +
            " utiliza la habilidad: " +
            ability.abilityName +
            " sobre " +
            target.Data.unitName
        );
        // Aplicar el efecto principal de la habilidad.
if (ability.abilityType == AbilityType.Attack)
{
    int damage = DamageCalculator.CalculateDamage(
        user,
        target,
        out bool isCritical
    );

    // La potencia de la habilidad aumenta el daño.
    damage += ability.basePower;

    // Si la habilidad no permite criticos,
    // recalculamos el daño sin ese multiplicador.
    if (!ability.canCritical && isCritical)
    {
        int normalDamage = Mathf.Max(
            1,
            user.Attack - target.Defense
        );

        damage = normalDamage + ability.basePower;
        isCritical = false;
    }

    Debug.Log(
        ability.abilityName +
        " causa " + damage + " de daño." +
        (isCritical ? " ¡CRITICO!" : "")
    );

    target.TakeDamage(damage);
}

        // Pendiente:
        // Aplicar daño, curacion o apoyo.
        // Aplicar desplazamientos.
        // Aplicar cooldowns.
        // Integrar estados alterados.

        // La habilidad aun no consume turno,
        // porque todavia no ejecuta sus efectos.
        return true;
    }
}
