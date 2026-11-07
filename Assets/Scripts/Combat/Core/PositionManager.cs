
using System.Collections.Generic;
using UnityEngine;

public class PositionManager : MonoBehaviour
{
    [Header("Equipo del jugador")]
    [SerializeField] private List<BattleUnit> playerUnits =
        new List<BattleUnit>();

    [Header("Equipo enemigo")]
    [SerializeField] private List<BattleUnit> enemyUnits =
        new List<BattleUnit>();

    [Header("Puntos visuales del jugador")]
    [SerializeField] private Transform[] playerPositions =
        new Transform[4];

    [Header("Puntos visuales del enemigo")]
    [SerializeField] private Transform[] enemyPositions =
        new Transform[4];

    private void OnDestroy()
    {
        SubscribeToTeam(playerUnits, false);
        SubscribeToTeam(enemyUnits, false);
    }

    // Suscribe (o cancela) la gestion de derrotas.
    // Quitar antes de agregar evita suscripciones duplicadas.
    private void SubscribeToTeam(
        List<BattleUnit> team,
        bool subscribe
    )
    {
        foreach (BattleUnit unit in team)
        {
            if (unit == null)
                continue;

            unit.OnFled -= HandleFledUnit;
            unit.OnDefeated -= HandleDefeatedUnit;
            unit.OnRevived -= HandleRevivedUnit;

            if (subscribe)
            {
                unit.OnDefeated += HandleDefeatedUnit;
                unit.OnRevived += HandleRevivedUnit;
                unit.OnFled += HandleFledUnit;
            }
        }
    }

    // Reincorpora a una unidad revivida a su formacion: vuelve a
    // mostrarse, recupera su posicion si sigue libre (o la primera
    // libre) y el equipo se reorganiza.
    private void HandleRevivedUnit(BattleUnit unit)
    {
        if (unit == null || !unit.IsAlive)
            return;

        SpriteRenderer sprite = unit.GetComponent<SpriteRenderer>();

        if (sprite != null)
            sprite.enabled = true;

        List<BattleUnit> team =
            unit.Side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        if (!team.Contains(unit))
            team.Add(unit);

        // Recupera su lugar original en la formacion (la posicion que
        // tenia al caer); los aliados vivos a partir de ahi retroceden una
        // posicion. Si el hueco no existe, se coloca tras el ultimo vivo.
        int aliveOthers = 0;

        foreach (BattleUnit other in team)
        {
            if (other != null && other != unit && other.IsAlive)
                aliveOthers++;
        }

        int desired = unit.Position;

        if (defeatedAt.TryGetValue(unit, out int slot))
        {
            defeatedAt.Remove(unit);

            // Posicion compactada = lugar original menos los huecos
            // anteriores que siguen ocupados por caidos.
            desired = slot - DeadSlotsBefore(unit.Side, slot);
        }

        desired = Mathf.Clamp(desired, 1, Mathf.Min(4, aliveOthers + 1));

        foreach (BattleUnit other in team)
        {
            if (other != null && other != unit && other.IsAlive &&
                other.Position >= desired)
                other.SetPosition(Mathf.Min(4, other.Position + 1));
        }

        unit.SetPosition(desired);
        ReorganizeTeam(unit.Side);
    }

    // Casilla de la formacion completa (1-4, contando a los caidos) que
    // ocupaba cada unidad derrotada.
    private readonly Dictionary<BattleUnit, int> defeatedAt =
        new Dictionary<BattleUnit, int>();

    private bool IsDeadSlot(TeamSide side, int slot)
    {
        foreach (KeyValuePair<BattleUnit, int> entry in defeatedAt)
        {
            if (entry.Key != null && entry.Key.Side == side &&
                entry.Value == slot)
                return true;
        }

        return false;
    }

    private int DeadSlotsBefore(TeamSide side, int slot)
    {
        int count = 0;

        foreach (KeyValuePair<BattleUnit, int> entry in defeatedAt)
        {
            if (entry.Key != null && entry.Key.Side == side &&
                entry.Value < slot)
                count++;
        }

        return count;
    }

    // Casilla en la formacion completa de una unidad que acaba de caer:
    // la n-esima casilla no ocupada por otros caidos, siendo n su
    // posicion compactada actual.
    private int FullFormationSlot(BattleUnit unit)
    {
        int slot = 0;
        int free = 0;

        while (free < unit.Position && slot < 4)
        {
            slot++;

            if (!IsDeadSlot(unit.Side, slot))
                free++;
        }

        return Mathf.Max(1, slot);
    }

