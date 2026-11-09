using System.Collections.Generic;
using UnityEngine;

// Efectos de comportamiento de las reliquias (las 12 del Codice). Los modificadores de estadisticas (bonificaciones y
// desventajas) los sigue calculando RelicInventory.GetMods; aqui viven los efectos que se disparan durante el combate.
//
// Es estatico y solo guarda estado DENTRO de un combate (se limpia en TurnManager.StartBattle): usos "una vez por combate",
// "una vez por turno" y "una vez por ronda". Solo actua para los protagonistas con la reliquia EQUIPADA en la partida en
// curso; los enemigos y las pruebas sin sesion nunca activan nada. Los sistemas de combate llaman a estos ganchos:
//   BattleUnit.TakeDamage / Speed, AbilitySystem (golpe directo, curacion, intercepcion, aturdimiento), ConsumableService,
//   AbilityCooldownController y TurnManager (inicio de turno / de combate).
public static class RelicEffects
{
    // Estado conocido del objetivo ANTES de recibir un golpe (los estados que aplica el propio golpe no cuentan).
    public struct PreHit
    {
        public bool bleeding;
        public bool poison;
        public bool burn;
        public bool mark;
        public BattleUnit markSource;   // quien aplico la Marca (Ojo del Acechador)
        public int markPotency;
        public int markTurns;
    }

    private class State
    {
        public bool cortezaUsed, idoloUsed, relojUsed;
        public bool colmilloTurn, campanaTurn, selloTurn, carbonTurn, fragmentoTurn;
        public bool espejoArmed;
        public int espejoLastRound = -1;
        public int espejoSeenTurns;
        public int insigniaRound = -1;
        public int botasTurnsLeft;
        public bool botasSkipEnd;

        // Segunda tanda
        public bool anilloArmed;
        public int anilloRound = -1;
        public bool dealtThisTurn;
        public bool brocheActive;
        public int espinaRound = -1;
        public bool dadoUsed;
        public bool ojoUsed;
        public int cadenaRound = -1;
        public bool rosarioTurn;
    }

    private static readonly Dictionary<BattleUnit, State> states = new Dictionary<BattleUnit, State>();
    private static int round;
    private static BattleUnit lastTurnUnit;

    private static State Of(BattleUnit unit)
    {
        if (!states.TryGetValue(unit, out State state))
        {
            state = new State();
            states[unit] = state;
        }

        return state;
    }

    // El portador es un protagonista con la reliquia equipada.
    public static bool Has(BattleUnit unit, RelicEffect effect)
    {
        if (unit == null)
            return false;

        // Enemigos con reliquia sorteada para este combate (EnemyRelics) o heroes con ella equipada.
        if (unit.Side == TeamSide.Enemy)
            return EnemyRelics.HasEffect(unit, effect);

        return unit.Side == TeamSide.Player && GameSession.IsActive &&
               RelicInventory.HasEffect(unit.gameObject.name, effect);
    }

    // ------------------------------------------------------------
    // Ciclo del combate
    // ------------------------------------------------------------

    public static void ResetCombat()
    {
        states.Clear();
        round = 0;
        lastTurnUnit = null;
    }

    // Inicio del turno de 'unit': cierra el turno anterior y reinicia los usos "una vez por turno".
    public static void OnTurnStart(BattleUnit unit, int currentRound)
    {
        round = currentRound;

        if (lastTurnUnit != null)
            EndTurnOf(lastTurnUnit);

        lastTurnUnit = unit;

        if (unit == null)
            return;

        State state = Of(unit);
        state.colmilloTurn = false;
        state.campanaTurn = false;
        state.selloTurn = false;
        state.carbonTurn = false;
        state.fragmentoTurn = false;
        state.dealtThisTurn = false;
        state.rosarioTurn = false;

        // Broche del Vigia: la Proteccion dura "hasta tu siguiente turno": se retira al empezar el turno.
        if (state.brocheActive)
        {
            state.brocheActive = false;
            StatusEffectController own = unit.GetComponent<StatusEffectController>();
            StatusEffectInstance boost = own != null ? own.GetEffect(StatusEffectType.DefenseBoost) : null;

            if (boost != null && boost.Potency == 15)
                own.RemoveEffect(StatusEffectType.DefenseBoost);
        }

        // Velo de Ascuas: al comenzar su turno, si hay algun enemigo con Quemadura, recupera 2 HP.
        if (unit.IsAlive && Has(unit, RelicEffect.VeloAscuas))
        {
            foreach (BattleUnit other in Object.FindObjectsByType<BattleUnit>())
            {
                if (other.Side == unit.Side || !other.IsAlive)
                    continue;

                StatusEffectController otherFx = other.GetComponent<StatusEffectController>();

                if (otherFx != null && otherFx.HasEffect(StatusEffectType.Burn))
                {
                    unit.Heal(2);
                    Debug.Log(unit.Data.unitName + " recupera 2 HP al ver a un enemigo arder (Velo de Ascuas).");
                    break;
                }
            }
        }
    }

