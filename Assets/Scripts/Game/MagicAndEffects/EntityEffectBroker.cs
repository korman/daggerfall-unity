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
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace DaggerfallWorkshop.Game.MagicAndEffects
{
    /// <summary>
    /// Enumerates available magic effects and coordinates their instantiation.
    /// Also coordinates "magic rounds" at 5-second intervals for all entity EntityEffectManager components active in scene.
    /// </summary>
    public class EntityEffectBroker : MonoBehaviour
    {
        #region Fields

        public const int CurrentSpellVersion = 1;

        public const TargetTypes TargetFlags_None = TargetTypes.None;
        public const TargetTypes TargetFlags_Self = TargetTypes.CasterOnly;
        public const TargetTypes TargetFlags_Other = TargetTypes.ByTouch | TargetTypes.SingleTargetAtRange | TargetTypes.AreaAroundCaster | TargetTypes.AreaAtRange;
        public const TargetTypes TargetFlags_All = TargetTypes.CasterOnly | TargetTypes.ByTouch | TargetTypes.SingleTargetAtRange | TargetTypes.AreaAroundCaster | TargetTypes.AreaAtRange;

        public const ElementTypes ElementFlags_None = ElementTypes.None;
        public const ElementTypes ElementFlags_MagicOnly = ElementTypes.Magic;
        public const ElementTypes ElementFlags_All = ElementTypes.Fire | ElementTypes.Cold | ElementTypes.Poison | ElementTypes.Shock | ElementTypes.Magic;

        public const MagicCraftingStations MagicCraftingFlags_None = MagicCraftingStations.None;
        public const MagicCraftingStations MagicCraftingFlags_All = MagicCraftingStations.SpellMaker | MagicCraftingStations.PotionMaker | MagicCraftingStations.ItemMaker;

        const float roundInterval = 5.0f;

        int magicRoundsSinceStartup = 0;
        float roundTimer = 0f;
        Dictionary<string, BaseEntityEffect> magicEffectTemplates = new Dictionary<string, BaseEntityEffect>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of 5-second "magic rounds" since startup.
        /// </summary>
        public int MagicRoundsSinceStartup
        {
            get { return magicRoundsSinceStartup; }
        }

        #endregion

        #region Unity

        void Start()
        {
            // Enumerate classes implementing an effect and create an instance to use as factory
            // TODO: Provide an external method for mods to register custom effects without reflections
            magicEffectTemplates.Clear();
            IEnumerable<BaseEntityEffect> effectTemplates = ReflectiveEnumerator.GetEnumerableOfType<BaseEntityEffect>();
            foreach(BaseEntityEffect effect in effectTemplates)
            {
                magicEffectTemplates.Add(effect.Key, effect);
            }
        }

        void Update()
        {
            // Increment magic round timer when not paused
            if (!GameManager.IsGamePaused)
            {
                roundTimer += Time.deltaTime;
                if (roundTimer > roundInterval)
                {
                    RaiseOnNewMagicRoundEvent();
                    magicRoundsSinceStartup++;
                    roundTimer = 0;
                    //Debug.Log("New magic round starting.");
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets group names of registered effects.
        /// </summary>
        /// <param name="sortAlpha">True to sort group names by alpha.</param>
        /// <param name="craftingStations">Filter by allowed magic crafting stations.</param>
        /// <returns>Array of group names.</returns>
        public string[] GetGroupNames(bool sortAlpha = true, MagicCraftingStations craftingStations = MagicCraftingFlags_All)
        {
            List<string> groupNames = new List<string>();

            // Get group list
            foreach(BaseEntityEffect effect in magicEffectTemplates.Values)
            {
                // Skip effects not fitting at least one station requirement
                if ((craftingStations & effect.Properties.AllowedCraftingStations) == 0)
                    continue;

                // Ignore duplicate groups
                if (!groupNames.Contains(effect.Properties.GroupName))
                    groupNames.Add(effect.Properties.GroupName);
            }

            // Sort if required
            if (sortAlpha)
                groupNames.Sort();

            return groupNames.ToArray();
        }

        /// <summary>
        /// Gets subgroup names of registered effects.
        /// </summary>
        /// <param name="groupName">The group name to collect subgroups of.</param>
        /// <param name="sortAlpha">True to sort subgroup names by alpha.</param>
        /// <param name="craftingStations">Filter by allowed magic crafting stations.</param>
        /// <returns>Array of subgroup names.</returns>
        public string[] GetSubGroupNames(string groupName, bool sortAlpha = true, MagicCraftingStations craftingStations = MagicCraftingFlags_All)
        {
            List<string> subGroupNames = new List<string>();

            foreach (BaseEntityEffect effect in magicEffectTemplates.Values.Where(effect => effect.Properties.GroupName == groupName))
            {
                // Skip effects not fitting at least one station requirement
                if ((craftingStations & effect.Properties.AllowedCraftingStations) == 0)
                    continue;

                subGroupNames.Add(effect.Properties.SubGroupName);
            }

            // Sort if required
            if (sortAlpha)
                subGroupNames.Sort();

            return subGroupNames.ToArray();
        }

        /// <summary>
        /// Gets interface to all effect templates belonging to group name.
        /// </summary>
        /// <param name="groupName">The group name to collect effects from.</param>
        /// <param name="craftingStations">Filter by allowed magic crafting stations.</param>
        /// <returns>List of effect templates.</returns>
        public List<IEntityEffect> GetEffectTemplates(string groupName, MagicCraftingStations craftingStations = MagicCraftingFlags_All)
        {
            List<IEntityEffect> effectTemplates = new List<IEntityEffect>();

            foreach (IEntityEffect effectTemplate in magicEffectTemplates.Values.Where(effect => effect.Properties.GroupName == groupName))
            {
                // Skip effects not fitting at least one station requirement
                if ((craftingStations & effectTemplate.Properties.AllowedCraftingStations) == 0)
                    continue;

                effectTemplates.Add(effectTemplate);
            }

            return effectTemplates;
        }

        /// <summary>
        /// Determine if a key exists in the templates dictionary.
        /// </summary>
        /// <param name="key">Key for template.</param>
        /// <returns>True if template exists.</returns>
        public bool HasEffectTemplate(string key)
        {
            return magicEffectTemplates.ContainsKey(key);
        }

        /// <summary>
        /// Gets interface to effect template.
        /// Use this to query properties to all effects with this key.
        /// </summary>
        /// <param name="key">Effect key.</param>
        /// <returns>Interface to effect template only (has default effect settings).</returns>
        public IEntityEffect GetEffectTemplate(string key)
        {
            if (!HasEffectTemplate(key))
                return null;

            return magicEffectTemplates[key];
        }

        /// <summary>
        /// Creates a new instance of effect with specified settings.
        /// Use this to create a new effect with unique settings for actual use.
        /// </summary>
        /// <param name="effectEntry">EffectEntry with effect settings.</param>
        /// <returns>Interface to new effect instance.</returns>
        public IEntityEffect InstantiateEffect(EffectEntry effectEntry)
        {
            return InstantiateEffect(effectEntry.Key, effectEntry.Settings);
        }

        /// <summary>
        /// Creates a new instance of effect with specified settings.
        /// Use this to create a new effect with unique settings for actual use.
        /// </summary>
        /// <param name="key">Effect key.</param>
        /// <param name="settings">Effect settings.</param>
        /// <returns>Interface to new effect instance.</returns>
        public IEntityEffect InstantiateEffect(string key, EffectSettings settings)
        {
            if (!HasEffectTemplate(key))
                return null;

            IEntityEffect effectTemplate = magicEffectTemplates[key];
            IEntityEffect effectInstance = Activator.CreateInstance(effectTemplate.GetType()) as IEntityEffect;
            effectInstance.Settings = settings;

            return effectInstance;
        }

        #endregion

        #region Events

        // OnNewMagicRound
        public delegate void OnNewMagicRoundEventHandler();
        public static event OnNewMagicRoundEventHandler OnNewMagicRound;
        protected virtual void RaiseOnNewMagicRoundEvent()
        {
            if (OnNewMagicRound != null)
                OnNewMagicRound();
        }

        #endregion
    }

    /// <summary>
    /// Find all subclasses of type and create instance.
    /// </summary>
    public static class ReflectiveEnumerator
    {
        static ReflectiveEnumerator() { }

        public static IEnumerable<T> GetEnumerableOfType<T>(params object[] constructorArgs) where T : class
        {
            List<T> objects = new List<T>();
            foreach (Type type in
                Assembly.GetAssembly(typeof(T)).GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T))))
            {
                objects.Add((T)Activator.CreateInstance(type, constructorArgs));
            }
            return objects;
        }
    }
}
