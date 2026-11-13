
using System;
using UnityEngine;

public class CharacterProgression : MonoBehaviour
{
    [Header("Configuracion de progresion")]
    [SerializeField]
    private CharacterProgressionData progressionData;

    [Header("Progreso del personaje")]
    [SerializeField, Min(1)]
    private int level = 1;

    [SerializeField, Min(0)]
    private int currentExperience = 0;

    [Header("Limites de la demo")]
    [SerializeField, Min(1)]
    private int maxLevel = 10;

    public int Level => level;

    public int CurrentExperience => currentExperience;

    public int MaxLevel => maxLevel;

    public CharacterProgressionData Data => progressionData;

    // Se ejecuta cuando el personaje sube de nivel.
    public event Action<int> OnLevelUp;

    public int ExperienceRequired
    {
        get
        {
            if (progressionData == null || level >= maxLevel)
                return 0;

            return progressionData.GetExperienceRequired(level);
        }
    }

    private void Awake()
    {
        level = Mathf.Clamp(level, 1, maxLevel);
        currentExperience = Mathf.Max(0, currentExperience);
    }

    // Fija nivel y XP directamente (nueva partida o restauracion de la
    // sesion). No dispara OnLevelUp: quien lo llame debe refrescar las
    // estadisticas de BattleUnit (InitializeUnit).
    public void SetProgress(int newLevel, int newExperience)
    {
        level = Mathf.Clamp(newLevel, 1, maxLevel);
        currentExperience = level >= maxLevel
            ? 0
            : Mathf.Max(0, newExperience);
    }

    public void ResetProgress()
    {
        SetProgress(1, 0);
    }

    // Recibe experiencia de misiones o combates.
    public void GainExperience(int amount)
    {
        if (progressionData == null)
        {
            Debug.LogWarning(
                "No hay configuracion de progresion en " +
                gameObject.name
            );
            return;
        }

        if (amount <= 0 || level >= maxLevel)
            return;

        currentExperience += amount;

        Debug.Log(
            gameObject.name +
            " obtiene " + amount + " XP."
        );

        // Permite subir varios niveles si hay
        // suficiente experiencia acumulada.
        while (level < maxLevel)
        {
            int required = progressionData
                .GetExperienceRequired(level);

            if (currentExperience < required)
                break;

            currentExperience -= required;
            level++;

            Debug.Log(
                gameObject.name +
                " subio al nivel " + level + "!"
            );

            OnLevelUp?.Invoke(level);
        }

        // En el nivel maximo ya no guardamos XP.
        if (level >= maxLevel)
        {
            currentExperience = 0;
        }
    }

    // Bonos de estadisticas por nivel.
    public int HealthBonus
    {
        get
        {
            if (progressionData == null)
                return 0;

            return progressionData.GetHealthBonus(level);
        }
    }

    public int DamageBonus
    {
        get
        {
            if (progressionData == null)
                return 0;

            return progressionData.GetDamageBonus(level);
        }
    }

    public int SpeedBonus
    {
        get
        {
            if (progressionData == null)
                return 0;

            return progressionData.GetSpeedBonus(level);
        }
    }
}
