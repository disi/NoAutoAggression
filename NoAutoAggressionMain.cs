﻿using System.IO;
using UnityEngine;
using TheForest.Utils;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Xml.Serialization;
using TheForest.Utils.Settings;
using System.Threading;

namespace NoAutoAggression
{
    class NoAutoAggression : MonoBehaviour
    {
        private static Dictionary<string, int> aggressionStore;
        private static ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
        private static int mutantDayCycleDay = Clock.Day;
        private static string noAutoAggressionMainSavePath = "C:/Program Files (x86)/Steam/steamapps/common/The Forest/Mods/NoAutoAggression/";
        private static string noAutoAggressionSaveSlot;
        // static values
        private static int startAggression = 1;
        private static int minimumAggression = -2; // +1
        private static int maximumAggression = 20;
        private static int aggressionIncrease = 5;
        private static int aggressionDecrease = 1;
        // debug yes/no
        public static bool debugAggression = false;
        public static bool debugAggressionIncrease = false;
        public static bool debugAttackChance = false;
        public static bool debugSaveSlot = false;

        [ModAPI.Attributes.ExecuteOnGameStart]
        static void AddMeToScene()
        {
            GameObject GO = new GameObject("__NoAutoAggression__");
            GO.AddComponent<NoAutoAggression>();
        }

        // is called by NAASpawnManager in OnEnable() usually when a new game is loaded
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void CreateAggressionStore()
        {
            // get our aggression
            aggressionStore = null;
            NAAUpdateSaveSlot();
            if (File.Exists(noAutoAggressionSaveSlot + "/aggression.xml"))
            {
                var serializer = new XmlSerializer(typeof(SerializableDictionary<string, int>));
                var stream = new FileStream(noAutoAggressionSaveSlot + "/aggression.xml", FileMode.Open);
                aggressionStore = serializer.Deserialize(stream) as SerializableDictionary<string, int>;
                stream.Close();
            }
            if (aggressionStore != null)
            {
                if (debugAggression) ModAPI.Log.Write("storedAggression ready");
            }
            else
            {
                aggressionStore = new SerializableDictionary<string, int>();
                if (debugAggression) ModAPI.Log.Write("storedAggression created");
            }
        }

        // is called to increase aggression on mutant death
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void IncreaseAggression(mutantAI myAI)
        {
            if (aggressionStore.ContainsKey(AiName(myAI)))
            {
                rwLock.EnterReadLock();
                int storeAggression = (int)aggressionStore[AiName(myAI)];
                rwLock.ExitReadLock();
                if (storeAggression < maximumAggression)
                {
                    storeAggression += aggressionIncrease;
                    if (storeAggression > maximumAggression)
                    {
                        storeAggression = maximumAggression;
                    }
                    rwLock.EnterWriteLock();
                    aggressionStore[AiName(myAI)] = storeAggression;
                    rwLock.ExitWriteLock();
                    if (debugAggressionIncrease) ModAPI.Log.Write(AiName(myAI) + " saved this higher aggression: " + storeAggression);
                }
            }
        }

        // is called by several threads to reset/update their aggression
        public static int GetAggression(mutantAI myAI, int myAggression)
        {
            // aggression store
            if (aggressionStore.ContainsKey(AiName(myAI)))
            {
                rwLock.EnterReadLock();
                int storeAggression = (int)aggressionStore[AiName(myAI)];
                rwLock.ExitReadLock();
                if (debugAggression) ModAPI.Log.Write(AiName(myAI) + " loaded this aggression: " + storeAggression);
                return storeAggression;
            }
            else
            {
                rwLock.EnterWriteLock();
                aggressionStore[AiName(myAI)] = startAggression;
                rwLock.ExitWriteLock();
                if (debugAggression) ModAPI.Log.Write(AiName(myAI) + " saved this new aggression: " + startAggression);
                return startAggression;
            }
        }

