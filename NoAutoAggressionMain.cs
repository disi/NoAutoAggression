using System.IO;
using UnityEngine;
using TheForest.Utils;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Xml.Serialization;
using TheForest.Utils.Settings;

namespace NoAutoAggression
{
    class NoAutoAggression : MonoBehaviour
    {
        private static Dictionary<string, int> aggressionStore;
        private static int mutantDayCycleDay = Clock.Day;
        private static string noAutoAggressionMainSavePath = "C:/Program Files (x86)/Steam/steamapps/common/The Forest/Mods/NoAutoAggression/";
        private static string noAutoAggressionSaveSlot;
        private static bool aggressionLock = false;
        // static values
        private static int minimumAggression = -1; // +1
        public static int maximumAggression = 20;
        public static int aggressionHitIncrease = 5;
        // debug yes/no
        public static bool debugAggression = false;
        public static bool debugSaveSlot = false;

        [ModAPI.Attributes.ExecuteOnGameStart]
        static void AddMeToScene()
        {
            GameObject GO = new GameObject("__NoAutoAggression__");
            GO.AddComponent<NoAutoAggression>();
        }

        // is called by NAASpawnManager in OnEnable() usually when a new game is loaded
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

        // is called by NAAMutantAnimatorControl when a mutant is hit
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static int StoreAggression(mutantAI myAI, int myAggression)
        {
            // aggression store
            if (!aggressionLock)
            {
                if (aggressionStore.ContainsKey(AiName(myAI)))
                {
                    if ((int)aggressionStore[AiName(myAI)] < myAggression)
                    {
                        aggressionStore[AiName(myAI)] = myAggression;
                        if (debugAggression) ModAPI.Log.Write(AiName(myAI) + " saved this higher aggression: " + (int)aggressionStore[AiName(myAI)]);
                        return myAggression;
                    }
                    else
                    {
                        if (debugAggression) ModAPI.Log.Write(AiName(myAI) + " loaded this aggression: " + (int)aggressionStore[AiName(myAI)]);
                        return (int)aggressionStore[AiName(myAI)];
                    }
                }
                else
                {
                    aggressionStore[AiName(myAI)] = 0;
                    if (debugAggression) ModAPI.Log.Write(AiName(myAI) + " saved this new aggression: 0");
                    return 0;
                }
            }
            return 0;
        }

        // is called by several threads to reset/update their aggression
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static int GetAggression(mutantAI myAI, int myAggression)
        {
            if (!aggressionLock)
            {
                // aggression store
                if (aggressionStore.ContainsKey(AiName(myAI)))
                {
                    if (debugAggression) ModAPI.Log.Write(AiName(myAI) + " loaded this aggression: " + (int)aggressionStore[AiName(myAI)]);
                    return (int)aggressionStore[AiName(myAI)];
                }
                else
                {
                    aggressionStore[AiName(myAI)] = 0;
                    if (debugAggression) ModAPI.Log.Write(AiName(myAI) + " saved this new aggression: 0");
                    return 0;
                }
            }
            return 0;
        }

        // is called by NAASpawnManager once a day
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void LowerAggression()
        {
            if (CheckMutantDayCycle())
            {
                if (!aggressionLock)
                {
                    NoAutoAggression.LockAggression();
                    List<string> keys = new List<string>(aggressionStore.Keys);
                    foreach (string key in keys)
                    {
                        int tempInt = aggressionStore[key] - 1;
                        if (tempInt > minimumAggression)
                        {
                            aggressionStore[key] = tempInt;
                            if (debugAggression) ModAPI.Log.Write(key + " aggression lowered on day " + Clock.Day + " to " + tempInt);
                        }
                    }
                    aggressionStore["mutantDayCycleDay"] = Clock.Day;
                    NoAutoAggression.LockAggression();
                }
            }
        }

