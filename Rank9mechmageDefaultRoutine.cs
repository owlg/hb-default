using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using Buddy.Coroutines;
using HREngine.Bots;
using IronPython.Modules;
using log4net;
using Microsoft.Scripting.Hosting;
using Triton.Bot;
using Triton.Common;
using Triton.Game;
using Triton.Game.Data;

//!CompilerOption|AddRef|IronPython.dll
//!CompilerOption|AddRef|IronPython.Modules.dll
//!CompilerOption|AddRef|Microsoft.Scripting.dll
//!CompilerOption|AddRef|Microsoft.Dynamic.dll
//!CompilerOption|AddRef|Microsoft.Scripting.Metadata.dll
using Triton.Game.Mapping;

using Logger = Triton.Common.LogUtilities.Logger;

namespace HREngine.Bots
{
    public class DefaultRoutine : IRoutine
    {
        private static readonly ILog Log = Logger.GetLoggerInstanceForType();
        private readonly ScriptManager _scriptManager = new ScriptManager();

        private readonly List<Tuple<string, string>> _mulliganRules = new List<Tuple<string, string>>();

        private int dirtyTargetSource = -1;
        private int stopAfterWins = 30;
        private int concedeLvl = 5; // the rank, till you want to concede
        private int dirtytarget = -1;
        private int dirtychoice = -1;
        private string choiceCardId = "";
        DateTime starttime = DateTime.Now;
        bool enemyConcede = false;

        public bool learnmode = false;
        public bool printlearnmode = true;

        bool useExternalProcess = true;
        public bool passiveWaiting = false;

        Behavior behave = new BehaviorControl();//change this to new BehaviorRush() for rush mode

        public DefaultRoutine()
        {
            // Global rules. Never keep a 3+ minion, unless it's Bolvar Fordragon (paladin).
            _mulliganRules.Add(new Tuple<string, string>("True", "card.Entity.Cost >= 3 and card.Entity.Id != \"GVG_063\""));

            // Never keep Tracking.
            //_mulliganRules.Add(new Tuple<string, string>("mulliganData.UserClass == TAG_CLASS.HUNTER", "card.Entity.Id == \"DS1_184\""));

            // Example rule for self.
            //_mulliganRules.Add(new Tuple<string, string>("mulliganData.UserClass == TAG_CLASS.MAGE", "card.Cost >= 5"));

            // Example rule for opponents.
            //_mulliganRules.Add(new Tuple<string, string>("mulliganData.OpponentClass == TAG_CLASS.MAGE", "card.Cost >= 3"));

            // Example rule for matchups.
            //_mulliganRules.Add(new Tuple<string, string>("mulliganData.userClass == TAG_CLASS.HUNTER && mulliganData.OpponentClass == TAG_CLASS.DRUID", "card.Cost >= 2"));

            bool concede = false;

            // play with these settings###################################
            int enfacehp = 30;  // hp of enemy when your hero is allowed to attack the enemy face with his weapon
            int mxwde = 6500;   // numer of boards which are taken to the next deep-lvl
            int twotsamount = 200;          // number of boards where the next turn is simulated
            bool enemySecondTurnSim = true; // if he simulates the next players-turn, he also simulates the enemys respons

            bool playaround = true;  //play around some enemys aoe-spells?
            //these two probs are >= 0 and <= 100
            int playaroundprob = 50;    //probability where the enemy plays the aoe-spell, but your minions will not die through it
            int playaroundprob2 = 80;   // probability where the enemy plays the aoe-spell, and your minions can die!
            this.useExternalProcess = false; // use silver.exe for calculations a lot faster than turning it off (true = recomended)

            int amountBoardsInEnemyTurnSim = 70;
            int amountBoardsInEnemyTurnSimSecondStepp = 200;
            int amountBoardsInEnemySecondTurnSim = 70;

            int nextturnsimDeep = 6;
            int nextturnsimMaxWidth = 200;
            int nexttunsimMaxBoards = 400;

            bool secrets = true; // playing arround enemys secrets

            int alpha = 50; // weight of the second turn in calculation (0<= alpha <= 100)

            HREngine.Bots.Settings.Instance.simulatePlacement = true;  // set this true, and ai will simulate all placements, whether you have a alpha/flametongue/argus
            //use it only with useExternalProcess = true !!!!

            //###########################################################


            Mulligan.Instance.setAutoConcede(concede);

            Silverfish.Instance.setnewLoggFile();

            Helpfunctions.Instance.ErrorLog("set enemy-face-hp to: " + enfacehp);
            ComboBreaker.Instance.attackFaceHP = enfacehp;

            Ai.Instance.setMaxWide(mxwde);
            Helpfunctions.Instance.ErrorLog("set maxwide to: " + mxwde);

            Ai.Instance.setTwoTurnSimulation(false, twotsamount);
            Helpfunctions.Instance.ErrorLog("calculate the second turn of the " + twotsamount + " best boards");
            if (twotsamount >= 1)
            {
                //Ai.Instance.nextTurnSimulator.setEnemyTurnsim(enemySecondTurnSim);
                HREngine.Bots.Settings.Instance.simEnemySecondTurn = enemySecondTurnSim;
                if (enemySecondTurnSim) Helpfunctions.Instance.ErrorLog("simulates the enemy turn on your second turn");
            }

            if (secrets)
            {

                HREngine.Bots.Settings.Instance.useSecretsPlayArround = secrets;
                Helpfunctions.Instance.ErrorLog("playing arround secrets is " + secrets);
            }


            if (playaround)
            {
                HREngine.Bots.Settings.Instance.playarround = playaround;
                HREngine.Bots.Settings.Instance.playaroundprob = playaroundprob;
                HREngine.Bots.Settings.Instance.playaroundprob2 = playaroundprob2;
                Ai.Instance.setPlayAround();
                Helpfunctions.Instance.ErrorLog("activated playaround");
            }


            HREngine.Bots.Settings.Instance.setWeights(alpha);


            bool teststuff = false;
            // set to true, to run a testfile (requires test.txt file in filder where _cardDB.txt file is located)
            bool printstuff = false; // if true, the best board of the tested file is printet stepp by stepp

            HREngine.Bots.Settings.Instance.enemyTurnMaxWide = amountBoardsInEnemyTurnSim;
            HREngine.Bots.Settings.Instance.enemySecondTurnMaxWide = amountBoardsInEnemySecondTurnSim;

            HREngine.Bots.Settings.Instance.nextTurnDeep = nextturnsimDeep;
            HREngine.Bots.Settings.Instance.nextTurnMaxWide = nextturnsimMaxWidth;
            HREngine.Bots.Settings.Instance.nextTurnTotalBoards = nexttunsimMaxBoards;

            Helpfunctions.Instance.ErrorLog("----------------------------");
            Helpfunctions.Instance.ErrorLog("you are running uai V" + Silverfish.Instance.versionnumber);
            Helpfunctions.Instance.ErrorLog("----------------------------");

            if (this.useExternalProcess) Helpfunctions.Instance.ErrorLog("YOU USE SILVER.EXE FOR CALCULATION, MAKE SURE YOU STARTED IT!");
            if (this.useExternalProcess) Helpfunctions.Instance.ErrorLog("SILVER.EXE IS LOCATED IN: " + HREngine.Bots.Settings.Instance.path);

            if (teststuff)
            {
                Ai.Instance.autoTester(printstuff);
            }
        }

