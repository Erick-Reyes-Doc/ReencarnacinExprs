
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

    private void Start()
    {
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

    // Reorganizar al equipo correspondiente.
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
