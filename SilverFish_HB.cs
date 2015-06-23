using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Triton.Bot;
using Triton.Common;
using Triton.Game;
using Triton.Game.Mapping;

//using System.Linq;
// ReSharper disable InconsistentNaming

namespace HREngine.Bots
{
    public class Silverfish
    {
        public string versionnumber = "114.11";
        private bool singleLog = false;
        private string botbehave = "rush";
        public bool waitingForSilver = false;

        private Playfield lastpf;
        private Settings sttngs = Settings.Instance;

        private List<Minion> ownMinions = new List<Minion>();
        private List<Minion> enemyMinions = new List<Minion>();
        private List<Handmanager.Handcard> handCards = new List<Handmanager.Handcard>();
        private int ownPlayerController = 0;
        private List<string> ownSecretList = new List<string>();
        private int enemySecretCount = 0;
        private List<int> enemySecretList = new List<int>();

        private int currentMana = 0;
        private int ownMaxMana = 0;
        private int numOptionPlayedThisTurn = 0;
        private int numMinionsPlayedThisTurn = 0;
        private int cardsPlayedThisTurn = 0;
        private int ueberladung = 0;

        private int enemyMaxMana = 0;

        private string ownHeroWeapon = "";
        private int heroWeaponAttack = 0;
        private int heroWeaponDurability = 0;

        private string enemyHeroWeapon = "";
        private int enemyWeaponAttack = 0;
        private int enemyWeaponDurability = 0;

        private string heroname = "";
        private string enemyHeroname = "";

        private CardDB.Card heroAbility = new CardDB.Card();
        private bool ownAbilityisReady = false;
        private CardDB.Card enemyAbility = new CardDB.Card();

        private int anzcards = 0;
        private int enemyAnzCards = 0;

        private int ownHeroFatigue = 0;
        private int enemyHeroFatigue = 0;
        private int ownDecksize = 0;
        private int enemyDecksize = 0;

        private Minion ownHero;
        private Minion enemyHero;

        private static Silverfish instance;

        public static Silverfish Instance
        {
            get { return instance ?? (instance = new Silverfish()); }
        }

        private Silverfish()
        {
            this.singleLog = Settings.Instance.writeToSingleFile;
            Helpfunctions.Instance.ErrorLog("init Silverfish");
            string p = "." + System.IO.Path.DirectorySeparatorChar + "Routines" + System.IO.Path.DirectorySeparatorChar + "DefaultRoutine" +
                       System.IO.Path.DirectorySeparatorChar + "Silverfish" + System.IO.Path.DirectorySeparatorChar;
            string path = p + "UltimateLogs" + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(path);
            sttngs.setFilePath(p + "Data" + Path.DirectorySeparatorChar);

            if (!singleLog)
            {
                sttngs.setLoggPath(path);
            }
            else
            {
                sttngs.setLoggPath(p);
                sttngs.setLoggFile("UILogg.txt");
                Helpfunctions.Instance.createNewLoggfile();
            }
            PenalityManager.Instance.setCombos();
            Mulligan m = Mulligan.Instance; // read the mulligan list
        }

        public void setnewLoggFile()
        {
            if (!singleLog)
            {
                sttngs.setLoggFile("UILogg" + DateTime.Now.ToString("_yyyy-MM-dd_HH-mm-ss") + ".txt");
                Helpfunctions.Instance.createNewLoggfile();
                Helpfunctions.Instance.ErrorLog("#######################################################");
                Helpfunctions.Instance.ErrorLog("fight is logged in: " + sttngs.logpath + sttngs.logfile);
                Helpfunctions.Instance.ErrorLog("#######################################################");
            }
            else
            {
                sttngs.setLoggFile("UILogg.txt");
            }
        }