    private static void EndTurnOf(BattleUnit unit)
    {
        if (!states.TryGetValue(unit, out State state))
            return;

        // Broche del Vigia: si el turno termino sin causar daño directo, 15 % de Proteccion hasta el siguiente turno.
        if (!state.dealtThisTurn && unit.IsAlive && Has(unit, RelicEffect.BrocheVigia))
        {
            StatusEffectController own = unit.GetComponent<StatusEffectController>();

            if (own != null && own.ApplyEffect(StatusEffectType.DefenseBoost, 15, 2, unit))
            {
                state.brocheActive = true;
                Debug.Log(unit.Data.unitName + " obtiene 15 % de Protección por no atacar (Broche del Vigía).");
            }
        }

        // Espejo del Dolor: el bono se pierde si no se ataca en el turno siguiente al daño recibido.
        if (state.espejoArmed)
        {
            state.espejoSeenTurns++;

            if (state.espejoSeenTurns >= 1)
            {
                state.espejoArmed = false;
                Debug.Log(unit.Data.unitName + ": el bonus del Espejo del Dolor se desvanece (no atacó en su turno).");
            }
        }

        // Botas del Mensajero: el turno en que se usa el consumible no cuenta; cuentan los dos siguientes.
        if (state.botasTurnsLeft > 0)
        {
            if (state.botasSkipEnd)
                state.botasSkipEnd = false;
            else
                state.botasTurnsLeft--;
        }
    }

    // Velocidad extra temporal (Botas del Mensajero).
    public static int TempSpeed(BattleUnit unit)
    {
        return unit != null && states.TryGetValue(unit, out State state) && state.botasTurnsLeft > 0 ? 2 : 0;
    }

    // ------------------------------------------------------------
    // Daño recibido
    // ------------------------------------------------------------

    // Antes de aplicar un golpe directo. Idolo del Ultimo Aliento: el primer golpe directo que deberia derrotar al
    // portador lo deja con 1 HP (una vez por combate) y despues sufre Debilitamiento -20 % durante 1 turno. No protege
    // del daño periodico (este gancho solo se llama para daño directo).
    public static int ModifyIncomingDirect(BattleUnit unit, int damage)
    {
        if (damage <= 0 || !Has(unit, RelicEffect.IdoloUltimoAliento))
            return damage;

        State state = Of(unit);

        if (state.idoloUsed || damage < unit.CurrentHealth)
            return damage;

        state.idoloUsed = true;
        int survived = Mathf.Max(0, unit.CurrentHealth - 1);

        Debug.Log(unit.Data.unitName + " resiste con 1 HP gracias al Ídolo del Último Aliento (daño " + damage + " -> " + survived + ").");

        StatusEffectController statuses = unit.GetComponent<StatusEffectController>();

        if (statuses != null)
            statuses.ApplyEffect(StatusEffectType.Weaken, 20, 1, unit);

        return survived;
    }

