using System;
using UnityEngine;

// Controla las fases de un jefe. Se agrega solo a jefes; los enemigos comunes
// no lo tienen y no se ven afectados.
//
// Deteccion del umbral: no consulta cada frame. Escucha BattleUnit.DamageApplied
// (evento que BattleUnit ya emite al perder vida) y solo reevalua cuando el
// dano es de esta unidad. Una fase solo avanza (nunca retrocede), cada una
// se activa una sola vez, y no se activa si el dano derroto al jefe.
// No cura, no reinicia cooldowns, no toca estados ni da acciones: solo
// cambia la fase actual y avisa al HUD/IA.
[RequireComponent(typeof(BattleUnit))]
public class BossPhaseController : MonoBehaviour
{
    [SerializeField] private BossPhaseData data;

    private BattleUnit unit;
    private int currentIndex;

    // Cambio de fase: (jefe, fase anterior, fase nueva). Una sola vez por fase.
    public static event Action<BossPhaseController, BossPhaseData.Phase, BossPhaseData.Phase> PhaseChanged;

    public int CurrentIndex => currentIndex;

    public BossPhaseData.Phase Current =>
        data != null && currentIndex < data.phases.Count ? data.phases[currentIndex] : null;

    public BattleUnit Unit
    {
        get
        {
            if (unit == null)
                unit = GetComponent<BattleUnit>();

            return unit;
        }
    }

    private void OnEnable()
    {
        BattleUnit.DamageApplied += HandleDamage;
        currentIndex = 0;
    }

    private void OnDisable()
    {
        BattleUnit.DamageApplied -= HandleDamage;
    }

    // Para pruebas o para asignar datos desde codigo.
    public void Configure(BossPhaseData newData)
    {
        data = newData;
        currentIndex = 0;
    }

    private void HandleDamage(BattleUnit target, int damage, bool periodic)
    {
        if (target != Unit || damage <= 0)
            return;

        Evaluate();
    }

    private void Evaluate()
    {
        if (data == null || !Unit.IsAlive)
            return;   // si el dano lo derroto no hay fase nueva

        // Puede cruzar mas de un umbral con un solo golpe: se avanzan en orden.
        while (currentIndex + 1 < data.phases.Count)
        {
            BossPhaseData.Phase next = data.phases[currentIndex + 1];

            // Enteros: HP/max <= umbral/100 sin redondeos (190/380 = 50 %).
            if (Unit.CurrentHealth * 100 > next.hpThresholdPercent * Unit.MaxHealth)
                break;

            BossPhaseData.Phase previous = Current;
            currentIndex++;

            Debug.Log(
                Unit.Data.unitName + " entra en la fase " + (currentIndex + 1) +
                ": " + next.phaseName + " (HP " + Unit.CurrentHealth + "/" +
                Unit.MaxHealth + ")."
            );

            PhaseChanged?.Invoke(this, previous, next);
        }
    }

    // ------------------------------------------------------------
    // Consultas para la IA
    // ------------------------------------------------------------

    // Una habilidad listada como exclusiva de una fase solo esta disponible
    // en esa fase. Las demas habilidades estan disponibles siempre.
    public bool IsAbilityAvailable(AbilityData ability)
    {
        if (data == null || ability == null)
            return true;

        bool exclusive = false;

        for (int i = 0; i < data.phases.Count; i++)
        {
            if (!data.phases[i].exclusiveAbilities.Contains(ability))
                continue;

            if (i == currentIndex)
                return true;

            exclusive = true;
        }

        return !exclusive;
    }

    public BossPhaseData.AiProfile AiProfile =>
        Current != null ? Current.ai : null;
}