        public bool updateEverything(Behavior botbase, bool runExtern = false, bool passiveWait = false)
        {
            this.updateBehaveString(botbase);


            ownPlayerController = TritonHs.OurHero.ControllerId;


            // create hero + minion data
            getHerostuff();

            //small fix for not knowing when to mulligan:
            if (ownMaxMana == 1 && currentMana == 1 && numMinionsPlayedThisTurn == 0 && cardsPlayedThisTurn == 0)
            {
                setnewLoggFile();
                getHerostuff();
            }

            getMinions();
            getHandcards();
            getDecks();

            // send ai the data:
            Hrtprozis.Instance.clearAll();
            Handmanager.Instance.clearAll();

            Hrtprozis.Instance.setOwnPlayer(ownPlayerController);
            Handmanager.Instance.setOwnPlayer(ownPlayerController);

            this.numOptionPlayedThisTurn = 0;
            this.numOptionPlayedThisTurn += this.cardsPlayedThisTurn + this.ownHero.numAttacksThisTurn;
            foreach (Minion m in this.ownMinions)
            {
                if (m.Hp >= 1) this.numOptionPlayedThisTurn += m.numAttacksThisTurn;
            }

            Hrtprozis.Instance.updatePlayer(this.ownMaxMana, this.currentMana, this.cardsPlayedThisTurn,
                this.numMinionsPlayedThisTurn, this.numOptionPlayedThisTurn, this.ueberladung, TritonHs.OurHero.EntityId,
                TritonHs.EnemyHero.EntityId);
            Hrtprozis.Instance.updateSecretStuff(this.ownSecretList, this.enemySecretCount);

            Hrtprozis.Instance.updateOwnHero(this.ownHeroWeapon, this.heroWeaponAttack, this.heroWeaponDurability,
                this.heroname, this.heroAbility, this.ownAbilityisReady, this.ownHero);
            Hrtprozis.Instance.updateEnemyHero(this.enemyHeroWeapon, this.enemyWeaponAttack, this.enemyWeaponDurability,
                this.enemyHeroname, this.enemyMaxMana, this.enemyAbility, this.enemyHero);

            Hrtprozis.Instance.updateMinions(this.ownMinions, this.enemyMinions);
            Handmanager.Instance.setHandcards(this.handCards, this.anzcards, this.enemyAnzCards);

            Hrtprozis.Instance.updateFatigueStats(this.ownDecksize, this.ownHeroFatigue, this.enemyDecksize,
                this.enemyHeroFatigue);

            Probabilitymaker.Instance.getEnemySecretGuesses(this.enemySecretList,
                Hrtprozis.Instance.enemyHeroStartClass);
            //learnmode :D

            Playfield p = new Playfield();

            if (lastpf != null)
            {
                if (lastpf.isEqualf(p))
                {
                    return false;
                }

                //board changed we update secrets!
                //if(Ai.Instance.nextMoveGuess!=null) Probabilitymaker.Instance.updateSecretList(Ai.Instance.nextMoveGuess.enemySecretList);
                Probabilitymaker.Instance.updateSecretList(p, lastpf);
                lastpf = p;
            }
            else
            {
                lastpf = p;
            }

            p = new Playfield(); //secrets have updated :D
            // calculate stuff
            Helpfunctions.Instance.ErrorLog("calculating stuff... " + DateTime.Now.ToString("HH:mm:ss.ffff"));
            if (runExtern)
            {
                Helpfunctions.Instance.logg("recalc-check###########");
                if (p.isEqual(Ai.Instance.nextMoveGuess, true))
                {
                    printstuff(false);
                    Ai.Instance.doNextCalcedMove();
                }
                else
                {
                    printstuff(true);
                    readActionFile(passiveWait);
                }
            }
            else
            {
                // Drew: This prevents the freeze during AI updates, but no API functions may be called
                // during this time!
                using (TritonHs.Memory.ReleaseFrame(true))
                {
                    printstuff(false);
                    Ai.Instance.dosomethingclever(botbase);    
                }
            }

            Helpfunctions.Instance.ErrorLog("calculating ended! " + DateTime.Now.ToString("HH:mm:ss.ffff"));
            return true;
        }