        #region Scripting

        private const string BoilerPlateExecute = @"
import sys
sys.stdout=ioproxy

def Execute():
    return bool({0})";

        public delegate void RegisterScriptVariableDelegate(ScriptScope scope);

        public bool GetCondition(string expression, IEnumerable<RegisterScriptVariableDelegate> variables)
        {
            var code = string.Format(BoilerPlateExecute, expression);
            var scope = _scriptManager.Scope;
            var scriptSource = _scriptManager.Engine.CreateScriptSourceFromString(code);
            scope.SetVariable("ioproxy", _scriptManager.IoProxy);
            foreach (var variable in variables)
            {
                variable(scope);
            }
            scriptSource.Execute(scope);
            return scope.GetVariable<Func<bool>>("Execute")();
        }

        public bool VerifyCondition(string expression,
            IEnumerable<string> variables, out Exception ex)
        {
            ex = null;
            try
            {
                var code = string.Format(BoilerPlateExecute, expression);
                var scope = _scriptManager.Scope;
                var scriptSource = _scriptManager.Engine.CreateScriptSourceFromString(code);
                scope.SetVariable("ioproxy", _scriptManager.IoProxy);
                foreach (var variable in variables)
                {
                    scope.SetVariable(variable, new object());
                }
                scriptSource.Compile();
            }
            catch (Exception e)
            {
                ex = e;
                return false;
            }
            return true;
        }

        #endregion

        #region Implementation of IAuthored

        /// <summary> The name of the routine. </summary>
        public string Name
        {
            get { return "DefaultRoutine"; }
        }

        /// <summary> The description of the routine. </summary>
        public string Description
        {
            get { return "The default routine for Hearthbuddy."; }
        }

        /// <summary>The author of this routine.</summary>
        public string Author
        {
            get { return "Bossland GmbH"; }
        }

        /// <summary>The version of this routine.</summary>
        public string Version
        {
            get { return "0.0.1.1"; }
        }

        #endregion

        #region Implementation of IBase

        /// <summary>Initializes this routine.</summary>
        public void Initialize()
        {
            _scriptManager.Initialize(null,
                new List<string>
                {
                    "Triton.Game",
                    "Triton.Bot",
                    "Triton.Common",
                    "Triton.Game.Mapping",
                    "Triton.Game.Abstraction"
                });
        }

