﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2016 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    Lypyl (lypyl@dfworkshop.net)
// 
// Notes:
//

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;

namespace DaggerfallWorkshop
{
    /// <summary>
    /// Defines and executes Daggerfall action records.
    /// </summary>
    public class DaggerfallAction : MonoBehaviour
    {
        public const int TYPE_11_TEXT_INDEX = 8600;
        public const int TYPE_12_TEXT_INDEX = 5400;
        public const int ANSWER_TEXT_INDEX = 5656;

        public bool ActionEnabled = false;                                          // Enable/disable action - not currently being used, but some objects are single activate
        public bool PlaySound = true;                                               // Play sound if present (ActionSound > 0)
        public string ModelDescription = string.Empty;                              // Description string for this model
        public DFBlock.RdbActionFlags ActionFlag = DFBlock.RdbActionFlags.None;     // Action flag value
        public DFBlock.RdbTriggerFlags TriggerFlag = DFBlock.RdbTriggerFlags.None;  // Trigger flag value
        public Vector3 ActionRotation = Vector3.zero;                               // Rotation to perform
        public Vector3 ActionTranslation = Vector3.zero;                            // Translation to perform
        public int Index = 0;                                                       // Index for things like spells, text, and also the raw sound index from daggerfall
        public int ActionAxisRawValue = 0;                                          // Used for some actions in place of magnitude
        public int activationCount = 0;                                             //how many times action has been activated
        public float Magnitude = 0.0f;                                              // How far to move, how much damage etc.
        public float ActionDuration = 0;                                            // Time to reach final state
        public Space ActionSpace = Space.Self;                                      // Relative space to perform action in (self or world)

        public GameObject NextObject;                                               // Next object in action chain
        public GameObject PreviousObject;                                           // Previous object in action chain

        ulong loadID = 0;
        Vector3 startingPosition;
        Quaternion startingRotation;

        AudioSource audioSource;
        ActionState currentState;

        //lookup for action type12, temp. storing them here
        static Dictionary<int, string[]> actionTypeTwelveLookup = new Dictionary<int, string[]>()
        {
            {5404, new string[]{"bow","bow arrow","crossbow","bows","crossbows"}},   //sheogorath answer index = 5660
            {5406, new string[]{"one","1"}},                                         //blind god, answer index = 5662
            {5423, new string[]{"benefactor","the benefactor"}},                      //benefactor answer index = 5679
            {5424, new string[]{"shut up","shutup","shaddup"}},                         //shaddup! answer index = 5680
            {5464, new string[]{"yes","oK","i agree","y","agreed","done","fine","okay","sure","yep"}}, //daggerfall guard answer index = 5720
        };

        public string[] type12_answers;//answers for action type 12 dialogue questions



        public ulong LoadID
        {
            get { return loadID; }
            set { loadID = value; }
        }

        public Vector3 StartingPosition
        {
            get { return startingPosition; }
        }

        public Quaternion StartingRotation
        {
            get { return startingRotation; }
        }

        public ActionState CurrentState
        {
            get { return currentState; }
            set { SetState(value); }
        }

        public bool IsMoving
        {
            get { return (currentState == ActionState.PlayingForward || currentState == ActionState.PlayingReverse); }
        }

        public bool IsFlat { get; set; }

        /// <summary>
        /// Gets the actual duration for timed actions.
        /// </summary>
        public float Duration
        {
            get { return ActionDuration / 20f; }
        }

        //Action flag -> Action Delegate lookup
        private delegate void ActionDelegate(GameObject obj, DaggerfallAction thisAction);