    private void Start()
    {
        SubscribeToTeam(playerUnits, true);
        SubscribeToTeam(enemyUnits, true);

        // Validar las posiciones iniciales.
        ValidateTeam(playerUnits, TeamSide.Player);
        ValidateTeam(enemyUnits, TeamSide.Enemy);

        // Mostrar las formaciones en consola.
        ShowTeamPositions(playerUnits, "JUGADORES");
        ShowTeamPositions(enemyUnits, "ENEMIGOS");

        // Colocar los sprites en sus posiciones.
        UpdateVisualPositions();
    }

    // Comprueba que las posiciones iniciales sean validas.
    private void ValidateTeam(
        List<BattleUnit> team,
        TeamSide expectedSide
    )
    {
        HashSet<int> usedPositions = new HashSet<int>();

        if (team.Count > 4)
        {
            Debug.LogError(
                "Un equipo no puede tener mas de 4 unidades."
            );
        }

        foreach (BattleUnit unit in team)
        {
            if (unit == null)
                continue;

            if (unit.Side != expectedSide)
            {
                Debug.LogError(
                    unit.Data.unitName +
                    " pertenece al equipo equivocado."
                );
            }

            if (unit.Position < 1 || unit.Position > 4)
            {
                Debug.LogError(
                    unit.Data.unitName +
                    " tiene una posicion invalida."
                );
            }

            if (!usedPositions.Add(unit.Position))
            {
                Debug.LogError(
                    "Posicion duplicada: " + unit.Position
                );
            }
        }
    }

    // Muestra la formacion actual en consola.
    private void ShowTeamPositions(
        List<BattleUnit> team,
        string teamName
    )
    {
        Debug.Log("=== FORMACION: " + teamName + " ===");

        List<BattleUnit> orderedTeam =
            new List<BattleUnit>(team);

        orderedTeam.RemoveAll(unit => unit == null);

        orderedTeam.Sort((a, b) =>
            a.Position.CompareTo(b.Position));

        foreach (BattleUnit unit in orderedTeam)
        {
            Debug.Log(
                unit.Data.unitName +
                " - Posicion: " + unit.Position
            );
        }
    }

    // Obtiene la unidad viva en una posicion determinada.
    public BattleUnit GetUnitAtPosition(
        TeamSide side,
        int position
    )
    {
        List<BattleUnit> team =
            side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        foreach (BattleUnit unit in team)
        {
            if (unit != null &&
                unit.IsAlive &&
                unit.Position == position)
            {
                return unit;
            }
        }

        return null;
    }

    // Reacomoda a los sobrevivientes hacia el frente.
    public void ReorganizeTeam(TeamSide side)
    {
        List<BattleUnit> team =
            side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        // Eliminar las unidades derrotadas de la formacion.
        team.RemoveAll(unit =>
            unit == null || !unit.IsAlive);

        // Ordenar por posicion actual.
        team.Sort((a, b) =>
            a.Position.CompareTo(b.Position));

        // Asignar posiciones consecutivas.
        for (int i = 0; i < team.Count; i++)
        {
            team[i].SetPosition(i + 1);
        }

        ShowTeamPositions(
            team,
            side.ToString()
        );

        // Actualizar la ubicacion visual.
        UpdateVisualPositions();
    }

    
public void HandleDefeatedUnit(BattleUnit unit)
{
    if (unit == null || unit.IsAlive)
        return;

    Debug.Log(
        unit.Data.unitName + " ha sido derrotado."
    );

    // Ocultar al personaje derrotado.
    SpriteRenderer sprite =
        unit.GetComponent<SpriteRenderer>();

    if (sprite != null)
    {
        sprite.enabled = false;
    }

    // Recordar su lugar en la formacion completa (contando los huecos de
    // los caidos) para poder restaurarlo si revive.
    defeatedAt[unit] = FullFormationSlot(unit);

    // Reorganizar al equipo correspondiente.
    ReorganizeTeam(unit.Side);
}


    // Un enemigo que huye desaparece del campo y de la formacion, sin mensaje de derrota (no es una baja).
    private void HandleFledUnit(BattleUnit unit)
    {
        if (unit == null)
            return;

        SpriteRenderer sprite = unit.GetComponent<SpriteRenderer>();

        if (sprite != null)
            sprite.enabled = false;

        ReorganizeTeam(unit.Side);
    }

    // Actualiza las posiciones visuales de ambos equipos.
    public void UpdateVisualPositions()
    {
        UpdateTeamVisualPositions(
            playerUnits,
            playerPositions
        );

        UpdateTeamVisualPositions(
            enemyUnits,
            enemyPositions
        );
    }

