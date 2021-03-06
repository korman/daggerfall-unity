﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2018 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Entity;

namespace DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects
{
    /// <summary>
    /// Heal drain effect base.
    /// Looks for an incumbent DrainEffect to heal.
    /// NOTE: Does not currently heal attribute loss from disease. Need to confirm if this is allowed in classic.
    /// </summary>
    public abstract class HealEffect : BaseEntityEffect
    {
        protected DFCareer.Stats healStat = DFCareer.Stats.None;

        public override void MagicRound()
        {
            base.MagicRound();

            DrainEffect incumbentDrain = manager.FindDrainStatIncumbent(healStat);
            if (incumbentDrain != null)
            {
                int magnitude = GetMagnitude(caster);
                incumbentDrain.Heal(magnitude);
                Debug.LogFormat("Healed {0} Drain {1} by {2} points", GetPeeredEntityBehaviour(manager).name, incumbentDrain.DrainStat.ToString(), magnitude);
            }
            else
            {
                Debug.LogFormat("Could not find incumbent Drain {0} on target", healStat.ToString());
            }
        }
    }
}