        private void getHerostuff()
        {
            List<HSCard> allcards = TritonHs.GetAllCards();

            HSCard ownHeroCard = TritonHs.OurHero;
            HSCard enemHeroCard = TritonHs.EnemyHero;
            int ownheroentity = TritonHs.OurHero.EntityId;
            int enemyheroentity = TritonHs.EnemyHero.EntityId;
            this.ownHero = new Minion();
            this.enemyHero = new Minion();

            foreach (HSCard ent in allcards)
            {
                if (ent.IsHero == true)
                {
                    if (ent.ControllerId == 1 && this.ownHero.cardClass == TAG_CLASS.INVALID)
                    {
                        this.ownHero.cardClass = (TAG_CLASS)ent.Class;
                    }
                    if (ent.ControllerId == 2 && this.enemyHero.cardClass == TAG_CLASS.INVALID)
                    {
                        this.enemyHero.cardClass = (TAG_CLASS)ent.Class;
                    }
                    if (ent.EntityId == enemyheroentity) enemHeroCard = ent;
                    if (ent.EntityId == ownheroentity) ownHeroCard = ent;
                }
            }

            //player stuff#########################
            //this.currentMana =ownPlayer.GetTag(HRGameTag.RESOURCES) - ownPlayer.GetTag(HRGameTag.RESOURCES_USED) + ownPlayer.GetTag(HRGameTag.TEMP_RESOURCES);
            this.currentMana = TritonHs.CurrentMana;
            this.ownMaxMana = TritonHs.Resources;
            this.enemyMaxMana = ownMaxMana;

            //count own secrets
            ownSecretList = new List<string>(); // the CARDIDS of the secrets
            enemySecretCount = 0;
            //count enemy secrets:
            enemySecretList.Clear();
            foreach (HSCard ent in allcards)
            {
                if (ent.IsSecret && ent.ControllerId != ownPlayerController && ent.GetTag(GAME_TAG.ZONE) == 7)
                {
                    enemySecretCount++;
                    enemySecretList.Add(ent.GetTag(GAME_TAG.ENTITY_ID));

                }
                if (ent.IsSecret && ent.ControllerId == ownPlayerController && ent.GetTag(GAME_TAG.ZONE) == 7)
                {
                    ownSecretList.Add(ent.Id);

                }
            }


            int ourSecretsCount = ownSecretList.Count;

            numMinionsPlayedThisTurn = TritonHs.NumMinionsPlayedThisTurn;
            cardsPlayedThisTurn = TritonHs.NumCardsPlayedThisTurn;
            ueberladung = TritonHs.RecallOwed;

            //get weapon stuff
            this.ownHeroWeapon = "";
            this.heroWeaponAttack = 0;
            this.heroWeaponDurability = 0;

            this.ownHeroFatigue = ownHeroCard.GetTag(GAME_TAG.FATIGUE);
            this.enemyHeroFatigue = enemHeroCard.GetTag(GAME_TAG.FATIGUE);

            this.ownDecksize = 0;
            this.enemyDecksize = 0;
            //count decksize
            foreach (HSCard ent in allcards)
            {
                if (ent.ControllerId == ownPlayerController && ent.GetTag(GAME_TAG.ZONE) == 2) ownDecksize++;
                if (ent.ControllerId != ownPlayerController && ent.GetTag(GAME_TAG.ZONE) == 2) enemyDecksize++;
            }

            //own hero stuff###########################
            int heroAtk = ownHeroCard.GetTag(GAME_TAG.ATK);
            int heroHp = ownHeroCard.GetTag(GAME_TAG.HEALTH) - ownHeroCard.GetTag(GAME_TAG.DAMAGE);
            int heroDefence = ownHeroCard.GetTag(GAME_TAG.ARMOR);
            this.heroname = Hrtprozis.Instance.heroIDtoName(TritonHs.OurHero.Id);

            bool heroImmuneToDamageWhileAttacking = false;
            bool herofrozen = (ownHeroCard.GetTag(GAME_TAG.FROZEN) == 0) ? false : true;
            int heroNumAttacksThisTurn = ownHeroCard.GetTag(GAME_TAG.NUM_ATTACKS_THIS_TURN);
            bool heroHasWindfury = (ownHeroCard.GetTag(GAME_TAG.WINDFURY) == 0) ? false : true;
            bool heroImmune = (ownHeroCard.GetTag(GAME_TAG.CANT_BE_DAMAGED) == 0) ? false : true;

            //Helpfunctions.Instance.ErrorLog(ownhero.GetName() + " ready params ex: " + exausted + " " + heroAtk + " " + numberofattacks + " " + herofrozen);


            if (TritonHs.DoWeHaveWeapon)
            {
                HSCard weapon = TritonHs.OurWeaponCard;
                ownHeroWeapon =
                    CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(weapon.Id)).name.ToString();
                heroWeaponAttack = weapon.GetTag(GAME_TAG.ATK);
                heroWeaponDurability = weapon.GetTag(GAME_TAG.DURABILITY) - weapon.GetTag(GAME_TAG.DAMAGE);
                    //weapon.GetDurability();
                if (ownHeroWeapon == "gladiatorslongbow")
                {
                    heroImmuneToDamageWhileAttacking = true;
                }
                if (this.ownHeroWeapon == "doomhammer")
                {
                    heroHasWindfury = true;
                }

                //Helpfunctions.Instance.ErrorLog("weapon: " + ownHeroWeapon + " " + heroWeaponAttack + " " + heroWeaponDurability);
            }