        // is called by NAASpawnManager once a day
        public static void LowerAggression()
        {
            if (CheckMutantDayCycle())
            {
                rwLock.EnterReadLock();
                List<string> keys = new List<string>(aggressionStore.Keys);
                rwLock.ExitReadLock();
                foreach (string key in keys)
                {
                    rwLock.EnterReadLock();
                    int tempInt = aggressionStore[key] - aggressionDecrease;
                    rwLock.ExitReadLock();
                    if (tempInt > minimumAggression)
                    {
                        rwLock.EnterWriteLock();
                        aggressionStore[key] = tempInt;
                        rwLock.ExitWriteLock();
                        if (debugAggression) ModAPI.Log.Write(key + " aggression lowered on day " + Clock.Day + " to " + tempInt);
                    }
                }
                rwLock.EnterWriteLock();
                aggressionStore["mutantDayCycleDay"] = Clock.Day;
                rwLock.ExitWriteLock();
            }
        }

        // check the last day aggression was lowered
        public static bool CheckMutantDayCycle()
        {
            // check and update day
            if (aggressionStore.ContainsKey("mutantDayCycleDay"))
            {
                rwLock.EnterReadLock();
                int mutantDayCycleDay = aggressionStore["mutantDayCycleDay"];
                rwLock.ExitReadLock();
                if (mutantDayCycleDay != Clock.Day)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        // is called by NAASpawnManager every few minutes when mutantspawns are counted or the NAASpawnManager destroyed
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void SaveAggression()
        {
            NAAUpdateSaveSlot();
            if (!File.Exists(noAutoAggressionSaveSlot))
            {
                System.IO.Directory.CreateDirectory(noAutoAggressionSaveSlot);
            }
            var serializer = new XmlSerializer(typeof(SerializableDictionary<string, int>));
            var stream = new FileStream(noAutoAggressionSaveSlot + "/aggression.xml", FileMode.Create);
            rwLock.EnterReadLock();
            serializer.Serialize(stream, aggressionStore);
            stream.Close();
            rwLock.ExitReadLock();
        }

        // distinguish between ai and return as string
        private static string AiName(mutantAI myMutantAi)
        {
            if (myMutantAi.femaleSkinny || myMutantAi.maleSkinny) return "skinny";
            else if (myMutantAi.skinned) return "skinned";
            else if (myMutantAi.painted) return "painted";
            else if (myMutantAi.pale) return "pale";
            else if (myMutantAi.creepy || myMutantAi.creepy_baby || myMutantAi.creepy_boss || myMutantAi.creepy_fat || myMutantAi.creepy_male) return "creepy";
            else return "regular";
        }

        // update savepath
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void NAAUpdateSaveSlot()
        {
            noAutoAggressionSaveSlot = noAutoAggressionMainSavePath + SaveSlotUtils.GetLocalSlotPath().Substring(SaveSlotUtils.GetLocalSlotPath().Length - 6);
            if (debugSaveSlot) ModAPI.Log.Write("Current SaveSlot: " + noAutoAggressionSaveSlot);
        }
    }

    // copied from https://weblogs.asp.net/pwelter34/444961 Thank you sooooo much!
    [XmlRoot("dictionary")]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
    {
        #region IXmlSerializable Members
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            bool wasEmpty = reader.IsEmptyElement;
            reader.Read();

            if (wasEmpty)
                return;

            while (reader.NodeType != System.Xml.XmlNodeType.EndElement)
            {
                reader.ReadStartElement("item");

                reader.ReadStartElement("key");
                TKey key = (TKey)keySerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadStartElement("value");
                TValue value = (TValue)valueSerializer.Deserialize(reader);
                reader.ReadEndElement();

                this.Add(key, value);

                reader.ReadEndElement();
                reader.MoveToContent();
            }
            reader.ReadEndElement();
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            foreach (TKey key in this.Keys)
            {
                writer.WriteStartElement("item");

                writer.WriteStartElement("key");
                keySerializer.Serialize(writer, key);
                writer.WriteEndElement();

                writer.WriteStartElement("value");
                TValue value = this[key];
                valueSerializer.Serialize(writer, value);
                writer.WriteEndElement();

                writer.WriteEndElement();
            }
        }
        #endregion
    }

