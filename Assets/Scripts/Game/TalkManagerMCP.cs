// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2017 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Michael Rauter (Nystul)

using System;
using DaggerfallWorkshop.Utility;
using DaggerfallConnect.Arena2;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;
using DaggerfallConnect.FallExe;

namespace DaggerfallWorkshop.Game
{
    public partial class TalkManager : IMacroContextProvider
    {
        public MacroDataSource GetMacroDataSource()
        {
            return new TalkManagerDataSource(this.currentQuestionListItem);
        }

        /// <summary>
        /// MacroDataSource context sensitive methods for items in Daggerfall Unity.
        /// </summary>
        private class TalkManagerDataSource : MacroDataSource
        {

            private TalkManager.ListItem parent;
            public TalkManagerDataSource(TalkManager.ListItem item)
            {
                this.parent = item;
            }

            public override string LocationDirection()
            {
                //if (parent.questionType == QuestionType.QuestLocation)
                //{
                //    return GameManager.Instance.TalkManager.GetQuestLocationDirection();
                //}
                //else if (parent.questionType == QuestionType.LocalBuilding)
                if (parent.questionType == QuestionType.LocalBuilding)
                {
                    return GameManager.Instance.TalkManager.GetKeySubjectLocationDirection();
                }
                return "never mind...";
            }

            public override string DialogHint()
            {
                if (parent.questionType == QuestionType.LocalBuilding)
                {
                    return GameManager.Instance.TalkManager.GetKeySubjectLocationHint();
                }
                else if (parent.questionType == QuestionType.QuestLocation || parent.questionType == QuestionType.QuestPerson || parent.questionType == QuestionType.QuestItem)
                {
                    return GameManager.Instance.TalkManager.GetDialogHint(parent);
                }
                return "never mind...";
            }

            public override string DialogHint2()
            {
                if (parent.questionType == QuestionType.LocalBuilding)
                {
                    return GameManager.Instance.TalkManager.GetKeySubjectLocationHint();
                }
                else if (parent.questionType == QuestionType.QuestLocation || parent.questionType == QuestionType.QuestPerson || parent.questionType == QuestionType.QuestItem)
                {
                    return GameManager.Instance.TalkManager.GetDialogHint2(parent);
                }
                return "never mind...";
            }
        }
    }
}