    // Despues de que 'unit' (viva) recibio un golpe directo con 'lost' HP perdidos.
    public static void OnDirectDamageTaken(BattleUnit unit, int lost)
    {
        if (lost <= 0 || unit == null)
            return;

        // Amuleto de Corteza: por debajo del 50 % de vida, 10 % de Proteccion durante 1 turno (una vez por combate).
        if (Has(unit, RelicEffect.AmuletoCorteza))
        {
            State state = Of(unit);

            if (!state.cortezaUsed && unit.CurrentHealth * 2 < unit.MaxHealth)
            {
                state.cortezaUsed = true;
                StatusEffectController statuses = unit.GetComponent<StatusEffectController>();

                if (statuses != null && statuses.ApplyEffect(StatusEffectType.DefenseBoost, 10, 1, unit))
                    Debug.Log(unit.Data.unitName + " obtiene 10 % de Protección (Amuleto de Corteza).");
            }
        }

        // Espejo del Dolor: tras recibir daño directo, su siguiente ataque directo acertado hace +4 (maximo una vez por ronda).
        if (Has(unit, RelicEffect.EspejoDolor))
        {
            State state = Of(unit);

            if (state.espejoLastRound != round)
            {
                state.espejoLastRound = round;
                state.espejoArmed = true;
                state.espejoSeenTurns = lastTurnUnit == unit ? -1 : 0;   // si ya es su turno, el "siguiente" es el proximo
                Debug.Log(unit.Data.unitName + " acumula dolor: su próximo ataque directo hará +4 (Espejo del Dolor).");
            }
        }
    }

    // Espina del Juramento: la primera vez en cada ronda que 'unit' recibe daño directo, el atacante sufre Sangrado 1 x 2 turnos.
    public static void OnHitBy(BattleUnit unit, BattleUnit attacker, int lost)
    {
        if (lost <= 0 || attacker == null || !attacker.IsAlive || !Has(unit, RelicEffect.EspinaJuramento))
            return;

        State state = Of(unit);

        if (state.espinaRound == round)
            return;

        state.espinaRound = round;
        StatusEffectController fx = attacker.GetComponent<StatusEffectController>();

        if (fx == null)
            fx = attacker.gameObject.AddComponent<StatusEffectController>();

        if (fx.ApplyEffect(StatusEffectType.Bleeding, 1, 2, unit))
            Debug.Log(attacker.Data.unitName + " sangra 1 HP por 2 turnos al golpear a " + unit.Data.unitName + " (Espina del Juramento).");
    }

    // Cadena del Bastion: cuando el protector intercepta un golpe a un aliado, otro aliado gana 10 % de Proteccion 1 turno
    // (una vez por ronda). Se elige al aliado mas herido entre los que no son ni el protector ni el protegido.
    public static void OnProtected(BattleUnit protector, BattleUnit protectedAlly)
    {
        if (!Has(protector, RelicEffect.CadenaBastion))
            return;

        State state = Of(protector);

        if (state.cadenaRound == round)
            return;

        BattleUnit best = null;
        float bestRatio = 2f;

        foreach (BattleUnit ally in Object.FindObjectsByType<BattleUnit>())
        {
            if (ally.Side != protector.Side || !ally.IsAlive || ally == protector || ally == protectedAlly)
                continue;

            float ratio = ally.CurrentHealth / (float)Mathf.Max(1, ally.MaxHealth);

            if (ratio < bestRatio)
            {
                bestRatio = ratio;
                best = ally;
            }
        }

        if (best == null)
            return;

        StatusEffectController fx = best.GetComponent<StatusEffectController>();

        if (fx != null && fx.ApplyEffect(StatusEffectType.DefenseBoost, 10, 1, protector))
        {
            state.cadenaRound = round;
            Debug.Log(best.Data.unitName + " obtiene 10 % de Protección (Cadena del Bastión).");
        }
    }

    // Anillo del Segundo Intento: un ataque directo fallado (esquivado) prepara +2 de daño para el siguiente que acierte
    // (se prepara como maximo una vez por ronda).
    public static void OnDirectAttackMissed(BattleUnit attacker)
    {
        if (!Has(attacker, RelicEffect.AnilloSegundoIntento))
            return;

        State state = Of(attacker);

        if (state.anilloRound == round)
            return;

        state.anilloRound = round;
        state.anilloArmed = true;
        Debug.Log(attacker.Data.unitName + " falló: su próximo ataque directo acertado hará +2 (Anillo del Segundo Intento).");
    }

