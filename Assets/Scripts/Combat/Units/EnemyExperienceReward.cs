using UnityEngine;

// Recompensa de experiencia que otorga un enemigo
// al ser derrotado el encuentro.
public class EnemyExperienceReward : MonoBehaviour
{
    [Header("Recompensa")]
    [SerializeField, Min(0)]
    private int experienceReward = 0;

    public int ExperienceReward => experienceReward;
}