        // check the last day aggression was lowered
        public static bool CheckMutantDayCycle()
        {
            // check and update day
            if (aggressionStore.ContainsKey("mutantDayCycleDay"))
            {
                if (aggressionStore["mutantDayCycleDay"] != Clock.Day)
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
            if (!aggressionLock)
            {
                NAAUpdateSaveSlot();
                NoAutoAggression.LockAggression();
                if (!File.Exists(noAutoAggressionSaveSlot))
                {
                    System.IO.Directory.CreateDirectory(noAutoAggressionSaveSlot);
                }
                var serializer = new XmlSerializer(typeof(SerializableDictionary<string, int>));
                var stream = new FileStream(noAutoAggressionSaveSlot + "/aggression.xml", FileMode.Create);
                serializer.Serialize(stream, aggressionStore);
                stream.Close();
                NoAutoAggression.LockAggression();
            }
        }

        // lock aggressionstore
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void LockAggression()
        {
            aggressionLock = !aggressionLock;
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
        public static void NAAUpdateSaveSlot()
        {
            noAutoAggressionSaveSlot = noAutoAggressionMainSavePath + SaveSlotUtils.GetLocalSlotPath().Substring(SaveSlotUtils.GetLocalSlotPath().Length - 6);
            if (debugSaveSlot) ModAPI.Log.Write("Current SaveSlot: " + noAutoAggressionSaveSlot);
        }
    }

    // copied from https://weblogs.asp.net/pwelter34/444961 Thank you sooooo much!
    [XmlRoot("dictionary")]
    public class SerializableDictionary<TKey, TValue>
    : Dictionary<TKey, TValue>, IXmlSerializable
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
            // save aggression to file
            NoAutoAggression.SaveAggression();
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
            if ((!base.creepy) && (!base.fsmInCave.Value))
            {
                base.aggression = NoAutoAggression.GetAggression(base.ai, base.aggression);
                base.fsmAggresion.Value = 0;
            }
        }
    }

    // several tasks/behaviour settings for mutants
    class NAAMutantAiManager : mutantAiManager
    {
        // to take out all the auto aggression
        private void UpdateAttackChance()
        {
            if ((!base.searchFunctions.fsmInCave.Value) && (!base.setup.ai.creepy && !base.setup.ai.creepy_baby && !base.setup.ai.creepy_boss && !base.setup.ai.creepy_fat && !base.setup.ai.creepy_male))
            {
                base.fsmAttackChance.Value = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiAttackChanceRatio) / 10);
                base.fsmAttack = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiFollowUpAfterAttackRatio) / 10);
                base.fsmRunTowardsScream.Value = UnityEngine.Random.Range(0f, base.fsmAttackChance.Value);
                base.fsmScreamRunTowards.Value = UnityEngine.Random.Range(0f, base.fsmAttackChance.Value);
                base.fsmScream.Value = UnityEngine.Random.Range(0f, base.fsmAttackChance.Value);
                base.fsmBackAway.Value = Mathf.Clamp(2 - base.fsmAttackChance.Value, 0, 2);
                base.fsmDisengage.Value = Mathf.Clamp(2 - base.fsmAttackChance.Value, 0, 2);
                if (NoAutoAggression.debugAggression) ModAPI.Log.Write("Mutant set this attackchance: " + base.fsmAttackChance.Value.ToString("N3"));
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
    }

    // event triggered when a mutant is hit
    class NAAMutantAnimatorControl : mutantAnimatorControl
    {
        // increase aggression if hit by player
        public override void runGotHitScripts()
        {
            // run normal code
            base.runGotHitScripts();
            // increase aggression
            if ((base.setup.search.currentTarget.CompareTag("Player") || base.setup.search.currentTarget.CompareTag("PlayerNet") || base.setup.search.currentTarget.CompareTag("PlayerRemote")) && (!base.setup.ai.creepy && !base.setup.ai.creepy_baby && !base.setup.ai.creepy_boss && !base.setup.ai.creepy_fat && !base.setup.ai.creepy_male) && (!base.setup.search.fsmInCave.Value))
            {
                base.setup.dayCycle.aggression += NoAutoAggression.aggressionHitIncrease;
                if (base.setup.dayCycle.aggression > NoAutoAggression.maximumAggression)
                {
                    base.setup.dayCycle.aggression = NoAutoAggression.maximumAggression;
                }
                base.setup.dayCycle.aggression = NoAutoAggression.StoreAggression(base.ai, base.setup.dayCycle.aggression);
                base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = 0;
            }
        }
    }
}