        static Dictionary<DFBlock.RdbActionFlags, ActionDelegate> actionFunctions = new Dictionary<DFBlock.RdbActionFlags, ActionDelegate>()
        {
        {DFBlock.RdbActionFlags.Translation,new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.Rotation,   new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.PositiveX,  new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.NegativeX,  new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.PositiveZ,  new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.NegativeZ,  new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.PositiveY,  new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.NegativeY,  new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.CastSpell,  new ActionDelegate(Move)},
        {DFBlock.RdbActionFlags.ShowText,   new ActionDelegate(ShowText)},
        {DFBlock.RdbActionFlags.ShowTextWithInput,   new ActionDelegate(ShowTextWithInput)},
        {DFBlock.RdbActionFlags.Teleport,   new ActionDelegate(Teleport)},
        {DFBlock.RdbActionFlags.LockDoor,   new ActionDelegate(LockDoor)},
        {DFBlock.RdbActionFlags.UnlockDoor, new ActionDelegate(UnlockDoor)},
        {DFBlock.RdbActionFlags.OpenDoor,   new ActionDelegate(OpenDoor)},
        {DFBlock.RdbActionFlags.CloseDoor,  new ActionDelegate(CloseDoor)},
        {DFBlock.RdbActionFlags.Hurt21,     new ActionDelegate(DrainHealth21)},      //random range damage
        {DFBlock.RdbActionFlags.Hurt22,     new ActionDelegate(DrainHealth)},       //22-25; dmg = level x magnitude
        {DFBlock.RdbActionFlags.Hurt23,     new ActionDelegate(DrainHealth)},
        {DFBlock.RdbActionFlags.Hurt24,     new ActionDelegate(DrainHealth)},
        {DFBlock.RdbActionFlags.Hurt25,     new ActionDelegate(DrainHealth)},
        {DFBlock.RdbActionFlags.Poison,     new ActionDelegate(Poison)},
        {DFBlock.RdbActionFlags.DrainMagicka, new ActionDelegate(DrainMagicka)},
        {DFBlock.RdbActionFlags.Activate,   new ActionDelegate(Activate)},
        };

        public enum TriggerTypes
        {
            None,
            ActionObject, //sent from other action object
            Direct,     //player clicked on this
            WalkOn,     //Only using WalkInto for now
            WalkInto,   //player collided with this object...doesn't normally trigger w/ floating up down, climbing etc
            Attack,     //player hit with weapon
            Door,       //door this attached to opened / closed
        }

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            currentState = ActionState.Start;
            startingPosition = transform.position;
            startingRotation = transform.rotation;
        }

        public void Receive(GameObject prev = null, TriggerTypes triggerType = TriggerTypes.ActionObject)
        {
            if (!gameObject.activeSelf || !this.enabled)
                return;

            if (IsPlaying())
                return;


            //assume actions triggered by other action objects are always valid, 
            //otherwise make sure trigger type is valid for this action
            if(triggerType != TriggerTypes.ActionObject)
            {
                switch (TriggerFlag)
                {
                    case DFBlock.RdbTriggerFlags.None:
                        {
                            if (triggerType != TriggerTypes.ActionObject)
                                return;
                        }
                        break;
                    case DFBlock.RdbTriggerFlags.Collision01:
                        {
                            if (triggerType != TriggerTypes.WalkOn)
                                return;
                        }
                        break;
                    case DFBlock.RdbTriggerFlags.Direct:
                        {
                            if (triggerType != TriggerTypes.Direct)
                                return;
                        }
                        break;
                    case DFBlock.RdbTriggerFlags.Collision03:
                        {
                            if (triggerType != TriggerTypes.WalkInto)
                                return;
                        }
                        break;
                    case DFBlock.RdbTriggerFlags.Attack:
                        {
                            if (triggerType != TriggerTypes.Attack)
                                return;
                        }
                        break;
                    case DFBlock.RdbTriggerFlags.Direct6:
                        {
                            if (triggerType != TriggerTypes.Direct)
                                return;
                        }
                        break;
                    case DFBlock.RdbTriggerFlags.MultiTrigger:
                        {
                            if (triggerType != TriggerTypes.Direct && triggerType != TriggerTypes.Attack && triggerType != TriggerTypes.WalkInto)
                                return;
                        }
                        break;
                    case DFBlock.RdbTriggerFlags.Collision09:
                        {
                            if (triggerType != TriggerTypes.Direct && triggerType != TriggerTypes.WalkInto)
                                return;
                        }
                        break;
                    case DFBlock.RdbTriggerFlags.Door:
                        {
                            if (triggerType != TriggerTypes.Door)
                                return;
                        }
                        break;
                    default:
                        return;
                }
            }

            //increment activation counter
            activationCount++;
            Play(prev);
            return;
        }

        public void Play(GameObject prev)
        {
            ActionDelegate d = null;
            if (actionFunctions.ContainsKey(ActionFlag))
                d = actionFunctions[ActionFlag];


            if (ActionFlag != DFBlock.RdbActionFlags.ShowTextWithInput)
                ActivateNext();

            if (PlaySound && Index > 0 && audioSource)
                audioSource.Play();

            //stop if failed to get valid delegate from lookup - ideally this check should be done before playing
            //sound & activating next, but for testing purposes is done after
            if (d == null)
            {
                //DaggerfallUnity.LogMessage(string.Format("No delegate found for this action flag: {0}", ActionFlag));
                return;
            }


            d(prev, this);
        }

