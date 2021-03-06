﻿using DrMangle.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrMangle
{
    public class GameController
    {
        public GameData Data { get; set; }
        public GameRepo Repo { get; set; }
        public ArenaBattleCalculator Arena { get; set; }
        public PlayerManager PlayerManager { get; set; }
        public PlayerData[] AllPlayers { get; set; }
        private Random RNG = new Random();
        private string currentFile = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();

        public GameController()
        {
            string textInput = "default";
            int intInput;

            Repo = new GameRepo();
            Arena = new ArenaBattleCalculator();
            PlayerManager = new PlayerManager();

            Repo.FileSetup();
            StaticUtility.TalkPause("Welcome to the Isle of Dr. Mangle.");
            if (Repo.gameIndex.Count > 1)
            {
                Data = Repo.LoadGame();
            }
            if (Data == null)
            {
                bool halt = true;
                while (halt)
                {
                    Console.WriteLine("Please enter a name for your game data:");
                    textInput = Console.ReadLine();
                    if(Repo.gameIndex.ContainsKey(textInput))
                    {
                        Console.WriteLine("A game by that name already exists.");
                    }
                    else
                    {
                        halt = false;
                    }
                }
                Console.WriteLine("And how many contestants will you be competing against?");
                intInput = StaticUtility.CheckInput(1, 7);
                Data = new GameData(textInput, intInput, Repo.GetNextGameID(), RNG);
                Repo.SaveGame(Data);
            }
            AllPlayers = new PlayerData[Data.AiPlayers.Length + 1];
            AllPlayers[0] = Data.CurrentPlayer;
            for (int i = 0; i < Data.AiPlayers.Length; i++)
            {
                AllPlayers[i + 1] = Data.AiPlayers[i];
            }
        }

        public GameController(bool forTest) {
            Arena = new ArenaBattleCalculator();
            PlayerManager = new PlayerManager();
        }

        public bool RunGame()
        {
            bool gameStatus = true;
            int intInput;

            #region search
            StaticUtility.TalkPause("A new day has dawned!");
            StaticUtility.TalkPause("The parks will be open for 5 hours...");
            StaticUtility.TalkPause("You will then have one more hour in your labs before the evening's entertainment.");

            for (int i = 1; i < 6; i++)
            {
                try
                {
                    StaticUtility.TalkPause("It is currently " + i + " o'clock. The parks close at 6.");
                    Data.MoveRegions();
                    gameStatus = ShowSearchOptions(i - 1);
                    AISearchTurn(Data, i);
                    if (!gameStatus)
                    {
                        return gameStatus;
                    }
                }
                catch (System.Exception ex)
                {
                    int currentLine = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber();
                    Repo.LogException(Data, $"Search Phase exception {currentFile} line {currentLine}", ex, false);
                }
            }
            #endregion

            #region build
            try
            {
                StaticUtility.TalkPause("It is now 6 o'clock. Return to your lab and prepare for the floorshow at 7.");
                Data.CurrentRegion = 0;
                foreach (var player in AllPlayers)
                {
                    PlayerManager.DumpBag(player);
                }
                Console.WriteLine("Bag contents added to workshop inventory.");
                gameStatus = ShowLabOptions();
                if (!gameStatus)
                {
                    return gameStatus;
                }
            }
            catch (System.Exception ex)
            {
                int currentLine = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber();
                Repo.LogException(Data, $"Player Build Phase exception {currentFile} line {currentLine}\n", ex, false);
            }

            try
            {
                AIBuildTurn(Data);
            }
            catch (Exception ex)
            {
                int currentLine = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber();
                Repo.LogException(Data, $"AI Build Phase exception {currentFile} line {currentLine}\n", ex, false);
            }


            #endregion

            #region fight
            try
            { 
            StaticUtility.TalkPause("Welcome to the evening's entertainment!");
            if (Data.CurrentPlayer.Monster != null && Data.CurrentPlayer.Monster.CanFight)
            {
                Console.WriteLine("Would you like to particpate tonight?");
                StaticUtility.TalkPause("1 - Yes, 2 - No");
                intInput = StaticUtility.CheckInput(1, 2);
                if (intInput != 1)
                {
                    StaticUtility.TalkPause("Well, maybe tomorrow then...");
                    Console.WriteLine("Let's find you a comfortable seat.");

                }
                else
                {
                    StaticUtility.TalkPause("Let the games begin!");
                }
            }
            else
            {
                StaticUtility.TalkPause("Seeing as you do not have a living, able bodied contestant...");
                Console.WriteLine("Let's find you a comfortable seat.");
            }
            CalculateFights();
            }
            catch (Exception ex)
            {
                int currentLine = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber();
                Repo.LogException(Data, $"Fighting Phase exception {currentFile} line {currentLine}\n", ex, false);
            }
            #endregion

            #region dayEnd
            try
            {
                SortPlayersByWins();
                Data.CurrentLevel.AddParts(RNG, AllPlayers.Length);
                Data.CurrentLevel.HalveParts();
                Data.GameDayNumber++;
                Repo.SaveGame(Data);
            }
            catch (Exception ex)
            { 
                int currentLine = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileLineNumber();
                Repo.LogException(Data, $"End of Day Phaseexception {currentFile} line {currentLine}\n", ex, false);
            }          
            return gameStatus;
            #endregion
        }

        public void AIBuildTurn(GameData data)
        {
            foreach (var ai in data.AiPlayers)
            {
                int start = 0;
                var monst = new PartData[6];
                if (ai.Monster != null)               
                {
                    bool betterBody = false;
                    List<PartData> heads = ai.Workshop.Where(x => x.PartType == 0 && x.PartRarity < ai.Monster.Parts[0].PartRarity).ToList();
                    List<PartData> torsos = ai.Workshop.Where(x => x.PartType == 1 && x.PartRarity < ai.Monster.Parts[1].PartRarity).ToList();
                    if (heads.Count > 0 || torsos.Count > 0) betterBody = true;

                    if (betterBody)
                    {
                        data.Graveyard.Add(new MonsterGhost(ai.Monster, data.GameDayNumber));
                        for (int i = 2; i < ai.Monster.Parts.Length; i++)
                        {
                            if(ai.Monster.Parts[i] != null) ai.Workshop.Add(ai.Monster.Parts[i]);
                        }
                        ai.Monster = null;
                        ai.Workshop.Sort(ai.Comparer);
                    }
                    else
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            monst[i] = ai.Monster.Parts[i];
                        }
                        start = 2;
                    }
                }
                for (int i = start; i < 6; i++)
                {
                    for (int j = ai.Workshop.Count - 1; j >= 0; j--)
                    {
                        PartData oldP = monst[i];
                        PartData newP = ai.Workshop[j];
                        float score = 0;

                        if (newP != null)
                        {
                            if (oldP != null && newP.PartType == i)
                            {
                                score += newP.Stats[0] - monst[i].Stats[0];
                                score += newP.Stats[1] - monst[i].Stats[1];
                                score += newP.Stats[2] - monst[i].Stats[2];
                                score += newP.Stats[3] - monst[i].Stats[3];
                            }
                            if ((oldP == null || score > 0f) && newP.PartType == i)
                            {
                                monst[i] = newP;
                            }
                        }
                    }
                }
                if (monst[0] != null && monst[1] != null)
                {
                    if (monst[2] != null || monst[3] != null || monst[4] != null || monst[5] != null)
                    {
                        ai.Monster = new MonsterData(ai.Name + "'s Monster", monst);
                        for (int i = ai.Workshop.Count - 1; i >= 0; i--)
                        {
                            if (ai.Workshop[i] != null)
                            {
                                PlayerManager.ScrapItem(ai, ai.Workshop, i);
                            }
                        }
                    }
                }
            }
        }

        public void AISearchTurn(GameData gd, int round)
        {
            foreach (var ai in gd.AiPlayers)
            {
                int region = RNG.Next(1, 4);
                if (gd.CurrentLevel.Locations[region].PartsList.Count != 0)
                {
                    ai.Bag[round - 1] = gd.CurrentLevel.Locations[region].PartsList.Last.Value;
                    gd.CurrentLevel.Locations[region].PartsList.RemoveLast();
                }
            }
        }
       
        private bool ShowSearchOptions(int bagSlot)
        {
            bool status = true;
            bool searching = true;
            while (searching)
            {
                int intInput;
                Console.WriteLine("Welcome to the " + Data.RegionText + "! Here you can: ");

                Console.WriteLine("0 - Menu");
                Console.WriteLine("1 - Search for parts");
                Console.WriteLine("2 - Scan for parts");
                Console.WriteLine("3 - Look in bag");
                Console.WriteLine("4 - Go to another region");

                intInput = StaticUtility.CheckInput(0, 4);

                switch (intInput)
                {
                    case 0:
                        status = RunMenu();
                        searching = status;
                        break;
                    case 1:
                        if (Data.CurrentLevel.Locations[Data.CurrentRegion].PartsList.Count == 0)
                        {
                            Console.WriteLine("There are no more parts in this region");
                        }
                        else
                        {
                            Data.CurrentPlayer.Bag[bagSlot] = Data.CurrentLevel.Locations[Data.CurrentRegion].PartsList.Last();
                            Data.CurrentLevel.Locations[Data.CurrentRegion].PartsList.RemoveLast();
                            Console.WriteLine("You found: " + Data.CurrentPlayer.Bag[bagSlot].PartName);
                        }
                        searching = false;
                        break;
                    case 2:
                        foreach (var park in Data.CurrentLevel.Locations)
                            Console.WriteLine("There are " + park.PartsList.Count + " parts left in the " + park.ParkName + ".");
                        searching = false;
                        break;
                    case 3:
                        PlayerManager.CheckBag(Data.CurrentPlayer);
                        break;
                    case 4:
                        Data.MoveRegions();
                        break;
                    default:
                        throw new Exception("Bad Input in GameController.ShowSearchOptions");
                }
            }
            return status;
        }

        private bool RunMenu()
        {
            bool gameStatus = true;

            Console.WriteLine("Would you like to quit?  Today's progress will not be saved.");
            Console.WriteLine("1 - Yes");
            Console.WriteLine("2 - No");
            int intInput = StaticUtility.CheckInput(1, 2);

            if (intInput == 1)
            {
                gameStatus = false;
            }

            return gameStatus;
        }

        private bool ShowLabOptions()
        {
            bool status = true;
            bool halt = true;
            while (halt)
            {
                Console.WriteLine("0 - Menu");
                Console.WriteLine("1 - Work on the monster");
                Console.WriteLine("2 - Scrap unwanted parts");
                Console.WriteLine("3 - Repair monster's parts");
                Console.WriteLine("4 - Head out to the floor show");

                int intInput = StaticUtility.CheckInput(0, 4);
                int answer = 0;

                switch (intInput)
                {
                    case 0:
                        status = RunMenu();
                        halt = status;
                        break;
                    case 1:
                        if (Data.CurrentPlayer.Monster == null)
                        {
                            Data.CurrentPlayer.Monster = BuildMonster(true);
                        }
                        else
                        {
                            Data.CurrentPlayer.Monster = BuildMonster(false);
                        }
                        break;
                    case 2:
                        Console.WriteLine("Which Item would you like to scrap?");
                        Console.WriteLine("0 - Exit");
                        PlayerManager.CheckWorkshop(Data.CurrentPlayer);
                        answer = StaticUtility.CheckInput(0, Data.CurrentPlayer.Workshop.Count);
                        if (answer != 0)
                        {
                            PlayerManager.ScrapItem(Data.CurrentPlayer, Data.CurrentPlayer.Workshop, answer - 1);
                        }
                        break;
                    case 3:
                        if (Data.CurrentPlayer.Monster != null)
                        {
                            Console.WriteLine("Which Item would you like to repair?");
                            Console.WriteLine("0 - Exit");
                            int count = 0;
                            foreach (var part in Data.CurrentPlayer.Monster.Parts)
                            {
                                count++;
                                if (part != null)
                                {
                                    Console.WriteLine(count + " - " + part.PartName + ": Durability " + part.PartDurability);
                                }
                            }
                            answer = StaticUtility.CheckInput(0, 7);
                            if (answer != 0)
                            {
                                if (Data.CurrentPlayer.Monster.Parts[answer-1] == null)
                                {
                                    Console.WriteLine("Please pick an existing part to repair that part.");
                                }
                                else
                                {
                                    PlayerManager.RepairMonster(Data.CurrentPlayer, answer - 1);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("You need a monster to repair a monster.");
                        }
                        break;
                    case 4:
                        halt = false;
                        break;
                    default:
                        throw new Exception("Bad Input in GameController.ShowLabOptions");
                }
            }
            return status;
        }

        private MonsterData BuildMonster(bool isNew)
        {
            int intInput;
            PartData[] table = new PartData[6];
            string type = "";
            PartData chosenPart;
            bool halt = false;
            bool leave = false;
            int loopStart = 0;
            MonsterData currentMonster = Data.CurrentPlayer.Monster;
            List<PartData> workshopCopy = new List<PartData>();

            if (isNew)
            {
                Console.WriteLine("You aproach the empty table...");
            }
            else
            {
                Console.WriteLine("Would you like to end " + currentMonster.Name +"'s career?  This is permanent..." );
                Console.WriteLine("1 - Yes, kill " + currentMonster.Name);
                Console.WriteLine("2 - No, upgrade limbs");
                intInput = StaticUtility.CheckInput(1, 2);
                if (intInput == 2)
                {
                    loopStart = 2;
                    Console.WriteLine(currentMonster.Name + " slides onto the table...");
                    for (int i = 0; i < 6; i++)
                    {
                        table[i] = currentMonster.Parts[i];
                    }
                }
                else
                {                   
                    Data.Graveyard.Add(new MonsterGhost(currentMonster, Data.GameDayNumber));
                    loopStart = 0;
                    Console.WriteLine("You gently dismember " + currentMonster.Name + " and bury its head and torso in the communal graveyard.");
                    Console.WriteLine(currentMonster.Name + " will be missed.");
                    Console.WriteLine("Limbs have been added to your workshop inventory");
                    for (int i = 2; i < currentMonster.Parts.Length; i++)
                    {
                        if (currentMonster.Parts[i] != null)
                        {
                            Console.WriteLine(currentMonster.Parts[i].PartName + ", Durability: " + currentMonster.Parts[i].PartDurability);
                            Data.CurrentPlayer.Workshop.Add(currentMonster.Parts[i]);
                        }
                    }
                    Data.CurrentPlayer.Monster = null;
                    currentMonster = null;
                    Data.CurrentPlayer.Workshop.Sort(Data.CurrentPlayer.Comparer);
                    isNew = true;
                }
            }

            workshopCopy = Data.CurrentPlayer.Workshop.Select(x => x).ToList();

            for (int i = loopStart; i < 6; i++)
            {
                switch (i)
                {
                    case 0:
                        type = "head";
                        break;
                    case 1:
                        type = "torso";
                        break;
                    case 2:
                        type = "left arm";
                        break;
                    case 3:
                        type = "right arm";
                        break;
                    case 4:
                        type = "left leg";
                        break;
                    case 5:
                        type = "right leg";
                        break;
                    default:
                        break;
                }

                halt = true;

                if (!workshopCopy.Any(x => x.PartType == i))
                {
                    Console.WriteLine("You do not have a " + type + " in your workshop.");
                    if (i == 0 || i == 1)
                    {
                        Console.WriteLine("A monster without a " + type + " is no moster at all, better luck tomorrow...");
                        table[0] = null; //this is in case they have a head but no torso
                        break;
                    }
                    halt = false;
                }

                while (halt)
                {
                    if (isNew == false && currentMonster.Parts[i] != null)
                    {
                        table[i] = currentMonster.Parts[i];
                        StaticUtility.TalkPause("Currently " + currentMonster.Name + " has the below " + type);
                        Console.WriteLine(currentMonster.Parts[i].PartName);
                        Console.WriteLine("Durability: " + currentMonster.Parts[i].PartDurability);
                        Console.WriteLine("Alacrity: " + currentMonster.Parts[i].Stats[0]);
                        Console.WriteLine("Strenght: " + currentMonster.Parts[i].Stats[1]);
                        Console.WriteLine("Endurance: " + currentMonster.Parts[i].Stats[2]);
                        StaticUtility.TalkPause("Technique: " + currentMonster.Parts[i].Stats[3]);
                    }

                    Console.WriteLine("Workshop Items:");
                    Console.WriteLine("0 - Leave Table");
                    int count = 0;
                    foreach (var item in workshopCopy)
                    {
                        count++;
                        Console.WriteLine(count + " - " + item.PartName);
                    }

                    Console.WriteLine("Please choose a " + type + ":");
                    intInput = StaticUtility.CheckInput(0, Data.CurrentPlayer.PartListCount(workshopCopy));

                    if (intInput == 0)
                    {
                        halt = false;
                        leave = true;
                        break;
                    }
                    chosenPart = workshopCopy[intInput - 1];

                    Console.WriteLine(chosenPart.PartName);
                    if (chosenPart.PartType != (i))
                    {
                        Console.WriteLine("That is not a " + type + "!");
                    }
                    else
                    {
                        Console.WriteLine("Durability: " + chosenPart.PartDurability);
                        Console.WriteLine("Alacrity: " + chosenPart.Stats[0]);
                        Console.WriteLine("Strenght: " + chosenPart.Stats[1]);
                        Console.WriteLine("Endurance: " + chosenPart.Stats[2]);
                        StaticUtility.TalkPause("Technique: " + chosenPart.Stats[3]);
                        Console.WriteLine("Use this part?");
                        Console.WriteLine("1 - Yes");
                        Console.WriteLine("2 - No");
                        Console.WriteLine("3 - Skip part");
                        Console.WriteLine("4 - Leave Table");
                        int intInput2 = StaticUtility.CheckInput(1, 4);

                        switch (intInput2)
                        {
                            case 1:
                                if (table[i] != null) workshopCopy.Add(table[i]);
                                table[i] = chosenPart;
                                workshopCopy[intInput - 1] = null;
                                workshopCopy = workshopCopy.Where(x => x != null).ToList();
                                halt = false;
                                break;
                            case 2:
                                break;
                            case 3:
                                halt = false;
                                break;
                            case 4:
                                leave = true;
                                halt = false;
                                break;
                            default:
                                break;
                        }

                    }

                }
                //leave table
                if (leave)
                {
                    break;
                }
            }

            if (table[0] != null && table[1] != null)
            {
                MonsterData newMonster = new MonsterData(null, table);

                StaticUtility.TalkPause("This is your monster...");
                foreach (var part in table)
                {
                    if (part != null)
                    {
                        Console.WriteLine(part.PartName);
                    } 
                }
                int count = 0;
                foreach (var stat in newMonster.MonsterStats)
                {
                    Console.WriteLine(StaticReference.statList[count] + ": " + stat);
                    count++;
                }
                Console.WriteLine("Would you like to keep this monster?");
                Console.WriteLine("1 - Yes, 2 - No");
                intInput = StaticUtility.CheckInput(1, 2);
                if (intInput == 1)
                {
                    if (isNew)
                    {
                        Console.WriteLine("What is its name?");
                        currentMonster = newMonster;
                        currentMonster.Name = Console.ReadLine();

                    }
                    else
                    {
                        currentMonster.Parts = table;
                    }
                    Data.CurrentPlayer.Workshop = workshopCopy.Select(x => x).ToList();
                }
                else
                {
                    Console.WriteLine("Better luck building tomorrow...");
                }
            }

            PlayerManager.DumpWorkshopNulls(Data.CurrentPlayer);
            return currentMonster;

        }

        private void CalculateFights()
        {
            Queue<PlayerData> fighters = new Queue<PlayerData>();

            //find all available competitors
            foreach (var player in AllPlayers)
            {
                if (player.Monster != null && player.Monster.CanFight)
                {
                    fighters.Enqueue(player);
                }
            }

            //pair off
            if (fighters.Count == 0)
            {
                StaticUtility.TalkPause("There will be no show tonight!  Better luck gathering tomorrow");
            }
            else if (fighters.Count == 1)
            {
                StaticUtility.TalkPause("Only one of you managed to scrape together a monster?  No shows tonight, but rewards for the one busy beaver.");
                Arena.GrantCash(fighters.Dequeue(), 1);
            }
            else
            {
                decimal countTotal = fighters.Count;
                //fight in rounds
                while (fighters.Count != 0)
                {
                    int round = 0;
                    if (fighters.Count == 1)
                    {
                        StaticUtility.TalkPause("And we have a winner!");
                        Arena.GrantCash(fighters.Dequeue(), round);
                    }
                    else
                    {
                        StaticUtility.TalkPause("Draw your eyes to the arena!");
                        PlayerData left = fighters.Dequeue();
                        PlayerData right = fighters.Dequeue();
                        fighters.Enqueue(Arena.MonsterFight(left, right));

                    }
                    if (fighters.Count <= Math.Ceiling(countTotal / 2))
                    {
                        round = round + 1;
                        countTotal = fighters.Count;
                    }

                }

            }

            //apply luck to losers
        }

        public void SortPlayersByWins()
        {
            for (int i = 0; i < AllPlayers.Length; i++)
            {
                PlayerData left = AllPlayers[i];
                PlayerData high = AllPlayers[i];
                int highIndex = i;

                for (int j = i + 1; j < AllPlayers.Length; j++)
                {
                    if (high.Compare(high, AllPlayers[j]) < 0)
                    {
                        high = AllPlayers[j];
                        highIndex = j;
                    }
                }

                if (left != high)
                {
                    AllPlayers[highIndex] = left;
                    AllPlayers[i] = high;
                }
            }
        }
    }
}