    // Ojo del Acechador: una unidad con Marca muere de forma que la Marca sigue puesta (daño periodico): se transfiere.
    public static void OnUnitDefeated(BattleUnit unit)
    {
        StatusEffectController fx = unit != null ? unit.GetComponent<StatusEffectController>() : null;
        StatusEffectInstance mark = fx != null ? fx.GetEffect(StatusEffectType.Mark) : null;

        if (mark != null)
            TransferMark(unit, mark.Source, mark.Potency, mark.RemainingTurns);
    }

    // La Marca de un enemigo caido pasa a otro enemigo vivo sin Marca (una vez por combate, solo si quien la aplico la lleva).
    private static void TransferMark(BattleUnit dead, BattleUnit source, int potency, int turns)
    {
        if (dead == null || source == null || !Has(source, RelicEffect.OjoAcechador))
            return;

        State state = Of(source);

        if (state.ojoUsed)
            return;

        BattleUnit next = null;

        foreach (BattleUnit other in Object.FindObjectsByType<BattleUnit>())
        {
            if (other == dead || other.Side != dead.Side || !other.IsAlive)
                continue;

            StatusEffectController otherFx = other.GetComponent<StatusEffectController>();

            if (otherFx != null && otherFx.HasEffect(StatusEffectType.Mark))
                continue;

            if (next == null || other.Position < next.Position)
                next = other;
        }

        if (next == null)
            return;

        StatusEffectController nextFx = next.GetComponent<StatusEffectController>();

        if (nextFx == null)
            nextFx = next.gameObject.AddComponent<StatusEffectController>();

        if (nextFx.ApplyEffect(StatusEffectType.Mark, potency, Mathf.Max(1, turns), source))
        {
            state.ojoUsed = true;
            Debug.Log("La Marca pasa de " + dead.Data.unitName + " a " + next.Data.unitName + " (Ojo del Acechador).");
        }
    }

    // Insignia de la Trinchera: el protector que intercepta devuelve 30 % del daño que realmente recibio (min 2, max 8).
    public static void OnIntercept(BattleUnit protector, BattleUnit attacker, int lostHp)
    {
        if (lostHp <= 0 || attacker == null || !attacker.IsAlive || !Has(protector, RelicEffect.InsigniaTrinchera))
            return;

        State state = Of(protector);

        if (state.insigniaRound == round)
            return;

        state.insigniaRound = round;
        int reflected = Mathf.Clamp(Mathf.RoundToInt(lostHp * 0.30f), 2, 8);
        Debug.Log(protector.Data.unitName + " devuelve " + reflected + " de daño a " + attacker.Data.unitName + " (Insignia de la Trinchera).");
        attacker.TakeDamage(reflected, true);   // no es un golpe directo nuevo: no dispara reliquias del atacante
    }

    // ------------------------------------------------------------
    // Golpes directos del portador
    // ------------------------------------------------------------

    public static PreHit Snapshot(BattleUnit target)
    {
        PreHit pre = default;

        if (target == null)
            return pre;

        StatusEffectController statuses = target.GetComponent<StatusEffectController>();

        if (statuses == null)
            return pre;

        pre.bleeding = statuses.HasEffect(StatusEffectType.Bleeding);
        pre.poison = statuses.HasEffect(StatusEffectType.Poison);
        pre.burn = statuses.HasEffect(StatusEffectType.Burn);
        pre.mark = statuses.HasEffect(StatusEffectType.Mark);

        if (pre.mark)
        {
            StatusEffectInstance mark = statuses.GetEffect(StatusEffectType.Mark);
            pre.markSource = mark.Source;
            pre.markPotency = mark.Potency;
            pre.markTurns = mark.RemainingTurns;
        }

        return pre;
    }