        public bool IsPlaying()
        {
            // Check if this action or any chained action is playing
            if (currentState == ActionState.PlayingForward || currentState == ActionState.PlayingReverse)
                return true;
            else
            {
                if (NextObject == null)
                    return false;

                DaggerfallAction nextAction = NextObject.GetComponent<DaggerfallAction>();
                if (nextAction == null)
                    return false;

                return nextAction.IsPlaying();
            }
        }

        #region Action Helper Methods
        /// <summary>
        /// Triggers the next action in chain if any
        /// </summary>
        private void ActivateNext()
        {
            if (NextObject == null)
            {
                //DaggerfallUnity.LogMessage(string.Format("Next action object in chain is null"));
                return;
            }
            else
            {
                DaggerfallAction action = NextObject.GetComponent<DaggerfallAction>();
                if (action != null)
                    action.Receive(this.gameObject, TriggerTypes.ActionObject);
            }

        }

        public void SetState(ActionState state)
        {
            currentState = state;
        }

        /// <summary>
        /// Restarts a tween in progress. For exmaple, if restoring from save.
        /// </summary>
        public void RestartTween(float durationScale = 1)
        {
            if (currentState == ActionState.PlayingForward)
                TweenToEnd(Duration * durationScale);
            else if (currentState == ActionState.PlayingReverse)
                TweenToStart(Duration * durationScale);
        }

        private void TweenToEnd(float duration)
        {
            Hashtable rotateParams = __ExternalAssets.iTween.Hash(
                 "amount", new Vector3(ActionRotation.x / 360f, ActionRotation.y / 360f, ActionRotation.z / 360f),
                "space", ActionSpace,
                "time", Duration,
                 "easetype", __ExternalAssets.iTween.EaseType.linear,
                "oncomplete", "SetState",
                "oncompleteparams", ActionState.End);

            Hashtable moveParams = __ExternalAssets.iTween.Hash(
                "position", StartingPosition + ActionTranslation,
                "time", Duration,
                "easetype", __ExternalAssets.iTween.EaseType.linear,
                "oncomplete", "SetState",
                "oncompleteparams", ActionState.End);

            __ExternalAssets.iTween.RotateBy(gameObject, rotateParams);
            __ExternalAssets.iTween.MoveTo(gameObject, moveParams);
        }

        //in daggerfall, if the player directly activates a move type action object, it does not go back to its start position.  Not aware of any objects that actually
        //use this feature.
        private void TweenToStart(float duration)
        {
            Hashtable rotateParams = __ExternalAssets.iTween.Hash(
                 "amount", new Vector3(-ActionRotation.x / 360f, -ActionRotation.y / 360f, -ActionRotation.z / 360f),
                "space", ActionSpace,
                "time", Duration,
                 "easetype", __ExternalAssets.iTween.EaseType.linear,
                "oncomplete", "SetState",
                "oncompleteparams", ActionState.Start);

            Hashtable moveParams = __ExternalAssets.iTween.Hash(
                "position", startingPosition,
                "time", Duration,
                "easetype", __ExternalAssets.iTween.EaseType.linear,
                "oncomplete", "SetState",
                "oncompleteparams", ActionState.Start);

            __ExternalAssets.iTween.RotateBy(gameObject, rotateParams);
            __ExternalAssets.iTween.MoveTo(gameObject, moveParams);
        }

        /// <summary>
        ///  Used by door actions (open / close / unlock), tries to get DaggerfallActionDoor from object
        ///  returns true if object is a valid action door, false if not
        /// </summary>
        public static bool GetDoor(GameObject go, out DaggerfallActionDoor door)
        {
            door = null;
            if (go == null)
                return false;

            door = go.GetComponent<DaggerfallActionDoor>();
            if (door == null)
                return false;

            else
                return true;
        }

        public static bool GetPlayer(out GameObject playerObject, out PlayerEntity playerEntity)
        {
            playerObject = GameManager.Instance.PlayerObject;
            playerEntity = GameManager.Instance.PlayerEntity;

            return (playerObject != null & playerEntity != null);

        }

        /// <summary>
        /// Handles the input return event for action type 12
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="userInput"></param>
        public void UserInputHandler(DaggerfallInputMessageBox sender, string userInput)
        {
            if (type12_answers == null || type12_answers.Length == 0)
            {
                DaggerfallUnity.LogMessage(string.Format(("No answers to check for: {0} {1}"), this.gameObject.name, this.Index));
                return;
            }
            userInput = userInput.ToLower();
            for (int i = 0; i < type12_answers.Length; i++)
            {
                if (userInput == type12_answers[i].ToLower())
                {
                    ActivateNext();
                    return;
                }
            }
            //Debug.Log("no matching answer found for: " + userInput);
        }


