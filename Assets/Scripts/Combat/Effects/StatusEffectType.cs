// Estados alterados disponibles en combate.
// Los valores nuevos se agregan siempre al final para no alterar
// los enums ya serializados en assets y escenas.
public enum StatusEffectType
{
    None,
    Bleeding,
    Mark,
    Regeneration,
    Stun,
    Burn,
    Weaken,
    Vulnerability,
    Poison,

    // Aumenta la defensa efectiva (potencia = porcentaje).
    DefenseBoost,

    // Reduce el daño directo recibido (potencia = porcentaje) y
    // puede proteger a un aliado. Ver StatusEffectController.ApplyGuard.
    Guard,

    // Marca tactica del Capitan: el objetivo recibe mas daño directo,
    // pero solo del atacante que la aplico (StatusEffectInstance.Source).
    PriorityTarget
}
