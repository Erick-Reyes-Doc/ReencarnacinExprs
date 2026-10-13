using UnityEngine;
using UnityEngine.InputSystem;

// Prueba temporal de depuracion: durante el turno de un protagonista,
// B Sangrado, N Aturdimiento, M Marca, K Debilitamiento y
// V Vulnerabilidad al enemigo seleccionado.
// Ninguna tecla consume el turno.
public class StatusEffectTest : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private TargetSelector targetSelector;

    [Header("Sangrado de prueba")]
    [SerializeField, Min(1)] private int potency = 4;
    [SerializeField, Min(1)] private int duration = 3;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // B = aplicar Sangrado de prueba.
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            ApplyTestEffect(
                StatusEffectType.Bleeding, potency, duration, "Sangrado"
            );
        }

        // N = aplicar Aturdimiento de prueba (potencia 1, duracion 1).
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            ApplyTestEffect(
                StatusEffectType.Stun, 1, 1, "Aturdimiento"
            );
        }

        // M = Marca (+20% daño directo, 3 turnos).
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            ApplyTestEffect(
                StatusEffectType.Mark, 20, 3, "Marca"
            );
        }

        // K = Debilitamiento (-20% ataque, 2 turnos).
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            ApplyTestEffect(
                StatusEffectType.Weaken, 20, 2, "Debilitamiento"
            );
        }

        // V = Vulnerabilidad (-25% defensa, 2 turnos).
        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            ApplyTestEffect(
                StatusEffectType.Vulnerability, 25, 2, "Vulnerabilidad"
            );
        }
    }

    private void ApplyTestEffect(
        StatusEffectType type,
        int effectPotency,
        int effectDuration,
        string label
    )
    {
        if (turnManager == null || targetSelector == null)
        {
            Debug.LogWarning("Faltan referencias en StatusEffectTest.");
            return;
        }

        BattleUnit user = turnManager.CurrentUnit;

        if (user == null || !user.IsAlive ||
            user.Side != TeamSide.Player)
        {
            Debug.Log("Solo funciona en el turno de un protagonista.");
            return;
        }

        BattleUnit target = targetSelector.SelectedTarget;

        if (target == null)
        {
            Debug.Log("Selecciona un enemigo antes de aplicar " + label + ".");
            return;
        }

        if (!target.IsAlive || target.Side != TeamSide.Enemy)
        {
            Debug.Log("El objetivo debe ser un enemigo vivo.");
            return;
        }

        StatusEffectController controller =
            target.GetComponent<StatusEffectController>();

        if (controller == null)
        {
            Debug.LogWarning(
                target.Data.unitName +
                " no tiene StatusEffectController."
            );
            return;
        }

        if (controller.ApplyEffect(
                type, effectPotency, effectDuration, user))
        {
            Debug.Log(
                user.Data.unitName + " aplica " + label + " (" +
                effectPotency + " x " + effectDuration + ") a " +
                target.Data.unitName + ". El turno continua."
            );
        }
    }
}