            //enemy hero stuff###############################################################
            this.enemyHeroname = Hrtprozis.Instance.heroIDtoName(TritonHs.EnemyHero.Id);

            int enemyAtk = enemHeroCard.GetTag(GAME_TAG.ATK); //lol should be zero :D
            int enemyHp = enemHeroCard.GetTag(GAME_TAG.HEALTH) - enemHeroCard.GetTag(GAME_TAG.DAMAGE);
            int enemyDefence = enemHeroCard.GetTag(GAME_TAG.ARMOR);
            bool enemyfrozen = (enemHeroCard.GetTag(GAME_TAG.FROZEN) == 0) ? false : true;
            bool enemyHeroImmune = (enemHeroCard.GetTag(GAME_TAG.CANT_BE_DAMAGED) == 0) ? false : true;

            this.enemyHeroWeapon = "";
            this.enemyWeaponAttack = 0;
            this.enemyWeaponDurability = 0;
            if (TritonHs.DoesEnemyHasWeapon)
            {
                HSCard weapon = TritonHs.EnemyWeaponCard;
                enemyHeroWeapon =
                    CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(weapon.Id)).name.ToString();
                enemyWeaponAttack = weapon.GetTag(GAME_TAG.ATK);
                enemyWeaponDurability = weapon.GetTag(GAME_TAG.DURABILITY) - weapon.GetTag(GAME_TAG.DAMAGE);
            }


            //own hero ablity stuff###########################################################