        /// <summary>Deinitializes this routine.</summary>
        public void Deinitialize()
        {
            _scriptManager.Deinitialize();
        }

        #endregion

        #region Implementation of IRunnable

        /// <summary> The routine start callback. Do any initialization here. </summary>
        public void Start()
        {
            GameEventManager.NewGame += GameEventManagerOnNewGame;
            GameEventManager.GameOver += GameEventManagerOnGameOver;
            GameEventManager.QuestUpdate += GameEventManagerOnQuestUpdate;
            GameEventManager.ArenaRewards += GameEventManagerOnArenaRewards;

            foreach (var tuple in _mulliganRules)
            {
                Exception ex;
                if (
                    !VerifyCondition(tuple.Item1, new List<string> {"mulliganData"}, out ex))
                {
                    Log.ErrorFormat("[Start] There is an error with a mulligan execution condition [{1}]: {0}.", ex,
                        tuple.Item1);
                    BotManager.Stop();
                }

                if (
                    !VerifyCondition(tuple.Item2, new List<string> {"mulliganData", "card"},
                        out ex))
                {
                    Log.ErrorFormat("[Start] There is an error with a mulligan card condition [{1}]: {0}.", ex,
                        tuple.Item2);
                    BotManager.Stop();
                }
            }
        }

        /// <summary> The routine tick callback. Do any update logic here. </summary>
        public void Tick()
        {
        }

        /// <summary> The routine stop callback. Do any pre-dispose cleanup here. </summary>
        public void Stop()
        {
            GameEventManager.NewGame -= GameEventManagerOnNewGame;
            GameEventManager.GameOver -= GameEventManagerOnGameOver;
            GameEventManager.QuestUpdate -= GameEventManagerOnQuestUpdate;
            GameEventManager.ArenaRewards -= GameEventManagerOnArenaRewards;
        }

        #endregion

        #region Implementation of IConfigurable