    // Daño plano extra de un golpe directo que SI va a acertar (Colmillo de Lobo +2, Espejo del Dolor +4). Consume los usos.
    public static int DirectHitBonus(BattleUnit attacker, BattleUnit target, PreHit pre)
    {
        if (attacker == null || attacker.Side != TeamSide.Player || !GameSession.IsActive)
            return 0;

        int bonus = 0;

        if (Has(attacker, RelicEffect.ColmilloLobo) && (pre.bleeding || pre.poison || pre.burn))
        {
            State state = Of(attacker);

            if (!state.colmilloTurn)
            {
                state.colmilloTurn = true;
                bonus += 2;
                Debug.Log(attacker.Data.unitName + ": +2 de daño contra un enemigo con daño periódico (Colmillo de Lobo).");
            }
        }

        if (Has(attacker, RelicEffect.AnilloSegundoIntento))
        {
            State state = Of(attacker);

            if (state.anilloArmed)
            {
                state.anilloArmed = false;
                bonus += 2;
                Debug.Log(attacker.Data.unitName + ": +2 de daño (Anillo del Segundo Intento).");
            }
        }

        // Dado del Ocaso: en el primer ataque directo acertado del combate, 50 % +3 de daño o 50 % 15 % de Proteccion 1 turno.
        if (Has(attacker, RelicEffect.DadoOcaso))
        {
            State state = Of(attacker);

            if (!state.dadoUsed)
            {
                state.dadoUsed = true;

                if (Random.value < 0.5f)
                {
                    bonus += 3;
                    Debug.Log(attacker.Data.unitName + ": el Dado del Ocaso da +3 de daño.");
                }
                else
                {
                    StatusEffectController own = attacker.GetComponent<StatusEffectController>();

                    if (own != null && own.ApplyEffect(StatusEffectType.DefenseBoost, 15, 1, attacker))
                        Debug.Log(attacker.Data.unitName + ": el Dado del Ocaso da 15 % de Protección.");
                }
            }
        }

        if (Has(attacker, RelicEffect.EspejoDolor))
        {
            State state = Of(attacker);

            if (state.espejoArmed)
            {
                state.espejoArmed = false;
                bonus += 4;
                Debug.Log(attacker.Data.unitName + ": +4 de daño (Espejo del Dolor).");
            }
        }

        return bonus;
    }

    // Despues de un golpe directo acertado (no esquivado): Sello del Desollador, Pluma del Ultimo Halcon, Corazon de Carbon.
    public static void OnDirectHitLanded(BattleUnit attacker, BattleUnit target, PreHit pre)
    {
        if (attacker == null || target == null || attacker.Side != TeamSide.Player || !GameSession.IsActive)
            return;

        // Broche del Vigia: este turno si se causo daño directo.
        Of(attacker).dealtThisTurn = true;

        // Ojo del Acechador: el golpe que consume la Marca de un enemigo y lo derrota la transfiere a otro enemigo.
        if (pre.mark && !target.IsAlive)
            TransferMark(target, pre.markSource, pre.markPotency, pre.markTurns);

        // Sello del Desollador: +3 HP al acertar contra un enemigo con Sangrado (una vez por turno).
        if (pre.bleeding && Has(attacker, RelicEffect.SelloDesollador))
        {
            State state = Of(attacker);

            if (!state.selloTurn && attacker.IsAlive)
            {
                state.selloTurn = true;
                attacker.Heal(3);
                Debug.Log(attacker.Data.unitName + " recupera 3 HP (Sello del Desollador).");
            }
        }

        StatusEffectController statuses = target.GetComponent<StatusEffectController>();

        // Pluma del Ultimo Halcon: el ataque que consume la Marca aplica Sangrado 2 HP x 2 turnos.
        if (pre.mark && target.IsAlive && statuses != null && Has(attacker, RelicEffect.PlumaHalcon))
        {
            if (statuses.ApplyEffect(StatusEffectType.Bleeding, 2, 2, attacker))
                Debug.Log(target.Data.unitName + " sangra 2 HP por 2 turnos (Pluma del Último Halcón).");
        }

        // Corazon de Carbon: consume un pulso pendiente de Quemadura y lo aplica ya con +2 de daño (una vez por turno).
        if (pre.burn && target.IsAlive && statuses != null && Has(attacker, RelicEffect.CorazonCarbon))
        {
            State state = Of(attacker);
            StatusEffectInstance burn = statuses.GetEffect(StatusEffectType.Burn);

            if (!state.carbonTurn && burn != null)
            {
                state.carbonTurn = true;
                int pulse = burn.Potency + 2;
                Debug.Log(target.Data.unitName + " recibe un pulso de Quemadura adelantado (" + pulse + ") (Corazón de Carbón).");
                burn.ReduceDuration();

                if (burn.IsExpired)
                    statuses.RemoveEffect(StatusEffectType.Burn);

                target.TakeDamage(pulse, true);
            }
        }
    }

