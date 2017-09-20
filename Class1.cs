using System.IO;
using UnityEngine;
using TheForest.Utils;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Xml.Serialization;
using TheForest.Utils.Settings;
using System.Collections;

namespace NoAutoAggression
{
    class NoAutoAggression : MonoBehaviour
    {
        private static Dictionary<string, int> aggressionStore;
        private static int mutantDayCycleDay = Clock.Day;
        private static string noAutoAggressionMainSavePath = "C:/Program Files (x86)/Steam/steamapps/common/The Forest/Mods/NoAutoAggression/";
        private static string noAutoAggressionSavePath;
        private static bool aggressionLock = false;
        //private static NoAutoAggressionScenes NAASceneManager;
        // static values
        public static List<int> NAAItemDB = new List<int> { 33, 38, 47, 60, 81, 90, 94, 99, 109, 115, 178, 189, 190, 193 };
        private static int minimumAggression = -1;
        public static int maximumAggression = 20;
        public static int aggressionHitIncrease = 5;
        // debug yes/no
        public static bool debugAggression = false;
        public static bool debugScenes = true;

        [ModAPI.Attributes.ExecuteOnGameStart]
        static void AddMeToScene()
        {
            GameObject GO = new GameObject("__NoAutoAggression__");
            GO.AddComponent<NoAutoAggression>();
            GO.AddComponent<NoAutoAggressionScenes>();
        }

        private void Update()
        {
            if (ModAPI.Input.GetButtonDown("SpawnItem"))
            {
                if (debugScenes) ModAPI.Log.Write("SpawnItem button pressed!");
                GameObject.FindObjectOfType<NoAutoAggression>().GetComponent<NoAutoAggressionScenes>().StartScene(0);
            }
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

    public class NoAutoAggressionScenes : MonoBehaviour
    {
        private List<int> NAAItemDB;
        private Coroutine scene0Coroutine;
        private Coroutine sceneDCoroutine;

        void Start()
        {
            NAAItemDB = NoAutoAggression.NAAItemDB;
            if (NoAutoAggression.debugScenes) ModAPI.Log.Write("NoAutoAggressionScenes initialized");
        }

        public void StartScene(int scene)
        {
            if (NoAutoAggression.debugScenes) ModAPI.Log.Write("StartScene method reached!");
            switch (scene)
            {
                case 0:
                    scene0Coroutine = StartCoroutine(Scene0());
                    break;
                default:
                    sceneDCoroutine = StartCoroutine(DummyRoutune());
                    break;
            }
        }

        // dummy
        private IEnumerator DummyRoutune()
        {
            yield break;
        }

        // random item spawn
        private IEnumerator Scene0()
        {
            List<GameObject> myEnemies = LocalPlayer.ScriptSetup.targetFunctions.visibleEnemies;
            if (myEnemies.Count > 0)
            {
                foreach (GameObject en in myEnemies)
                {
                    mutantTypeSetup myMutant = en.GetComponent<mutantTypeSetup>();
                    if ((myMutant.setup.dayCycle.aggression < 10) && (myMutant.setup.ai.leader))
                    {
                        if (NoAutoAggression.debugScenes) ModAPI.Log.Write("found visible mutant leader with 0 aggression");
                        foreach (GameObject fam in myMutant.spawner.allMembers)
                        {
                            mutantTypeSetup famMutant = fam.GetComponent<mutantTypeSetup>();
                            famMutant.setup.ai.cancelDefaultActions();
                            famMutant.setup.pmBrain.FsmVariables.GetFsmBool("fearOverrideBool").Value = true;
                            famMutant.setup.pmCombat.FsmVariables.GetFsmBool("doGoToLeader").Value = true;
                            famMutant.setup.Invoke("resetGoToLeader", 10f);
                            famMutant.setup.Invoke("setFleeOverride", 75f);
                            famMutant.setup.pmBrain.SendEvent("toSetFearful");
                            famMutant.setup.aiManager.flee = true;
                            famMutant.setup.pmBrain.FsmVariables.GetFsmGameObject("fearTargetGo").Value = LocalPlayer.GameObject;
                            if (NoAutoAggression.debugScenes) ModAPI.Log.Write("sent mutant to leader");
                        }
                        myMutant.setup.pmBrain.FsmVariables.GetFsmBool("fearOverrideBool").Value = true;
                        myMutant.setup.enemyEvents.disableWeapon();
                        myMutant.setup.search.updateCurrentWaypoint(LocalPlayer.Transform.position + LocalPlayer.Transform.forward * 10f);
                        myMutant.setup.search.setToWaypoint();
                        for (int i = 0; ((i < 60) || (myMutant.setup.ai.mainPlayerDist > 12f)); i++)
                        {
                            if (NoAutoAggression.debugScenes) ModAPI.Log.Write("waiting!");
                            yield return null;
                        }
                        myMutant.setup.enemyEvents.playSightedScream();
                        if (NoAutoAggression.debugScenes) ModAPI.Log.Write("moved leader to player");
                        int rnd = (int)Random.Range(1f, (float)NoAutoAggression.NAAItemDB.Count);
                        GameObject present = TheForest.Items.Utils.ItemUtils.SpawnItem(NAAItemDB[rnd], (en.transform.position + en.transform.forward * 2f), Quaternion.identity);
                        GameObject.Instantiate(present);
                        if (NoAutoAggression.debugScenes) ModAPI.Log.Write("spawned item" + NAAItemDB[rnd]);
                        float randDelay = (float)Random.Range(0f, 0.5f);
                        myMutant.setup.Invoke("turnAround", randDelay);
                        myMutant.setup.search.findCloseCaveWayPoint();
                        myMutant.setup.search.getNextWayPoint();
                        myMutant.setup.search.updateCurrentWaypoint(myMutant.setup.currentWaypoint.transform.position);
                        myMutant.setup.search.setToWaypoint();
                        if (NoAutoAggression.debugScenes) ModAPI.Log.Write("Scene0 finished!");
                        //yield break;
                    }
                    else
                    {
                        if (NoAutoAggression.debugScenes) ModAPI.Log.Write("no suitable enemies");
                        yield break;
                    }
                }
            }
            else
            {
                if (NoAutoAggression.debugScenes) ModAPI.Log.Write("no suitable enemies");
                yield break;
            }
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
        protected override void Start()
        {
            // original code
            base.Start();
            // get/set initial aggression
            if (!base.creepy)
            {
                base.aggression = NoAutoAggression.GetAggression(base.ai, base.aggression);
                base.fsmAggresion.Value = 0;
            }
        }

        protected override void setDayConditions()
        {
            // original code
            base.setDayConditions();
            // reset aggression
            if (!base.creepy)
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

        public override void setOnStructureCombat()
        {
            // original code
            base.setOnStructureCombat();
            // reset current aggression
            base.setup.dayCycle.aggression = NoAutoAggression.GetAggression(base.setup.ai, base.setup.dayCycle.aggression);
            base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = 0;
        }

        public override void setPlaneCrashCombat()
        {
            // original code
            base.setAggressiveCombat();
            // reset current aggression
            base.setup.dayCycle.aggression = NoAutoAggression.GetAggression(base.setup.ai, base.setup.dayCycle.aggression);
            base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = 0;
        }


        public override void setCaveCombat()
        {
            // original code
            base.setAggressiveCombat();
            // reset current aggression
            base.setup.dayCycle.aggression = NoAutoAggression.GetAggression(base.setup.ai, base.setup.dayCycle.aggression);
            base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = 0;
        }


        public override void setDefaultCombat()
        {
            // original code
            base.setAggressiveCombat();
            // reset current aggression
            base.setup.dayCycle.aggression = NoAutoAggression.GetAggression(base.setup.ai, base.setup.dayCycle.aggression);
            base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = 0;
        }

        public override void setAggressiveCombat()
        {
            // original code
            base.setAggressiveCombat();
            // reset current aggression
            base.setup.dayCycle.aggression = NoAutoAggression.GetAggression(base.setup.ai, base.setup.dayCycle.aggression);
            base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = 0;
        }

        public override void setSkinnyAggressiveCombat()
        {
            // original code
            base.setSkinnyAggressiveCombat();
            // reset current aggression
            base.setup.dayCycle.aggression = NoAutoAggression.GetAggression(base.setup.ai, base.setup.dayCycle.aggression);
            base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = 0;
        }

        public override void setSkinnedMutantCombat()
        {
            // original code
            base.setSkinnedMutantCombat();
            // reset current aggression
            base.setup.dayCycle.aggression = NoAutoAggression.GetAggression(base.setup.ai, base.setup.dayCycle.aggression);
            base.setup.pmBrain.FsmVariables.GetFsmInt("aggression").Value = 0;
        }

        public override void setDayStalking()
        {
            // original code
            base.setDayStalking();
            // reset attackchance based on aggression
            base.fsmAttackChance.Value = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiAttackChanceRatio) / 10);
            if (NoAutoAggression.debugAggression) ModAPI.Log.Write("Mutant set this attackchance  " + base.fsmAttackChance.Value.ToString("N3"));
        }

        public override void setDefaultStalking()
        {
            // original code
            base.setDefaultStalking();
            // reset attackchance based on aggression
            base.fsmAttackChance.Value = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiAttackChanceRatio) / 10);
            if (NoAutoAggression.debugAggression) ModAPI.Log.Write("Mutant set this attackchance  " + base.fsmAttackChance.Value.ToString("N3"));
        }

