
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
        int damage = DamageCalculator.CalculateDamage(attacker, target, out bool _);

        bool isCritical = false;

        Debug.Log(
            attacker.Data.unitName +
            " ataca a " +
            target.Data.unitName +
            (isCritical ? " ¡CRITICO!" : "")
        );

        target.TakeDirectDamage(damage);
    }
}