    // ------------------------------------------------------------
    // Curacion
    // ------------------------------------------------------------

    // HP extra de la primera curacion de cada turno a un aliado con 50 % o menos de vida (Campana del Socorro).
    // Llamar ANTES de curar; consume el uso solo si aplica.
    public static int HealBonus(BattleUnit healer, BattleUnit target)
    {
        if (healer == null || target == null || !Has(healer, RelicEffect.CampanaSocorro))
            return 0;

        State state = Of(healer);

        if (state.campanaTurn || target.CurrentHealth * 2 > target.MaxHealth)
            return 0;

        state.campanaTurn = true;
        Debug.Log(healer.Data.unitName + ": la curación restaura 3 HP adicionales (Campana del Socorro).");
        return 3;
    }

    // Despues de una curacion de 'healer' a 'target': Fragmento de la Mascara (tras La mascara rota) da 10 % de Proteccion y
    // Rosario de la Aurora da Regeneracion 1 x 2 turnos (ambos con la primera curacion de cada turno).
    public static void OnHealApplied(BattleUnit healer, BattleUnit target)
    {
        if (healer == null || target == null || !target.IsAlive)
            return;

        if (GameSession.IsNoelleLiberated && Has(healer, RelicEffect.FragmentoMascara))
        {
            State state = Of(healer);

            if (!state.fragmentoTurn)
            {
                state.fragmentoTurn = true;
                StatusEffectController statuses = target.GetComponent<StatusEffectController>();

                if (statuses != null && statuses.ApplyEffect(StatusEffectType.DefenseBoost, 10, 1, healer))
                    Debug.Log(target.Data.unitName + " obtiene 10 % de Protección (Fragmento de la Máscara).");
            }
        }

        if (Has(healer, RelicEffect.RosarioAurora))
        {
            State state = Of(healer);

            if (!state.rosarioTurn)
            {
                state.rosarioTurn = true;
                RegenerationEffect regeneration = target.GetComponent<RegenerationEffect>();

                if (regeneration == null)
                    regeneration = target.gameObject.AddComponent<RegenerationEffect>();

                regeneration.ApplyRegeneration(1, 2);
            }
        }
    }

    // Botas del Mensajero: tras usar un consumible, +2 Velocidad durante los 2 turnos siguientes.
    public static void OnConsumableUsed(BattleUnit user)
    {
        if (!Has(user, RelicEffect.BotasMensajero))
            return;

        State state = Of(user);
        state.botasTurnsLeft = 2;
        state.botasSkipEnd = true;
        Debug.Log(user.Data.unitName + " gana +2 de Velocidad durante 2 turnos (Botas del Mensajero).");
    }

    // ------------------------------------------------------------
    // Habilidades
    // ------------------------------------------------------------

    // Reloj de Arena Roto: la primera habilidad del combate con enfriamiento de 2 o mas turnos lo reduce en 1.
    public static int CooldownReduction(BattleUnit unit, AbilityData ability)
    {
        if (ability == null || ability.cooldown < 2 || !Has(unit, RelicEffect.RelojArenaRoto))
            return 0;

        State state = Of(unit);

        if (state.relojUsed)
            return 0;

        state.relojUsed = true;
        Debug.Log(unit.Data.unitName + ": " + ability.abilityName + " reduce su enfriamiento en 1 (Reloj de Arena Roto).");
        return 1;
    }

    // Fragmento de la Mascara (antes de La mascara rota): +10 puntos de probabilidad de Aturdimiento en Impacto Quebrantador.
    // Los jefes siguen siendo inmunes (lo comprueba StatusEffectController).
    public static float StunChanceBonus(BattleUnit user, AbilityData ability)
    {
        if (ability == null || GameSession.IsNoelleLiberated || !Has(user, RelicEffect.FragmentoMascara))
            return 0f;

        return ability.name.Contains("ImpactoQuebrantador") ? 10f : 0f;
    }
}
