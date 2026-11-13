
using UnityEngine;
using UnityEngine.InputSystem;

public class ExperienceTest : MonoBehaviour
{
    [Header("Personajes que reciben experiencia")]
    [SerializeField]
    private CharacterProgression[] characters;

    [Header("Experiencia de prueba")]
    [SerializeField, Min(1)]
    private int experienceAmount = 50;

    private void Update()
    {
        // Presiona X para dar experiencia al equipo.
        if (Keyboard.current != null &&
            Keyboard.current.xKey.wasPressedThisFrame)
        {
            GiveExperience();
        }
    }

    private void GiveExperience()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning(
                "No hay personajes asignados."
            );
            return;
        }

        Debug.Log(
            "=== RECOMPENSA DE EXPERIENCIA ==="
        );

        foreach (CharacterProgression character in characters)
        {
            if (character == null)
                continue;

            character.GainExperience(experienceAmount);

            Debug.Log(
                character.gameObject.name +
                " | Nivel: " + character.Level +
                " | XP: " + character.CurrentExperience +
                "/" + character.ExperienceRequired
            );

            Debug.Log(
                "Bonos acumulados: " +
                "HP +" + character.HealthBonus +
                ", daño +" + character.DamageBonus +
                ", VEL +" + character.SpeedBonus
            );
        }
    }
}
