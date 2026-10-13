
using UnityEngine;

public class BattleTest : MonoBehaviour
{
    [Header("Unidades de combate")]
    [SerializeField] private BattleUnit player;
    [SerializeField] private BattleUnit enemy;

    private void Start()
    {
        if (player == null || enemy == null)
        {
            Debug.LogError(
                "Debes asignar al jugador y al enemigo."
            );
            return;
        }

        Debug.Log("=== INICIO DE PRUEBA ===");

        // El caballero ataca al enemigo.
        PerformAttack(player, enemy);

        // El enemigo responde si sigue vivo.
        if (enemy.IsAlive)
        {
            PerformAttack(enemy, player);
        }

        Debug.Log("=== FIN DE PRUEBA ===");
    }

    private void PerformAttack(
        BattleUnit attacker,
        BattleUnit target
    )
    {
        if (!attacker.IsAlive || !target.IsAlive)
            return;

        // Daño base considerando la defensa.
        int damage = Mathf.Max(
            1,
            attacker.Attack - target.Defense
        );

        // Determinar si ocurre un golpe crítico.
        bool isCritical =
            Random.Range(0f, 100f) <
            attacker.CriticalChance;

        if (isCritical)
        {
            damage = Mathf.RoundToInt(
                damage * attacker.CriticalDamage
            );
        }

        Debug.Log(
            attacker.Data.unitName +
            " ataca a " +
            target.Data.unitName +
            (isCritical ? " ¡CRITICO!" : "")
        );

        target.TakeDirectDamage(damage);
    }
}