    // single instance to manage the mutant spawns
    class NAASpawnManager : mutantSpawnManager
    {
        private void OnEnable()
        {
            // create aggressionStore - potential override in future game versions
            NoAutoAggression.CreateAggressionStore();
        }

        protected override void addToMutantAmounts()
        {
            // original code
            base.addToMutantAmounts();
            // lower aggression once a day by 1
            NoAutoAggression.LowerAggression();
        }

        private void OnDisable()
        {
            // save aggression to file - potential override in future game versions
            NoAutoAggression.SaveAggression();
        }
    }

    // one for each mutant to set daily routine
    class NAAMutantDayCycle : mutantDayCycle
    {
        protected override void setDayConditions()
        {
            // original code
            base.setDayConditions();
            // set/reset aggression values
            if ((!base.setup.search.fsmInCave.Value) && (!base.creepy))
            {
                base.aggression = NoAutoAggression.GetAggression(base.ai, base.aggression);
                base.fsmAggresion.Value = base.aggression;
            }
        }
    }

    // several tasks/behaviour settings for mutants
    class NAAMutantAiManager : mutantAiManager
    {
        // to take out all the auto aggression
        private void UpdateAttackChance()
        {
            if ((!base.searchFunctions.fsmInCave.Value) && (!base.setup.dayCycle.creepy))
            {
                // FSM Data auslesen
                /*
                //Fsm myFSM = base.setup.pmBrain.Fsm;
                //Fsm myFSM = base.setup.pmEncounter.Fsm;
                //Fsm myFSM = base.setup.pmCombat.Fsm;
                Fsm myFSM = base.setup.pmSleep.Fsm;
                ModAPI.Log.Write(myFSM.Name + " FSM Owner:" + myFSM.Owner.name);
                foreach (var item in myFSM.Events)
                {
                    ModAPI.Log.Write(myFSM.Name + " FSM Events:" + item.Name);
                }
                foreach (var item in myFSM.States)
                {
                    ModAPI.Log.Write(myFSM.Name + " FSM States:" + item.Name);
                }
                foreach (var item in myFSM.GlobalTransitions)
                {
                    ModAPI.Log.Write(myFSM.Name + " FSM Transitions:" + item.EventName);
                }
                ModAPI.Log.Write("----------------------------------------------");
                */
                base.setup.dayCycle.aggression = NoAutoAggression.GetAggression(base.setup.ai, base.setup.dayCycle.aggression);
                base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = base.setup.dayCycle.aggression;
                base.fsmAttackChance.Value = ((base.setup.dayCycle.aggression * GameSettings.Ai.aiAttackChanceRatio) / 10f);
                base.fsmAttack = ((base.setup.dayCycle.aggression * GameSettings.Ai.aiFollowUpAfterAttackRatio) / 10f);
                base.fsmRunAwayChance.Value = Mathf.Clamp(3f - base.fsmAttackChance.Value, 0f, 3f);
                base.fsmRunTowardsScream.Value = Mathf.Clamp(UnityEngine.Random.Range(0f, base.fsmAttackChance.Value), 0f, 2f);
                base.fsmScreamRunTowards.Value = Mathf.Clamp(UnityEngine.Random.Range(0f, base.fsmAttackChance.Value), 0f, 2f);
                base.fsmScream.Value = Mathf.Clamp(UnityEngine.Random.Range(0f, base.fsmAttackChance.Value), 0f, 2f);
                base.fsmBackAway.Value = Mathf.Clamp(2f - base.fsmAttackChance.Value, 0f, 2f);
                base.fsmDisengage.Value = Mathf.Clamp(2f - base.fsmAttackChance.Value, 0f, 2f);
                if (NoAutoAggression.debugAttackChance) ModAPI.Log.Write("Mutant set this attackchance: " + base.fsmAttackChance.Value.ToString("N3"));
                // send mutants away if they are friendly
                if (base.setup.dayCycle.aggression <= 0)
                {
                    float runDistance = base.setup.dayCycle.aggression * 5 + 5;
                    if (base.setup.animControl.fsmPlayerDist.Value < runDistance)
                    {
                        if (base.setup.pmCombat != null)
                        {
                            if (base.setup.ai.leader)
                            {
                                base.setup.pmCombat.SendEvent("goToRunAway");
                            }
                            else
                            {
                                base.setup.pmCombat.SendEvent("goToLeader");
                            }
                        }
                        if (base.setup.pmEncounter != null)
                        {
                            base.setup.pmEncounter.SendEvent("FINISHED");
                        }
                        if ((base.ai.skinned) || (base.ai.femaleSkinny) || (base.ai.maleSkinny))
                        {
                            base.setup.pmBrain.SendEvent("toSetFearful");
                        }
                        else
                        {
                            base.setup.pmBrain.SendEvent("toSetPassive");
                        }
                    }
                }
            }
        }