    // Coloca cada unidad en su punto visual correspondiente.
    private void UpdateTeamVisualPositions(
        List<BattleUnit> team,
        Transform[] positions
    )
    {
        foreach (BattleUnit unit in team)
        {
            if (unit == null || !unit.IsAlive)
                continue;

            // Convertir posicion 1-4 a indice 0-3.
            int index = unit.Position - 1;

            if (index < 0 || index >= positions.Length)
            {
                Debug.LogWarning(
                    "Posicion visual fuera de rango: " +
                    unit.Position
                );

                continue;
            }

            Transform targetPosition = positions[index];

            if (targetPosition == null)
            {
                Debug.LogWarning(
                    "Falta configurar el punto visual " +
                    unit.Position
                );

                continue;
            }

            // Mover el GameObject a su posicion asignada.
            unit.transform.position = targetPosition.position;
        }
    }

    
    // Anade una unidad a su formacion (carga de encuentros). Se rechaza si
    // ya esta registrada, si su posicion es invalida o si esta ocupada.
    public bool RegisterUnit(BattleUnit unit)
    {
        if (unit == null || unit.Position < 1 || unit.Position > 4)
            return false;

        List<BattleUnit> team =
            unit.Side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        if (team.Contains(unit) ||
            GetUnitAtPosition(unit.Side, unit.Position) != null)
            return false;

        team.Add(unit);

        unit.OnDefeated -= HandleDefeatedUnit;
        unit.OnDefeated += HandleDefeatedUnit;
        unit.OnFled -= HandleFledUnit;
        unit.OnFled += HandleFledUnit;
        unit.OnRevived -= HandleRevivedUnit;
        unit.OnRevived += HandleRevivedUnit;

        UpdateVisualPositions();

        return true;
    }

    // Vacia la formacion de un equipo (carga de encuentros).
    public void ClearTeam(TeamSide side)
    {
        List<BattleUnit> team =
            side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        foreach (BattleUnit unit in team)
        {
            if (unit != null)
            {
                unit.OnFled -= HandleFledUnit;
                unit.OnDefeated -= HandleDefeatedUnit;
                unit.OnRevived -= HandleRevivedUnit;
            }
        }

        // Los caidos de este equipo ya no cuentan para la formacion.
        List<BattleUnit> stale = new List<BattleUnit>();

        foreach (BattleUnit key in defeatedAt.Keys)
        {
            if (key == null || key.Side == side)
                stale.Add(key);
        }

        foreach (BattleUnit key in stale)
            defeatedAt.Remove(key);

        team.Clear();
    }

    // Comprueba, sin modificar nada, si MoveUnit() tendria exito.
    public bool CanMoveUnit(BattleUnit unit, int direction)
    {
        if (unit == null || !unit.IsAlive)
            return false;

        if (direction != -1 && direction != 1)
            return false;

        List<BattleUnit> team =
            unit.Side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        if (!team.Contains(unit))
            return false;

        int targetPosition = unit.Position + direction;

        return targetPosition >= 1 && targetPosition <= 4;
    }

    // Mueve una unidad una posicion adelante o atras.
    // direction: -1 = avanzar, +1 = retroceder.
    public bool MoveUnit(BattleUnit unit, int direction)
    {
        if (unit == null || !unit.IsAlive)
        {
            Debug.LogWarning("La unidad no puede moverse.");
            return false;
        }

        if (direction != -1 && direction != 1)
        {
            Debug.LogWarning("Direccion no valida.");
            return false;
        }

        // Comprobar que pertenece a la formacion.
        List<BattleUnit> team =
            unit.Side == TeamSide.Player
            ? playerUnits
            : enemyUnits;

        if (!team.Contains(unit))
        {
            Debug.LogWarning(
                "La unidad no pertenece a este combate."
            );
            return false;
        }

        int originalPosition = unit.Position;
        int targetPosition = originalPosition + direction;

        // Validar los limites de la formacion.
        if (targetPosition < 1 || targetPosition > 4)
        {
            Debug.Log(
                "No es posible moverse fuera de la formacion."
            );
            return false;
        }

        // Buscar si otro aliado ocupa el destino.
        BattleUnit otherUnit = GetUnitAtPosition(
            unit.Side,
            targetPosition
        );

        if (otherUnit != null && otherUnit != unit)
        {
            // Intercambiar posiciones.
            otherUnit.SetPosition(originalPosition);

            Debug.Log(
                unit.Data.unitName +
                " intercambia posicion con " +
                otherUnit.Data.unitName
            );
        }

        // Mover la unidad a la nueva posicion.
        unit.SetPosition(targetPosition);

        // Actualizar los sprites.
        UpdateVisualPositions();

        Debug.Log(
            unit.Data.unitName +
            " ahora esta en posicion " +
            unit.Position
        );

        return true;
    }

}