        /// <summary> The routine's settings control. This will be added to the Hearthbuddy Settings tab.</summary>
        public UserControl Control
        {
            get
            {
                using (var fs = new FileStream(@"Routines\DefaultRoutine\SettingsGui.xaml", FileMode.Open))
                {
                    var root = (UserControl) XamlReader.Load(fs);

                    // Your settings binding here.

                    // ArenaPreferredClass1
                    if (
                        !Wpf.SetupComboBoxItemsBinding(root, "ArenaPreferredClass1ComboBox", "AllClasses",
                            BindingMode.OneWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxItemsBinding failed for 'ArenaPreferredClass1ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    if (
                        !Wpf.SetupComboBoxSelectedItemBinding(root, "ArenaPreferredClass1ComboBox",
                            "ArenaPreferredClass1", BindingMode.TwoWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxSelectedItemBinding failed for 'ArenaPreferredClass1ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    // ArenaPreferredClass2
                    if (
                        !Wpf.SetupComboBoxItemsBinding(root, "ArenaPreferredClass2ComboBox", "AllClasses",
                            BindingMode.OneWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxItemsBinding failed for 'ArenaPreferredClass2ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    if (
                        !Wpf.SetupComboBoxSelectedItemBinding(root, "ArenaPreferredClass2ComboBox",
                            "ArenaPreferredClass2", BindingMode.TwoWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxSelectedItemBinding failed for 'ArenaPreferredClass2ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    // ArenaPreferredClass3
                    if (
                        !Wpf.SetupComboBoxItemsBinding(root, "ArenaPreferredClass3ComboBox", "AllClasses",
                            BindingMode.OneWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxItemsBinding failed for 'ArenaPreferredClass3ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    if (
                        !Wpf.SetupComboBoxSelectedItemBinding(root, "ArenaPreferredClass3ComboBox",
                            "ArenaPreferredClass3", BindingMode.TwoWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxSelectedItemBinding failed for 'ArenaPreferredClass3ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    // ArenaPreferredClass4
                    if (
                        !Wpf.SetupComboBoxItemsBinding(root, "ArenaPreferredClass4ComboBox", "AllClasses",
                            BindingMode.OneWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxItemsBinding failed for 'ArenaPreferredClass4ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    if (
                        !Wpf.SetupComboBoxSelectedItemBinding(root, "ArenaPreferredClass4ComboBox",
                            "ArenaPreferredClass4", BindingMode.TwoWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxSelectedItemBinding failed for 'ArenaPreferredClass4ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    // ArenaPreferredClass5
                    if (
                        !Wpf.SetupComboBoxItemsBinding(root, "ArenaPreferredClass5ComboBox", "AllClasses",
                            BindingMode.OneWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxItemsBinding failed for 'ArenaPreferredClass5ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    if (
                        !Wpf.SetupComboBoxSelectedItemBinding(root, "ArenaPreferredClass5ComboBox",
                            "ArenaPreferredClass5", BindingMode.TwoWay, DefaultRoutineSettings.Instance))
                    {
                        Log.DebugFormat(
                            "[SettingsControl] SetupComboBoxSelectedItemBinding failed for 'ArenaPreferredClass5ComboBox'.");
                        throw new Exception("The SettingsControl could not be created.");
                    }

                    // Your settings event handlers here.

                    return root;
                }
            }
        }

        /// <summary>The settings object. This will be registered in the current configuration.</summary>
        public JsonSettings Settings
        {
            get { return DefaultRoutineSettings.Instance; }
        }

        #endregion

        #region Implementation of IRoutine

        /// <summary>
        /// Sends data to the routine with the associated name.
        /// </summary>
        /// <param name="name">The name of the configuration.</param>
        /// <param name="param">The data passed for the configuration.</param>
        public void SetConfiguration(string name, params object[] param)
        {
        }

        /// <summary>
        /// Requests data from the routine with the associated name.
        /// </summary>
        /// <param name="name">The name of the configuration.</param>
        /// <returns>Data from the routine.</returns>
        public object GetConfiguration(string name)
        {
            return null;
        }

        /// <summary>
        /// The routine's coroutine logic to execute.
        /// </summary>
        /// <param name="type">The requested type of logic to execute.</param>
        /// <param name="context">Data sent to the routine from the bot for the current logic.</param>
        /// <returns>true if logic was executed to handle this type and false otherwise.</returns>
        public async Task<bool> Logic(string type, object context)
        {
            // The bot is requesting mulligan logic.
            if (type == "mulligan")
            {
                await MulliganLogic(context as MulliganData);
                return true;
            }

            // The bot is requesting emote logic.
            if (type == "emote")
            {
                await EmoteLogic(context as EmoteData);
                return true;
            }

            // The bot is requesting our turn logic.
            if (type == "our_turn")
            {
                await OurTurnLogic();
                return true;
            }

            // The bot is requesting opponent turn logic.
            if (type == "opponent_turn")
            {
                await OpponentTurnLogic();
                return true;
            }

            // The bot is requesting arena draft logic.
            if (type == "arena_draft")
            {
                await ArenaDraftLogic(context as ArenaDraftData);
                return true;
            }

            // The bot is requesting quest handling logic.
            if (type == "handle_quests")
            {
                await HandleQuestsLogic(context as QuestData);
                return true;
            }

            // Whatever the current logic type is, this routine doesn't implement it.
            return false;
        }

        #region Mulligan

        private int RandomMulliganThinkTime()
        {
            var random = Client.Random;
            var type = random.Next(0, 100)%4;

            if (type == 0)
            {
                return random.Next(1000, 1500);
            }

            if (type == 1)
            {
                return random.Next(2500, 3500);
            }

            if (type == 2)
            {
                return random.Next(4500, 5500);
            }

            return 0;
        }

        /// <summary>
        /// This task implements custom mulligan choosing logic for the bot.
        /// The user is expected to set the Mulligans list elements to true/false 
        /// to signal to the bot which cards should/shouldn't be mulliganed. 
        /// This task should also implement humanization factors, such as hovering 
        /// over cards, or delaying randomly before returning, as the mulligan 
        /// process takes place as soon as the task completes.
        /// </summary>
        /// <param name="mulliganData">An object that contains relevant data for the mulligan process.</param>
        /// <returns></returns>
        public async Task MulliganLogic(MulliganData mulliganData)
        {
            Log.InfoFormat("[Mulligan] {0} vs {1}.", mulliganData.UserClass, mulliganData.OpponentClass);

            var count = mulliganData.Cards.Count;

            for (var i = 0; i < count; i++)
            {
                var card = mulliganData.Cards[i];

                try
                {
                    foreach (var tuple in _mulliganRules)
                    {
                        if (GetCondition(tuple.Item1,
                            new List<RegisterScriptVariableDelegate>
                            {
                                scope => scope.SetVariable("mulliganData", mulliganData)
                            }))
                        {
                            if (GetCondition(tuple.Item2,
                                new List<RegisterScriptVariableDelegate>
                                {
                                    scope => scope.SetVariable("mulliganData", mulliganData),
                                    scope => scope.SetVariable("card", card)
                                }))
                            {
                                mulliganData.Mulligans[i] = true;
                                Log.InfoFormat(
                                    "[Mulligan] {0} should be mulliganed because it matches the user's mulligan rule: [{1}] ({2}).",
                                    card.Entity.Id, tuple.Item2, tuple.Item1);
                            }
                        }
                        else
                        {
                            Log.InfoFormat(
                                "[Mulligan] The mulligan execution check [{0}] is false, so the mulligan criteria [{1}] will not be evaluated.",
                                tuple.Item1, tuple.Item2);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("[Mulligan] An exception occurred: {0}.", ex);
                    BotManager.Stop();
                    return;
                }
            }

            var thinkList = new List<KeyValuePair<int, int>>();
            for (var i = 0; i < count; i++)
            {
                thinkList.Add(new KeyValuePair<int, int>(i%count, RandomMulliganThinkTime()));
            }
            thinkList.Shuffle();

            foreach (var entry in thinkList)
            {
                var card = mulliganData.Cards[entry.Key];

                Log.InfoFormat("[Mulligan] Now thinking about mulliganing {0} for {1} ms.", card.Entity.Id, entry.Value);

                // Instant think time, skip the card.
                if (entry.Value == 0)
                    continue;

                Client.MouseOver(card.InteractPoint);

                await Coroutine.Sleep(entry.Value);
            }
        }

        #endregion

        #region Emote

        /// <summary>
        /// This task implements player emote detection logic for the bot.
        /// </summary>
        /// <param name="data">An object that contains relevant data for the emote event.</param>
        /// <returns></returns>
        public async Task EmoteLogic(EmoteData data)
        {
            Log.InfoFormat("[Emote] The enemy is using the emote [{0}].", data.Emote);

            if (data.Emote == EmoteType.GREETINGS)
            {
            }
            else if (data.Emote == EmoteType.WELL_PLAYED)
            {
            }
            else if (data.Emote == EmoteType.OOPS)
            {
            }
            else if (data.Emote == EmoteType.THREATEN)
            {
            }
            else if (data.Emote == EmoteType.THANKS)
            {
            }
            else if (data.Emote == EmoteType.SORRY)
            {
            }
        }

        #endregion

        #region Turn

        /// <summary>
        /// Under construction.
        /// </summary>
        /// <returns></returns>
        public async Task OurTurnLogic()
        {
            if (this.passiveWaiting && Silverfish.Instance.waitingForSilver)
            {
                if (!Silverfish.Instance.readActionFile(true))
                {
                    await Coroutine.Sleep(50);
                    return;
                }
            }

            if (this.learnmode && (TritonHs.IsInTargetMode() || TritonHs.IsInChoiceMode()))
            {
                await Coroutine.Sleep(50);
                return;
            }

            if (TritonHs.IsInTargetMode())
            {
                if (dirtytarget >= 0)
                {
                    Log.Info("targeting...");
                    HSCard source = null;
                    if (dirtyTargetSource == 9000) // 9000 = ability
                    {
                        source = TritonHs.OurHeroPowerCard;
                    }
                    else
                    {
                        source = getEntityWithNumber(dirtyTargetSource);
                    }
                    HSCard target = getEntityWithNumber(dirtytarget);

                    if (target == null)
                    {
                        Log.Error("target is null...");
                        TritonHs.CancelTargetingMode();
                        return;
                    }

                    dirtytarget = -1;
                    dirtyTargetSource = -1;

                    if (source == null) await TritonHs.DoTarget(target);
                    else await source.DoTarget(target);

                    await Coroutine.Sleep(1000);

                    return;
                }

                Log.Error("target failure...");
                TritonHs.CancelTargetingMode();
            }

            if (TritonHs.IsInChoiceMode())
            {
                if (dirtychoice >= 1)
                {
                    //dirtychoice == 1 -> choose left card, 
                    // dirty choice == 2 -> right card

                    Helpfunctions.Instance.logg("chooses the card: " + dirtychoice);
                    if (dirtychoice == 1)
                    {
                        TritonHs.ChooseOneClickLeft();
                    }
                    else
                    {
                        TritonHs.ChooseOneClickRight();
                    }
                    dirtychoice = -1;
                    await Coroutine.Sleep(2000);
                    return;
                }
                //Todo: ultimate tracking-simulation!
                var r = new Random();
                int choice = r.Next(0, 2);
                Helpfunctions.Instance.logg("chooses a random card");
                TritonHs.ChooseOneClickLeft();
                await Coroutine.Sleep(2000);
                return;
            }

            bool templearn = Silverfish.Instance.updateEverything(behave, this.useExternalProcess, this.passiveWaiting);
            if (templearn == true) this.printlearnmode = true;

            if (this.passiveWaiting && Silverfish.Instance.waitingForSilver)
            {
                await Coroutine.Sleep(50);
                return;
            }

            if (this.learnmode)
            {
                if (this.printlearnmode)
                {
                    Ai.Instance.simmulateWholeTurnandPrint();
                }
                this.printlearnmode = false;

                //do nothing
                await Coroutine.Sleep(50);
                return;
            }

            var moveTodo = Ai.Instance.bestmove;
            if (moveTodo == null || moveTodo.actionType == actionEnum.endturn)
            {
                Helpfunctions.Instance.ErrorLog("end turn");
                await TritonHs.EndTurn();
                return;
            }
            Helpfunctions.Instance.ErrorLog("play action");
            moveTodo.print();

            //play the move#########################################################################

            //play a card form hand
            if (moveTodo.actionType == actionEnum.playcard)
            {
                HSCard cardtoplay = getCardWithNumber(moveTodo.card.entity);
                if (moveTodo.target != null)
                {
                    HSCard target = getEntityWithNumber(moveTodo.target.entitiyID);
                    Helpfunctions.Instance.ErrorLog("play: " + cardtoplay.Name + " target: " + target.Name +
                                                    " targetEnt " + target.EntityId);
                    Helpfunctions.Instance.logg("play: " + cardtoplay.Name + " target: " + target.Name + " choice: " +
                                                moveTodo.druidchoice);

                    if (moveTodo.druidchoice >= 1)
                    {
                        dirtytarget = moveTodo.target.entitiyID;
                        dirtychoice = moveTodo.druidchoice; //1=leftcard, 2= rightcard
                        choiceCardId = moveTodo.card.card.cardIDenum.ToString();
                    }

                    //safe targeting stuff for hsbuddy
                    dirtyTargetSource = moveTodo.card.entity;
                    dirtytarget = moveTodo.target.entitiyID;


                    //we can place mobs (if api supports it)
                    /*
                if (moveTodo.card.card.type == CardDB.cardtype.MOB)
                {
                    //moveTodo.owntarget (maybe +1 (depends on api)) is the place, where the mob should be placed
                    //return;
                }
                */

                    // TODO: This logic needs to be reworked to integrate with SF better using the new setup.
                    // 

                    await cardtoplay.Pickup();
                    await Coroutine.Sleep(500);

                    if (moveTodo.card.card.type == CardDB.cardtype.MOB)
                    {
                        await cardtoplay.UseAt(moveTodo.place);
                    }
                    else if (moveTodo.card.card.type == CardDB.cardtype.WEAPON) // This fixes perdition's blade
                    {
                        await cardtoplay.UseOn(target.Card);
                    }
                    else if (moveTodo.card.card.type == CardDB.cardtype.SPELL)
                    {
                        await cardtoplay.UseOn(target.Card);
                    }
                    else
                    {
                        await cardtoplay.UseOn(target.Card); 
                        // not sure if this is right, but if we have a target, and it's not a MOB card,
                        // a targeting arrow should be created for us when we go to the destination.

                        //await cardtoplay.Use();
                    }
                    await Coroutine.Sleep(500);

                    return;
                }

                Helpfunctions.Instance.ErrorLog("play: " + cardtoplay.Name + " target nothing");
                Helpfunctions.Instance.logg("play: " + cardtoplay.Name + " choice: " + moveTodo.druidchoice);
                if (moveTodo.druidchoice >= 1)
                {
                    dirtychoice = moveTodo.druidchoice; //1=leftcard, 2= rightcard
                    choiceCardId = moveTodo.card.card.cardIDenum.ToString();
                }

                dirtyTargetSource = -1;
                dirtytarget = -1;

                //mob placement...
                /*
                if (moveTodo.card.card.type == CardDB.cardtype.MOB)
                {
                    //moveTodo.owntarget (maybe +1 (depends on api)) is the place, where the mob should be placed
                    //return;
                }*/

                await cardtoplay.Pickup();
                await Coroutine.Sleep(500);

                if (moveTodo.card.card.type == CardDB.cardtype.MOB)
                {
                    await cardtoplay.UseAt(moveTodo.place);
                }
                else
                {
                    await cardtoplay.Use();
                }
                await Coroutine.Sleep(500);

                return;
            }

            //attack with minion
            if (moveTodo.actionType == actionEnum.attackWithMinion)
            {
                HSCard attacker = getEntityWithNumber(moveTodo.own.entitiyID);
                HSCard target = getEntityWithNumber(moveTodo.target.entitiyID);
                Helpfunctions.Instance.ErrorLog("minion attack: " + attacker.Name + " target: " + target.Name);
                Helpfunctions.Instance.logg("minion attack: " + attacker.Name + " target: " + target.Name);
                await attacker.DoAttack(target);
                return;
            }
            //attack with hero
            if (moveTodo.actionType == actionEnum.attackWithHero)
            {
                HSCard attacker = getEntityWithNumber(moveTodo.own.entitiyID);
                HSCard target = getEntityWithNumber(moveTodo.target.entitiyID);
                dirtytarget = moveTodo.target.entitiyID;
                Helpfunctions.Instance.ErrorLog("heroattack: " + attacker.Name + " target: " + target.Name);
                Helpfunctions.Instance.logg("heroattack: " + attacker.Name + " target: " + target.Name);

                //safe targeting stuff for hsbuddy
                dirtyTargetSource = moveTodo.own.entitiyID;
                dirtytarget = moveTodo.target.entitiyID;
                await attacker.DoAttack(target);
                return;
            }

            //use ability
            if (moveTodo.actionType == actionEnum.useHeroPower)
            {
                HSCard cardtoplay = TritonHs.OurHeroPowerCard;

                // Drew: Hunter's hero power sets a target when it's not needed, so the bot clicks on the opponent.
                // Need to fix this in the AI itself, as the target should be implied?

                if (moveTodo.target != null)
                {
                    HSCard target = getEntityWithNumber(moveTodo.target.entitiyID);
                    dirtyTargetSource = 9000;
                    dirtytarget = moveTodo.target.entitiyID;

                    Helpfunctions.Instance.ErrorLog("use ablitiy: " + cardtoplay.Name + " target " + target.Name);
                    Helpfunctions.Instance.logg("use ablitiy: " + cardtoplay.Name + " target " + target.Name);

                    await cardtoplay.Pickup();
                    await Coroutine.Sleep(500);

                    await cardtoplay.UseOn(target.Card);
                    await Coroutine.Sleep(500);
                }
                else
                {
                    Helpfunctions.Instance.ErrorLog("use ablitiy: " + cardtoplay.Name + " target nothing");
                    Helpfunctions.Instance.logg("use ablitiy: " + cardtoplay.Name + " target nothing");

                    await cardtoplay.Pickup();
                    await Coroutine.Sleep(500);
                }

                return;
            }

            await TritonHs.EndTurn();
        }

        /// <summary>
        /// Under construction.
        /// </summary>
        /// <returns></returns>
        public async Task OpponentTurnLogic()
        {
            Log.InfoFormat("[OpponentTurn]");
        }

        #endregion

        #region ArenaDraft

        /// <summary>
        /// Under construction.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task ArenaDraftLogic(ArenaDraftData data)
        {
            Log.InfoFormat("[ArenaDraft]");

            // We don't have a hero yet, so choose one.
            if (data.Hero == null)
            {
                Log.InfoFormat("[ArenaDraft] Hero: [{0} ({3}) | {1} ({4}) | {2} ({5})].",
                    data.Choices[0].EntityDef.CardId, data.Choices[1].EntityDef.CardId, data.Choices[2].EntityDef.CardId,
                    data.Choices[0].EntityDef.Name, data.Choices[1].EntityDef.Name, data.Choices[2].EntityDef.Name);

                // Quest support logic!
                var questIds = TritonHs.CurrentQuests.Select(q => q.Id).ToList();
                foreach (var choice in data.Choices)
                {
                    var @class = choice.EntityDef.Class;
                    foreach (var questId in questIds)
                    {
                        if (TritonHs.IsQuestForClass(questId, @class))
                        {
                            data.Selection = choice;
                            Log.InfoFormat(
                                "[ArenaDraft] Choosing hero \"{0}\" because it matches a current quest.",
                                data.Selection.EntityDef.Name);
                            return;
                        }
                    }
                }

                // TODO: I'm sure there's a better way to do this, but w/e, no time to waste right now.

                // #1
                foreach (var choice in data.Choices)
                {
                    if ((TAG_CLASS)choice.EntityDef.Class == DefaultRoutineSettings.Instance.ArenaPreferredClass1)
                    {
                        data.Selection = choice;
                        Log.InfoFormat(
                            "[ArenaDraft] Choosing hero \"{0}\" because it matches the first preferred arena class.",
                            data.Selection.EntityDef.Name);
                        return;
                    }
                }

                // #2
                foreach (var choice in data.Choices)
                {
                    if ((TAG_CLASS)choice.EntityDef.Class == DefaultRoutineSettings.Instance.ArenaPreferredClass2)
                    {
                        data.Selection = choice;
                        Log.InfoFormat(
                            "[ArenaDraft] Choosing hero \"{0}\" because it matches the second preferred arena class.",
                            data.Selection.EntityDef.Name);
                        return;
                    }
                }

                // #3
                foreach (var choice in data.Choices)
                {
                    if ((TAG_CLASS)choice.EntityDef.Class == DefaultRoutineSettings.Instance.ArenaPreferredClass3)
                    {
                        data.Selection = choice;
                        Log.InfoFormat(
                            "[ArenaDraft] Choosing hero \"{0}\" because it matches the third preferred arena class.",
                            data.Selection.EntityDef.Name);
                        return;
                    }
                }

                // #4
                foreach (var choice in data.Choices)
                {
                    if ((TAG_CLASS)choice.EntityDef.Class == DefaultRoutineSettings.Instance.ArenaPreferredClass4)
                    {
                        data.Selection = choice;
                        Log.InfoFormat(
                            "[ArenaDraft] Choosing hero \"{0}\" because it matches the fourth preferred arena class.",
                            data.Selection.EntityDef.Name);
                        return;
                    }
                }

                // #5
                foreach (var choice in data.Choices)
                {
                    if ((TAG_CLASS)choice.EntityDef.Class == DefaultRoutineSettings.Instance.ArenaPreferredClass5)
                    {
                        data.Selection = choice;
                        Log.InfoFormat(
                            "[ArenaDraft] Choosing hero \"{0}\" because it matches the fifth preferred arena class.",
                            data.Selection.EntityDef.Name);
                        return;
                    }
                }

                // Choose a random hero.
                data.RandomSelection();

                Log.InfoFormat(
                    "[ArenaDraft] Choosing hero \"{0}\" because no other preferred arena classes were available.",
                    data.Selection.EntityDef.Name);

                return;
            }

            // Normal card choices.
            Log.InfoFormat("[ArenaDraft] Card: [{0} ({3}) | {1} ({4}) | {2} ({5})].", data.Choices[0].EntityDef.CardId,
                data.Choices[1].EntityDef.CardId, data.Choices[2].EntityDef.CardId, data.Choices[0].EntityDef.Name,
                data.Choices[1].EntityDef.Name, data.Choices[2].EntityDef.Name);

            /*Log.InfoFormat("[ArenaDraft] Current Deck:");
            foreach (var entry in data.Deck)
            {
                Log.InfoFormat("[ArenaDraft] {0} ({1})", entry.CardId, entry.Name);
            }*/

            var actor =
                data.Choices.Where(c => ArenavaluesReader.Get.ArenaValues.ContainsKey(c.EntityDef.CardId))
                    .OrderByDescending(c => ArenavaluesReader.Get.ArenaValues[c.EntityDef.CardId]).FirstOrDefault();
            if (actor != null)
            {
                data.Selection = actor;
            }
            else
            {
                data.RandomSelection();
            }
        }

        #endregion

        #region Handle Quests

        /// <summary>
        /// Under construction.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task HandleQuestsLogic(QuestData data)
        {
            Log.InfoFormat("[HandleQuests]");

            // Loop though all quest tiles.
            foreach (var questTile in data.QuestTiles)
            {
                // If we can't cancel a quest, we shouldn't try to.
                if (questTile.IsCancelable)
                {
                    // We never want to do this specific quest, if we've not started it.
                    // User logic may vary though, but this is just an example.
                    if (questTile.Achievement.Name.Equals("Beat Down") && questTile.Achievement.CurProgress == 0)
                    {
                        // Mark the quest tile to be canceled.
                        //questTile.ShouldCancel = true;

                        // We can only cancel *1* quest, so no point trying to process the rest.
                        return;
                    }
                }
            }
        }

        #endregion

        #endregion

        #region Override of Object

        /// <summary>
        /// ToString override.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name + ": " + Description;
        }

        #endregion

        private void GameEventManagerOnGameOver(object sender, GameOverEventArgs gameOverEventArgs)
        {
            Log.InfoFormat("[GameEventManagerOnGameOver] {0}{2} => {1}.", gameOverEventArgs.Result,
                GameEventManager.Instance.LastGamePresenceStatus, gameOverEventArgs.Conceded ? " [conceded]" : "");
        }

        private void GameEventManagerOnNewGame(object sender, NewGameEventArgs newGameEventArgs)
        {
            Log.InfoFormat("[GameEventManagerOnNewGame]");
        }

        private void GameEventManagerOnQuestUpdate(object sender, QuestUpdateEventArgs questUpdateEventArgs)
        {
            Log.InfoFormat("[GameEventManagerOnQuestUpdate]");
            foreach (var quest in TritonHs.CurrentQuests)
            {
                Log.InfoFormat("[GameEventManagerOnQuestUpdate] {0}: {1} ({2} / {3}) [{5}x {4}]", quest.Name, quest.Description, quest.CurProgress,
                    quest.MaxProgress, quest.RewardData[0].Type, quest.RewardData[0].Count);
            }
        }

        private void GameEventManagerOnArenaRewards(object sender, ArenaRewardsEventArgs arenaRewardsEventArgs)
        {
            Log.InfoFormat("[GameEventManagerOnArenaRewards]");
            foreach (var reward in arenaRewardsEventArgs.Rewards)
            {
                Log.InfoFormat("[GameEventManagerOnArenaRewards] {1}x {0}.", reward.Type, reward.Count);
            }
        }

        private HSCard getEntityWithNumber(int number)
        {
            foreach (HSCard e in getallEntitys())
            {
                if (number == e.EntityId) return e;
            }
            return null;
        }

        private HSCard getCardWithNumber(int number)
        {
            foreach (HSCard e in getallHandCards())
            {
                if (number == e.EntityId) return e;
            }
            return null;
        }

        private List<HSCard> getallEntitys()
        {
            var result = new List<HSCard>();
            HSCard ownhero = TritonHs.OurHero;
            HSCard enemyhero = TritonHs.EnemyHero;
            HSCard ownHeroAbility = TritonHs.OurHeroPowerCard;
            List<HSCard> list2 = TritonHs.GetCards(CardZone.Battlefield, true);
            List<HSCard> list3 = TritonHs.GetCards(CardZone.Battlefield, false);

            result.Add(ownhero);
            result.Add(enemyhero);
            result.Add(ownHeroAbility);

            result.AddRange(list2);
            result.AddRange(list3);

            return result;
        }

        private List<HSCard> getallHandCards()
        {
            List<HSCard> list = TritonHs.GetCards(CardZone.Hand, true);
            return list;
        }

    }
}
