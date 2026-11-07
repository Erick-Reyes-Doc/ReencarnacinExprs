
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewProgression",
    menuName = "Reencarnacion Express/Combat/Character Progression"
)]
public class CharacterProgressionData : ScriptableObject
{
    [Header("Crecimiento por nivel")]
    [Min(0)] public int healthPerLevel = 1;

    [Tooltip("Niveles en los que el daño minimo y maximo suben 1 punto.")]
    public int[] damageBonusLevels = { 4, 7, 10 };

    [Header("Crecimiento de velocidad")]
    [Min(0)] public int speedIncrease = 0;
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

    // +1 al daño minimo y maximo por cada nivel de la lista alcanzado.
    public int GetDamageBonus(int level)
    {
        int bonus = 0;

        if (damageBonusLevels != null)
            foreach (int threshold in damageBonusLevels)
                if (level >= threshold)
                    bonus++;

        return bonus;
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