        #endregion

        #region Actions
        /// <summary>
        /// 1-8
        /// Handles translation / rotation type actions.  
        /// </summary>
        public static void Move(GameObject triggerObj, DaggerfallAction thisAction)
        {
            if (thisAction.CurrentState == ActionState.Start)
            {
                thisAction.CurrentState = ActionState.PlayingForward;
                thisAction.TweenToEnd(thisAction.Duration);
            }
            else if (thisAction.CurrentState == ActionState.End)
            {
                thisAction.CurrentState = ActionState.PlayingReverse;
                thisAction.TweenToStart(thisAction.Duration);
            }

        }


        /// <summary>
        /// 9
        /// Creates spell. Use Action's index to get the spell by index from Spells.STD
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="thisAction"></param>
        public static void CastSpell(GameObject triggerObj, DaggerfallAction thisAction)
        {
            //Debug.Log("Action Type 9: CastSpell");
        }

        /// <summary>
        /// 11
        /// Pop-up text
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="thisAction"></param>
        public static void ShowText(GameObject triggerObj, DaggerfallAction thisAction)
        {
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallWorkshop.Game.DaggerfallUI.UIManager, null);
            messageBox.SetTextTokens(thisAction.Index + TYPE_11_TEXT_INDEX);
            messageBox.ClickAnywhereToClose = true;
            messageBox.ParentPanel.BackgroundColor = Color.clear;
            messageBox.Show();
        }

        /// <summary>
        /// 12
        /// Pop-up text that returns player input
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="thisAction"></param>
        public static void ShowTextWithInput(GameObject triggerObj, DaggerfallAction thisAction)
        {
            int textID = thisAction.Index + TYPE_12_TEXT_INDEX;
            if (actionTypeTwelveLookup.ContainsKey(textID))
            {
                thisAction.type12_answers = actionTypeTwelveLookup[textID];
            }
            else
            {
                Debug.LogError(string.Format("Error: invalid key: {0} for action type 12, couldn't get answer(s)", textID));//todo - display error message
            }
            DaggerfallInputMessageBox inputBox = new DaggerfallInputMessageBox(DaggerfallWorkshop.Game.DaggerfallUI.UIManager, textID, 20, "\t> ", false, null);
            inputBox.ParentPanel.BackgroundColor = Color.clear;
            inputBox.OnGotUserInput += thisAction.UserInputHandler;
            inputBox.Show();
        }





        /// <summary>
        /// 14
        /// Teleports player to next object position.
        /// </summary>
        public static void Teleport(GameObject triggerObj, DaggerfallAction thisAction)
        {

            if (thisAction.NextObject == null)
            {
                DaggerfallUnity.LogMessage(string.Format("Teleport next object null - can't teleport"), true);
                return;
            }

            GameObject playerObject;
            PlayerEntity playerEntity;

            if (!GetPlayer(out playerObject, out playerEntity))
            {
                DaggerfallUnity.LogMessage("Failed to get Player or Player entity", true);
                return;
            }
            playerObject.transform.position = thisAction.NextObject.transform.position;
            playerObject.transform.rotation = thisAction.NextObject.transform.rotation;
        }

        /// <summary>
        /// 16
        /// Locks door when activated. Lock value used is unknown
        /// </summary>
        /// <param name="prevObject"></param>
        /// <param name="thisAction"></param>
        public static void LockDoor(GameObject triggerObj, DaggerfallAction thisAction)
        {
            DaggerfallActionDoor door;
            if (!GetDoor(thisAction.gameObject, out door))
                DaggerfallUnity.LogMessage(string.Format("No DaggerfallActionDoor component found to lock door"), true);
            else
            {
                if (!door.IsLocked)
                    door.CurrentLockValue = 16; //don't know what what setting Daggerfall uses here
            }
        }


        /// <summary>
        /// 17
        /// Unlocks a door
        /// </summary>
        public static void UnlockDoor(GameObject triggerObj, DaggerfallAction thisAction)
        {
            DaggerfallActionDoor door;
            if (!GetDoor(thisAction.gameObject, out door))
                DaggerfallUnity.LogMessage(string.Format("No DaggerfallActionDoor component found to unlock door"), true);
            else
                door.CurrentLockValue = 0;
        }


        /// <summary>
        /// 18
        /// Opens (and unlocks if is locked) door
        /// </summary>
        public static void OpenDoor(GameObject triggerObj, DaggerfallAction thisAction)
        {
            DaggerfallActionDoor door;
            if (!GetDoor(thisAction.gameObject, out door))
                DaggerfallUnity.LogMessage(string.Format("No DaggerfallActionDoor component found"), true);
            else
            {
                if (door.IsOpen)
                    return;
                else
                {
                    door.CurrentLockValue = 0;
                    door.ToggleDoor();
                }
            }
        }


        /// <summary>
        /// 20
        /// Closes door on activate.  If door has a starting lock value, will re-lock door.
        /// </summary>
        public static void CloseDoor(GameObject triggerObj, DaggerfallAction thisAction)
        {

            DaggerfallActionDoor door;
            if (!GetDoor(thisAction.gameObject, out door))
                DaggerfallUnity.LogMessage(string.Format("No DaggerfallActionDoor component found"), true);
            else
            {
                if (!door.IsOpen)
                    return;
                else
                {
                    door.ToggleDoor();
                    door.CurrentLockValue = door.StartingLockValue;
                }

            }

        }

        /// <summary>
        /// 21
        /// Damages players health, uses random range & activates sporadically
        /// </summary>
        /// <param name="prevObj"></param>
        /// <param name="thisAction"></param>
        public static void DrainHealth21(GameObject triggerObj, DaggerfallAction thisAction)
        {
            //action type 21 activates every ~20 times for some reason.  Might be better to rand instead
            if (thisAction.activationCount % 20 != 0)
                return;

            GameObject playerObject;
            PlayerEntity playerEntity;

            if(!GetPlayer(out playerObject, out playerEntity))
            {
                DaggerfallUnity.LogMessage("Failed to get Player or Player entity", true);
                return;
            }

            int damage = 0;
            damage = UnityEngine.Random.Range(Mathf.Max(1, (int)thisAction.Magnitude), Mathf.Max(1, thisAction.Index)) * Mathf.Max(playerEntity.Level, 1);
            playerObject.SendMessage("RemoveHealth", damage);
            //DaggerfallUnity.LogMessage("DrainHealth21, damage: " + damage, true);
        }


        /// <summary>
        /// 22-25
        /// Damages players health every hit
        /// </summary>
        /// <param name="prevObj"></param>
        /// <param name="thisAction"></param>
        public static void DrainHealth(GameObject triggerObj, DaggerfallAction thisAction)
        {

            GameObject playerObject;
            PlayerEntity playerEntity;

            if (!GetPlayer(out playerObject, out playerEntity))
            {
                DaggerfallUnity.LogMessage("Failed to get Player or Player entity", true);
                return;
            }

            int damage = 0;
            if(thisAction.IsFlat)
                damage = (int)thisAction.Magnitude * Mathf.Max(playerEntity.Level, 1);
            else
                damage = (int)thisAction.ActionAxisRawValue * Mathf.Max(playerEntity.Level, 1);

            playerObject.SendMessage("RemoveHealth", damage);

            //DaggerfallUnity.LogMessage("DrainHealth damage: " + damage, true);
        }


        /// <summary>
        /// 26
        /// Seems to poison / infect player.
        /// </summary>
        /// <param name="triggerObj"></param>
        /// <param name="thisAction"></param>
        public static void Poison(GameObject triggerObj, DaggerfallAction thisAction)
        {
            //Debug.Log("Action Type 26: Poison");
        }

        /// <summary>
        /// 28
        /// Drains Magicka
        /// Only on models in vanilla daggerfall (usually on bottom of pits, drains magica while you walk around)
        /// </summary>
        /// <param name="playerObj"></param>
        /// <param name="thisAction"></param>
        public static void DrainMagicka(GameObject triggerObj, DaggerfallAction thisAction)
        {
            GameObject playerObject;
            PlayerEntity playerEntity;

            if (!GetPlayer(out playerObject, out playerEntity))
            {
                DaggerfallUnity.LogMessage("Failed to get Player or Player entity", true);
                return;
            }
            if(thisAction.IsFlat)
                playerEntity.DecreaseMagicka((int)Mathf.Max(1, thisAction.Magnitude));
            else
                playerEntity.DecreaseMagicka((int)Mathf.Max(1, thisAction.ActionAxisRawValue));
        }


        // <summary>
        /// Just activates next object in chain.
        /// </summary>
        public static void Activate(GameObject triggerObj, DaggerfallAction thisAction)
        {
            return;
        }



        #endregion
    }
}