        public override void setAggressiveCombat()
        {
            base.setAggressiveCombat();
            UpdateAttackChance();
        }

        public override void setCaveCombat()
        {
            base.setCaveCombat();
            UpdateAttackChance();
        }

        public override void setCaveSearching()
        {
            base.setCaveSearching();
            UpdateAttackChance();
        }

        public override void setDaySearching()
        {
            base.setDaySearching();
            UpdateAttackChance();
        }

        public override void setDayStalking()
        {
            base.setDayStalking();
            UpdateAttackChance();
        }

        public override void setDefaultCombat()
        {
            base.setDefaultCombat();
            UpdateAttackChance();
        }

        public override void setDefaultSearching()
        {
            base.setDefaultSearching();
            UpdateAttackChance();
        }

        public override void setDefaultStalking()
        {
            base.setDefaultStalking();
            UpdateAttackChance();
        }

        public override void setDefensiveCombat()
        {
            base.setDefensiveCombat();
            UpdateAttackChance();
        }

        public override void setFiremanCombat()
        {
            base.setFiremanCombat();
            UpdateAttackChance();
        }

        public override void setOnStructureCombat()
        {
            base.setOnStructureCombat();
            UpdateAttackChance();
        }

        public override void setPlaneCrashCombat()
        {
            base.setPlaneCrashCombat();
            UpdateAttackChance();
        }

        public override void setPlaneCrashStalking()
        {
            base.setPlaneCrashStalking();
            UpdateAttackChance();
        }

        public override void setSkinnedMutantCombat()
        {
            base.setSkinnedMutantCombat();
            UpdateAttackChance();
        }

        public override void setSkinnyAggressiveCombat()
        {
            base.setSkinnyAggressiveCombat();
            UpdateAttackChance();
        }

        public override void setSkinnyCombat()
        {
            base.setSkinnyCombat();
            UpdateAttackChance();
        }

        public override void setSkinnyDaySearching()
        {
            base.setSkinnyDaySearching();
            UpdateAttackChance();
        }

        public override void setSkinnyNightSearching()
        {
            base.setSkinnyNightSearching();
            UpdateAttackChance();
        }

        public override void setSkinnyNightStalking()
        {
            base.setSkinnyNightStalking();
            UpdateAttackChance();
        }

        public override void setSkinnyStalking()
        {
            base.setSkinnyStalking();
            UpdateAttackChance();
        }

        public override void setTestSearching()
        {
            base.setTestSearching();
            UpdateAttackChance();
        }

        public override void setTestStalking()
        {
            base.setTestStalking();
            UpdateAttackChance();
        }
    }

    // trigger aggression increase if a mutant dies by player or trap (not stealthkill)
    class NAAEnemyHealth : EnemyHealth
    {
        public override void Die()
        {
            // check if player killed the mutant and increase aggression
            if ((!base.setup.search.fsmInCave.Value) && (!base.setup.dayCycle.creepy))
            {
                if (base.setup.search.currentTarget.CompareTag("Player") || base.setup.search.currentTarget.CompareTag("PlayerNet") || base.setup.search.currentTarget.CompareTag("PlayerRemote"))
                {
                    if ((!base.doStealthKill) || (base.animator.GetBool("trapBool")))
                    {
                        NoAutoAggression.IncreaseAggression(base.setup.ai);
                    }
                }
            }
            // original code
            base.Die();
        }
    }
}

