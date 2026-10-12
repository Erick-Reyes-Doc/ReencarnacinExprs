
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewProgression",
    menuName = "Reencarnacion Express/Combat/Character Progression"
)]
public class CharacterProgressionData : ScriptableObject
{
    [Header("Crecimiento por nivel")]
    [Min(0)] public int healthPerLevel = 5;
    [Min(0)] public int attackPerLevel = 1;
    [Min(0)] public int defensePerLevel = 1;

    [Header("Crecimiento de velocidad")]
    [Min(0)] public int speedIncrease = 1;
    [Min(1)] public int speedEveryLevels = 4;

    [Header("Experiencia")]
    [Min(1)] public int baseExperience = 100;
    [Min(0)] public int experienceIncrease = 50;

    // Experiencia necesaria para pasar del nivel actual
    // al siguiente nivel.
    public int GetExperienceRequired(int currentLevel)
    {
        currentLevel = Mathf.Max(1, currentLevel);

        return baseExperience +
               (currentLevel - 1) * experienceIncrease;
    }

    // Obtiene las estadisticas acumuladas por nivel.
    public int GetHealthBonus(int level)
    {
        return Mathf.Max(0, level - 1) * healthPerLevel;
    }

    public int GetAttackBonus(int level)
    {
        return Mathf.Max(0, level - 1) * attackPerLevel;
    }

    public int GetDefenseBonus(int level)
    {
        return Mathf.Max(0, level - 1) * defensePerLevel;
    }

    public int GetSpeedBonus(int level)
    {
        int levelsGained = Mathf.Max(0, level - 1);

        if (speedIncrease == 0)
            return 0;

        return (levelsGained / speedEveryLevels) *
               speedIncrease;
    }
}
