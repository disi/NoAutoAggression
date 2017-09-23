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
        private static string noAutoAggressionSavePath;
        private static bool aggressionLock = false;
        // static values
        public static List<int> NAAItemDB = new List<int> { 33, 38, 47, 60, 81, 90, 94, 99, 109, 115, 178, 189, 190, 193 };
        private static int minimumAggression = -1; // +1
        public static int maximumAggression = 20;
        public static int aggressionHitIncrease = 5;
        // debug yes/no
        public static bool debugAggression = false;

        [ModAPI.Attributes.ExecuteOnGameStart]
        static void AddMeToScene()
        {
            GameObject GO = new GameObject("__NoAutoAggression__");
            GO.AddComponent<NoAutoAggression>();
        }

        // is called by NAASpawnManager in Start() usually when a new game is loaded
        public static void CreateAggressionStore()
        {
            // get our aggression
            aggressionStore = null;
            noAutoAggressionSavePath = noAutoAggressionMainSavePath + SaveSlotUtils.GetLocalSlotPath().Substring(SaveSlotUtils.GetLocalSlotPath().Length - 6);
            if (File.Exists(noAutoAggressionSavePath + "/aggression.xml"))
            {
                var serializer = new XmlSerializer(typeof(SerializableDictionary<string, int>));
                var stream = new FileStream(noAutoAggressionSavePath + "/aggression.xml", FileMode.Open);
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
            if (aggressionLock)
            {
                return 0;
            }
            else
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
        }

        // is called by several threads to reset/update their aggression
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static int GetAggression(mutantAI myAI, int myAggression)
        {
            if (aggressionLock)
            {
                return 0;
            }
            else
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
        }

        // is called by NAASpawnManager once a day or when game is loaded
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void LowerAggression()
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
                NoAutoAggression.LockAggression();
            }
        }

        // is called by NAASpawnManager every few minutes or when mutantspawns are counted
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void SaveAggression()
        {
            if (!aggressionLock)
            {
                NoAutoAggression.LockAggression();
                noAutoAggressionSavePath = noAutoAggressionMainSavePath + SaveSlotUtils.GetLocalSlotPath().Substring(SaveSlotUtils.GetLocalSlotPath().Length - 6);
                if (!File.Exists(noAutoAggressionSavePath))
                {
                    System.IO.Directory.CreateDirectory(noAutoAggressionSavePath);
                }
                var serializer = new XmlSerializer(typeof(SerializableDictionary<string, int>));
                var stream = new FileStream(noAutoAggressionSavePath + "/aggression.xml", FileMode.Create);
                serializer.Serialize(stream, aggressionStore);
                stream.Close();
                NoAutoAggression.LockAggression();
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void LockAggression()
        {
            aggressionLock = !aggressionLock;
        }



        private static string AiName(mutantAI myMutantAi)
        {
            if (myMutantAi.femaleSkinny || myMutantAi.maleSkinny) return "skinny";
            else if (myMutantAi.skinned) return "skinned";
            else if (myMutantAi.painted) return "painted";
            else if (myMutantAi.pale) return "pale";
            else if (myMutantAi.creepy || myMutantAi.creepy_baby || myMutantAi.creepy_boss || myMutantAi.creepy_fat || myMutantAi.creepy_male) return "creepy";
            else return "regular";
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
        private int mutantDayCycleDay = Clock.Day;

        protected override void Start()
        {
            // original code
            base.Start();
            // create aggressionStore
            NoAutoAggression.CreateAggressionStore();
        }

        protected override void addToMutantAmounts()
        {
            // original code
            base.addToMutantAmounts();
            // lower aggression once a day by 1
            if (Clock.Day != mutantDayCycleDay)
            {
                mutantDayCycleDay = Clock.Day;
                NoAutoAggression.LowerAggression();
            }
            // save aggression to file
            NoAutoAggression.SaveAggression();
        }

        private void OnDestroy()
        {
            // save aggression to file
            NoAutoAggression.SaveAggression();
        }
    }

    // one for each mutant to set daily routine
    class NAAMutantDayCycle : mutantDayCycle
    {
        // store old aggression
        private int oldAggression;

        void Update()
        {
            if ((!base.creepy) && (oldAggression != base.aggression) && (!base.fsmInCave.Value))
            {
                base.aggression = NoAutoAggression.GetAggression(base.ai, base.aggression);
                base.fsmAggresion.Value = 0;
                oldAggression = base.aggression;
                if (NoAutoAggression.debugAggression) ModAPI.Log.Write("Mutant set to this aggression: " + base.aggression);
            }
        }
    }

    // several tasks/behaviour settings for mutants
    class NAAMutantAiManager : mutantAiManager
    {
        // store old attackchance
        private float oldAttackChance;

        // to take out all the auto aggression
        void Update()
        {
            if ((oldAttackChance != base.fsmAttackChance.Value) && (!base.searchFunctions.fsmInCave.Value))
            {
                base.fsmAttackChance.Value = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiAttackChanceRatio) / 10);
                base.fsmAttack = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiFollowUpAfterAttackRatio) / 10);
                base.fsmRunTowardsScream.Value = UnityEngine.Random.Range(0f, base.fsmAttackChance.Value);
                base.fsmScreamRunTowards.Value = UnityEngine.Random.Range(0f, base.fsmAttackChance.Value);
                base.fsmScream.Value = UnityEngine.Random.Range(0f, base.fsmAttackChance.Value);
                base.fsmBackAway.Value = 1 - base.fsmAttackChance.Value;
                base.fsmDisengage.Value = 1 - base.fsmAttackChance.Value;
                oldAttackChance = base.fsmAttackChance.Value;
                if (NoAutoAggression.debugAggression) ModAPI.Log.Write("Mutant set this attackchance: " + base.fsmAttackChance.Value.ToString("N3"));
            }
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