            this.heroAbility =
                CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(TritonHs.OurHeroPowerCard.Id));
            this.ownAbilityisReady = (TritonHs.OurHeroPowerCard.GetTag(GAME_TAG.EXHAUSTED) == 0) ? true : false;
            this.enemyAbility =
                CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(TritonHs.OurHeroPowerCard.Id));
            int ownHeroAbilityEntity = TritonHs.OurHeroPowerCard.EntityId;
            foreach (HSCard ent in allcards)
            {
                if (ent.EntityId != ownHeroAbilityEntity && ent.GetTag(GAME_TAG.CARDTYPE) == 10)
                {
                    enemyAbility = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(ent.Id));
                    break;
                }
            }

            //generate Heros
            this.ownHero.isHero = true;
            this.enemyHero.isHero = true;
            this.ownHero.own = true;
            this.enemyHero.own = false;
            this.ownHero.maxHp = ownHeroCard.GetTag(GAME_TAG.HEALTH);
            this.enemyHero.maxHp = enemHeroCard.GetTag(GAME_TAG.HEALTH);
            this.ownHero.entitiyID = ownHeroCard.EntityId;
            this.enemyHero.entitiyID = enemHeroCard.EntityId;

            this.ownHero.Angr = heroAtk;
            this.ownHero.Hp = heroHp;
            this.ownHero.armor = heroDefence;
            this.ownHero.frozen = herofrozen;
            this.ownHero.immuneWhileAttacking = heroImmuneToDamageWhileAttacking;
            this.ownHero.immune = heroImmune;
            this.ownHero.numAttacksThisTurn = heroNumAttacksThisTurn;
            this.ownHero.windfury = heroHasWindfury;

            this.enemyHero.Angr = enemyAtk;
            this.enemyHero.Hp = enemyHp;
            this.enemyHero.frozen = enemyfrozen;
            this.enemyHero.armor = enemyDefence;
            this.enemyHero.immune = enemyHeroImmune;
            this.enemyHero.Ready = false;

            this.ownHero.updateReadyness();


            //load enchantments of the heros
            List<miniEnch> miniEnchlist = new List<miniEnch>();
            foreach (HSCard ent in allcards)
            {
                if (ent.GetTag(GAME_TAG.ATTACHED) == this.ownHero.entitiyID && ent.GetTag(GAME_TAG.ZONE) == 1) //1==play
                {
                    CardDB.cardIDEnum id = CardDB.Instance.cardIdstringToEnum(ent.Id);
                    int controler = ent.GetTag(GAME_TAG.CONTROLLER);
                    int creator = ent.GetTag(GAME_TAG.CREATOR);
                    miniEnchlist.Add(new miniEnch(id, creator, controler));
                }

            }

            this.ownHero.loadEnchantments(miniEnchlist, ownHeroCard.GetTag(GAME_TAG.CONTROLLER));

            miniEnchlist.Clear();

            foreach (HSCard ent in allcards)
            {
                if (ent.GetTag(GAME_TAG.ATTACHED) == this.enemyHero.entitiyID && ent.GetTag(GAME_TAG.ZONE) == 1)
                    //1==play
                {
                    CardDB.cardIDEnum id = CardDB.Instance.cardIdstringToEnum(ent.Id);
                    int controler = ent.GetTag(GAME_TAG.CONTROLLER);
                    int creator = ent.GetTag(GAME_TAG.CREATOR);
                    miniEnchlist.Add(new miniEnch(id, creator, controler));
                }

            }

            this.enemyHero.loadEnchantments(miniEnchlist, enemHeroCard.GetTag(GAME_TAG.CONTROLLER));
            //fastmode weapon correction:
            if (this.ownHero.Angr < this.heroWeaponAttack) this.ownHero.Angr = this.heroWeaponAttack;
            if (this.enemyHero.Angr < this.enemyWeaponAttack) this.enemyHero.Angr = this.enemyWeaponAttack;
        }

        private void getMinions()
        {
            // ALL minions on Playfield:
            List<HSCard> list = TritonHs.GetCards(CardZone.Battlefield, true);
            list.AddRange(TritonHs.GetCards(CardZone.Battlefield, false));

            var enchantments = new List<HSCard>();
            ownMinions.Clear();
            enemyMinions.Clear();
            List<HSCard> allcards = TritonHs.GetAllCards();

            foreach (HSCard entiti in list)
            {
                int zp = entiti.GetTag(GAME_TAG.ZONE_POSITION);

                if (entiti.IsMinion && zp >= 1)
                {

                    HSCard entitiy = entiti;

                    foreach (HSCard ent in allcards)
                    {
                        if (ent.EntityId == entiti.EntityId)
                        {
                            entitiy = ent;
                            break;
                        }
                    }

                    //Helpfunctions.Instance.ErrorLog("zonepos " + zp);
                    CardDB.Card c = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(entitiy.Id));
                    Minion m = new Minion();
                    m.name = c.name;
                    m.handcard.card = c;

                    m.Angr = entitiy.GetTag(GAME_TAG.ATK);
                    m.maxHp = entitiy.GetTag(GAME_TAG.HEALTH);
                    m.Hp = entitiy.GetTag(GAME_TAG.HEALTH) - entitiy.GetTag(GAME_TAG.DAMAGE);
                    if (m.Hp <= 0) continue;
                    m.wounded = false;
                    if (m.maxHp > m.Hp) m.wounded = true;


                    m.exhausted = (entitiy.GetTag(GAME_TAG.EXHAUSTED) == 0) ? false : true;

                    m.taunt = (entitiy.GetTag(GAME_TAG.TAUNT) == 0) ? false : true;

                    m.numAttacksThisTurn = entitiy.GetTag(GAME_TAG.NUM_ATTACKS_THIS_TURN);

                    int temp = entitiy.GetTag(GAME_TAG.NUM_TURNS_IN_PLAY);
                    m.playedThisTurn = (temp == 0) ? true : false;

                    m.windfury = (entitiy.GetTag(GAME_TAG.WINDFURY) == 0) ? false : true;

                    m.frozen = (entitiy.GetTag(GAME_TAG.FROZEN) == 0) ? false : true;

                    m.divineshild = (entitiy.GetTag(GAME_TAG.DIVINE_SHIELD) == 0) ? false : true;

                    m.stealth = (entitiy.GetTag(GAME_TAG.STEALTH) == 0) ? false : true;

                    m.poisonous = (entitiy.GetTag(GAME_TAG.POISONOUS) == 0) ? false : true;

                    m.immune = (entitiy.GetTag(GAME_TAG.IMMUNE_WHILE_ATTACKING) == 0) ? false : true;

                    m.silenced = (entitiy.GetTag(GAME_TAG.SILENCED) == 0) ? false : true;

                    // Drew: fixed | is the tag removed when silenced, via Mass Dispel?
                    m.cantBeTargetedBySpellsOrHeroPowers = (entitiy.GetTag(GAME_TAG.CANT_BE_TARGETED_BY_HERO_POWERS) == 0) ? false : true;
                    
                    m.charge = 0;

                    if (!m.silenced && m.name == CardDB.cardName.southseadeckhand &&
                        entitiy.GetTag(GAME_TAG.CHARGE) == 1) m.charge = 1;
                    if (!m.silenced && m.handcard.card.Charge) m.charge++;

                    m.zonepos = zp;

                    m.entitiyID = entitiy.EntityId;


                    //Helpfunctions.Instance.ErrorLog(  m.name + " ready params ex: " + m.exhausted + " charge: " +m.charge + " attcksthisturn: " + m.numAttacksThisTurn + " playedthisturn " + m.playedThisTurn );


                    List<miniEnch> enchs = new List<miniEnch>();
                    foreach (HSCard ent in allcards)
                    {
                        if (ent.GetTag(GAME_TAG.ATTACHED) == m.entitiyID && ent.GetTag(GAME_TAG.ZONE) == 1) //1==play
                        {
                            CardDB.cardIDEnum id = CardDB.Instance.cardIdstringToEnum(ent.Id);
                            int controler = ent.GetTag(GAME_TAG.CONTROLLER);
                            int creator = ent.GetTag(GAME_TAG.CREATOR);
                            enchs.Add(new miniEnch(id, creator, controler));
                        }

                    }

                    m.loadEnchantments(enchs, entitiy.GetTag(GAME_TAG.CONTROLLER));

                    m.Ready = false; // if exhausted, he is NOT ready

                    m.updateReadyness();


                    if (entitiy.GetTag(GAME_TAG.CONTROLLER) == this.ownPlayerController) // OWN minion
                    {
                        m.own = true;
                        this.ownMinions.Add(m);
                    }
                    else
                    {
                        m.own = false;
                        this.enemyMinions.Add(m);
                    }

                }



            }


        }

        private void getHandcards()
        {
            handCards.Clear();
            anzcards = 0;
            enemyAnzCards = 0;
            List<HSCard> list = TritonHs.GetCards(CardZone.Hand);

            //List<HSCard> list2 = TritonHs.GetCards(CardZone.Graveyard);
            //List<HRCard> list = HRCard.GetCards(HRPlayer.GetLocalPlayer(), HRCardZone.HAND);

            foreach (HSCard entitiy in list)
            {
                if (entitiy.ZonePosition >= 1) // own handcard 
                {
                    CardDB.Card c = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(entitiy.Id));
                    //c.cost = entitiy.GetCost();
                    //c.entityID = entitiy.GetEntityId();

                    var hc = new Handmanager.Handcard();
                    hc.card = c;
                    hc.position = entitiy.ZonePosition;
                    hc.entity = entitiy.EntityId;
                    hc.manacost = entitiy.Cost;
                    hc.addattack = 0;
                    if (c.name == CardDB.cardName.bolvarfordragon)
                    {
                        hc.addattack = entitiy.GetTag(GAME_TAG.ATK) - 1;
                            // -1 because it starts with 1, we count only the additional attackvalue
                    }
                    handCards.Add(hc);
                    anzcards++;
                }
            }

            List<HSCard> allcards = TritonHs.GetAllCards();
            enemyAnzCards = 0;
            foreach (HSCard hs in allcards)
            {
                if (hs.GetTag(GAME_TAG.ZONE) == 3 && hs.ControllerId != ownPlayerController &&
                    hs.GetTag(GAME_TAG.ZONE_POSITION) >= 1) enemyAnzCards++;
            }
            // dont know if you can count the enemys-handcars in this way :D
        }

        private void getDecks()
        {
            List<HSCard> allEntitys = TritonHs.GetAllCards();
            int owncontroler = TritonHs.OurHero.GetTag(GAME_TAG.CONTROLLER);
            int enemycontroler = TritonHs.EnemyHero.GetTag(GAME_TAG.CONTROLLER);

                /*var wer = TritonHs.OurHero.ControllerId;
                using (TritonHs.AcquireFrame())
                {
                    foreach (var kvp in CollectionManager.Get().GetDecks())
                    {
                        var eee = kvp.Key;
                        var eed = kvp.Value.m_name;
                        var deck = CollectionManager.Get().GetDeck(kvp.Key);
                        if (deck.m_netContentsLoaded == false)
                        {
                            var ffs = 1;
                        }
                        foreach (var slot in deck.GetSlots())
                        {
                            var ddsee = slot.m_cardId;
                        }
                    }
                }*/

            List<CardDB.cardIDEnum> ownCards = new List<CardDB.cardIDEnum>();
            List<CardDB.cardIDEnum> enemyCards = new List<CardDB.cardIDEnum>();
            List<GraveYardItem> graveYard = new List<GraveYardItem>();

            foreach (HSCard ent in allEntitys)
            {
                if (ent.GetTag(GAME_TAG.ZONE) == 7 && ent.GetTag(GAME_TAG.CONTROLLER) == enemycontroler)
                    continue; // cant know enemy secrets :D
                if (ent.GetTag(GAME_TAG.ZONE) == 2) continue;
                if (ent.GetTag(GAME_TAG.CARDTYPE) == 4 || ent.GetTag(GAME_TAG.CARDTYPE) == 5 ||
                    ent.GetTag(GAME_TAG.CARDTYPE) == 7) //is minion, weapon or spell
                {
                    CardDB.cardIDEnum cardid = CardDB.Instance.cardIdstringToEnum(ent.Id);
                    if (ent.GetTag(GAME_TAG.ZONE) == 4) // 4 == graveyard
                    {
                        GraveYardItem gyi = new GraveYardItem(cardid, ent.EntityId,
                            ent.GetTag(GAME_TAG.CONTROLLER) == owncontroler);
                        graveYard.Add(gyi);

                        if (ent.GetTag(GAME_TAG.CONTROLLER) == owncontroler) ownCards.Add(cardid);
                        else if (ent.GetTag(GAME_TAG.CONTROLLER) == enemycontroler) enemyCards.Add(cardid);
                    }

                    //int creator = ent.GetTag(GAME_TAG.CREATOR);
                    //if (creator != 0 && creator != owncontroler && creator != enemycontroler) continue; //if creator is someone else, it was not played

                }
            }

            Probabilitymaker.Instance.setOwnCards(ownCards);
            Probabilitymaker.Instance.setEnemyCards(enemyCards);
            bool isTurnStart = false;
            if (Ai.Instance.nextMoveGuess.mana == -100)
            {
                isTurnStart = true;
                Ai.Instance.updateTwoTurnSim();
            }
            Probabilitymaker.Instance.setGraveYard(graveYard, isTurnStart);

        }

        private void updateBehaveString(Behavior botbase)
        {
            this.botbehave = "rush";
            if (botbase is BehaviorControl) this.botbehave = "control";
            if (botbase is BehaviorMana) this.botbehave = "mana";
            this.botbehave += " " + Ai.Instance.maxwide;
            this.botbehave += " face " + ComboBreaker.Instance.attackFaceHP;
            if (Settings.Instance.secondTurnAmount > 0)
            {
                if (Ai.Instance.nextMoveGuess.mana == -100)
                {
                    Ai.Instance.updateTwoTurnSim();
                }
                this.botbehave += " twoturnsim " + Settings.Instance.secondTurnAmount + " ntss " +
                                  Settings.Instance.nextTurnDeep + " " + Settings.Instance.nextTurnMaxWide + " " +
                                  Settings.Instance.nextTurnTotalBoards;
            }

            if (Settings.Instance.playarround)
            {
                this.botbehave += " playaround";
                this.botbehave += " " + Settings.Instance.playaroundprob + " " + Settings.Instance.playaroundprob2;
            }

            this.botbehave += " ets " + Settings.Instance.enemyTurnMaxWide;

            if (Settings.Instance.simEnemySecondTurn)
            {
                this.botbehave += " ets2 " + Settings.Instance.enemyTurnMaxWideSecondTime;
                this.botbehave += " ents " + Settings.Instance.enemySecondTurnMaxWide;
            }

            if (Settings.Instance.useSecretsPlayArround)
            {
                this.botbehave += " secret";
            }

            if (Settings.Instance.secondweight != 0.5f)
            {
                this.botbehave += " weight " + (int) (Settings.Instance.secondweight*100f);
            }

            if (Settings.Instance.simulatePlacement)
            {
                this.botbehave += " plcmnt";
            }

            this.botbehave += " iC " + Settings.Instance.ImprovedCalculations;
            
            


        }

        public static int getLastAffected(int entityid)
        {

            List<HSCard> allEntitys = TritonHs.GetAllCards();

            foreach (HSCard ent in allEntitys)
            {
                if (ent.GetTag(GAME_TAG.LAST_AFFECTED_BY) == entityid) return ent.GetTag(GAME_TAG.ENTITY_ID);
            }

            return 0;
        }

        public static int getCardTarget(int entityid)
        {

            List<HSCard> allEntitys = TritonHs.GetAllCards();

            foreach (HSCard ent in allEntitys)
            {
                if (ent.GetTag(GAME_TAG.ENTITY_ID) == entityid) return ent.GetTag(GAME_TAG.CARD_TARGET);
            }

            return 0;

        }

        private void printstuff(bool runEx)
        {
            int ownsecretcount = ownSecretList.Count;
            string dtimes = DateTime.Now.ToString("HH:mm:ss:ffff");
            string enemysecretIds = "";
            enemysecretIds = Probabilitymaker.Instance.getEnemySecretData();
            Helpfunctions.Instance.logg("#######################################################################");
            Helpfunctions.Instance.logg("#######################################################################");
            Helpfunctions.Instance.logg("start calculations, current time: " + DateTime.Now.ToString("HH:mm:ss") + " V" +
                                        this.versionnumber + " " + this.botbehave);
            Helpfunctions.Instance.logg("#######################################################################");
            Helpfunctions.Instance.logg("mana " + currentMana + "/" + ownMaxMana);
            Helpfunctions.Instance.logg("emana " + enemyMaxMana);
            Helpfunctions.Instance.logg("own secretsCount: " + ownsecretcount);
            Helpfunctions.Instance.logg("enemy secretsCount: " + enemySecretCount + " ;" + enemysecretIds);

            Ai.Instance.currentCalculatedBoard = dtimes;

            if (runEx)
            {
                Helpfunctions.Instance.resetBuffer();
                Helpfunctions.Instance.writeBufferToActionFile();
                Helpfunctions.Instance.resetBuffer();

                Helpfunctions.Instance.writeToBuffer(
                    "#######################################################################");
                Helpfunctions.Instance.writeToBuffer(
                    "#######################################################################");
                Helpfunctions.Instance.writeToBuffer("start calculations, current time: " + dtimes + " V" +
                                                     this.versionnumber + " " + this.botbehave);
                Helpfunctions.Instance.writeToBuffer(
                    "#######################################################################");
                Helpfunctions.Instance.writeToBuffer("mana " + currentMana + "/" + ownMaxMana);
                Helpfunctions.Instance.writeToBuffer("emana " + enemyMaxMana);
                Helpfunctions.Instance.writeToBuffer("own secretsCount: " + ownsecretcount);
                Helpfunctions.Instance.writeToBuffer("enemy secretsCount: " + enemySecretCount + " ;" + enemysecretIds);
            }
            Hrtprozis.Instance.printHero(runEx);
            Hrtprozis.Instance.printOwnMinions(runEx);
            Hrtprozis.Instance.printEnemyMinions(runEx);
            Handmanager.Instance.printcards(runEx);
            Probabilitymaker.Instance.printTurnGraveYard(runEx);
            Probabilitymaker.Instance.printGraveyards(runEx);

            if (runEx) Helpfunctions.Instance.writeBufferToFile();

        }

        public bool readActionFile(bool passiveWaiting = false)
        {
            bool readed = true;
            List<string> alist = new List<string>();
            float value = 0f;
            string boardnumm = "-1";
            this.waitingForSilver = true;
            while (readed)
            {
                try
                {
                    string data = System.IO.File.ReadAllText(Settings.Instance.path + "actionstodo.txt");
                    if (data != "" && data != "<EoF>" && data.EndsWith("<EoF>"))
                    {
                        data = data.Replace("<EoF>", "");
                        //Helpfunctions.Instance.ErrorLog(data);
                        Helpfunctions.Instance.resetBuffer();
                        Helpfunctions.Instance.writeBufferToActionFile();
                        alist.AddRange(data.Split(new string[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries));
                        string board = alist[0];
                        if (board.StartsWith("board "))
                        {
                            boardnumm = (board.Split(' ')[1].Split(' ')[0]);
                            alist.RemoveAt(0);
                            if (boardnumm != Ai.Instance.currentCalculatedBoard)
                            {
                                if (passiveWaiting)
                                {
                                    System.Threading.Thread.Sleep(10);
                                    return false;
                                }
                                continue;
                            }
                        }
                        string first = alist[0];
                        if (first.StartsWith("value "))
                        {
                            value = float.Parse((first.Split(' ')[1].Split(' ')[0]));
                            alist.RemoveAt(0);
                        }
                        readed = false;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(10);
                        if (passiveWaiting)
                        {
                            return false;
                        }
                    }

                }
                catch
                {
                    System.Threading.Thread.Sleep(10);
                }

            }
            this.waitingForSilver = false;
            Helpfunctions.Instance.logg("received " + boardnumm + " actions to do:");
            Ai.Instance.currentCalculatedBoard = "0";
            Playfield p = new Playfield();
            List<Action> aclist = new List<Action>();

            foreach (string a in alist)
            {
                aclist.Add(new Action(a, p));
                Helpfunctions.Instance.logg(a);
            }

            Ai.Instance.setBestMoves(aclist, value);

            return true;
        }

    }
}

// ReSharper restore InconsistentNaming