        public override void setPlaneCrashStalking()
        {
            // original code
            base.setPlaneCrashStalking();
            // reset attackchance based on aggression
            base.fsmAttackChance.Value = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiAttackChanceRatio) / 10);
            if (NoAutoAggression.debugAggression) ModAPI.Log.Write("Mutant set this attackchance  " + base.fsmAttackChance.Value.ToString("N3"));
        }

        public override void setSkinnyNightStalking()
        {
            // original code
            base.setSkinnyNightStalking();
            // reset attackchance based on aggression
            base.fsmAttackChance.Value = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiAttackChanceRatio) / 10);
            if (NoAutoAggression.debugAggression) ModAPI.Log.Write("Mutant set this attackchance  " + base.fsmAttackChance.Value.ToString("N3"));
        }

        public override void setSkinnyStalking()
        {
            // original code
            base.setSkinnyStalking();
            // reset attackchance based on aggression
            base.fsmAttackChance.Value = (float)((base.setup.dayCycle.aggression * GameSettings.Ai.aiAttackChanceRatio) / 10);
            if (NoAutoAggression.debugAggression) ModAPI.Log.Write("Mutant set this attackchance  " + base.fsmAttackChance.Value.ToString("N3"));
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
            if ((base.setup.search.currentTarget.CompareTag("Player") || base.setup.search.currentTarget.CompareTag("PlayerNet") || base.setup.search.currentTarget.CompareTag("PlayerRemote")) && (!this.setup.ai.creepy && !this.setup.ai.creepy_baby && !this.setup.ai.creepy_boss && !this.setup.ai.creepy_fat && !this.setup.ai.creepy_male))
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

