﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using Server.MirDatabase;
using Server.MirNetwork;
using Server.MirObjects;
using S = ServerPackets;

namespace Server.MirEnvir
{
    public class MobThread
    {
        public int Id = 0;
        public long LastRunTime = 0;
        public long StartTime = 0;
        public long EndTime = 0;
        public LinkedList<MapObject> ObjectsList = new LinkedList<MapObject>();
        public LinkedListNode<MapObject> current = null;
        public Boolean Stop = false;
    }

    public class RandomProvider
    {
        private static int seed = Environment.TickCount;
        private static ThreadLocal<Random> RandomWrapper = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        public static Random GetThreadRadom()
        {
            return RandomWrapper.Value;
        }

        public int Next()
        {
            return RandomWrapper.Value.Next();
        }
        public int Next(int maxValue)
        {
            return RandomWrapper.Value.Next(maxValue);
        }
        public int Next(int minValue, int maxValue)
        {
            return RandomWrapper.Value.Next(minValue, maxValue);
        }
    }

    public class Envir
    {
        public static object AccountLock = new object();
        public static object LoadLock = new object();

        public const int Version = 77;
        public const int CustomVersion = 0;
        public const string DatabasePath = @".\Server.MirDB";
        public const string AccountPath = @".\Server.MirADB";
        public const string BackUpPath = @".\Back Up\";
        public bool ResetGS = false;

        private static readonly Regex AccountIDReg, PasswordReg, EMailReg, CharacterReg;

        public static int LoadVersion;
        public static int LoadCustomVersion;

        private readonly DateTime _startTime = DateTime.Now;
        public readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        public long Time { get; private set; }
        public RespawnTimer RespawnTick = new RespawnTimer();
        private static List<string> DisabledCharNames = new List<string>();

        public DateTime Now
        {
            get { return _startTime.AddMilliseconds(Time); }
        }

        public bool Running { get; private set; }


        private static uint _objectID;
        public uint ObjectID
        {
            get { return ++_objectID; }
        }

        public static int _playerCount;
        public int PlayerCount
        {
            get { return Players.Count; }
        }

        public RandomProvider Random = new RandomProvider();


        private Thread _thread;
        private TcpListener _listener;
        private bool StatusPortEnabled = true;
        public List<MirStatusConnection> StatusConnections = new List<MirStatusConnection>();
        private TcpListener _StatusPort;
        private int _sessionID;
        public List<MirConnection> Connections = new List<MirConnection>();
        

        //Server DB
        public int MapIndex, ItemIndex, MonsterIndex, NPCIndex, QuestIndex, GameshopIndex, ConquestIndex, RespawnIndex;
        public List<MapInfo> MapInfoList = new List<MapInfo>();
        public List<ItemInfo> ItemInfoList = new List<ItemInfo>();
        public List<MonsterInfo> MonsterInfoList = new List<MonsterInfo>();
        public List<MagicInfo> MagicInfoList = new List<MagicInfo>();
        public List<NPCInfo> NPCInfoList = new List<NPCInfo>();
        public DragonInfo DragonInfo = new DragonInfo();
        public List<QuestInfo> QuestInfoList = new List<QuestInfo>();
        public List<GameShopItem> GameShopList = new List<GameShopItem>();
        public Dictionary<int, int> GameshopLog = new Dictionary<int, int>();

        //User DB
        public int NextAccountID, NextCharacterID;
        public long NextUserItemID, NextAuctionID, NextMailID;
        public List<AccountInfo> AccountList = new List<AccountInfo>();
        public List<CharacterInfo> CharacterList = new List<CharacterInfo>(); 
        public LinkedList<AuctionInfo> Auctions = new LinkedList<AuctionInfo>();
        public int GuildCount, NextGuildID;
        public List<GuildObject> GuildList = new List<GuildObject>();
       

        //Live Info
        public List<Map> MapList = new List<Map>();
        public List<SafeZoneInfo> StartPoints = new List<SafeZoneInfo>(); 
        public List<ItemInfo> StartItems = new List<ItemInfo>();
        public List<MailInfo> Mail = new List<MailInfo>();
        public List<PlayerObject> Players = new List<PlayerObject>();
        public bool Saving = false;
        public LightSetting Lights;
        public LinkedList<MapObject> Objects = new LinkedList<MapObject>();

        public List<ConquestInfo> ConquestInfos = new List<ConquestInfo>();
        public List<ConquestObject> Conquests = new List<ConquestObject>();
        


        //multithread vars
        readonly object _locker = new object();
        public MobThread[] MobThreads = new MobThread[Settings.ThreadLimit];
        private Thread[] MobThreading = new Thread[Settings.ThreadLimit];
        public int spawnmultiplyer = 1;//set this to 2 if you want double spawns (warning this can easely lag your server far beyond what you imagine)

        public List<string> CustomCommands = new List<string>();
        public Dragon DragonSystem;
        public NPCObject DefaultNPC;
        public NPCObject MonsterNPC;
        public NPCObject RobotNPC;

        public List<DropInfo> FishingDrops = new List<DropInfo>();
        public List<DropInfo> AwakeningDrops = new List<DropInfo>();
        public List<DropInfo> LotteryTicketDrops = new List<DropInfo>();

        public List<DropInfo> StrongboxDrops = new List<DropInfo>();
        public List<DropInfo> BlackstoneDrops = new List<DropInfo>();

        public List<GuildAtWar> GuildsAtWar = new List<GuildAtWar>();
        public List<MapRespawn> SavedSpawns = new List<MapRespawn>();

        public List<Rank_Character_Info> RankTop = new List<Rank_Character_Info>();
        public List<Rank_Character_Info>[] RankClass = new List<Rank_Character_Info>[5];
        public int[] RankBottomLevel = new int[6];

        static Envir()
        {
            AccountIDReg =
                new Regex(@"^[A-Za-z0-9]{" + Globals.MinAccountIDLength + "," + Globals.MaxAccountIDLength + "}$");
            PasswordReg =
                new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");
            EMailReg = new Regex(@"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*");
            CharacterReg =
                new Regex(@"^.{" + Globals.MinCharacterNameLength + "," + Globals.MaxCharacterNameLength +
                          "}$");

            string path = Path.Combine(Settings.EnvirPath,  "DisabledChars.txt");
            DisabledCharNames.Clear();
            if (!File.Exists(path))
            {
                File.WriteAllText(path,"");
            }
            else
            {
                string[] lines = File.ReadAllLines(path);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    DisabledCharNames.Add(lines[i].ToUpper());
                }
            }
        }

        public static int LastCount = 0, LastRealCount = 0;
        public static long LastRunTime = 0;
        public int MonsterCount;

        private long warTime, mailTime, guildTime, conquestTime, rentalItemsTime;
        private int DailyTime = DateTime.Now.Day;

        private bool MagicExists(Spell spell)
        {
            for (int i = 0; i < MagicInfoList.Count; i++ )
            {
                if (MagicInfoList[i].Spell == spell) return true;
            }
            return false;
        }

        private void UpdateMagicInfo()
        {
            for (int i = 0; i < MagicInfoList.Count; i++)
            {
                switch(MagicInfoList[i].Spell)
                {
                        //warrior
                    case Spell.刺杀剑术:
                        MagicInfoList[i].MultiplierBase = 0.25f;
                        MagicInfoList[i].MultiplierBonus = 0.25f;
                        break;
                    case Spell.半月弯刀:
                        MagicInfoList[i].MultiplierBase = 0.3f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.野蛮冲撞:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                    case Spell.双龙斩:
                        MagicInfoList[i].MultiplierBase = 0.8f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.烈火剑法:
                        MagicInfoList[i].MultiplierBase = 1.4f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                    case Spell.狂风斩:
                        MagicInfoList[i].MultiplierBase = 0.4f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.空破闪:
                        MagicInfoList[i].MultiplierBase = 1f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                    case Spell.日闪:
                        MagicInfoList[i].MultiplierBase = 3.25f;
                        MagicInfoList[i].MultiplierBonus = 0.25f;
                        break;
                        //wiz
                    case Spell.抗拒火环:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                        //tao
                    case Spell.施毒术:
                        MagicInfoList[i].MPowerBase = 0;
                        break;
                    case Spell.诅咒术:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.烦恼:
                        MagicInfoList[i].MPowerBase = 0;
                        MagicInfoList[i].PowerBase = 0;
                        break;
                        //sin
                    case Spell.绝命剑法:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.风剑术:
                        MagicInfoList[i].MultiplierBase = 0.8f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.烈风击:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                    case Spell.月影术:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.烈火身:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.血风击:
                        MagicInfoList[i].MultiplierBase = 0.2f;
                        MagicInfoList[i].MultiplierBonus = 0.05f;
                        break;
                    case Spell.月华乱舞:
                        MagicInfoList[i].MultiplierBase = 1f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                        //archer
                        //no changes :p
                }
            }
        }

        private void FillMagicInfoList()
        {
            //Warrior
            if (!MagicExists(Spell.基本剑术)) MagicInfoList.Add(new MagicInfo {Name = "基本剑术", Spell = Spell.基本剑术, Icon = 2, Level1 = 7, Level2 = 9, Level3 = 12, Need1 = 270, Need2 = 600, Need3 = 1300, Range = 0 });
            if (!MagicExists(Spell.攻杀剑术)) MagicInfoList.Add(new MagicInfo { Name = "攻杀剑术", Spell = Spell.攻杀剑术, Icon = 6, Level1 = 15, Level2 = 17, Level3 = 20, Need1 = 500, Need2 = 1100, Need3 = 1800, Range = 0 });
            if (!MagicExists(Spell.刺杀剑术)) MagicInfoList.Add(new MagicInfo { Name = "刺杀剑术", Spell = Spell.刺杀剑术, Icon = 11, Level1 = 22, Level2 = 24, Level3 = 27, Need1 = 2000, Need2 = 3500, Need3 = 6000, Range = 0, MultiplierBase = 0.25f, MultiplierBonus = 0.25f });
            if (!MagicExists(Spell.半月弯刀)) MagicInfoList.Add(new MagicInfo { Name = "半月弯刀", Spell = Spell.半月弯刀, Icon = 24, Level1 = 26, Level2 = 28, Level3 = 31, Need1 = 5000, Need2 = 8000, Need3 = 14000, BaseCost = 3, Range = 0, MultiplierBase =0.3f, MultiplierBonus = 0.1f });
            if (!MagicExists(Spell.野蛮冲撞)) MagicInfoList.Add(new MagicInfo { Name = "野蛮冲撞", Spell = Spell.野蛮冲撞, Icon = 26, Level1 = 30, Level2 = 32, Level3 = 34, Need1 = 3000, Need2 = 4000, Need3 = 6000, BaseCost = 4, LevelCost = 4, DelayBase = 2500, Range = 0 , MPowerBase = 4});
            if (!MagicExists(Spell.双龙斩)) MagicInfoList.Add(new MagicInfo { Name = "双龙斩", Spell = Spell.双龙斩, Icon = 37, Level1 = 32, Level2 = 34, Level3 = 37, Need1 = 4000, Need2 = 6000, Need3 = 10000, BaseCost = 10, Range = 0 , MultiplierBase = 0.8f, MultiplierBonus = 0.1f});
            if (!MagicExists(Spell.捕绳剑)) MagicInfoList.Add(new MagicInfo { Name = "捕绳剑", Spell = Spell.捕绳剑, Icon = 46, Level1 = 32, Level2 = 35, Level3 = 37, Need1 = 2000, Need2 = 3500, Need3 = 5500, BaseCost = 15, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.烈火剑法)) MagicInfoList.Add(new MagicInfo { Name = "烈火剑法", Spell = Spell.烈火剑法, Icon = 25, Level1 = 35, Level2 = 37, Level3 = 40, Need1 = 2000, Need2 = 4000, Need3 = 6000, BaseCost = 7, Range = 0, MultiplierBase = 1.4f, MultiplierBonus = 0.4f});
            if (!MagicExists(Spell.狮子吼)) MagicInfoList.Add(new MagicInfo { Name = "狮子吼", Spell = Spell.狮子吼, Icon = 42, Level1 = 36, Level2 = 39, Level3 = 41, Need1 = 5000, Need2 = 8000, Need3 = 12000, BaseCost = 14, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.狂风斩)) MagicInfoList.Add(new MagicInfo { Name = "狂风斩", Spell = Spell.狂风斩, Icon = 33, Level1 = 38, Level2 = 40, Level3 = 42, Need1 = 7000, Need2 = 11000, Need3 = 16000, BaseCost = 6, Range = 0, MultiplierBase = 0.4f, MultiplierBonus = 0.1f });
            if (!MagicExists(Spell.空破闪)) MagicInfoList.Add(new MagicInfo { Name = "空破闪", Spell = Spell.空破闪, Icon = 43, Level1 = 38, Level2 = 41, Level3 = 43, Need1 = 5000, Need2 = 8000, Need3 = 12000, BaseCost = 14, LevelCost = 4, Range = 0, MultiplierBonus = 0.3f});
            if (!MagicExists(Spell.护身气幕)) MagicInfoList.Add(new MagicInfo { Name = "护身气幕", Spell = Spell.护身气幕, Icon = 50, Level1 = 39, Level2 = 42, Level3 = 45, Need1 = 6000, Need2 = 12000, Need3 = 18000, BaseCost = 23, LevelCost = 6, Range = 0 });
            if (!MagicExists(Spell.剑气爆)) MagicInfoList.Add(new MagicInfo { Name = "剑气爆", Spell = Spell.剑气爆, Icon = 49, Level1 = 44, Level2 = 47, Level3 = 50, Need1 = 8000, Need2 = 14000, Need3 = 20000, BaseCost = 20, LevelCost = 5, Range = 0 });
            if (!MagicExists(Spell.血龙剑法)) MagicInfoList.Add(new MagicInfo { Name = "血龙剑法", Spell = Spell.血龙剑法, Icon = 76, Level1 = 45, Level2 = 48, Level3 = 51, Need1 = 8000, Need2 = 14000, Need3 = 20000, BaseCost = 10, LevelCost = 4, DelayBase = 600000, DelayReduction = 120000, Range = 0 });
            if (!MagicExists(Spell.天务)) MagicInfoList.Add(new MagicInfo { Name = "天务", Spell = Spell.天务, Icon = 72, Level1 = 47, Level2 = 51, Level3 = 55, Need1 = 7000, Need2 = 11000, Need3 = 15000, BaseCost = 12, LevelCost = 4, DelayBase = 24000, Range = 0 , MultiplierBonus = 0.4f});
            if (!MagicExists(Spell.日闪)) MagicInfoList.Add(new MagicInfo { Name = "日闪", Spell = Spell.日闪, Icon = 55, Level1 = 50, Level2 = 53, Level3 = 56, Need1 = 10000, Need2 = 16000, Need3 = 24000, BaseCost = 25, LevelCost = 4, MPowerBase = 1, PowerBase = 3, DelayBase = 14000, DelayReduction = 4000, Range = 0 , MultiplierBase = 3.25f, MultiplierBonus = 0.25f});
            if (!MagicExists(Spell.金刚不坏)) MagicInfoList.Add(new MagicInfo { Name = "金刚不坏", Spell = Spell.金刚不坏, Icon = 80, Level1 = 60, Level2 = 61, Level3 = 62, Need1 = 1560, Need2 = 2200, Need3 = 3000, BaseCost = 10, LevelCost = 4, DelayBase = 600000, DelayReduction = 120000, Range = 0 });

            //Wizard
            if (!MagicExists(Spell.火球术)) MagicInfoList.Add(new MagicInfo { Name = "火球术", Spell = Spell.火球术, Icon = 0, Level1 = 7, Level2 = 9, Level3 = 11, Need1 = 200, Need2 = 350, Need3 = 700, BaseCost = 3, LevelCost = 2, MPowerBase = 8, PowerBase = 2, Range = 9 });
            if (!MagicExists(Spell.抗拒火环)) MagicInfoList.Add(new MagicInfo { Name = "抗拒火环", Spell = Spell.抗拒火环, Icon = 7, Level1 = 12, Level2 = 15, Level3 = 19, Need1 = 500, Need2 = 1300, Need3 = 2200, BaseCost = 2, LevelCost = 2, Range = 0, MPowerBase = 4});
            if (!MagicExists(Spell.诱惑之光)) MagicInfoList.Add(new MagicInfo { Name = "诱惑之光", Spell = Spell.诱惑之光, Icon = 19, Level1 = 13, Level2 = 18, Level3 = 24, Need1 = 530, Need2 = 1100, Need3 = 2200, BaseCost = 3, LevelCost = 1, Range = 9 });
            if (!MagicExists(Spell.大火球)) MagicInfoList.Add(new MagicInfo { Name = "大火球", Spell = Spell.大火球, Icon = 4, Level1 = 15, Level2 = 18, Level3 = 21, Need1 = 2000, Need2 = 2700, Need3 = 3500, BaseCost = 5, LevelCost = 1, MPowerBase = 6, PowerBase = 10, Range = 9 });
            if (!MagicExists(Spell.地狱火)) MagicInfoList.Add(new MagicInfo { Name = "地狱火", Spell = Spell.地狱火, Icon = 8, Level1 = 16, Level2 = 20, Level3 = 24, Need1 = 700, Need2 = 2700, Need3 = 3500, BaseCost = 10, LevelCost = 3, MPowerBase = 14, PowerBase = 6, Range = 0 });
            if (!MagicExists(Spell.雷电术)) MagicInfoList.Add(new MagicInfo { Name = "雷电术", Spell = Spell.雷电术, Icon = 10, Level1 = 17, Level2 = 20, Level3 = 23, Need1 = 500, Need2 = 2000, Need3 = 3500, BaseCost = 9, LevelCost = 2, MPowerBase = 8, MPowerBonus = 20, PowerBase = 9, Range = 9 });
            if (!MagicExists(Spell.瞬息移动)) MagicInfoList.Add(new MagicInfo { Name = "瞬息移动", Spell = Spell.瞬息移动, Icon = 20, Level1 = 19, Level2 = 22, Level3 = 25, Need1 = 350, Need2 = 1000, Need3 = 2000, BaseCost = 10, LevelCost = 3, Range = 0 });
            if (!MagicExists(Spell.爆裂火焰)) MagicInfoList.Add(new MagicInfo { Name = "爆裂火焰", Spell = Spell.爆裂火焰, Icon = 22, Level1 = 22, Level2 = 25, Level3 = 28, Need1 = 3000, Need2 = 5000, Need3 = 10000, BaseCost = 14, LevelCost = 4, MPowerBase = 8, PowerBase = 8, Range = 9 });
            if (!MagicExists(Spell.火墙)) MagicInfoList.Add(new MagicInfo { Name = "火墙", Spell = Spell.火墙, Icon = 21, Level1 = 24, Level2 = 28, Level3 = 33, Need1 = 4000, Need2 = 10000, Need3 = 20000, BaseCost = 30, LevelCost = 5, MPowerBase = 3, PowerBase = 3, Range = 9 });
            if (!MagicExists(Spell.疾光电影)) MagicInfoList.Add(new MagicInfo { Name = "疾光电影", Spell = Spell.疾光电影, Icon = 9, Level1 = 26, Level2 = 29, Level3 = 32, Need1 = 3000, Need2 = 6000, Need3 = 12000, BaseCost = 38, LevelCost = 7, MPowerBase = 12, PowerBase = 12, Range = 0 });
            if (!MagicExists(Spell.寒冰掌)) MagicInfoList.Add(new MagicInfo { Name = "寒冰掌", Spell = Spell.寒冰掌, Icon = 38, Level1 = 28, Level2 = 30, Level3 = 33, Need1 = 3000, Need2 = 5000, Need3 = 8000, BaseCost = 15, LevelCost = 3, MPowerBase = 12, PowerBase = 12, Range = 9 });
            if (!MagicExists(Spell.地狱雷光)) MagicInfoList.Add(new MagicInfo { Name = "地狱雷光", Spell = Spell.地狱雷光, Icon = 23, Level1 = 30, Level2 = 32, Level3 = 34, Need1 = 4000, Need2 = 8000, Need3 = 12000, BaseCost = 29, LevelCost = 9, MPowerBase = 10, MPowerBonus = 20, PowerBase = 10, PowerBonus = 20, Range = 0 });
            if (!MagicExists(Spell.魔法盾)) MagicInfoList.Add(new MagicInfo { Name = "魔法盾", Spell = Spell.魔法盾, Icon = 30, Level1 = 31, Level2 = 34, Level3 = 38, Need1 = 3000, Need2 = 7000, Need3 = 10000, BaseCost = 35, LevelCost = 5, Range = 0 });
            if (!MagicExists(Spell.圣言术)) MagicInfoList.Add(new MagicInfo { Name = "圣言术", Spell = Spell.圣言术, Icon = 31, Level1 = 32, Level2 = 35, Level3 = 39, Need1 = 3000, Need2 = 7000, Need3 = 10000, BaseCost = 52, LevelCost = 13, Range = 9 });
            if (!MagicExists(Spell.噬血术)) MagicInfoList.Add(new MagicInfo { Name = "噬血术", Spell = Spell.噬血术, Icon = 47, Level1 = 33, Level2 = 36, Level3 = 40, Need1 = 3000, Need2 = 5000, Need3 = 8000, BaseCost = 26, LevelCost = 13, MPowerBase = 12, PowerBase = 12, Range = 9 });
            if (!MagicExists(Spell.冰咆哮)) MagicInfoList.Add(new MagicInfo { Name = "冰咆哮", Spell = Spell.冰咆哮, Icon = 32, Level1 = 35, Level2 = 37, Level3 = 40, Need1 = 4000, Need2 = 8000, Need3 = 12000, BaseCost = 33, LevelCost = 3, MPowerBase = 12, PowerBase = 14, Range = 9 });
            if (!MagicExists(Spell.灭天火)) MagicInfoList.Add(new MagicInfo { Name = "灭天火", Spell = Spell.灭天火, Icon = 34, Level1 = 38, Level2 = 40, Level3 = 42, Need1 = 5000, Need2 = 9000, Need3 = 14000, BaseCost = 28, LevelCost = 3, MPowerBase = 15, MPowerBonus = 20, PowerBase = 9, Range = 9 });
            if (!MagicExists(Spell.分身术)) MagicInfoList.Add(new MagicInfo { Name = "分身术", Spell = Spell.分身术, Icon = 41, Level1 = 41, Level2 = 43, Level3 = 45, Need1 = 6000, Need2 = 11000, Need3 = 16000, BaseCost = 21, Range = 0 });
            if (!MagicExists(Spell.火龙气焰)) MagicInfoList.Add(new MagicInfo { Name = "火龙气焰", Spell = Spell.火龙气焰, Icon = 44, Level1 = 42, Level2 = 43, Level3 = 45, Need1 = 6000, Need2 = 11000, Need3 = 16000, BaseCost = 45, LevelCost = 8, MPowerBase = 100, PowerBase = 25, Range = 9 });
            if (!MagicExists(Spell.天霜冰环)) MagicInfoList.Add(new MagicInfo { Name = "天霜冰环", Spell = Spell.天霜冰环, Icon = 51, Level1 = 44, Level2 = 47, Level3 = 50, Need1 = 8000, Need2 = 16000, Need3 = 24000, BaseCost = 65, LevelCost = 10, MPowerBase = 30, MPowerBonus = 10, PowerBase = 20, PowerBonus = 5, Range = 9 });
            if (!MagicExists(Spell.深延术)) MagicInfoList.Add(new MagicInfo { Name = "深延术", Spell = Spell.深延术, Icon = 73, Level1 = 47, Level2 = 49, Level3 = 52, Need1 = 12000, Need2 = 18000, Need3 = 24000, BaseCost = 150, LevelCost = 15, Range = 0 });
            if (!MagicExists(Spell.天上秘术)) MagicInfoList.Add(new MagicInfo { Name = "天上秘术", Spell = Spell.天上秘术, Icon = 52, Level1 = 49, Level2 = 52, Level3 = 55, Need1 = 15000, Need2 = 20000, Need3 = 25000, BaseCost = 115, LevelCost = 17, MPowerBase = 40, MPowerBonus = 10, PowerBase = 20, PowerBonus = 15, Range = 9 });
            if (!MagicExists(Spell.冰焰术)) MagicInfoList.Add(new MagicInfo { Name = "冰焰术", Spell = Spell.冰焰术, Icon = 56, Level1 = 53, Level2 = 56, Level3 = 59, Need1 = 17000, Need2 = 22000, Need3 = 27000, BaseCost = 100, LevelCost = 20, MPowerBase = 100, PowerBase = 50, Range = 0 });
            if (!MagicExists(Spell.Blink)) MagicInfoList.Add(new MagicInfo { Name = "Blink", Spell = Spell.Blink, Icon = 20, Level1 = 19, Level2 = 22, Level3 = 25, Need1 = 350, Need2 = 1000, Need3 = 2000, BaseCost = 10, LevelCost = 3, Range = 9 });
            //if (!MagicExists(Spell.FastMove)) MagicInfoList.Add(new MagicInfo { Name = "FastMove", Spell = Spell.ImmortalSkin, Icon = ?, Level1 = ?, Level2 = ?, Level3 = ?, Need1 = ?, Need2 = ?, Need3 = ?, BaseCost = ?, LevelCost = ?, DelayBase = ?, DelayReduction = ? });
            if (!MagicExists(Spell.StormEscape)) MagicInfoList.Add(new MagicInfo { Name = "StormEscape", Spell = Spell.StormEscape, Icon = 23, Level1 = 60, Level2 = 61, Level3 = 62, Need1 = 2200, Need2 = 3300, Need3 = 4400, BaseCost = 65, LevelCost = 8, MPowerBase = 12, PowerBase = 4, Range = 9 });
            
            
            //Taoist
            if (!MagicExists(Spell.治愈术)) MagicInfoList.Add(new MagicInfo { Name = "治愈术", Spell = Spell.治愈术, Icon = 1, Level1 = 7, Level2 = 11, Level3 = 14, Need1 = 150, Need2 = 350, Need3 = 700, BaseCost = 3, LevelCost = 2, MPowerBase = 14, Range = 9 });
            if (!MagicExists(Spell.精神力战法)) MagicInfoList.Add(new MagicInfo { Name = "精神力战法", Spell = Spell.精神力战法, Icon = 3, Level1 = 9, Level2 = 12, Level3 = 15, Need1 = 350, Need2 = 1300, Need3 = 2700, Range = 0 });
            if (!MagicExists(Spell.施毒术)) MagicInfoList.Add(new MagicInfo { Name = "施毒术", Spell = Spell.施毒术, Icon = 5, Level1 = 14, Level2 = 17, Level3 = 20, Need1 = 700, Need2 = 1300, Need3 = 2700, BaseCost = 2, LevelCost = 1, Range = 9 });
            if (!MagicExists(Spell.灵魂火符)) MagicInfoList.Add(new MagicInfo { Name = "灵魂火符", Spell = Spell.灵魂火符, Icon = 12, Level1 = 18, Level2 = 21, Level3 = 24, Need1 = 1300, Need2 = 2700, Need3 = 4000, BaseCost = 3, LevelCost = 1, MPowerBase = 8, PowerBase = 3, Range = 9 });
            if (!MagicExists(Spell.召唤骷髅)) MagicInfoList.Add(new MagicInfo { Name = "召唤骷髅", Spell = Spell.召唤骷髅, Icon = 16, Level1 = 19, Level2 = 22, Level3 = 26, Need1 = 1000, Need2 = 2000, Need3 = 3500, BaseCost = 12, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.隐身术)) MagicInfoList.Add(new MagicInfo { Name = "隐身术", Spell = Spell.隐身术, Icon = 17, Level1 = 20, Level2 = 23, Level3 = 26, Need1 = 1300, Need2 = 2700, Need3 = 5300, BaseCost = 1, LevelCost = 1, Range = 0 });
            if (!MagicExists(Spell.集体隐身术)) MagicInfoList.Add(new MagicInfo { Name = "集体隐身术", Spell = Spell.集体隐身术, Icon = 18, Level1 = 21, Level2 = 25, Level3 = 29, Need1 = 1300, Need2 = 2700, Need3 = 5300, BaseCost = 2, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.幽灵盾)) MagicInfoList.Add(new MagicInfo { Name = "幽灵盾", Spell = Spell.幽灵盾, Icon = 13, Level1 = 22, Level2 = 24, Level3 = 26, Need1 = 2000, Need2 = 3500, Need3 = 7000, BaseCost = 2, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.心灵启示)) MagicInfoList.Add(new MagicInfo { Name = "心灵启示", Spell = Spell.心灵启示, Icon = 27, Level1 = 23, Level2 = 25, Level3 = 28, Need1 = 1500, Need2 = 2500, Need3 = 4000, BaseCost = 4, LevelCost = 4, Range = 9 });
            if (!MagicExists(Spell.神圣战甲术)) MagicInfoList.Add(new MagicInfo { Name = "神圣战甲术", Spell = Spell.神圣战甲术, Icon = 14, Level1 = 25, Level2 = 27, Level3 = 29, Need1 = 4000, Need2 = 6000, Need3 = 10000, BaseCost = 2, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.气功波)) MagicInfoList.Add(new MagicInfo { Name = "气功波", Spell = Spell.气功波, Icon = 36, Level1 = 27, Level2 = 29, Level3 = 31, Need1 = 1800, Need2 = 2400, Need3 = 3200, BaseCost = 2, LevelCost = 2, Range = 0, MPowerBase = 4 });
            if (!MagicExists(Spell.困魔咒)) MagicInfoList.Add(new MagicInfo { Name = "困魔咒", Spell = Spell.困魔咒, Icon = 15, Level1 = 28, Level2 = 30, Level3 = 32, Need1 = 2500, Need2 = 5000, Need3 = 10000, BaseCost = 7, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.净化术)) MagicInfoList.Add(new MagicInfo { Name = "净化术", Spell = Spell.净化术, Icon = 39, Level1 = 30, Level2 = 32, Level3 = 35, Need1 = 3000, Need2 = 5000, Need3 = 8000, BaseCost = 14, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.群体治疗术)) MagicInfoList.Add(new MagicInfo { Name = "群体治疗术", Spell = Spell.群体治疗术, Icon = 28, Level1 = 31, Level2 = 33, Level3 = 36, Need1 = 2000, Need2 = 4000, Need3 = 8000, BaseCost = 28, LevelCost = 3, MPowerBase = 10, PowerBase = 4, Range = 9 });
            if (!MagicExists(Spell.迷魂术)) MagicInfoList.Add(new MagicInfo { Name = "迷魂术", Spell = Spell.迷魂术, Icon = 48, Level1 = 31, Level2 = 34, Level3 = 36, Need1 = 4000, Need2 = 6000, Need3 = 9000, BaseCost = 22, LevelCost = 10, Range = 9 });
            if (!MagicExists(Spell.无极真气)) MagicInfoList.Add(new MagicInfo { Name = "无极真气", Spell = Spell.无极真气, Icon = 35, Level1 = 33, Level2 = 35, Level3 = 38, Need1 = 5000, Need2 = 7000, Need3 = 10000, BaseCost = 28, LevelCost = 4, Range = 9 });
            if (!MagicExists(Spell.召唤神兽)) MagicInfoList.Add(new MagicInfo { Name = "召唤神兽", Spell = Spell.召唤神兽, Icon = 29, Level1 = 35, Level2 = 37, Level3 = 40, Need1 = 2000, Need2 = 4000, Need3 = 6000, BaseCost = 28, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.苏生术)) MagicInfoList.Add(new MagicInfo { Name = "苏生术", Spell = Spell.苏生术, Icon = 53, Level1 = 37, Level2 = 39, Level3 = 41, Need1 = 2000, Need2 = 6000, Need3 = 10000, BaseCost = 125, LevelCost = 17, Range = 9 });
            if (!MagicExists(Spell.精魂召唤术)) MagicInfoList.Add(new MagicInfo { Name = "精魂召唤术", Spell = Spell.精魂召唤术, Icon = 40, Level1 = 38, Level2 = 41, Level3 = 43, Need1 = 4000, Need2 = 6000, Need3 = 9000, BaseCost = 28, LevelCost = 4, Range = 0 });
            if (!MagicExists(Spell.诅咒术)) MagicInfoList.Add(new MagicInfo { Name = "诅咒术", Spell = Spell.诅咒术, Icon = 45, Level1 = 40, Level2 = 42, Level3 = 44, Need1 = 4000, Need2 = 6000, Need3 = 9000, BaseCost = 17, LevelCost = 3, Range = 9, MPowerBase = 20 });
            if (!MagicExists(Spell.烦恼)) MagicInfoList.Add(new MagicInfo { Name = "烦恼", Spell = Spell.烦恼, Icon = 74, Level1 = 42, Level2 = 44, Level3 = 47, Need1 = 5000, Need2 = 9000, Need3 = 13000, BaseCost = 20, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.毒雾)) MagicInfoList.Add(new MagicInfo { Name = "毒雾", Spell = Spell.毒雾, Icon = 54, Level1 = 43, Level2 = 45, Level3 = 48, Need1 = 4000, Need2 = 8000, Need3 = 12000, BaseCost = 30, LevelCost = 5, MPowerBase = 40, PowerBase = 20, DelayBase = 18000, DelayReduction = 2000, Range = 9 });
            if (!MagicExists(Spell.先天气功)) MagicInfoList.Add(new MagicInfo { Name = "先天气功", Spell = Spell.先天气功, Icon = 57, Level1 = 48, Level2 = 51, Level3 = 54, Need1 = 5000, Need2 = 9000, Need3 = 13000, BaseCost = 50, LevelCost = 20, Range = 9 });
            if (!MagicExists(Spell.血龙水)) MagicInfoList.Add(new MagicInfo { Name = "血龙水", Spell = Spell.血龙水, Icon = 78, Level1 = 45, Level2 = 48, Level3 = 51, Need1 = 4000, Need2 = 8000, Need3 = 12000, BaseCost = 30, LevelCost = 40, Range = 0 });
            //if (!MagicExists(Spell.HealingCircle)) MagicInfoList.Add(new MagicInfo { Name = "HealingCircle", Spell = Spell.ImmortalSkin, Icon = ?, Level1 = ?, Level2 = ?, Level3 = ?, Need1 = ?, Need2 = ?, Need3 = ?, BaseCost = ?, LevelCost = ?, DelayBase = ?, DelayReduction = ? });

            //Assassin
            if (!MagicExists(Spell.绝命剑法)) MagicInfoList.Add(new MagicInfo { Name = "绝命剑法", Spell = Spell.绝命剑法, Icon = 58, Level1 = 7, Level2 = 9, Level3 = 12, Need1 = 500, Need2 = 1000, Need3 = 2300, Range = 0 });
            if (!MagicExists(Spell.风剑术)) MagicInfoList.Add(new MagicInfo { Name = "风剑术", Spell = Spell.风剑术, Icon = 59, Level1 = 15, Level2 = 17, Level3 = 19, Need1 = 700, Need2 = 1500, Need3 = 2200, BaseCost = 2, LevelCost = 1 });
            if (!MagicExists(Spell.体迅风)) MagicInfoList.Add(new MagicInfo { Name = "体迅风", Spell = Spell.体迅风, Icon = 60, Level1 = 20, Level2 = 22, Level3 = 25, Need1 = 2000, Need2 = 3000, Need3 = 6000, BaseCost = 3, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.拔刀术)) MagicInfoList.Add(new MagicInfo { Name = "拔刀术", Spell = Spell.拔刀术, Icon = 61, Level1 = 25, Level2 = 27, Level3 = 30, Need1 = 4000, Need2 = 7000, Need3 = 9000, BaseCost = 12, LevelCost = 2, DelayBase = 200, Range = 0 });
            if (!MagicExists(Spell.风身术)) MagicInfoList.Add(new MagicInfo { Name = "风身术", Spell = Spell.风身术, Icon = 68, Level1 = 27, Level2 = 29, Level3 = 32, Need1 = 5000, Need2 = 7000, Need3 = 10000, BaseCost = 11, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.迁移剑)) MagicInfoList.Add(new MagicInfo { Name = "迁移剑", Spell = Spell.迁移剑, Icon = 62, Level1 = 30, Level2 = 32, Level3 = 35, Need1 = 4000, Need2 = 8000, Need3 = 10000, BaseCost = 13, LevelCost = 2, MPowerBase = 8, Range = 0 });
            if (!MagicExists(Spell.烈风击)) MagicInfoList.Add(new MagicInfo { Name = "烈风击", Spell = Spell.烈风击, Icon = 63, Level1 = 33, Level2 = 35, Level3 = 38, Need1 = 4000, Need2 = 6000, Need3 = 8000, BaseCost = 10, LevelCost = 1, Range = 0 });
            if (!MagicExists(Spell.捕缚术)) MagicInfoList.Add(new MagicInfo { Name = "捕缚术", Spell = Spell.捕缚术, Icon = 64, Level1 = 33, Level2 = 35, Level3 = 38, Need1 = 2000, Need2 = 4000, Need3 = 6000, BaseCost = 14, LevelCost = 2, DelayBase = 60000, DelayReduction = 15000, Range = 9 });
            if (!MagicExists(Spell.猛毒剑气)) MagicInfoList.Add(new MagicInfo { Name = "猛毒剑气", Spell = Spell.猛毒剑气, Icon = 69, Level1 = 34, Level2 = 36, Level3 = 39, Need1 = 5000, Need2 = 8000, Need3 = 11000, BaseCost = 14, LevelCost = 3, Range = 0 });
            if (!MagicExists(Spell.月影术)) MagicInfoList.Add(new MagicInfo { Name = "月影术", Spell = Spell.月影术, Icon = 65, Level1 = 36, Level2 = 39, Level3 = 42, Need1 = 3000, Need2 = 5000, Need3 = 8000, BaseCost = 36, LevelCost = 3, Range = 0 });
            if (!MagicExists(Spell.吸气)) MagicInfoList.Add(new MagicInfo { Name = "吸气", Spell = Spell.吸气, Icon = 66, Level1 = 38, Level2 = 41, Level3 = 44, Need1 = 5000, Need2 = 8000, Need3 = 11000, Range = 0 });
            if (!MagicExists(Spell.轻身步)) MagicInfoList.Add(new MagicInfo { Name = "轻身步", Spell = Spell.轻身步, Icon = 67, Level1 = 40, Level2 = 43, Level3 = 46, Need1 = 4000, Need2 = 6000, Need3 = 9000, BaseCost = 17, LevelCost = 5, DelayBase = 210000, DelayReduction = 40000, Range = 0 });
            if (!MagicExists(Spell.烈火身)) MagicInfoList.Add(new MagicInfo { Name = "烈火身", Spell = Spell.烈火身, Icon = 70, Level1 = 46, Level2 = 49, Level3 = 52, Need1 = 6000, Need2 = 10000, Need3 = 14000, BaseCost = 40, LevelCost = 7, Range = 0 });
            if (!MagicExists(Spell.血风击)) MagicInfoList.Add(new MagicInfo { Name = "血风击", Spell = Spell.血风击, Icon = 75, Level1 = 47, Level2 = 51, Level3 = 55, Need1 = 9000, Need2 = 15000, Need3 = 21000, Range = 0 });
            if (!MagicExists(Spell.月华乱舞)) MagicInfoList.Add(new MagicInfo { Name = "月华乱舞", Spell = Spell.月华乱舞, Icon = 71, Level1 = 50, Level2 = 53, Level3 = 56, Need1 = 12000, Need2 = 16000, Need3 = 24000, BaseCost = 19, LevelCost = 5, Range = 0 });
            //if (!MagicExists(Spell.MoonMist)) MagicInfoList.Add(new MagicInfo { Name = "MoonMist", Spell = Spell.ImmortalSkin, Icon = ?, Level1 = ?, Level2 = ?, Level3 = ?, Need1 = ?, Need2 = ?, Need3 = ?, BaseCost = ?, LevelCost = ?, DelayBase = ?, DelayReduction = ? });

            //Archer
            if (!MagicExists(Spell.必中闪)) MagicInfoList.Add(new MagicInfo { Name = "必中闪", Spell = Spell.必中闪, Icon = 88, Level1 = 7, Level2 = 13, Level3 = 17, Need1 = 270, Need2 = 600, Need3 = 1300, Range = 0 });
            if (!MagicExists(Spell.天日闪)) MagicInfoList.Add(new MagicInfo { Name = "天日闪", Spell = Spell.天日闪, Icon = 89, Level1 = 9, Level2 = 12, Level3 = 16, Need1 = 350, Need2 = 750, Need3 = 1400, BaseCost = 3, LevelCost = 2, MPowerBase = 8, PowerBase = 3, Range = 9 });
            if (!MagicExists(Spell.无我闪)) MagicInfoList.Add(new MagicInfo { Name = "无我闪", Spell = Spell.无我闪, Icon = 90, Level1 = 14, Level2 = 18, Level3 = 21, Need1 = 700, Need2 = 1500, Need3 = 2100, BaseCost = 3, LevelCost = 2, MPowerBase = 6, PowerBase = 2, Range = 9 });
            if (!MagicExists(Spell.爆阱)) MagicInfoList.Add(new MagicInfo { Name = "爆阱", Spell = Spell.爆阱, Icon = 91, Level1 = 22, Level2 = 25, Level3 = 30, Need1 = 2000, Need2 = 3500, Need3 = 5000, BaseCost = 10, LevelCost = 3, MPowerBase = 15, PowerBase = 15, Range = 0 });
            if (!MagicExists(Spell.爆闪)) MagicInfoList.Add(new MagicInfo { Name = "爆闪", Spell = Spell.爆闪, Icon = 92, Level1 = 31, Level2 = 34, Level3 = 39, Need1 = 3000, Need2 = 7000, Need3 = 10000, BaseCost = 8, LevelCost = 2, MPowerBase = 30, PowerBase = 15, Range = 9 });
            if (!MagicExists(Spell.气功术)) MagicInfoList.Add(new MagicInfo { Name = "气功术", Spell = Spell.气功术, Icon = 93, Level1 = 19, Level2 = 24, Level3 = 29, Need1 = 1800, Need2 = 2600, Need3 = 5600, BaseCost = 8, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.万斤闪)) MagicInfoList.Add(new MagicInfo { Name = "万斤闪", Spell = Spell.万斤闪, Icon = 94, Level1 = 20, Level2 = 25, Level3 = 31, Need1 = 1800, Need2 = 2700, Need3 = 6000, BaseCost = 8, LevelCost = 2, MPowerBase = 6, PowerBase = 3, Range = 9 });
            if (!MagicExists(Spell.气流术)) MagicInfoList.Add(new MagicInfo { Name = "气流术", Spell = Spell.气流术, Icon = 96, Level1 = 23, Level2 = 27, Level3 = 32, Need1 = 2100, Need2 = 3800, Need3 = 6500, BaseCost = 8, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.金刚术)) MagicInfoList.Add(new MagicInfo { Name = "金刚术", Spell = Spell.金刚术, Icon = 98, Level1 = 33, Level2 = 38, Level3 = 44, Need1 = 3000, Need2 = 7000, Need3 = 10000, BaseCost = 10, LevelCost = 2, MPowerBase = 15, PowerBase = 5, Range = 0 });
            if (!MagicExists(Spell.风弹步)) MagicInfoList.Add(new MagicInfo { Name = "风弹步", Spell = Spell.风弹步, Icon = 95, Level1 = 30, Level2 = 34, Level3 = 38, Need1 = 2400, Need2 = 3000, Need3 = 6000, BaseCost = 12, LevelCost = 2, DelayBase = 2500, Range = 0 });
            if (!MagicExists(Spell.BindingShot)) MagicInfoList.Add(new MagicInfo { Name = "BindingShot", Spell = Spell.BindingShot, Icon = 97, Level1 = 35, Level2 = 39, Level3 = 42, Need1 = 400, Need2 = 7000, Need3 = 9500, BaseCost = 7, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.吸血地精)) MagicInfoList.Add(new MagicInfo { Name = "吸血地精", Spell = Spell.吸血地精, Icon = 99, Level1 = 28, Level2 = 33, Level3 = 41, Need1 = 2000, Need2 = 2700, Need3 = 7500, BaseCost = 10, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.吸血地闪)) MagicInfoList.Add(new MagicInfo { Name = "吸血地闪", Spell = Spell.吸血地闪, Icon = 100, Level1 = 26, Level2 = 32, Level3 = 36, Need1 = 3000, Need2 = 6000, Need3 = 12000, BaseCost = 12, LevelCost = 3, MPowerBase = 10, PowerBase = 7, Range = 9 });
            if (!MagicExists(Spell.痹魔阱)) MagicInfoList.Add(new MagicInfo { Name = "痹魔阱", Spell = Spell.痹魔阱, Icon = 101, Level1 = 37, Level2 = 43, Level3 = 47, Need1 = 5800, Need2 = 10000, Need3 = 13000, BaseCost = 10, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.毒魔闪)) MagicInfoList.Add(new MagicInfo { Name = "毒魔闪", Spell = Spell.毒魔闪, Icon = 102, Level1 = 40, Level2 = 45, Level3 = 49, Need1 = 6000, Need2 = 14000, Need3 = 16000, BaseCost = 10, LevelCost = 4, MPowerBase = 10, PowerBase = 10, Range = 9 });
            if (!MagicExists(Spell.邪爆闪)) MagicInfoList.Add(new MagicInfo { Name = "邪爆闪", Spell = Spell.邪爆闪, Icon = 103, Level1 = 43, Level2 = 47, Level3 = 50, Need1 = 12000, Need2 = 15000, Need3 = 18000, BaseCost = 15, LevelCost = 3, MPowerBase = 10, MPowerBonus = 20, PowerBase = 10, Range = 9 });
            if (!MagicExists(Spell.蛇柱阱)) MagicInfoList.Add(new MagicInfo { Name = "蛇柱阱", Spell = Spell.蛇柱阱, Icon = 104, Level1 = 46, Level2 = 51, Level3 = 54, Need1 = 14000, Need2 = 17000, Need3 = 20000, BaseCost = 10, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.血龙闪)) MagicInfoList.Add(new MagicInfo { Name = "血龙闪", Spell = Spell.血龙闪, Icon = 105, Level1 = 48, Level2 = 52, Level3 = 55, Need1 = 15000, Need2 = 18000, Need3 = 21000, BaseCost = 40, LevelCost = 10, MPowerBase = 25, MPowerBonus = 25, PowerBase = 25, Range = 9 });
            if (!MagicExists(Spell.OneWithNature)) MagicInfoList.Add(new MagicInfo { Name = "OneWithNature", Spell = Spell.OneWithNature, Icon = 106, Level1 = 50, Level2 = 53, Level3 = 56, Need1 = 17000, Need2 = 19000, Need3 = 24000, BaseCost = 80, LevelCost = 15, MPowerBase = 75, MPowerBonus = 35, PowerBase = 30, PowerBonus = 20, Range = 9 });
            if (!MagicExists(Spell.MentalState)) MagicInfoList.Add(new MagicInfo { Name = "MentalState", Spell = Spell.MentalState, Icon = 81, Level1 = 11, Level2 = 15, Level3 = 22, Need1 = 500, Need2 = 900, Need3 = 1800, BaseCost = 1, LevelCost = 1, Range = 0 });

            //Custom
            if (!MagicExists(Spell.Portal)) MagicInfoList.Add(new MagicInfo { Name = "Portal", Spell = Spell.Portal, Icon = 1, Level1 = 7, Level2 = 11, Level3 = 14, Need1 = 150, Need2 = 350, Need3 = 700, BaseCost = 3, LevelCost = 2, Range = 9 });
        }

        private string CanStartEnvir()
        {
            if (Settings.EnforceDBChecks)
            {
                if (StartPoints.Count == 0) return "不能在没有出生点的情况下启动服务器";

                if (GetMonsterInfo(Settings.SkeletonName, true) == null) return "不能启动缺少怪物: " + Settings.SkeletonName;
                if (GetMonsterInfo(Settings.ShinsuName, true) == null) return "不能启动缺少怪物: " + Settings.ShinsuName;
                if (GetMonsterInfo(Settings.BugBatName, true) == null) return "不能启动缺少怪物: " + Settings.BugBatName;
                if (GetMonsterInfo(Settings.Zuma1, true) == null) return "不能启动缺少怪物: " + Settings.Zuma1;
                if (GetMonsterInfo(Settings.Zuma2, true) == null) return "不能启动缺少怪物: " + Settings.Zuma2;
                if (GetMonsterInfo(Settings.Zuma3, true) == null) return "不能启动缺少怪物: " + Settings.Zuma3;
                if (GetMonsterInfo(Settings.Zuma4, true) == null) return "不能启动缺少怪物: " + Settings.Zuma4;
                if (GetMonsterInfo(Settings.Zuma5, true) == null) return "不能启动缺少怪物: " + Settings.Zuma5;
                if (GetMonsterInfo(Settings.Zuma6, true) == null) return "不能启动缺少怪物: " + Settings.Zuma6;
                if (GetMonsterInfo(Settings.Zuma7, true) == null) return "不能启动缺少怪物: " + Settings.Zuma7;
                if (GetMonsterInfo(Settings.Turtle1, true) == null) return "不能启动缺少怪物: " + Settings.Turtle1;
                if (GetMonsterInfo(Settings.Turtle2, true) == null) return "不能启动缺少怪物: " + Settings.Turtle2;
                if (GetMonsterInfo(Settings.Turtle3, true) == null) return "不能启动缺少怪物: " + Settings.Turtle3;
                if (GetMonsterInfo(Settings.Turtle4, true) == null) return "不能启动缺少怪物: " + Settings.Turtle4;
                if (GetMonsterInfo(Settings.Turtle5, true) == null) return "不能启动缺少怪物: " + Settings.Turtle5;
                if (GetMonsterInfo(Settings.BoneMonster1, true) == null) return "不能启动缺少怪物: " + Settings.BoneMonster1;
                if (GetMonsterInfo(Settings.BoneMonster2, true) == null) return "不能启动缺少怪物: " + Settings.BoneMonster2;
                if (GetMonsterInfo(Settings.BoneMonster3, true) == null) return "不能启动缺少怪物: " + Settings.BoneMonster3;
                if (GetMonsterInfo(Settings.BoneMonster4, true) == null) return "不能启动缺少怪物: " + Settings.BoneMonster4;
                if (GetMonsterInfo(Settings.WhiteSnake, true) == null) return "不能启动缺少怪物: " + Settings.WhiteSnake;
                if (GetMonsterInfo(Settings.AngelName, true) == null) return "不能启动缺少怪物: " + Settings.AngelName;
                if (GetMonsterInfo(Settings.BombSpiderName, true) == null) return "不能启动缺少怪物: " + Settings.BombSpiderName;
                if (GetMonsterInfo(Settings.CloneName, true) == null) return "不能启动缺少怪物: " + Settings.CloneName;
                if (GetMonsterInfo(Settings.AssassinCloneName, true) == null) return "不能启动缺少怪物: " + Settings.AssassinCloneName;
                if (GetMonsterInfo(Settings.VampireName, true) == null) return "不能启动缺少怪物: " + Settings.VampireName;
                if (GetMonsterInfo(Settings.ToadName, true) == null) return "不能启动缺少怪物: " + Settings.ToadName;
                if (GetMonsterInfo(Settings.SnakeTotemName, true) == null) return "不能启动缺少怪物: " + Settings.SnakeTotemName;
                if (GetMonsterInfo(Settings.FishingMonster, true) == null) return "不能启动缺少怪物: " + Settings.FishingMonster;
                if (GetMonsterInfo(Settings.SkeletonName, true) == null) return "不能启动缺少怪物: " + Settings.SkeletonName;
                if (GetMonsterInfo(Settings.ShinsuName, true) == null) return "不能启动缺少怪物: " + Settings.ShinsuName;
                if (GetMonsterInfo(Settings.BugBatName, true) == null) return "不能启动缺少怪物: " + Settings.BugBatName;
                if (GetMonsterInfo(Settings.Zuma1, true) == null) return "不能启动缺少怪物: " + Settings.Zuma1;
                if (GetMonsterInfo(Settings.Zuma2, true) == null) return "不能启动缺少怪物: " + Settings.Zuma2;
                if (GetMonsterInfo(Settings.Zuma3, true) == null) return "不能启动缺少怪物: " + Settings.Zuma3;
                if (GetMonsterInfo(Settings.Zuma4, true) == null) return "不能启动缺少怪物: " + Settings.Zuma4;
                if (GetMonsterInfo(Settings.Zuma5, true) == null) return "不能启动缺少怪物: " + Settings.Zuma5;
                if (GetMonsterInfo(Settings.Zuma6, true) == null) return "不能启动缺少怪物: " + Settings.Zuma6;
                if (GetMonsterInfo(Settings.Zuma7, true) == null) return "不能启动缺少怪物: " + Settings.Zuma7;
                if (GetMonsterInfo(Settings.Turtle1, true) == null) return "不能启动缺少怪物: " + Settings.Turtle1;
                if (GetMonsterInfo(Settings.Turtle2, true) == null) return "不能启动缺少怪物: " + Settings.Turtle2;
                if (GetMonsterInfo(Settings.Turtle3, true) == null) return "不能启动缺少怪物: " + Settings.Turtle3;
                if (GetMonsterInfo(Settings.Turtle4, true) == null) return "不能启动缺少怪物: " + Settings.Turtle4;
                if (GetMonsterInfo(Settings.Turtle5, true) == null) return "不能启动缺少怪物: " + Settings.Turtle5;
                if (GetMonsterInfo(Settings.BoneMonster1, true) == null) return "不能启动缺少怪物: " + Settings.BoneMonster1;
                if (GetMonsterInfo(Settings.BoneMonster2, true) == null) return "不能启动缺少怪物: " + Settings.BoneMonster2;
                if (GetMonsterInfo(Settings.BoneMonster3, true) == null) return "不能启动缺少怪物: " + Settings.BoneMonster3;
                if (GetMonsterInfo(Settings.BoneMonster4, true) == null) return "不能启动缺少怪物: " + Settings.BoneMonster4;
                if (GetMonsterInfo(Settings.BehemothMonster1, true) == null) return "不能启动缺少怪物: " + Settings.BehemothMonster1;
                if (GetMonsterInfo(Settings.BehemothMonster2, true) == null) return "不能启动缺少怪物: " + Settings.BehemothMonster2;
                if (GetMonsterInfo(Settings.BehemothMonster3, true) == null) return "不能启动缺少怪物: " + Settings.BehemothMonster3;
                if (GetMonsterInfo(Settings.HellKnight1, true) == null) return "不能启动缺少怪物: " + Settings.HellKnight1;
                if (GetMonsterInfo(Settings.HellKnight2, true) == null) return "不能启动缺少怪物: " + Settings.HellKnight2;
                if (GetMonsterInfo(Settings.HellKnight3, true) == null) return "不能启动缺少怪物: " + Settings.HellKnight3;
                if (GetMonsterInfo(Settings.HellKnight4, true) == null) return "不能启动缺少怪物: " + Settings.HellKnight4;
                if (GetMonsterInfo(Settings.HellBomb1, true) == null) return "不能启动缺少怪物: " + Settings.HellBomb1;
                if (GetMonsterInfo(Settings.HellBomb2, true) == null) return "不能启动缺少怪物: " + Settings.HellBomb2;
                if (GetMonsterInfo(Settings.HellBomb3, true) == null) return "不能启动缺少怪物: " + Settings.HellBomb3;
                if (GetMonsterInfo(Settings.WhiteSnake, true) == null) return "不能启动缺少怪物: " + Settings.WhiteSnake;
                if (GetMonsterInfo(Settings.AngelName, true) == null) return "不能启动缺少怪物: " + Settings.AngelName;
                if (GetMonsterInfo(Settings.BombSpiderName, true) == null) return "不能启动缺少怪物: " + Settings.BombSpiderName;
                if (GetMonsterInfo(Settings.CloneName, true) == null) return "不能启动缺少怪物: " + Settings.CloneName;
                if (GetMonsterInfo(Settings.AssassinCloneName, true) == null) return "不能启动缺少怪物: " + Settings.AssassinCloneName;
                if (GetMonsterInfo(Settings.VampireName, true) == null) return "不能启动缺少怪物: " + Settings.VampireName;
                if (GetMonsterInfo(Settings.ToadName, true) == null) return "不能启动缺少怪物: " + Settings.ToadName;
                if (GetMonsterInfo(Settings.SnakeTotemName, true) == null) return "不能启动缺少怪物: " + Settings.SnakeTotemName;
                if (GetMonsterInfo(Settings.FishingMonster, true) == null) return "不能启动缺少怪物: " + Settings.FishingMonster;

                if (GetItemInfo(Settings.RefineOreName) == null) return "不能启动缺少物品: " + Settings.RefineOreName;
            }

            //add intelligent creature checks?

            return "true";
        }

        private void WorkLoop()
        {
            try
            {
                Time = Stopwatch.ElapsedMilliseconds;

                long conTime = Time;
                long saveTime = Time + Settings.SaveDelay * Settings.Minute;
                long userTime = Time + Settings.Minute * 5;
                long SpawnTime = Time;
                long processTime = Time + 1000;
                long StartTime = Time;

                int processCount = 0;
                int processRealCount = 0;

                LinkedListNode<MapObject> current = null;

                if (Settings.Multithreaded)
                {
                    for (int j = 0; j < MobThreads.Length; j++)
                    {
                        MobThreads[j] = new MobThread();
                        MobThreads[j].Id = j;
                    }
                }

                StartEnvir();
                string canstartserver = CanStartEnvir();
                if (canstartserver != "true")
                {
                    SMain.Enqueue(canstartserver);
                    StopEnvir();
                    _thread = null;
                    Stop();
                    return;
                }

                if (Settings.Multithreaded)
                {
                    for (int j = 0; j < MobThreads.Length; j++)
                    {
                        MobThread Info = MobThreads[j];
                        if (j > 0) //dont start up 0 
                        {
                            MobThreading[j] = new Thread(() => ThreadLoop(Info));
                            MobThreading[j].IsBackground = true;
                            MobThreading[j].Start();
                        }
                    }
                }

                StartNetwork();

                try
                {
                    while (Running)
                    {
                        Time = Stopwatch.ElapsedMilliseconds;

                        if (Time >= processTime)
                        {
                            LastCount = processCount;
                            LastRealCount = processRealCount;
                            processCount = 0;
                            processRealCount = 0;
                            processTime = Time + 1000;
                        }


                        if (conTime != Time)
                        {
                            conTime = Time;

                            AdjustLights();

                            lock (Connections)
                            {
                                for (int i = Connections.Count - 1; i >= 0; i--)
                                {
                                    Connections[i].Process();
                                }
                            }

                            lock (StatusConnections)
                            {
                                for (int i = StatusConnections.Count - 1; i >= 0; i--)
                                {
                                    StatusConnections[i].Process();
                                }
                            }
                        }


                        if (current == null)
                            current = Objects.First;

                        if (current == Objects.First)
                        {
                            LastRunTime = Time - StartTime;
                            StartTime = Time;
                        }

                        if (Settings.Multithreaded)
                        {
                            for (int j = 1; j < MobThreads.Length; j++)
                            {
                                MobThread Info = MobThreads[j];

                                if (Info.Stop == true)
                                {
                                    Info.EndTime = Time + 10;
                                    Info.Stop = false;
                                }
                            }
                            lock (_locker)
                            {
                                Monitor.PulseAll(_locker);         // changing a blocking condition. (this makes the threads wake up!)
                            }
                            //run the first loop in the main thread so the main thread automaticaly 'halts' untill the other threads are finished
                            ThreadLoop(MobThreads[0]);
                        }

                        Boolean TheEnd = false;
                        long Start = Stopwatch.ElapsedMilliseconds;
                        while ((!TheEnd) && (Stopwatch.ElapsedMilliseconds - Start < 20))
                        {
                            if (current == null)
                            {
                                TheEnd = true;
                                break;
                            }
                            else
                            {
                                LinkedListNode<MapObject> next = current.Next;
                                if (!Settings.Multithreaded || ((current.Value.Race != ObjectType.Monster) || (current.Value.Master != null)))
                                {
                                    if (Time > current.Value.OperateTime)
                                    {

                                        current.Value.Process();
                                        current.Value.SetOperateTime();
                                    }
                                    processCount++;
                                }
                                current = next;
                            }
                        }

                        for (int i = 0; i < MapList.Count; i++)
                            MapList[i].Process();

                        if (DragonSystem != null) DragonSystem.Process();

                        Process();

                        if (Time >= saveTime)
                        {
                            saveTime = Time + Settings.SaveDelay * Settings.Minute;
                            if (!Settings.UseSQLServer)
                            {
                                BeginSaveAccounts();
                                SaveGuilds();
                                SaveGoods();
                                SaveConquests();
                            }
                        }

                        if (Time >= userTime)
                        {
                            userTime = Time + Settings.Minute * 5;
                            Broadcast(new S.Chat
                                {
                                    Message = string.Format("在线玩家: {0}", Players.Count),
                                    Type = ChatType.Hint
                                });
                        }

                        if (Time >= SpawnTime)
                        {
                            SpawnTime = Time + (Settings.Second * 10);//technicaly this limits the respawn tick code to a minimum of 10 second each but lets assume it's not meant to be this accurate
                            SMain.Envir.RespawnTick.Process();
                        }

                        //   if (Players.Count == 0) Thread.Sleep(1);
                        //   GC.Collect();


                    }

                }
                catch (Exception ex)
                {
                    SMain.Enqueue(ex);

                    lock (Connections)
                    {
                        for (int i = Connections.Count - 1; i >= 0; i--)
                            Connections[i].SendDisconnect(3);
                    }

                    // Get stack trace for the exception with source file information
                    var st = new StackTrace(ex, true);
                    // Get the top stack frame
                    var frame = st.GetFrame(0);
                    // Get the line number from the stack frame
                    var line = frame.GetFileLineNumber();

                    File.AppendAllText(@".\Error.txt",
                                           string.Format("[{0}] {1} at line {2}{3}", Now, ex, line, Environment.NewLine));
                }

                StopNetwork();
                StopEnvir();
                if (!Settings.UseSQLServer)
                {
                    SaveAccounts();
                    SaveGuilds(true);
                    SaveConquests(true);
                }
                

            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();

                SMain.Enqueue("[outer workloop error]" + ex);
                File.AppendAllText(@".\Error.txt",
                                       string.Format("[{0}] {1} at line {2}{3}", Now, ex, line, Environment.NewLine));
            }
            _thread = null;

        }
        
        private void ThreadLoop(MobThread Info)
        {
            Info.Stop = false;
            long starttime = Time;
            try
            {

                bool stopping = false;
                if (Info.current == null)
                    Info.current = Info.ObjectsList.First;
                stopping = Info.current == null;
                //while (stopping == false)
                while (Running)
                {
                    if (Info.current == null)
                        Info.current = Info.ObjectsList.First;
                    else
                    {
                        LinkedListNode<MapObject> next = Info.current.Next;

                        //if we reach the end of our list > go back to the top (since we are running threaded, we dont want the system to sit there for xxms doing nothing)
                        if (Info.current == Info.ObjectsList.Last)
                        {
                            next = Info.ObjectsList.First;
                            Info.LastRunTime = (Info.LastRunTime + (Time - Info.StartTime)) / 2;
                            //Info.LastRunTime = (Time - Info.StartTime) /*> 0 ? (Time - Info.StartTime) : Info.LastRunTime */;
                            Info.StartTime = Time;
                        }
                        if (Time > Info.current.Value.OperateTime)
                        {
                            if (Info.current.Value.Master == null)//since we are running multithreaded, dont allow pets to be processed (unless you constantly move pets into their map appropriate thead)
                            {
                                Info.current.Value.Process();
                                Info.current.Value.SetOperateTime();
                            }
                        }
                        Info.current = next;
                    }
                    //if it's the main thread > make it loop till the subthreads are done, else make it stop after 'endtime'
                    if (Info.Id == 0)
                    {
                        stopping = true;
                        for (int x = 1; x < MobThreads.Length; x++)
                            if (MobThreads[x].Stop == false)
                                stopping = false;
                        if (stopping)
                        {
                            Info.Stop = stopping;
                            return;
                        }
                    }
                    else
                    {
                        if ((Stopwatch.ElapsedMilliseconds > Info.EndTime) && Running)
                        {
                            Info.Stop = true;
                            lock (_locker)
                            {
                                while (Info.Stop) Monitor.Wait(_locker);
                            }
                        }
                        
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is ThreadInterruptedException) return;
                SMain.Enqueue(ex);

                File.AppendAllText(@".\Error.txt",
                                       string.Format("[{0}] {1}{2}", Now, ex, Environment.NewLine));
            }
            //Info.Stop = true;
        }

        private void AdjustLights()
        {
            LightSetting oldLights = Lights;

            int hours = (Now.Hour * 2) % 24;
            if (hours == 6 || hours == 7)
                Lights = LightSetting.Dawn;
            else if (hours >= 8 && hours <= 15)
                Lights = LightSetting.Day;
            else if (hours == 16 || hours == 17)
                Lights = LightSetting.Evening;
            else
                Lights = LightSetting.Night;

            if (oldLights == Lights) return;

            Broadcast(new S.TimeOfDay { Lights = Lights });
        }

        public void Process()
        {        
            //if we get to a new day : reset daily's
            if (Now.Day != DailyTime)
            {
                DailyTime = Now.Day;
                ProcessNewDay();
            }

            if(Time >= warTime)
            {
                for (int i = GuildsAtWar.Count - 1; i >= 0; i--)
                {
                    GuildsAtWar[i].TimeRemaining -= Settings.Minute;

                    if (GuildsAtWar[i].TimeRemaining < 0)
                    {
                        GuildsAtWar[i].EndWar();
                        GuildsAtWar.RemoveAt(i);
                    }
                }
                
                warTime = Time + Settings.Minute;
            }

            if (Time >= mailTime)
            {
                for (int i = Mail.Count - 1; i >= 0; i--)
                {
                    MailInfo mail = Mail[i];

                    if(mail.Receive())
                    {
                        //collected mail ok
                    }
                }

                mailTime = Time + (Settings.Minute * 1);
            }

            if (Time >= guildTime)
            {
                guildTime = Time + (Settings.Minute);
                for (int i = 0; i < GuildList.Count; i++)
                {
                    GuildList[i].Process();
                }
            }

            if (Time >= conquestTime)
            {
                conquestTime = Time + (Settings.Second * 10);
                for (int i = 0; i < Conquests.Count; i++)
                    Conquests[i].Process();
            }

            if (Time >= rentalItemsTime)
            {
                rentalItemsTime = Time + Settings.Minute * 5;
                ProcessRentedItems();
            }

        }

        public void Broadcast(Packet p)
        {
            for (int i = 0; i < Players.Count; i++) Players[i].Enqueue(p);
        }

        public void RequiresBaseStatUpdate()
        {
            for (int i = 0; i < Players.Count; i++) Players[i].HasUpdatedBaseStats = false;
        }

        public void SaveDB()
        {
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    SaveItems(ctx);
                    SaveMonsters(ctx);
                    SaveMagics(ctx);
                    SaveMaps(ctx);
                    SaveNPCs(ctx);
                    SaveQuests(ctx);
                    SaveDragon(ctx);
                    SaveGameShop(ctx);
                    SaveConquests(ctx);
                }

                return;
            }
            using (FileStream stream = File.Create(DatabasePath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(CustomVersion);
                writer.Write(MapIndex);
                writer.Write(ItemIndex);
                writer.Write(MonsterIndex);
                writer.Write(NPCIndex);
                writer.Write(QuestIndex);
                writer.Write(GameshopIndex);
                writer.Write(ConquestIndex);
                writer.Write(RespawnIndex);
                
                writer.Write(MapInfoList.Count);
                for (int i = 0; i < MapInfoList.Count; i++)
                    MapInfoList[i].Save(writer);
                
                writer.Write(ItemInfoList.Count);
                for (int i = 0; i < ItemInfoList.Count; i++)
                    ItemInfoList[i].Save(writer);
                
                writer.Write(MonsterInfoList.Count);
                for (int i = 0; i < MonsterInfoList.Count; i++)
                    MonsterInfoList[i].Save(writer);
                
                writer.Write(NPCInfoList.Count);
                for (int i = 0; i < NPCInfoList.Count; i++)
                    NPCInfoList[i].Save(writer);
                
                writer.Write(QuestInfoList.Count);
                for (int i = 0; i < QuestInfoList.Count; i++)
                    QuestInfoList[i].Save(writer);
                
                DragonInfo.Save(writer);
                writer.Write(MagicInfoList.Count);
                for (int i = 0; i < MagicInfoList.Count; i++)
                    MagicInfoList[i].Save(writer);
                
                writer.Write(GameShopList.Count);
                for (int i = 0; i < GameShopList.Count; i++)
                    GameShopList[i].Save(writer);

                writer.Write(ConquestInfos.Count);
                for (int i = 0; i < ConquestInfos.Count; i++)
                    ConquestInfos[i].Save(writer);

                RespawnTick.Save(writer);
            }
        }

        public void SaveConquests(DataContext ctx)
        {
            foreach (var info in ConquestInfos)
            {
                var dbInfo = ctx.ConquestInfos.FirstOrDefault(i => i.Index == info.Index);
                if (dbInfo == null)
                {
                    ctx.ConquestInfos.Add(info);
                }
                else
                {
                    ctx.Entry(dbInfo).CurrentValues.SetValues(info);
                }
                //ctx.SaveChanges();
                foreach (var gateInfo in info.ConquestGates)
                {
                    gateInfo.ConquestInfoIndex = dbInfo?.Index ?? info.Index;
                    var dbGateInfo = ctx.ConquestGateInfos.FirstOrDefault(i => i.Index == gateInfo.Index);
                    if (dbGateInfo == null)
                    {
                        ctx.ConquestGateInfos.Add(gateInfo);
                    }
                    else
                    {
                        ctx.Entry(dbGateInfo).CurrentValues.SetValues(gateInfo);
                    }
                    //ctx.SaveChanges();
                }
                foreach (var guardInfo in info.ConquestGuards)
                {
                    guardInfo.ConquestInfoIndex = dbInfo?.Index ?? info.Index;
                    var dbGuardInfo = ctx.ConquestArcherInfos.FirstOrDefault(i => i.Index == guardInfo.Index);
                    if (dbGuardInfo == null)
                    {
                        ctx.ConquestArcherInfos.Add(guardInfo);
                    }
                    else
                    {
                        ctx.Entry(dbGuardInfo).CurrentValues.SetValues(guardInfo);
                    }
                    //ctx.SaveChanges();
                }
                foreach (var siegeInfo in info.ConquestSieges)
                {
                    siegeInfo.ConquestInfoIndex = dbInfo?.Index ?? info.Index;
                    var dbSiegeInfo = ctx.ConquestSiegeInfos.FirstOrDefault(i => i.Index == siegeInfo.Index);
                    if (dbSiegeInfo == null)
                    {
                        ctx.ConquestSiegeInfos.Add(siegeInfo);
                    }
                    else
                    {
                        ctx.Entry(dbSiegeInfo).CurrentValues.SetValues(siegeInfo);
                    }
                    //ctx.SaveChanges();
                }
                foreach (var wallInfo in info.ConquestWalls)
                {
                    wallInfo.ConquestInfoIndex = dbInfo?.Index ?? info.Index;
                    var dbWallInfo = ctx.ConquestWallInfos.FirstOrDefault(i => i.Index == wallInfo.Index);
                    if (dbWallInfo == null)
                    {
                        ctx.ConquestWallInfos.Add(wallInfo);
                    }
                    else
                    {
                        ctx.Entry(dbWallInfo).CurrentValues.SetValues(wallInfo);
                    }
                    
                }
            }
            ctx.SaveChanges();
        }

        public void SaveGameShop(DataContext ctx)
        {
            foreach (var item in GameShopList)
            {
                var dbItem = ctx.GameShopItems.FirstOrDefault(i => i.GIndex == item.GIndex);
                if (dbItem == null)
                {
                    ctx.GameShopItems.Add(item);
                }
                else
                {
                    ctx.Entry(dbItem).CurrentValues.SetValues(item);
                }
                ctx.SaveChanges();
            }
            
        }

        public void SaveDragon(DataContext ctx)
        {
            var dbDragonInfo = ctx.DragonInfos.FirstOrDefault(i => i.id == DragonInfo.id);
            if (dbDragonInfo == null)
            {
                ctx.DragonInfos.Add(DragonInfo);
            }
            else
            {
                ctx.Entry(dbDragonInfo).CurrentValues.SetValues(DragonInfo);
            }
            ctx.SaveChanges();
        }

        public void SaveQuests(DataContext ctx)
        {
            foreach (var questInfo in QuestInfoList)
            {
                var dbQuestInfo = ctx.QuestInfos.FirstOrDefault(i => i.Index == questInfo.Index);
                if (dbQuestInfo == null)
                {
                    ctx.QuestInfos.Add(questInfo);
                }
                else
                {
                    ctx.Entry(dbQuestInfo).CurrentValues.SetValues(questInfo);
                }
                
            }
            ctx.SaveChanges();
        }

        public void SaveNPCs(DataContext ctx)
        {
            foreach (var npcInfo in NPCInfoList)
            {
                var dbNPCInfo = ctx.NpcInfos.FirstOrDefault(i => i.Index == npcInfo.Index);
                if (dbNPCInfo == null)
                {
                    ctx.NpcInfos.Add(npcInfo);
                }
                else
                {
                    ctx.Entry(dbNPCInfo).CurrentValues.SetValues(npcInfo);
                }
                
            }
            ctx.SaveChanges();
        }

        public void SaveMaps(DataContext ctx)
        {
            foreach (var mapInfo in MapInfoList)
            {
                var dbMapInfo = ctx.MapInfos.FirstOrDefault(i => i.Index == mapInfo.Index);
                if (dbMapInfo == null)
                {
                    ctx.MapInfos.Add(mapInfo);
                }
                else
                {
                    ctx.Entry(dbMapInfo).CurrentValues.SetValues(mapInfo);
                }
                //ctx.SaveChanges();
                foreach (var safeZoneInfo in mapInfo.SafeZones)
                {
                    safeZoneInfo.MapInfoIndex = dbMapInfo?.Index ?? mapInfo.Index;
                    var dbSafeZoneInfo = ctx.SafeZoneInfos.FirstOrDefault(i => i.id == safeZoneInfo.id);
                    if (dbSafeZoneInfo == null)
                    {
                        ctx.SafeZoneInfos.Add(safeZoneInfo);
                    }
                    else
                    {
                        ctx.Entry(dbSafeZoneInfo).CurrentValues.SetValues(safeZoneInfo);
                    }
                }
                //ctx.SaveChanges();
                foreach (var movement in mapInfo.Movements)
                {
                    var dbMovementInfo = ctx.MovementInfos.FirstOrDefault(i => i.id == movement.id);
                    movement.SourceMapIndex = mapInfo.Index;
                    if (dbMovementInfo == null)
                    {
                        ctx.MovementInfos.Add(movement);
                    }
                    else
                    {
                        ctx.Entry(dbMovementInfo).CurrentValues.SetValues(movement);
                    }
                    
                }
                //ctx.SaveChanges();
                foreach (var respawnInfo in mapInfo.Respawns)
                {
                    respawnInfo.MapInfoIndex = dbMapInfo?.Index ?? mapInfo.Index;
                    var dbRespawnInfo = ctx.RespawnInfos.FirstOrDefault(i => i.id == respawnInfo.id);
                    if (dbRespawnInfo == null)
                    {
                        ctx.RespawnInfos.Add(respawnInfo);
                    }
                    else
                    {
                        ctx.Entry(dbRespawnInfo).CurrentValues.SetValues(respawnInfo);
                    }
                    
                }
                //ctx.SaveChanges();
                foreach (var mineZoneInfo in mapInfo.MineZones)
                {
                    mineZoneInfo.MapInfoIndex = dbMapInfo?.Index ?? mapInfo.Index;
                    var dbMineZoneInfo = ctx.MineZones.FirstOrDefault(i => i.id == mineZoneInfo.id);
                    if (dbMineZoneInfo == null)
                    {
                        ctx.MineZones.Add(mineZoneInfo);
                    }
                    else
                    {
                        ctx.Entry(dbMineZoneInfo).CurrentValues.SetValues(mineZoneInfo);
                    }
                    
                }
                
            }
            ctx.SaveChanges();
        }

        public void SaveMagics(DataContext ctx)
        {
            foreach (var info in MagicInfoList)
            {
                var dbInfo = ctx.MagicInfos.FirstOrDefault(i => i.id == info.id);
                if (dbInfo == null)
                {
                    ctx.MagicInfos.Add(info);
                }
                else
                {
                    ctx.Entry(dbInfo).CurrentValues.SetValues(info);
                }
                
            }
            ctx.SaveChanges();
        }

        public void SaveMonsters(DataContext ctx)
        {
            foreach (var info in MonsterInfoList)
            {
                var dbInfo = ctx.MonsterInfos.FirstOrDefault(i => i.Index == info.Index);
                if (dbInfo == null)
                {
                    ctx.MonsterInfos.Add(info);
                }
                else
                {
                    ctx.Entry(dbInfo).CurrentValues.SetValues(info);
                }
                
            }
            ctx.SaveChanges();
        }

        public void SaveItems(DataContext ctx)
        {
            foreach (var itemInfo in ItemInfoList)
            {
                var dbItemInfo = ctx.ItemInfos.FirstOrDefault(i => i.Index == itemInfo.Index);
                if (dbItemInfo == null)
                {
                    ctx.ItemInfos.Add(itemInfo);
                }
                else
                {
                    ctx.Entry(dbItemInfo).CurrentValues.SetValues(itemInfo);
                }
            }
            ctx.SaveChanges();
        }

        public void SaveAccounts()
        {
            while (Saving)
                Thread.Sleep(1);

            try
            {
                using (FileStream stream = File.Create(AccountPath + "n"))
                    SaveAccounts(stream);
                if (File.Exists(AccountPath))
                    File.Move(AccountPath, AccountPath + "o");
                File.Move(AccountPath + "n", AccountPath);
                if (File.Exists(AccountPath + "o"))
                File.Delete(AccountPath + "o");
            }
            catch (Exception ex)
            {
                SMain.Enqueue(ex);
            }
        }

        private void SaveAccounts(Stream stream)
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(CustomVersion);
                writer.Write(NextAccountID);
                writer.Write(NextCharacterID);
                writer.Write(NextUserItemID);
                writer.Write(GuildList.Count);
                writer.Write(NextGuildID);
                writer.Write(AccountList.Count);
                for (int i = 0; i < AccountList.Count; i++)
                    AccountList[i].Save(writer);

                writer.Write(NextAuctionID);
                writer.Write(Auctions.Count);
                foreach (AuctionInfo auction in Auctions)
                    auction.Save(writer);

                writer.Write(NextMailID);
                writer.Write(Mail.Count);
                foreach (MailInfo mail in Mail)
                        mail.Save(writer);
                if (Settings.UseSQLServer)
                {
                    using (var ctx = new DataContext())
                    {
                        ctx.GameShopPurchases.RemoveRange(ctx.GameShopPurchases.Where(p => p.id > 0));
                        foreach (var item in GameshopLog)
                        {
                            ctx.GameShopPurchases.Add(new GameShopPurchase()
                            {
                                GameShopItemIndex = item.Key,
                                GameShopItemQty = item.Value
                            });
                        }
                    }
                }
                writer.Write(GameshopLog.Count);
                foreach (var item in GameshopLog)
                {
                    
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }
                if (Settings.UseSQLServer)
                {
                    using (var ctx = new DataContext())
                    {
                        ctx.RespawnSaves.RemoveRange(ctx.RespawnSaves.Where(s => true));
                        ctx.SaveChanges();
                        foreach (MapRespawn Spawn in SavedSpawns)
                        {
                            RespawnSave Save = new RespawnSave { RespawnIndex = Spawn.Info.RespawnIndex, NextSpawnTick = Spawn.NextSpawnTick, Spawned = (Spawn.Count >= (Spawn.Info.Count * spawnmultiplyer)) };
                            ctx.RespawnSaves.Add(Save);
                        }
                        ctx.SaveChanges();
                    }
                }
                else
                {
                writer.Write(SavedSpawns.Count);
                foreach (MapRespawn Spawn in SavedSpawns)
                {
                    RespawnSave Save = new RespawnSave { RespawnIndex = Spawn.Info.RespawnIndex, NextSpawnTick = Spawn.NextSpawnTick, Spawned = (Spawn.Count >= (Spawn.Info.Count * spawnmultiplyer)) };
                    Save.save(writer);
                }
            }
                
        }
        }

        private void SaveGuilds(bool forced = false)
        {
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    foreach (var guildObject in GuildList)
                    {
                        var dbGuild = ctx.Guilds.FirstOrDefault(g => g.Guildindex == guildObject.Guildindex);
                        if (dbGuild == null)
                        {
                            dbGuild = guildObject;
                            ctx.Guilds.Add(dbGuild);
                        }
                        else
                        {
                            ctx.Entry(dbGuild).CurrentValues.SetValues(guildObject);
                        }
                        ctx.SaveChanges();
                        foreach (var rank in guildObject.Ranks)
                        {
                            rank.GuildIndex = dbGuild.Guildindex;
                            ctx.Ranks.AddOrUpdate(rank);
                            ctx.SaveChanges();
                            foreach (var member in rank.Members)
                            {
                                member.RankId = rank.id;
                                ctx.GuildMembers.AddOrUpdate(member);
                            }
                            ctx.SaveChanges();
                        }
                        foreach (var buff in guildObject.BuffList)
                        {
                            buff.GuildIndex = dbGuild.Guildindex;
                            ctx.GuildBuffs.AddOrUpdate(buff);
                        }
                        ctx.SaveChanges();
                        ctx.GuildStorageItems.RemoveRange(
                            ctx.GuildStorageItems.Where(i => i.GuildIndex == guildObject.Guildindex));
                        ctx.SaveChanges();
                        foreach (var item in guildObject.StoredItems)
                        {
                            
                            ctx.GuildStorageItems.Add(new GuildStorageItem
                            {
                                GuildIndex = guildObject.Guildindex,
                                ItemUniqueID = item?.Item?.UniqueID,
                                UserId = item?.UserId
                            });
                        }
                        ctx.SaveChanges();
                    }
                    
                }
                return;
            }
            if (!Directory.Exists(Settings.GuildPath)) Directory.CreateDirectory(Settings.GuildPath);
            for (int i = 0; i < GuildList.Count; i++)
            {
                if (GuildList[i].NeedSave || forced)
                {
                    GuildList[i].NeedSave = false;
                    MemoryStream mStream = new MemoryStream();
                    BinaryWriter writer = new BinaryWriter(mStream);
                    GuildList[i].Save(writer);
                    FileStream fStream = new FileStream(Settings.GuildPath + i.ToString() + ".mgdn", FileMode.Create);
                    byte[] data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveGuildsAsync, fStream);
                }
            }
        }
        private void EndSaveGuildsAsync(IAsyncResult result)
        {
            FileStream fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream != null)
                {
                    string oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                    string newfilename = fStream.Name;
                    fStream.EndWrite(result);
                    fStream.Dispose();
                    if (File.Exists(oldfilename))
                        File.Move(oldfilename, oldfilename + "o");
                    File.Move(newfilename, oldfilename);
                    if (File.Exists(oldfilename + "o"))
                        File.Delete(oldfilename + "o");
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void SaveGoods(bool forced = false)
        {
            if (!Directory.Exists(Settings.GoodsPath)) Directory.CreateDirectory(Settings.GoodsPath);

            for (int i = 0; i < MapList.Count; i++)
            {
                Map map = MapList[i];

                if (map.NPCs.Count < 1) continue;

                for (int j = 0; j < map.NPCs.Count; j++)
                {
                    NPCObject npc = map.NPCs[j];

                    if (forced)
                    {
                        npc.ProcessGoods(forced);
                    }

                    if (!npc.NeedSave) continue;

                    string path = Settings.GoodsPath + npc.Info.Index.ToString() + ".msdn";

                    MemoryStream mStream = new MemoryStream();
                    BinaryWriter writer = new BinaryWriter(mStream);
                    int Temp = 9999;
                    writer.Write(Temp);
                    writer.Write(Version);
                    writer.Write(CustomVersion);
                    writer.Write(npc.UsedGoods.Count);

                    for (int k = 0; k < npc.UsedGoods.Count; k++)
                    {
                        npc.UsedGoods[k].Save(writer);
                    }

                    FileStream fStream = new FileStream(path, FileMode.Create);
                    byte[] data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveGoodsAsync, fStream);
                }
            }
        }
        private void EndSaveGoodsAsync(IAsyncResult result)
        {
            try
            {
                FileStream fStream = result.AsyncState as FileStream;
                if (fStream != null)
                {
                    string oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                    string newfilename = fStream.Name;
                    fStream.EndWrite(result);
                    fStream.Dispose();
                    if (File.Exists(oldfilename))
                        File.Move(oldfilename, oldfilename + "o");
                    File.Move(newfilename, oldfilename);
                    if (File.Exists(oldfilename + "o"))
                        File.Delete(oldfilename + "o");
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void SaveConquests(bool forced = false)
        {
            if (!Directory.Exists(Settings.ConquestsPath)) Directory.CreateDirectory(Settings.ConquestsPath);
            for (int i = 0; i < Conquests.Count; i++)
            {
                if (Conquests[i].NeedSave || forced)
                {
                    Conquests[i].NeedSave = false;
                    MemoryStream mStream = new MemoryStream();
                    BinaryWriter writer = new BinaryWriter(mStream);
                    Conquests[i].Save(writer);
                    FileStream fStream = new FileStream(Settings.ConquestsPath + Conquests[i].Info.Index.ToString() + ".mcdn", FileMode.Create);
                    byte[] data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveConquestsAsync, fStream);
                }
            }
        }
        private void EndSaveConquestsAsync(IAsyncResult result)
        {
            FileStream fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream != null)
                {
                    string oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                    string newfilename = fStream.Name;
                    fStream.EndWrite(result);
                    fStream.Dispose();
                    if (File.Exists(oldfilename))
                        File.Move(oldfilename, oldfilename + "o");
                    File.Move(newfilename, oldfilename);
                    if (File.Exists(oldfilename + "o"))
                        File.Delete(oldfilename + "o");
                }
            }
            catch (Exception ex)
            {
                
            }
        }

        public void BeginSaveAccounts()
        {
            if (Saving) return;

            Saving = true;
            

            using (MemoryStream mStream = new MemoryStream())
            {
                if (File.Exists(AccountPath))
                {
                    if (!Directory.Exists(BackUpPath)) Directory.CreateDirectory(BackUpPath);
                    string fileName = string.Format("Accounts {0:0000}-{1:00}-{2:00} {3:00}-{4:00}-{5:00}.bak", Now.Year, Now.Month, Now.Day, Now.Hour, Now.Minute, Now.Second);
                    if (File.Exists(Path.Combine(BackUpPath, fileName))) File.Delete(Path.Combine(BackUpPath, fileName));
                    File.Move(AccountPath, Path.Combine(BackUpPath, fileName));
                }

                SaveAccounts(mStream);
                FileStream fStream = new FileStream(AccountPath + "n", FileMode.Create);

                byte[] data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveAccounts, fStream);
            }

        }
        private void EndSaveAccounts(IAsyncResult result)
        {
            FileStream fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream != null)
                {
                    string oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                    string newfilename = fStream.Name;
                    fStream.EndWrite(result);
                    fStream.Dispose();
                    if (File.Exists(oldfilename))
                        File.Move(oldfilename, oldfilename + "o");
                    File.Move(newfilename, oldfilename);
                    if (File.Exists(oldfilename + "o"))
                        File.Delete(oldfilename + "o");
                }
            }
            catch (Exception ex)
            {
            }

            Saving = false;
        }

        public void LoadDB()
        {
            lock (LoadLock)
            {
                if (Settings.UseSQLServer)
                {
                    LoadVersion = 999;
                    LoadCustomVersion = 999;
                    using (var ctx = new DataContext())
                    {
                        MapIndex = ctx.MapInfos.Max(m => (int?)m.Index) ?? 0;
                        ItemIndex = ctx.ItemInfos.Max(i => (int?)i.Index) ?? 0;
                        MonsterIndex = ctx.MonsterInfos.Max(i => (int?)i.Index) ?? 0;
                        NPCIndex = ctx.NpcInfos.Max(i => (int?)i.Index) ?? 0;
                        QuestIndex = ctx.QuestInfos.Max(i => (int?)i.Index) ?? 0;
                        GameshopIndex = ctx.GameShopItems.Max(i => (int?)i.GIndex) ?? 0;
                        ConquestIndex = ctx.ConquestInfos.Max(i => (int?)i.Index) ?? 0;
                        RespawnIndex = ctx.RespawnInfos.Max(i => (int?)i.RespawnIndex) ?? 0;
                        MapInfoList = ctx.MapInfos.ToList();

                        MapInfoList.ForEach(x =>
                        {
                            x.Respawns =
                                ctx.RespawnInfos.Where(i => i.MapInfoIndex == x.Index).ToList();
                            x.SafeZones =
                                ctx.SafeZoneInfos.Where(i => i.MapInfoIndex == x.Index).ToList();
                            x.SafeZones.ForEach(z => z.Info = x);
                            x.Movements = ctx.MovementInfos.Where(i => i.SourceMapIndex == x.Index).ToList();
                            //x.NPCs = ctx.NpcInfos.Where(i => i.MapIndex == x.Index).ToList();
                        });
                        ItemInfoList = ctx.ItemInfos.ToList();
                        ItemInfoList.ForEach(x =>
                        {
                            if (x.RandomStatsId < Settings.RandomItemStatsList.Count)
                            {
                                x.RandomStats = Settings.RandomItemStatsList[x.RandomStatsId];
                            }

                        });
                        MonsterInfoList = ctx.MonsterInfos.ToList();
                        NPCInfoList = ctx.NpcInfos.ToList();
                        QuestInfoList = ctx.QuestInfos.ToList();
                        QuestInfoList.ForEach(q =>
                        {
                            q.LoadInfo();
                        });
                        DragonInfo = ctx.DragonInfos.FirstOrDefault() ?? new DragonInfo();
                        MagicInfoList = ctx.MagicInfos.ToList();
                        FillMagicInfoList();
                        UpdateMagicInfo();
                        GameShopList.Clear();
                        var gameShopList = ctx.GameShopItems.ToList();
                        foreach (var shopItem in gameShopList)
                        {
                            if (SMain.Envir.BindGameShop(shopItem))
                            {
                                GameShopList.Add(shopItem);
                            }
                        }
                        ConquestInfos = ctx.ConquestInfos.ToList();
                        RespawnTick = new RespawnTimer();
                    }
                    Settings.LinkGuildCreationItems(ItemInfoList);
                    return;
                }
                if (!File.Exists(DatabasePath))
                    SaveDB();

                using (FileStream stream = File.OpenRead(DatabasePath))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    if (LoadVersion > 57)
                        LoadCustomVersion = reader.ReadInt32();
                    MapIndex = reader.ReadInt32();
                    ItemIndex = reader.ReadInt32();
                    MonsterIndex = reader.ReadInt32();

                    if (LoadVersion > 33)
                    {
                        NPCIndex = reader.ReadInt32();
                        QuestIndex = reader.ReadInt32();
                    }
                    if (LoadVersion >= 63)
                    {
                        GameshopIndex = reader.ReadInt32();
                    }

                    if (LoadVersion >= 66)
                    {
                        ConquestIndex = reader.ReadInt32();
                    }

                    if (LoadVersion >= 68)
                        RespawnIndex = reader.ReadInt32();


                    int count = reader.ReadInt32();
                    MapInfoList.Clear();
                    for (int i = 0; i < count; i++)
                        MapInfoList.Add(new MapInfo(reader));

                    count = reader.ReadInt32();
                    ItemInfoList.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        ItemInfoList.Add(new ItemInfo(reader, LoadVersion, LoadCustomVersion));
                        if ((ItemInfoList[i] != null) && (ItemInfoList[i].RandomStatsId < Settings.RandomItemStatsList.Count))
                        {
                            ItemInfoList[i].RandomStats = Settings.RandomItemStatsList[ItemInfoList[i].RandomStatsId];
                        }
                    }
                    count = reader.ReadInt32();
                    MonsterInfoList.Clear();
                    for (int i = 0; i < count; i++)
                        MonsterInfoList.Add(new MonsterInfo(reader));

                    if (LoadVersion > 33)
                    {
                        count = reader.ReadInt32();
                        NPCInfoList.Clear();
                        for (int i = 0; i < count; i++)
                            NPCInfoList.Add(new NPCInfo(reader));

                        count = reader.ReadInt32();
                        QuestInfoList.Clear();
                        for (int i = 0; i < count; i++)
                            QuestInfoList.Add(new QuestInfo(reader));
                    }

                    if (LoadVersion >= 11) DragonInfo = new DragonInfo(reader);
                    else DragonInfo = new DragonInfo();
                    if (LoadVersion >= 58)
                    {
                        count = reader.ReadInt32();
                        for (int i = 0; i < count; i++)
                            MagicInfoList.Add(new MagicInfo(reader, LoadVersion, LoadCustomVersion));
                    }
                    FillMagicInfoList();
                    if (LoadVersion <= 70)
                        UpdateMagicInfo();

                    if (LoadVersion >= 63)
                    {
                        count = reader.ReadInt32();
                        GameShopList.Clear();
                        for (int i = 0; i < count; i++)
                        {
                            GameShopItem item = new GameShopItem(reader, LoadVersion, LoadCustomVersion);
                            if (SMain.Envir.BindGameShop(item))
                            {
                                GameShopList.Add(item);
                            }
                        }
                    }

                    if (LoadVersion >= 66)
                    {
                        ConquestInfos.Clear();
                        count = reader.ReadInt32();
                        for (int i = 0; i < count; i++)
                        {
                            ConquestInfos.Add(new ConquestInfo(reader));
                        }
                    }

                    if (LoadVersion > 67)
                        RespawnTick = new RespawnTimer(reader);
                }

                Settings.LinkGuildCreationItems(ItemInfoList);
            }

        }

        public void LoadAccounts()
        {
            //reset ranking
            for (int i = 0; i < RankClass.Count(); i++)
            {
                if (RankClass[i] != null)
                    RankClass[i].Clear();
                else
                    RankClass[i] = new List<Rank_Character_Info>();
            }
            RankTop.Clear();
            for (int i = 0; i < RankBottomLevel.Count(); i++)
            {
                RankBottomLevel[i] = 0;
            }


            lock (LoadLock)
            {
                if (Settings.UseSQLServer)
                {
                    using (var ctx = new DataContext())
                    {
                        NextAccountID = (ctx.AccountInfos.Max(i => (int?)i.Index) ?? 0);
                        NextCharacterID = (ctx.CharacterInfos.Max(i => (int?)i.Index) ?? 0);
                        NextUserItemID = (ctx.UserItems.Max(i => (long?) i.UniqueID) ?? 0);
                        GuildCount = ctx.Guilds.Count();
                        NextGuildID = (ctx.Guilds.Max(g => (int?) g.Guildindex) ?? 0);
                        AccountList = ctx.AccountInfos.ToList();
                        CharacterList = ctx.CharacterInfos.Include(c => c.AccountInfo).ToList();
                        CharacterList.ForEach(x =>
                        {
                            var Inventoryitems =
                                ctx.Inventories.Include(i => i.UserItem)
                                    .Include(i => i.UserItem.Info)
                                    
                                    .Where(i => i.CharacterIndex == x.Index)
                                    .ToList();
                            Array.Resize(ref x.Inventory, Inventoryitems.Count);
                            for (int i = 0; i < Inventoryitems.Count; i++)
                            {

                                if (Inventoryitems[i].UserItem != null && SMain.Envir.BindItem(Inventoryitems[i].UserItem) && i < x.Inventory.Length)
                                {
                                    x.Inventory[i] = Inventoryitems[i].UserItem;
                                }
                            }
                            var EquipmentItems =
                                ctx.Equipments.Include(e => e.UserItem)
                                    .Include(e => e.UserItem.Info)
                                    
                                    .Where(e => e.CharacterIndex == x.Index)
                                    .ToList();
                            for (int i = 0; i < EquipmentItems.Count; i++)
                            {
                                if (EquipmentItems[i].UserItem != null && SMain.Envir.BindItem(EquipmentItems[i].UserItem) && i < x.Equipment.Length)
                                {
                                    x.Equipment[i] = EquipmentItems[i].UserItem;
                                }
                            }
                            var QuestInventorys =
                                ctx.QuestInventories.Include(i => i.UserItem)
                                    .Include(i => i.UserItem.Info)
                                    
                                    .Where(i => i.CharacterIndex == x.Index)
                                    .ToList();
                            for (int i = 0; i < QuestInventorys.Count; i++)
                            {
                                if (QuestInventorys[i].UserItem != null && SMain.Envir.BindItem(QuestInventorys[i].UserItem) && i < x.QuestInventory.Length)
                                {
                                    x.QuestInventory[i] = QuestInventorys[i].UserItem;
                                }
                            }
                            x.Magics = ctx.UserMagics.Where(m => m.CharacterIndex == x.Index).ToList();
                            x.Magics.ForEach(m =>
                            {
                                m.CastTime = 0;
                            });

                        });
                        AccountList.ForEach(x =>
                        {
                            x.Characters = CharacterList.Where(c => c.AccountInfoIndex == x.Index).ToList();
                            var storageItems =
                                ctx.StorageItems.Where(i => i.AccountIndex == x.Index).Include(i => i.UserItem).ToList();
                            Array.Resize(ref x.Storage, storageItems.Count <= 80 ? 80 : storageItems.Count);
                            int storageIndex = 0;
                            foreach (var item in storageItems)
                            {
                                if (item.UserItemUniqueID != null)
                                {
                                    if (item.UserItem != null && SMain.Envir.BindItem(item.UserItem) &&
                                        storageIndex < x.Storage.Length)
                                    {
                                        x.Storage[storageIndex] = item.UserItem;
                                    }
                                }
                                storageIndex++;
                            }

                        });
                        foreach (AuctionInfo auction in Auctions)
                            auction.CharacterInfo.AccountInfo.Auctions.Remove(auction);
                        Auctions =
                            new LinkedList<AuctionInfo>(
                                ctx.AuctionInfos.Include(a => a.CharacterInfo)
                                    .Include(a => a.CharacterInfo.AccountInfo)
                                    .Include(a => a.Item)
                                    
                                    .ToList());
                        NextAuctionID = (Auctions.Max(a => (long?) a.AuctionID) ?? 0);
                        foreach (var auction in Auctions)
                        {
                            if (!BindItem(auction.Item) || !BindCharacter(auction)) continue;
                            auction.CharacterInfo.AccountInfo.Auctions.AddLast(auction);
                            var accountInfo =
                                AccountList.FirstOrDefault(i => i.Index == auction.CharacterInfo.AccountInfo.Index);
                            if (accountInfo != null) accountInfo.Auctions.AddLast(auction);
                        }
                        NextMailID = (ctx.Mails.Max(m => (long?) m.MailID) ?? 0) + 1;
                        Mail = ctx.Mails.ToList();
                        Mail.ForEach(x =>
                        {
                            if (x.RecipientIndex != 0)
                            {
                                x.RecipientInfo = CharacterList.FirstOrDefault(c => c.Index == x.RecipientIndex);
                            }
                            if (x.CharacterIndex != 0)
                            {
                                x.CharacterInfo = CharacterList.FirstOrDefault(c => c.Index == x.CharacterIndex);
                            }
                            x.Items =
                                ctx.MailItems.Where(item => item.MailID == x.MailID)
                                    .Include(item => item.UserItem)
                                    .Include(item => item.UserItem.Info)
                                    .Select(item => item.UserItem)
                                    .ToList();
                        });
                        var respawnSaves = ctx.RespawnSaves.ToList();
                        foreach (var Saved in respawnSaves)
                        {
                            foreach (MapRespawn Respawn in SavedSpawns)
                            {
                                if (Respawn.Info.RespawnIndex == Saved.RespawnIndex)
                                {
                                    Respawn.NextSpawnTick = Saved.NextSpawnTick;
                                    if ((Saved.Spawned) && ((Respawn.Info.Count * spawnmultiplyer) > Respawn.Count))
                                    {
                                        int mobcount = (Respawn.Info.Count * spawnmultiplyer) - Respawn.Count;
                                        for (int j = 0; j < mobcount; j++)
                                        {
                                            Respawn.Spawn();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    return;
                }
                if (!File.Exists(AccountPath))
                    SaveAccounts();

                using (FileStream stream = File.OpenRead(AccountPath))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    if (LoadVersion > 57) LoadCustomVersion = reader.ReadInt32();
                    NextAccountID = reader.ReadInt32();
                    NextCharacterID = reader.ReadInt32();
                    NextUserItemID = (long) reader.ReadUInt64();

                    if (LoadVersion > 27)
                    {
                        GuildCount = reader.ReadInt32();
                        NextGuildID = reader.ReadInt32();
                    }

                    int count = reader.ReadInt32();
                    AccountList.Clear();
                    CharacterList.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        AccountList.Add(new AccountInfo(reader));
                        CharacterList.AddRange(AccountList[i].Characters);
                    }

                    if (LoadVersion < 7) return;

                    foreach (AuctionInfo auction in Auctions)
                        auction.CharacterInfo.AccountInfo.Auctions.Remove(auction);
                    Auctions.Clear();

                    if (LoadVersion >= 8)
                        NextAuctionID = (long) reader.ReadUInt64();

                    count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        AuctionInfo auction = new AuctionInfo(reader, LoadVersion, LoadCustomVersion);

                        if (!BindItem(auction.Item) || !BindCharacter(auction)) continue;

                        Auctions.AddLast(auction);
                        auction.CharacterInfo.AccountInfo.Auctions.AddLast(auction);
                    }

                    if (LoadVersion == 7)
                    {
                        foreach (AuctionInfo auction in Auctions)
                        {
                            if (auction.Sold && auction.Expired) auction.Expired = false;

                            auction.AuctionID = ++NextAuctionID;
                        }
                    }

                    if(LoadVersion > 43)
                    {
                        NextMailID = (long) reader.ReadUInt64();

                        Mail.Clear();

                        count = reader.ReadInt32();
                        for (int i = 0; i < count; i++)
                        {
                            Mail.Add(new MailInfo(reader, LoadVersion, LoadCustomVersion));
                        }
                    }

                    if(LoadVersion >= 63)
                    {
                        int logCount = reader.ReadInt32();
                        for (int i = 0; i < logCount; i++)
                        {
                            GameshopLog.Add(reader.ReadInt32(), reader.ReadInt32());
                        }

                        if (ResetGS) ClearGameshopLog();
                    }

                    if (LoadVersion >= 68)
                    {
                        int SaveCount = reader.ReadInt32();
                        for (int i = 0; i < SaveCount; i++)
                        {
                            RespawnSave Saved = new RespawnSave(reader);
                            foreach (MapRespawn Respawn in SavedSpawns)
                            {
                                if (Respawn.Info.RespawnIndex == Saved.RespawnIndex)
                                {
                                    Respawn.NextSpawnTick = Saved.NextSpawnTick;
                                    if ((Saved.Spawned) && ((Respawn.Info.Count * spawnmultiplyer) > Respawn.Count))
                                    {
                                        int mobcount = (Respawn.Info.Count * spawnmultiplyer) - Respawn.Count;
                                        for (int j = 0; j < mobcount; j++)
                                        {
                                            Respawn.Spawn();
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
            }
        }

        public void LoadGuilds()
        {
            lock (LoadLock)
            {
                int count = 0;

                GuildList.Clear();
                if (Settings.UseSQLServer)
                {
                    using (var ctx = new DataContext())
                    {
                        var BaseGuildList = ctx.Guilds.ToList();
                        GuildList.Clear();
                        foreach (var baseGuildObject in BaseGuildList)
                        {
                            var guild = new GuildObject();
                            Functions.CopyProperties(guild, baseGuildObject);
                            GuildList.Add(guild);
                        }
                        GuildList.ForEach(g =>
                        {
                            g.StoredItems = ctx.GuildStorageItems.Include(i => i.Item).Include(i => i.Item.Info).Where(i => i.GuildIndex == g.Guildindex).ToArray();
                            for (int i = 0; i < g.StoredItems.Length; i++)
                            {
                                if (g.StoredItems[i].ItemUniqueID == null) g.StoredItems[i] = null;
                            }
                            g.Ranks = ctx.Ranks.Include(r => r.GuildMembers).Where(r => r.GuildIndex == g.Guildindex).ToList();
                            g.Ranks.ForEach(r => r.Members = r.GuildMembers.ToList());
                        });
                    }
                    
                    return;
                }
                for (int i = 0; i < GuildCount; i++)
                {
                    GuildObject newGuild;
                    if (File.Exists(Settings.GuildPath + i.ToString() + ".mgd"))
                    {
                        using (FileStream stream = File.OpenRead(Settings.GuildPath + i.ToString() + ".mgd"))
                        using (BinaryReader reader = new BinaryReader(stream))
                            newGuild = new GuildObject(reader);
    
                        //if (!newGuild.Ranks.Any(a => (byte)a.Options == 255)) continue;
                        //if (GuildList.Any(e => e.Name == newGuild.Name)) continue;
                        GuildList.Add(newGuild);

                        count++;
                    }
                }

                if (count != GuildCount) GuildCount = count;
            }
        }

        public void LoadFishingDrops()
        {
            FishingDrops.Clear();
            
            for (byte i = 0; i <= 19; i++)
            {
                string path = Path.Combine(Settings.DropPath, Settings.FishingDropFilename + ".txt");

                path = path.Replace("00", i.ToString("D2"));

                if (!File.Exists(path) && i < 2)
                {
                    FileStream newfile = File.Create(path);
                    newfile.Close();
                }

                if (!File.Exists(path)) continue;

                string[] lines = File.ReadAllLines(path);

                for (int j = 0; j < lines.Length; j++)
                {
                    if (lines[j].StartsWith(";") || string.IsNullOrWhiteSpace(lines[j])) continue;

                    DropInfo drop = DropInfo.FromLine(lines[j]);
                    if (drop == null)
                    {
                        SMain.Enqueue(string.Format("不能加载钓鱼掉率: {0}", lines[j]));
                        continue;
                    }

                    drop.Type = i;

                    FishingDrops.Add(drop);
                }

                FishingDrops.Sort((drop1, drop2) =>
                {
                    if (drop1.Chance > 0 && drop2.Chance == 0)
                        return 1;
                    if (drop1.Chance == 0 && drop2.Chance > 0)
                        return -1;

                    return drop1.Item.Type.CompareTo(drop2.Item.Type);
                });
            }  
        }

        public void LoadAwakeningMaterials()
        {
            AwakeningDrops.Clear();

            string path = Path.Combine(Settings.DropPath, Settings.AwakeningDropFilename + ".txt");

            if (!File.Exists(path))
            {
                FileStream newfile = File.Create(path);
                newfile.Close();

            }

            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;

                DropInfo drop = DropInfo.FromLine(lines[i]);
                if (drop == null)
                {
                    SMain.Enqueue(string.Format("不能加载觉醒掉率: {0}", lines[i]));
                    continue;
                }

                AwakeningDrops.Add(drop);
            }

            AwakeningDrops.Sort((drop1, drop2) =>
            {
                if (drop1.Chance > 0 && drop2.Chance == 0)
                    return 1;
                if (drop1.Chance == 0 && drop2.Chance > 0)
                    return -1;

                return drop1.Item.Type.CompareTo(drop2.Item.Type);
            });
        }

        public void LoadLotteryTicketDrops()
        {
            LotteryTicketDrops.Clear();
            
            string path = Path.Combine(Settings.DropPath, Settings.LotteryTicketDropFilename + ".txt");

            if (!File.Exists(path))
            {
                FileStream newfile = File.Create(path);
                newfile.Close();
            }

            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;

                DropInfo drop = DropInfo.FromLine(lines[i]);
                if (drop == null)
                {
                    SMain.Enqueue(string.Format("不能加载彩票掉率: {0}", lines[i]));
                    continue;
                }

                LotteryTicketDrops.Add(drop);
            }

            LotteryTicketDrops.Sort((drop1, drop2) =>
            {
                if (drop1.Gold > 0 && drop2.Gold == 0)
                    return 1;
                if (drop1.Gold == 0 && drop2.Gold > 0)
                    return -1;

                return drop1.Item.Type.CompareTo(drop2.Item.Type);
            });
        }
        public void LoadStrongBoxDrops()
        {
            StrongboxDrops.Clear();

            string path = Path.Combine(Settings.DropPath, Settings.StrongboxDropFilename + ".txt");

            if (!File.Exists(path))
            {
                FileStream newfile = File.Create(path);
                newfile.Close();
            }

            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;

                DropInfo drop = DropInfo.FromLine(lines[i]);
                if (drop == null)
                {
                    SMain.Enqueue(string.Format("Could not load strongbox drop: {0}", lines[i]));
                    continue;
                }

                StrongboxDrops.Add(drop);
            }

            StrongboxDrops.Sort((drop1, drop2) =>
            {
                if (drop1.Chance > 0 && drop2.Chance == 0)
                    return 1;
                if (drop1.Chance == 0 && drop2.Chance > 0)
                    return -1;

                return drop1.Item.Type.CompareTo(drop2.Item.Type);
            });
        }

        public void LoadBlackStoneDrops()
        {
            BlackstoneDrops.Clear();

            string path = Path.Combine(Settings.DropPath, Settings.BlackstoneDropFilename + ".txt");

            if (!File.Exists(path))
            {
                FileStream newfile = File.Create(path);
                newfile.Close();

            }

            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;

                DropInfo drop = DropInfo.FromLine(lines[i]);
                if (drop == null)
                {
                    SMain.Enqueue(string.Format("Could not load blackstone drop: {0}", lines[i]));
                    continue;
                }

                BlackstoneDrops.Add(drop);
            }

            BlackstoneDrops.Sort((drop1, drop2) =>
            {
                if (drop1.Chance > 0 && drop2.Chance == 0)
                    return 1;
                if (drop1.Chance == 0 && drop2.Chance > 0)
                    return -1;

                return drop1.Item.Type.CompareTo(drop2.Item.Type);
            });
        }

        public void LoadConquests()
        {
            lock (LoadLock)
            {
                int count = 0;

                Conquests.Clear();

                ConquestObject newConquest;
                Map tempMap;
                ConquestArcherObject tempArcher;
                ConquestGateObject tempGate;
                ConquestWallObject tempWall;
                ConquestSiegeObject tempSiege;
                ConquestFlagObject tempFlag;

                for (int i = 0; i < ConquestInfos.Count; i++)
                {
                    newConquest = null;
                    tempMap = GetMap(ConquestInfos[i].MapIndex);

                    if (tempMap == null) continue;

                    if (File.Exists(Settings.ConquestsPath + ConquestInfos[i].Index.ToString() + ".mcd"))
                    {
                        using (FileStream stream = File.OpenRead(Settings.ConquestsPath + ConquestInfos[i].Index.ToString() + ".mcd"))
                        using (BinaryReader reader = new BinaryReader(stream))
                            newConquest = new ConquestObject(reader) { Info = ConquestInfos[i], ConquestMap = tempMap };

                        for (int k = 0; k < GuildList.Count; k++)
                        {
                            if (newConquest.Owner == GuildList[k].Guildindex)
                            {
                                newConquest.Guild = GuildList[k];
                                GuildList[k].Conquest = newConquest;
                            }
                        }

                        Conquests.Add(newConquest);
                        tempMap.Conquest.Add(newConquest);
                        count++;
                    }
                    else
                    {
                        newConquest = new ConquestObject { Info = ConquestInfos[i], NeedSave = true, ConquestMap = tempMap };

                        Conquests.Add(newConquest);
                        tempMap.Conquest.Add(newConquest);
                    }

                    //Bind Info to Saved Archer objects or create new objects
                    for (int j = 0; j < ConquestInfos[i].ConquestGuards.Count; j++)
                    {
                        tempArcher = newConquest.ArcherList.FirstOrDefault(x => x.Index == ConquestInfos[i].ConquestGuards[j].Index);

                        if (tempArcher != null)
                        {
                            tempArcher.Info = ConquestInfos[i].ConquestGuards[j];
                            tempArcher.Conquest = newConquest;
                        }
                        else
                        {
                            newConquest.ArcherList.Add(new ConquestArcherObject { Info = ConquestInfos[i].ConquestGuards[j], Alive = true, Index = ConquestInfos[i].ConquestGuards[j].Index, Conquest = newConquest });
                        }
                    }

                    //Remove archers that have been removed from DB
                    for (int j = 0; j < newConquest.ArcherList.Count; j++)
                    {
                        if (newConquest.ArcherList[j].Info == null)
                            newConquest.ArcherList.Remove(newConquest.ArcherList[j]);
                    }

                    //Bind Info to Saved Gate objects or create new objects
                    for (int j = 0; j < ConquestInfos[i].ConquestGates.Count; j++)
                    {
                        tempGate = newConquest.GateList.FirstOrDefault(x => x.Index == ConquestInfos[i].ConquestGates[j].Index);

                        if (tempGate != null)
                        {
                            tempGate.Info = ConquestInfos[i].ConquestGates[j];
                            tempGate.Conquest = newConquest;
                        }
                        else
                        {
                            newConquest.GateList.Add(new ConquestGateObject { Info = ConquestInfos[i].ConquestGates[j], Health = uint.MaxValue, Index = ConquestInfos[i].ConquestGates[j].Index, Conquest = newConquest });
                        }
                    }

                    //Bind Info to Saved Flag objects or create new objects
                    for (int j = 0; j < ConquestInfos[i].ConquestFlags.Count; j++)
                    {
                        newConquest.FlagList.Add(new ConquestFlagObject { Info = ConquestInfos[i].ConquestFlags[j], Index = ConquestInfos[i].ConquestFlags[j].Index, Conquest = newConquest });
                    }

                    //Remove Gates that have been removed from DB
                    for (int j = 0; j < newConquest.GateList.Count; j++)
                    {
                        if (newConquest.GateList[j].Info == null)
                            newConquest.GateList.Remove(newConquest.GateList[j]);
                    }

                    //Bind Info to Saved Wall objects or create new objects
                    for (int j = 0; j < ConquestInfos[i].ConquestWalls.Count; j++)
                    {
                        tempWall = newConquest.WallList.FirstOrDefault(x => x.Index == ConquestInfos[i].ConquestWalls[j].Index);

                        if (tempWall != null)
                        {
                            tempWall.Info = ConquestInfos[i].ConquestWalls[j];
                            tempWall.Conquest = newConquest;
                        }
                        else
                        {
                            newConquest.WallList.Add(new ConquestWallObject { Info = ConquestInfos[i].ConquestWalls[j], Index = ConquestInfos[i].ConquestWalls[j].Index, Health = uint.MaxValue, Conquest = newConquest });
                        }
                    }

                    //Remove Walls that have been removed from DB
                    for (int j = 0; j < newConquest.WallList.Count; j++)
                    {
                        if (newConquest.WallList[j].Info == null)
                            newConquest.WallList.Remove(newConquest.WallList[j]);
                    }

                    
                    //Bind Info to Saved Siege objects or create new objects
                    for (int j = 0; j < ConquestInfos[i].ConquestSieges.Count; j++)
                    {
                        tempSiege = newConquest.SiegeList.FirstOrDefault(x => x.Index == ConquestInfos[i].ConquestSieges[j].Index);

                        if (tempSiege != null)
                        {
                            tempSiege.Info = ConquestInfos[i].ConquestSieges[j];
                            tempSiege.Conquest = newConquest;
                        }
                        else
                        {
                            newConquest.SiegeList.Add(new ConquestSiegeObject { Info = ConquestInfos[i].ConquestSieges[j], Index = ConquestInfos[i].ConquestSieges[j].Index, Health = uint.MaxValue, Conquest = newConquest });
                        }
                    }

                    //Remove Siege that have been removed from DB
                    for (int j = 0; j < newConquest.SiegeList.Count; j++)
                    {
                        if (newConquest.SiegeList[j].Info == null)
                            newConquest.SiegeList.Remove(newConquest.SiegeList[j]);
                    }

                    //Bind Info to Saved Flag objects or create new objects
                    for (int j = 0; j < ConquestInfos[i].ControlPoints.Count; j++)
                    {
                        ConquestFlagObject cp = null;
                        newConquest.ControlPoints.Add(cp = new ConquestFlagObject { Info = ConquestInfos[i].ControlPoints[j], Index = ConquestInfos[i].ControlPoints[j].Index, Conquest = newConquest }, new Dictionary<GuildObject, int>());

                        cp.Spawn();
                    }


                    newConquest.LoadArchers();
                    newConquest.LoadGates();
                    newConquest.LoadWalls();
                    newConquest.LoadSieges();
                    newConquest.LoadFlags();
                    newConquest.LoadNPCs();
                }
            }
        }

        private bool BindCharacter(AuctionInfo auction)
        {
            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (CharacterList[i].Index != auction.CharacterIndex) continue;

                auction.CharacterInfo = CharacterList[i];
                return true;
            }
            return false;

        }

        public void Start()
        {
            if (Running || _thread != null) return;

            Running = true;

            _thread = new Thread(WorkLoop) {IsBackground = true};
            _thread.Start();

        }
        public void Stop()
        {
            Running = false;

            lock (_locker)
            {
                Monitor.PulseAll(_locker);         // changing a blocking condition. (this makes the threads wake up!)
            }

            //simply intterupt all the mob threads if they are running (will give an invisible error on them but fastest way of getting rid of them on shutdowns)
            for (int i = 1; i < MobThreading.Length; i++)
            {
                if (MobThreads[i] != null)
                    MobThreads[i].EndTime = Time + 9999;
                if ((MobThreading[i] != null) &&
                    (MobThreading[i].ThreadState != System.Threading.ThreadState.Stopped) && (MobThreading[i].ThreadState != System.Threading.ThreadState.Unstarted))
                {
                    MobThreading[i].Interrupt();
                }
            }


                while (_thread != null)
                    Thread.Sleep(1);
        }

        public void Reboot()
        {
            (new Thread(() =>
            {
                SMain.Enqueue("服务器重启中...");
                Stop();
                Start();
            })).Start();
        }
        
        private void StartEnvir()
        {
            Players.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            MapList.Clear();
            GameshopLog.Clear();
            CustomCommands.Clear();
            MonsterCount = 0;

            LoadDB();

            for (int i = 0; i < MapInfoList.Count; i++)
                MapInfoList[i].CreateMap();
            SMain.Enqueue(string.Format("{0} 个地图被加载.", MapInfoList.Count));

            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                if (ItemInfoList[i].StartItem)
                    StartItems.Add(ItemInfoList[i]);
            }

            for (int i = 0; i < MonsterInfoList.Count; i++)
                MonsterInfoList[i].LoadDrops();

            LoadFishingDrops();
            LoadAwakeningMaterials();
            LoadLotteryTicketDrops();
            LoadStrongBoxDrops();
            LoadBlackStoneDrops();
            SMain.Enqueue("已经加载掉率.");

            if (DragonInfo.Enabled)
            {
                DragonSystem = new Dragon(DragonInfo);
                if (DragonSystem != null)
                {
                    if (DragonSystem.Load()) DragonSystem.Info.LoadDrops();
                }

                SMain.Enqueue("Dragon Loaded.");
            }

            DefaultNPC = new NPCObject(new NPCInfo() { Name = "DefaultNPC", FileName = Settings.DefaultNPCFilename, IsDefault = true });
            MonsterNPC = new NPCObject(new NPCInfo() { Name = "MonsterNPC", FileName = Settings.MonsterNPCFilename, IsDefault = true });
            RobotNPC = new NPCObject(new NPCInfo() { Name = "RobotNPC", FileName = Settings.RobotNPCFilename, IsDefault = true, IsRobot = true });

            SMain.Enqueue("Envir Started.");
        }
        private void StartNetwork()
        {
            Connections.Clear();

            LoadAccounts();

            LoadGuilds();

            LoadConquests();

            _listener = new TcpListener(IPAddress.Parse(Settings.IPAddress), Settings.Port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(Connection, null);

            if (StatusPortEnabled)
            {
                _StatusPort = new TcpListener(IPAddress.Parse(Settings.IPAddress), 3000);
                _StatusPort.Start();
                _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
            }
            SMain.Enqueue("网络已经启动.");

            //FixGuilds();
        }

        private void StopEnvir()
        {
            SaveGoods(true);

            MapList.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            Objects.Clear();
            Players.Clear();

            CleanUp();

            GC.Collect();

            SMain.Enqueue("Envir Stopped.");
        }
        private void StopNetwork()
        {
            _listener.Stop();
            lock (Connections)
            {
                for (int i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].SendDisconnect(0);
            }

            if (StatusPortEnabled)
            {
                _StatusPort.Stop();
                for (int i = StatusConnections.Count - 1; i >= 0; i--)
                    StatusConnections[i].SendDisconnect();
            }

            long expire = Time + 5000;

            while (Connections.Count != 0 && Stopwatch.ElapsedMilliseconds < expire)
            {
                Time = Stopwatch.ElapsedMilliseconds;

                for (int i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].Process();

                Thread.Sleep(1);
            }
            

            Connections.Clear();

            expire = Time + 10000;
            while (StatusConnections.Count != 0 && Stopwatch.ElapsedMilliseconds < expire)
            {
                Time = Stopwatch.ElapsedMilliseconds;

                for (int i = StatusConnections.Count - 1; i >= 0; i--)
                    StatusConnections[i].Process();

                Thread.Sleep(1);
            }


            StatusConnections.Clear();
            SMain.Enqueue("网络已停止.");
        }

        private void CleanUp()
        {
            for (int i = 0; i < CharacterList.Count; i++)
            {
                CharacterInfo info = CharacterList[i];

                if (info.Deleted)
                {
                    #region Mentor Cleanup
                    if (info.Mentor > 0)
                    {
                        CharacterInfo Mentor = GetCharacterInfo(info.Mentor);

                        if (Mentor != null)
                        {
                            Mentor.Mentor = 0;
                            Mentor.MentorExp = 0;
                            Mentor.isMentor = false;
                        }

                        info.Mentor = 0;
                        info.MentorExp = 0;
                        info.isMentor = false;
                    }
                    #endregion

                    #region Marriage Cleanup
                    if (info.Married > 0)
                    {
                        CharacterInfo Lover = GetCharacterInfo(info.Married);

                        info.Married = 0;
                        info.MarriedDate = DateTime.Now;

                        Lover.Married = 0;
                        Lover.MarriedDate = DateTime.Now;
                        if (Lover.Equipment[(int)EquipmentSlot.RingL] != null)
                            Lover.Equipment[(int)EquipmentSlot.RingL].WeddingRing = -1;
                    }
                    #endregion

                    if (info.DeleteDate < DateTime.Now.AddDays(-7))
                    {
                        //delete char from db
                    }
                }

                if(info.Mail.Count > Settings.MailCapacity)
                {
                    for (int j = (info.Mail.Count - 1 - (int)Settings.MailCapacity); j >= 0; j--)
                    {
                        if (info.Mail[j].DateOpened > DateTime.Now && info.Mail[j].Collected && info.Mail[j].Items.Count == 0 && info.Mail[j].Gold == 0)
                        {
                            info.Mail.Remove(info.Mail[j]);
            }
        }
                }
            }
        }

        private void Connection(IAsyncResult result)
        {
            if (!Running || !_listener.Server.IsBound) return;

            try
            {
                TcpClient tempTcpClient = _listener.EndAcceptTcpClient(result);
                lock (Connections)
                    Connections.Add(new MirConnection(++_sessionID, tempTcpClient));
            }
            catch (Exception ex)
            {
                SMain.Enqueue(ex);
            }
            finally
            {
                while (Connections.Count >= Settings.MaxUser)
                    Thread.Sleep(1);

                if (Running && _listener.Server.IsBound)
                    _listener.BeginAcceptTcpClient(Connection, null);
            }
        }

        private void StatusConnection(IAsyncResult result)
        {
            if (!Running || !_StatusPort.Server.IsBound) return;

            try
            {
                TcpClient tempTcpClient = _StatusPort.EndAcceptTcpClient(result);
                lock (StatusConnections)
                    StatusConnections.Add(new MirStatusConnection(tempTcpClient));
            }
            catch (Exception ex)
            {
                SMain.Enqueue(ex);
            }
            finally
            {
                while (StatusConnections.Count >= 5) //dont allow to many status port connections it's just an abuse thing
                    Thread.Sleep(1);

                if (Running && _StatusPort.Server.IsBound)
                    _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
            }
        }
     
        public void NewAccount(ClientPackets.NewAccount p, MirConnection c)
        {
            if (!Settings.AllowNewAccount)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 0});
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 1});
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 2});
                return;
            }
            if (!string.IsNullOrWhiteSpace(p.EMailAddress) && !EMailReg.IsMatch(p.EMailAddress) ||
                p.EMailAddress.Length > 50)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 3});
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.UserName) && p.UserName.Length > 20)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 4});
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretQuestion) && p.SecretQuestion.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 5});
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretAnswer) && p.SecretAnswer.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 6});
                return;
            }

            lock (AccountLock)
            {
                if (AccountExists(p.AccountID))
                {
                    c.Enqueue(new ServerPackets.NewAccount {Result = 7});
                    return;
                }
                if (Settings.UseSQLServer)
                {
                    using (var ctx = new DataContext())
                    {
                        var account = new AccountInfo(p) {CreationIP = c.IPAddress};
                        ctx.AccountInfos.Add(account);
                        ctx.SaveChanges();
                        AccountList.Add(account);
                        for (int i = 0; i < account.Storage.Length; i++)
                        {
                            ctx.StorageItems.Add(new StorageItem()
                            {
                                AccountIndex = account.Index,
                                UserItemUniqueID = null
                            });
                        }
                        ctx.SaveChanges();
                    }
                }
                else
                {
                    AccountList.Add(new AccountInfo(p) {Index = ++NextAccountID, CreationIP = c.IPAddress});
                }

                c.Enqueue(new ServerPackets.NewAccount {Result = 8});
            }
        }
        public void ChangePassword(ClientPackets.ChangePassword p, MirConnection c)
        {
            if (!Settings.AllowChangePassword)
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 0});
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 1});
                return;
            }

            if (!PasswordReg.IsMatch(p.CurrentPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 2});
                return;
            }

            if (!PasswordReg.IsMatch(p.NewPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 3});
                return;
            }

            AccountInfo account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 4});
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    c.Enqueue(new ServerPackets.ChangePasswordBanned {Reason = account.BanReason, ExpiryDate = account.ExpiryDate.GetValueOrDefault() });
                    return;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;

            if (String.CompareOrdinal(account.Password, p.CurrentPassword) != 0)
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 5});
                return;
            }

            account.Password = p.NewPassword;
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.AccountInfos.Attach(account);
                    ctx.Entry(account).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }
            c.Enqueue(new ServerPackets.ChangePassword {Result = 6});
        }
        public void Login(ClientPackets.Login p, MirConnection c)
        {
            if (!Settings.AllowLogin)
            {
                c.Enqueue(new ServerPackets.Login { Result = 0 });
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.Login { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.Login { Result = 2 });
                return;
            }
            AccountInfo account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.Login { Result = 3 });
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > DateTime.Now)
                {
                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate.GetValueOrDefault()
                    });
                    return;
                }
                account.Banned = false;
            }
                account.BanReason = string.Empty;
                account.ExpiryDate = DateTime.MinValue;


            if (String.CompareOrdinal(account.Password, p.Password) != 0)
            {
                if (account.WrongPasswordCount++ >= 5)
                {
                    account.Banned = true;
                    account.BanReason = "错误次数太多.";
                    account.ExpiryDate = DateTime.Now.AddMinutes(2);

                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate.GetValueOrDefault()
                    });
                    return;
                }

                c.Enqueue(new ServerPackets.Login { Result = 4 });
                return;
            }
            account.WrongPasswordCount = 0;

            lock (AccountLock)
            {
                if (account.Connection != null)
                    account.Connection.SendDisconnect(1);

                account.Connection = c;
            }

            c.Account = account;
            c.Stage = GameStage.Select;

            account.LastDate = Now;
            account.LastIP = c.IPAddress;
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.AccountInfos.Attach(account);
                    ctx.Entry(account).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }
            SMain.Enqueue(account.Connection.SessionID + ", " + account.Connection.IPAddress + ", User logged in.");
            c.Enqueue(new ServerPackets.LoginSuccess { Characters = account.GetSelectInfo() });
        }
        public void NewCharacter(ClientPackets.NewCharacter p, MirConnection c, bool IsGm)
        {
            if (!Settings.AllowNewCharacter)
            {
                c.Enqueue(new ServerPackets.NewCharacter {Result = 0});
                return;
            }

            if (!CharacterReg.IsMatch(p.Name))
            {
                c.Enqueue(new ServerPackets.NewCharacter {Result = 1});
                return;
            }

            if ((!IsGm) && (DisabledCharNames.Contains(p.Name.ToUpper())))
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 1 });
                return;
            }

            if (p.Gender != MirGender.Male && p.Gender != MirGender.Female)
            {
                c.Enqueue(new ServerPackets.NewCharacter {Result = 2});
                return;
            }

            if (p.Class != MirClass.Warrior && p.Class != MirClass.Wizard && p.Class != MirClass.Taoist &&
                p.Class != MirClass.Assassin && p.Class != MirClass.Archer)
            {
                c.Enqueue(new ServerPackets.NewCharacter {Result = 3});
                return;
            }

            if((p.Class == MirClass.Assassin && !Settings.AllowCreateAssassin) ||
                (p.Class == MirClass.Archer && !Settings.AllowCreateArcher))
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 3 });
                return;
            }

            int count = 0;

            for (int i = 0; i < c.Account.Characters.Count; i++)
            {
                if (c.Account.Characters[i].Deleted) continue;

                if (++count >= Globals.MaxCharacterCount)
                {
                    c.Enqueue(new ServerPackets.NewCharacter {Result = 4});
                    return;
                }
            }

            lock (AccountLock)
            {
                if (CharacterExists(p.Name))
                {
                    c.Enqueue(new ServerPackets.NewCharacter {Result = 5});
                    return;
                }

                CharacterInfo info = new CharacterInfo(p, c) { Index = ++NextCharacterID, AccountInfo = c.Account };
                var whiteSpacePattern = new Regex(@"\s+");
                info.Name = whiteSpacePattern.Replace(info.Name, "");
                if (Settings.UseSQLServer)
                {
                    using (var ctx = new DataContext())
                    {
                        info.Index = 0;
                        info.AccountInfoIndex = info.AccountInfo.Index;
                        //info.AccountInfo = null;
                        ctx.CharacterInfos.Attach(info);
                        ctx.Entry(info).State = EntityState.Added;
                        ctx.SaveChanges();
                        //info.AccountInfo = c.Account;
                        for (int i = 0; i < info.Inventory.Length; i++)
                        {
                            ctx.Inventories.Add(new InventoryItem()
                            {
                                CharacterIndex = info.Index,
                                ItemUniqueID = null
                            });
                        }
                        for (int i = 0; i < info.QuestInventory.Length; i++)
                        {
                            ctx.QuestInventories.Add(new QuestInventoryItem()
                            {
                                CharacterIndex = info.Index,
                                ItemUniqueID = null
                            });
                        }
                        for (int i = 0; i < info.Equipment.Length; i++)
                        {
                            ctx.Equipments.Add(new EquipmentItem()
                            {
                                CharacterIndex = info.Index,
                                ItemUniqueID = null
                            });
                        }
                        ctx.SaveChanges();
                    }
                }
                c.Account.Characters.Add(info);
                CharacterList.Add(info);

                c.Enqueue(new ServerPackets.NewCharacterSuccess {CharInfo = info.ToSelectInfo()});
            }
        }

        public bool AccountExists(string accountID)
        {
                for (int i = 0; i < AccountList.Count; i++)
                    if (String.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                        return true;

                return false;
        }
        public bool CharacterExists(string name)
        {
            for (int i = 0; i < CharacterList.Count; i++)
                if (String.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        private AccountInfo GetAccount(string accountID)
        {
                for (int i = 0; i < AccountList.Count; i++)
                    if (String.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                        return AccountList[i];

                return null;
        }
        public List<AccountInfo> MatchAccounts(string accountID, bool match = false)
        {
            if (string.IsNullOrEmpty(accountID)) return new List<AccountInfo>(AccountList);

            List<AccountInfo> list = new List<AccountInfo>();

            for (int i = 0; i < AccountList.Count; i++)
            {
                if (match)
                {
                    if (AccountList[i].AccountID.Equals(accountID, StringComparison.OrdinalIgnoreCase))
                        list.Add(AccountList[i]);
                }
                else
                {
                    if (AccountList[i].AccountID.IndexOf(accountID, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(AccountList[i]);
                }
            }

            return list;
        }

        public List<AccountInfo> MatchAccountsByPlayer(string playerName, bool match = false)
        {
            if (string.IsNullOrEmpty(playerName)) return new List<AccountInfo>(AccountList);

            List<AccountInfo> list = new List<AccountInfo>();

            for (int i = 0; i < AccountList.Count; i++)
            {
                for (int j = 0; j < AccountList[i].Characters.Count; j++)
                {
                    if (match)
                    {
                        if (AccountList[i].Characters[j].Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                            list.Add(AccountList[i]);
                    }
                    else
                    {
                        if (AccountList[i].Characters[j].Name.IndexOf(playerName, StringComparison.OrdinalIgnoreCase) >= 0)
                            list.Add(AccountList[i]);
                    }
                }
            }

            return list;
        }

        public void CreateAccountInfo()
        {
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    var newAccount = new AccountInfo();
                    ctx.AccountInfos.Add(newAccount);
                    ctx.SaveChanges();
                    AccountList.Add(newAccount);
                }
            }
            else
            {
                AccountList.Add(new AccountInfo { Index = ++NextAccountID });
            }
        }
        public void CreateMapInfo()
        {
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    var newMap = new MapInfo();
                    ctx.MapInfos.Add(newMap);
                    ctx.SaveChanges();
                    MapInfoList.Add(newMap);
                }
            }
            else
            {
                MapInfoList.Add(new MapInfo {Index = ++MapIndex});
            }
        }
        public void CreateItemInfo(ItemType type = ItemType.无)
        {
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    var newItem = new ItemInfo() {Type = type, RandomStatsId = 255};
                    ctx.ItemInfos.Add(newItem);
                    ctx.SaveChanges();
                    ItemInfoList.Add(newItem);
                }
            }
            else
            {
                ItemInfoList.Add(new ItemInfo { Index = ++ItemIndex, Type = type, RandomStatsId = 255 });
            }
        }
        public void CreateMonsterInfo()
        {
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    var newMonster = new MonsterInfo();
                    ctx.MonsterInfos.Add(newMonster);
                    ctx.SaveChanges();
                    MonsterInfoList.Add(newMonster);
                }
            }
            else
            {
                MonsterInfoList.Add(new MonsterInfo {Index = ++MonsterIndex});
            }
        }
        public void CreateNPCInfo()
        {
            NPCInfoList.Add(new NPCInfo { Index = ++NPCIndex });
        }
        public void CreateQuestInfo()
        {
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    var newQuest = new QuestInfo();
                    ctx.QuestInfos.Add(newQuest);
                    ctx.SaveChanges();
                    QuestInfoList.Add(newQuest);
                }
            }
            else
            {
                QuestInfoList.Add(new QuestInfo { Index = ++QuestIndex });
            }
        }

        public void AddToGameShop(ItemInfo Info)
        {
            GameShopList.Add(new GameShopItem { GIndex = ++GameshopIndex, GoldPrice = (uint)(1000 * Settings.CredxGold), CreditPrice = 1000, ItemIndex = Info.Index, Info = Info, Date = DateTime.Now, Class = "All", Category = Info.Type.ToString() });
        }

        public void Remove(MapInfo info)
        {
            MapInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(ItemInfo info)
        {
            ItemInfoList.Remove(info);
        }
        public void Remove(MonsterInfo info)
        {
            MonsterInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(NPCInfo info)
        {
            NPCInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(QuestInfo info)
        {
            QuestInfoList.Remove(info);
            //Desync all objects\
        }

        public void Remove(GameShopItem info)
        {
            GameShopList.Remove(info);

            if (GameShopList.Count == 0)
            {
                GameshopIndex = 0;
            }
                
            //Desync all objects\
        }

        public UserItem CreateFreshItem(ItemInfo info)
        {
            UserItem item = new UserItem(info)
                {
                    CurrentDura = info.Durability,
                    MaxDura = info.Durability
                };
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.UserItems.Attach(item);
                    ctx.Entry(item).State = EntityState.Added;
                    ctx.SaveChanges();
                }
            }
            else
            {
                item.UniqueID = ++NextUserItemID;
            }
            UpdateItemExpiry(item);

            return item;
        }
        public UserItem CreateDropItem(int index)
        {
            return CreateDropItem(GetItemInfo(index));
        }
        public UserItem CreateDropItem(ItemInfo info)
        {
            if (info == null) return null;

            UserItem item = new UserItem(info)
                {
                    UniqueID = ++NextUserItemID,
                    MaxDura = info.Durability,
                    CurrentDura = (ushort) Math.Min(info.Durability, Random.Next(info.Durability) + 1000)
                };

            UpgradeItem(item);

            UpdateItemExpiry(item);

            if (!info.NeedIdentify) item.Identified = true;
            return item;
        }

        public void UpdateItemExpiry(UserItem item)
        {
            //can't have expiry on usable items
            if (item.Info.Type == ItemType.卷轴 || item.Info.Type == ItemType.药剂 || 
                item.Info.Type == ItemType.手稿 || item.Info.Type == ItemType.变换) return;

            ExpireInfo expiryInfo = new ExpireInfo();

            Regex r = new Regex(@"\[(.*?)\]");
            Match expiryMatch = r.Match(item.Info.Name);

            if (expiryMatch.Success)
            {
                string parameter = expiryMatch.Groups[1].Captures[0].Value;

                var numAlpha = new Regex("(?<Numeric>[0-9]*)(?<Alpha>[a-zA-Z]*)");
                var match = numAlpha.Match(parameter);

                string alpha = match.Groups["Alpha"].Value;
                int num = 0;

                int.TryParse(match.Groups["Numeric"].Value, out num);

                switch (alpha)
                {
                    case "m":
                        expiryInfo.ExpiryDate = DateTime.Now.AddMinutes(num);
                        break;
                    case "h":
                        expiryInfo.ExpiryDate = DateTime.Now.AddHours(num);
                        break;
                    case "d":
                        expiryInfo.ExpiryDate = DateTime.Now.AddDays(num);
                        break;
                    case "M":
                        expiryInfo.ExpiryDate = DateTime.Now.AddMonths(num);
                        break;
                    case "y":
                        expiryInfo.ExpiryDate = DateTime.Now.AddYears(num);
                        break;
                    default:
                        expiryInfo.ExpiryDate = DateTime.MaxValue;
                        break;
                }

                item.ExpireInfo = expiryInfo;
            }
        }

        public void UpgradeItem(UserItem item)
        {
            if (item.Info.RandomStats == null) return;
            RandomItemStat stat = item.Info.RandomStats;
            if ((stat.MaxDuraChance > 0) && (Random.Next(stat.MaxDuraChance) == 0))
            {
                int dura = RandomomRange(stat.MaxDuraMaxStat, stat.MaxDuraStatChance);
                item.MaxDura = (ushort)Math.Min(ushort.MaxValue, item.MaxDura + dura * 1000);
                item.CurrentDura = (ushort)Math.Min(ushort.MaxValue, item.CurrentDura + dura * 1000);
            }

            if ((stat.MaxAcChance > 0) && (Random.Next(stat.MaxAcChance) == 0)) item.AC = (byte)(RandomomRange(stat.MaxAcMaxStat-1, stat.MaxAcStatChance)+1);
            if ((stat.MaxMacChance > 0) && (Random.Next(stat.MaxMacChance) == 0)) item.MAC = (byte)(RandomomRange(stat.MaxMacMaxStat-1, stat.MaxMacStatChance)+1);
            if ((stat.MaxDcChance > 0) && (Random.Next(stat.MaxDcChance) == 0)) item.DC = (byte)(RandomomRange(stat.MaxDcMaxStat-1, stat.MaxDcStatChance)+1);
            if ((stat.MaxMcChance > 0) && (Random.Next(stat.MaxScChance) == 0)) item.MC = (byte)(RandomomRange(stat.MaxMcMaxStat-1, stat.MaxMcStatChance)+1);
            if ((stat.MaxScChance > 0) && (Random.Next(stat.MaxMcChance) == 0)) item.SC = (byte)(RandomomRange(stat.MaxScMaxStat-1, stat.MaxScStatChance)+1);
            if ((stat.AccuracyChance > 0) && (Random.Next(stat.AccuracyChance) == 0)) item.Accuracy = (byte)(RandomomRange(stat.AccuracyMaxStat-1, stat.AccuracyStatChance)+1);
            if ((stat.AgilityChance > 0) && (Random.Next(stat.AgilityChance) == 0)) item.Agility = (byte)(RandomomRange(stat.AgilityMaxStat-1, stat.AgilityStatChance)+1);
            if ((stat.HpChance > 0) && (Random.Next(stat.HpChance) == 0)) item.HP = (byte)(RandomomRange(stat.HpMaxStat-1, stat.HpStatChance)+1);
            if ((stat.MpChance > 0) && (Random.Next(stat.MpChance) == 0)) item.MP = (byte)(RandomomRange(stat.MpMaxStat-1, stat.MpStatChance)+1);
            if ((stat.StrongChance > 0) && (Random.Next(stat.StrongChance) == 0)) item.Strong = (byte)(RandomomRange(stat.StrongMaxStat-1, stat.StrongStatChance)+1);
            if ((stat.MagicResistChance > 0) && (Random.Next(stat.MagicResistChance) == 0)) item.MagicResist = (byte)(RandomomRange(stat.MagicResistMaxStat-1, stat.MagicResistStatChance)+1);
            if ((stat.PoisonResistChance > 0) && (Random.Next(stat.PoisonResistChance) == 0)) item.PoisonResist = (byte)(RandomomRange(stat.PoisonResistMaxStat-1, stat.PoisonResistStatChance)+1);
            if ((stat.HpRecovChance > 0) && (Random.Next(stat.HpRecovChance) == 0)) item.HealthRecovery = (byte)(RandomomRange(stat.HpRecovMaxStat-1, stat.HpRecovStatChance)+1);
            if ((stat.MpRecovChance > 0) && (Random.Next(stat.MpRecovChance) == 0)) item.ManaRecovery = (byte)(RandomomRange(stat.MpRecovMaxStat-1, stat.MpRecovStatChance)+1);
            if ((stat.PoisonRecovChance > 0) && (Random.Next(stat.PoisonRecovChance) == 0)) item.PoisonRecovery = (byte)(RandomomRange(stat.PoisonRecovMaxStat-1, stat.PoisonRecovStatChance)+1);
            if ((stat.CriticalRateChance > 0) && (Random.Next(stat.CriticalRateChance) == 0)) item.CriticalRate = (byte)(RandomomRange(stat.CriticalRateMaxStat-1, stat.CriticalRateStatChance)+1);
            if ((stat.CriticalDamageChance > 0) && (Random.Next(stat.CriticalDamageChance) == 0)) item.CriticalDamage = (byte)(RandomomRange(stat.CriticalDamageMaxStat-1, stat.CriticalDamageStatChance)+1);
            if ((stat.FreezeChance > 0) && (Random.Next(stat.FreezeChance) == 0)) item.Freezing = (byte)(RandomomRange(stat.FreezeMaxStat-1, stat.FreezeStatChance)+1);
            if ((stat.PoisonAttackChance > 0) && (Random.Next(stat.PoisonAttackChance) == 0)) item.PoisonAttack = (byte)(RandomomRange(stat.PoisonAttackMaxStat-1, stat.PoisonAttackStatChance)+1);
            if ((stat.AttackSpeedChance > 0) && (Random.Next(stat.AttackSpeedChance) == 0)) item.AttackSpeed = (byte) (RandomomRange(stat.AttackSpeedMaxStat-1, stat.AttackSpeedStatChance)+1);
            if ((stat.LuckChance > 0) && (Random.Next(stat.LuckChance) == 0)) item.Luck = (byte) (RandomomRange(stat.LuckMaxStat-1, stat.LuckStatChance)+1);
            if ((stat.CurseChance > 0) && (Random.Next(100) <= stat.CurseChance)) item.Cursed = true;
        }

        public int RandomomRange(int count, int rate)
        {
            int x = 0;
            for (int i = 0; i < count; i++) if (Random.Next(rate) == 0) x++;
            return x;
        }
        public bool BindItem(UserItem item)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                ItemInfo info = ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;

                return BindSlotItems(item);
            }
            return false;
        }

        public bool BindGameShop(GameShopItem item, bool EditEnvir = true)
        {
            for (int i = 0; i < SMain.EditEnvir.ItemInfoList.Count; i++)
            {
                ItemInfo info = SMain.EditEnvir.ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;

                return true;
            }
            return false;
        }

        public bool BindSlotItems(UserItem item)
        {           
            for (int i = 0; i < item.Slots.Length; i++)
            {
                if (item.Slots[i] == null) continue;

                if (!BindItem(item.Slots[i])) return false;
            }

            item.SetSlotSize();

            return true;
        }

        public bool BindQuest(QuestProgressInfo quest)
        {
            for (int i = 0; i < QuestInfoList.Count; i++)
            {
                QuestInfo info = QuestInfoList[i];
                if (info.Index != quest.Index) continue;
                quest.Info = info;
                return true;
            }
            return false;
        }

        public Map GetMap(int index)
        {
            return MapList.FirstOrDefault(t => t.Info.Index == index);
        }

        public Map GetMapByNameAndInstance(string name, int instanceValue = 0)
        {
            if (instanceValue < 0) instanceValue = 0;
            if (instanceValue > 0) instanceValue--;

            var instanceMapList = MapList.Where(t => String.Equals(t.Info.FileName, name, StringComparison.CurrentCultureIgnoreCase)).ToList();
            return instanceValue < instanceMapList.Count() ? instanceMapList[instanceValue] : null;
        }

        public MapObject GetObject(uint objectID)
        {
            return Objects.FirstOrDefault(e => e.ObjectID == objectID);
        }

        public MonsterInfo GetMonsterInfo(int index)
        {
            for (int i = 0; i < MonsterInfoList.Count; i++)
                if (MonsterInfoList[i].Index == index) return MonsterInfoList[i];

            return null;
        }

        public NPCObject GetNPC(string name)
        {
            return MapList.SelectMany(t1 => t1.NPCs.Where(t => t.Info.Name == name)).FirstOrDefault();
        }
        /*
        public MonsterInfo GetMonsterInfo(string name)
        {
            for (int i = 0; i < MonsterInfoList.Count; i++)
            {
                MonsterInfo info = MonsterInfoList[i];
                //if (info.Name != name && !info.Name.Replace(" ", "").StartsWith(name, StringComparison.OrdinalIgnoreCase)) continue;
                if (String.Compare(info.Name, name, StringComparison.OrdinalIgnoreCase) != 0 && String.Compare(info.Name.Replace(" ", ""), name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) != 0) continue;
                return info;
            }
            return null;
        }
        */
        public MonsterInfo GetMonsterInfo(string name, bool Strict = false)
        {
            for (int i = 0; i < MonsterInfoList.Count; i++)
            {
                MonsterInfo info = MonsterInfoList[i];
                if (Strict)
                {
                    if (info.Name != name) continue;
                    return info;
                }
                else
                {
                    //if (info.Name != name && !info.Name.Replace(" ", "").StartsWith(name, StringComparison.OrdinalIgnoreCase)) continue;
                    if (String.Compare(info.Name, name, StringComparison.OrdinalIgnoreCase) != 0 && String.Compare(info.Name.Replace(" ", ""), name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) != 0) continue;
                    return info;
                }
            }
            return null;
        }
        public PlayerObject GetPlayer(string name)
        {
            for (int i = 0; i < Players.Count; i++)
                if (String.Compare(Players[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return Players[i];

            return null;
        }
        public PlayerObject GetPlayer(uint PlayerId)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].Info.Index == PlayerId)
                    return Players[i];

            return null;
        }
        public CharacterInfo GetCharacterInfo(string name)
        {
            for (int i = 0; i < CharacterList.Count; i++)
                if (String.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return CharacterList[i];

            return null;
        }

        public CharacterInfo GetCharacterInfo(int index)
        {
            for (int i = 0; i < CharacterList.Count; i++)
                if (CharacterList[i].Index == index)
                    return CharacterList[i];

            return null;
        }

        public ItemInfo GetItemInfo(int index)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                ItemInfo info = ItemInfoList[i];
                if (info.Index != index) continue;
                return info;
            }
            return null;
        }
        public ItemInfo GetItemInfo(string name)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                ItemInfo info = ItemInfoList[i];
                if (String.Compare(info.Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                return info;
            }
            return null;
        }
        public QuestInfo GetQuestInfo(int index)
        {
            return QuestInfoList.FirstOrDefault(info => info.Index == index);
        }

        public ItemInfo GetBook(short Skill)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                ItemInfo info = ItemInfoList[i];
                if ((info.Type != ItemType.书籍) || (info.Shape != Skill)) continue;
                return info;
            }
            return null;
        }

        public void MessageAccount(AccountInfo account, string message, ChatType type)
        {
            if (account == null) return;
            if (account.Characters == null) return;

            for (int i = 0; i < account.Characters.Count; i++)
            {
                if (account.Characters[i].Player == null) continue;
                account.Characters[i].Player.ReceiveChat(message, type);
                return;
            }
        }
        public GuildObject GetGuild(string name)
        {
            for (int i = 0; i < GuildList.Count; i++)
            {
                if (String.Compare(GuildList[i].Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                return GuildList[i];
            }
            return null;
        }
        public GuildObject GetGuild(int index)
        {
            for (int i = 0; i < GuildList.Count; i++)
                if (GuildList[i].Guildindex == index)
                    return GuildList[i];
            return null;
        }

        public void ProcessNewDay()
        {
            foreach (CharacterInfo c in CharacterList)
            {
                ClearDailyQuests(c);

                c.NewDay = true;

                if(c.Player != null)
                {
                    c.Player.CallDefaultNPC(DefaultNPCType.Daily);
                }
            }
        }

        private void ProcessRentedItems()
        {
            foreach (var characterInfo in CharacterList)
            {
                if (characterInfo.RentedItems.Count <= 0)
                    continue;

                foreach (var rentedItemInfo in characterInfo.RentedItems)
                {
                    if (rentedItemInfo.ItemReturnDate >= Now)
                        continue;

                    var rentingPlayer = GetCharacterInfo(rentedItemInfo.RentingPlayerName);

                    for (var i = 0; i < rentingPlayer.Inventory.Length; i++)
                    {
                        if (rentedItemInfo.ItemId != rentingPlayer?.Inventory[i]?.UniqueID)
                            continue;

                        var item = rentingPlayer.Inventory[i];

                        if (item?.RentalInformation == null)
                            continue;

                        if (Now <= item.RentalInformation.ExpiryDate)
                            continue;

                        ReturnRentalItem(item, item.RentalInformation.OwnerName, rentingPlayer);
                        rentingPlayer.Inventory[i] = null;
                        rentingPlayer.HasRentedItem = false;

                        if (rentingPlayer.Player == null)
                            continue;

                        rentingPlayer.Player.ReceiveChat($"{item.Info.FriendlyName} has just expired from your inventory.", ChatType.Hint);
                        rentingPlayer.Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentingPlayer.Player.RefreshStats();
                    }

                    for (var i = 0; i < rentingPlayer.Equipment.Length; i++)
                    {
                        var item = rentingPlayer.Equipment[i];

                        if (item?.RentalInformation == null)
                            continue;

                        if (Now <= item.RentalInformation.ExpiryDate)
                            continue;

                        ReturnRentalItem(item, item.RentalInformation.OwnerName, rentingPlayer);
                        rentingPlayer.Equipment[i] = null;
                        rentingPlayer.HasRentedItem = false;
                        
                        if (rentingPlayer.Player == null)
                            continue;

                        rentingPlayer.Player.ReceiveChat($"{item.Info.FriendlyName} has just expired from your inventory.", ChatType.Hint);
                        rentingPlayer.Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentingPlayer.Player.RefreshStats();
                    }
                }
            }
        }

        public bool ReturnRentalItem(UserItem rentedItem, string ownerName, CharacterInfo rentingCharacterInfo)
        {
            if (rentedItem.RentalInformation == null)
                return false;

            var owner = GetCharacterInfo(ownerName);
            var returnItems = new List<UserItem>();

            foreach (var rentalInformation in owner.RentedItems)
                if (rentalInformation.ItemId == rentedItem.UniqueID)
                    owner.RentedItemsToRemove.Add(rentalInformation);
            
            rentedItem.RentalInformation.BindingFlags = BindMode.none;
            rentedItem.RentalInformation.RentalLocked = true;
            rentedItem.RentalInformation.ExpiryDate = rentedItem.RentalInformation.ExpiryDate.AddDays(1);

            returnItems.Add(rentedItem);

            var mail = new MailInfo(owner.Index, true)
            {
                Sender = rentingCharacterInfo.Name,
                Message = rentedItem.Info.FriendlyName,
                Items = returnItems
            };

            mail.Send();

            foreach (var rentalInformationToRemove in owner.RentedItemsToRemove)
                owner.RentedItems.Remove(rentalInformationToRemove);

            owner.RentedItemsToRemove.Clear();

            return true;
        }

        private void ClearDailyQuests(CharacterInfo info)
        {
            foreach (var quest in QuestInfoList)
            {
                if (quest.Type != QuestType.Daily) continue;

                for (int i = 0; i < info.CompletedQuests.Count; i++)
                {
                    if (info.CompletedQuests[i] != quest.Index) continue;

                    info.CompletedQuests.RemoveAt(i);
                } 
            }

            if (info.Player != null)
            {
                info.Player.GetCompletedQuests();
            }       
        }

        public GuildBuffInfo FindGuildBuffInfo(int Id)
        {
            for (int i = 0; i < Settings.Guild_BuffList.Count; i++)
                if (Settings.Guild_BuffList[i].Id == Id)
                    return Settings.Guild_BuffList[i];
            return null;
        }

        public void ClearGameshopLog()
        {
            SMain.Envir.GameshopLog.Clear();

            for (int i = 0; i < AccountList.Count; i++)
            {
                for (int f = 0; f < AccountList[i].Characters.Count; f++)
                {
                    AccountList[i].Characters[f].GSpurchases.Clear();
                }
            }

            ResetGS = false;
            SMain.Enqueue("商城交易记录已清除.");

        }

        int RankCount = 100;//could make this a global but it made sence since this is only used here, it should stay here
        public int InsertRank(List<Rank_Character_Info> Ranking, Rank_Character_Info NewRank)
        {
            if (Ranking.Count == 0)
            {
                Ranking.Add(NewRank);
                return Ranking.Count;
            }
            for (int i = 0; i < Ranking.Count; i++)
            {
               //if level is lower
               if (Ranking[i].level < NewRank.level)
               {
                    Ranking.Insert(i, NewRank);
                    return i+1;
                }
                //if exp is lower but level = same
                if ((Ranking[i].level == NewRank.level) && (Ranking[i].Experience < NewRank.Experience))
                {
                   Ranking.Insert(i, NewRank);
                   return i+1;
                }
            }
            if (Ranking.Count < RankCount)
            {
                Ranking.Add(NewRank);
                return Ranking.Count;
            }
            return 0;
        }

        public bool TryAddRank(List<Rank_Character_Info> Ranking, CharacterInfo info, byte type)
        {
            Rank_Character_Info NewRank = new Rank_Character_Info() { Name = info.Name, Class = info.Class, Experience = info.Experience, level = info.Level, PlayerId = info.Index, info = info };
            int NewRankIndex = InsertRank(Ranking, NewRank);
            if (NewRankIndex == 0) return false;
            for (int i = NewRankIndex; i < Ranking.Count; i++ )
            {
                SetNewRank(Ranking[i], i + 1, type);
            }
            info.Rank[type] = NewRankIndex;
            return true;
        }

        public int FindRank(List<Rank_Character_Info> Ranking, CharacterInfo info, byte type)
        {
            int startindex = info.Rank[type];
            if (startindex > 0) //if there's a previously known rank then the user can only have gone down in the ranking (or stayed the same)
            {
                for (int i = startindex-1; i < Ranking.Count; i++)
                {
                    if (Ranking[i].Name == info.Name)
                        return i;
                }
                info.Rank[type] = 0;//set the rank to 0 to tell future searches it's not there anymore
            }
            else //if there's no previously known ranking then technicaly it shouldnt be listed, but check anyway?
            {
                //currently not used so not coded it < if there's a reason to, easy to add :p
            }
            return -1;//index can be 0
        }

        public bool UpdateRank(List<Rank_Character_Info> Ranking, CharacterInfo info, byte type)
        {
            int CurrentRank = FindRank(Ranking, info, type);
            if (CurrentRank == -1) return false;//not in ranking list atm
            
            int NewRank = CurrentRank;
            //next find our updated rank
            for (int i = CurrentRank-1; i >= 0; i-- )
            {
                if ((Ranking[i].level > info.Level) || ((Ranking[i].level == info.Level) && (Ranking[i].Experience > info.Experience))) break;
                    NewRank =i;
            }

            Ranking[CurrentRank].level = info.Level;
            Ranking[CurrentRank].Experience = info.Experience;

            if (NewRank < CurrentRank)
            {//if we gained any ranks
                Ranking.Insert(NewRank, Ranking[CurrentRank]);
                Ranking.RemoveAt(CurrentRank + 1);
                for (int i = NewRank + 1; i < Math.Min(Ranking.Count, CurrentRank +1); i++)
                {
                    SetNewRank(Ranking[i], i + 1, type);
                }
            }
            info.Rank[type] = NewRank+1;
            
            return true;
        }

        public void SetNewRank(Rank_Character_Info Rank, int Index, byte type)
        {
            CharacterInfo Player = Rank.info as CharacterInfo;
            if (Player == null) return;
            Player.Rank[type] = Index;
        }

        public void RemoveRank(CharacterInfo info)
        {
            List<Rank_Character_Info> Ranking;
            int Rankindex = -1;
            //first check overall top           
            if (info.Level >= RankBottomLevel[0])
            {
                Ranking = RankTop;
                Rankindex = FindRank(Ranking, info, 0);
                if (Rankindex >= 0)
                {
                    Ranking.RemoveAt(Rankindex);
                    for (int i = Rankindex; i < Ranking.Count(); i++)
                    {
                        SetNewRank(Ranking[i], i, 0);
                    }
                }
            }
            //next class based top
            if (info.Level >= RankBottomLevel[(byte)info.Class + 1])
            {
                Ranking = RankTop;
                Rankindex = FindRank(Ranking, info, 1);
                if (Rankindex >= 0)
                {
                    Ranking.RemoveAt(Rankindex);
                    for (int i = Rankindex; i < Ranking.Count(); i++)
                    {
                        SetNewRank(Ranking[i], i, 1);
                    }
                }
            }
        }

        public void CheckRankUpdate(CharacterInfo info)
        {
            List<Rank_Character_Info> Ranking;
            Rank_Character_Info NewRank;
            
            //first check overall top           
            if (info.Level >= RankBottomLevel[0])
            {
                Ranking = RankTop;
                if (!UpdateRank(Ranking, info,0))
                {
                    if (TryAddRank(Ranking, info, 0))
                    {
                        if (Ranking.Count > RankCount)
                        {
                            SetNewRank(Ranking[RankCount], 0, 0);
                            Ranking.RemoveAt(RankCount);

                        }
                    }
                }
                if (Ranking.Count >= RankCount)
                { 
                    NewRank = Ranking[Ranking.Count -1];
                    if (NewRank != null)
                        RankBottomLevel[0] = NewRank.level;
                }
            }
            //now check class top
            if (info.Level >= RankBottomLevel[(byte)info.Class + 1])
            {
                Ranking = RankClass[(byte)info.Class];
                if (!UpdateRank(Ranking, info,1))
                {
                    if (TryAddRank(Ranking, info, 1))
                    {
                        if (Ranking.Count > RankCount)
                        {
                            SetNewRank(Ranking[RankCount], 0, 1);
                            Ranking.RemoveAt(RankCount);
                        }
                    }
                }
                if (Ranking.Count >= RankCount)
                {
                    NewRank = Ranking[Ranking.Count -1];
                    if (NewRank != null)
                        RankBottomLevel[(byte)info.Class + 1] = NewRank.level;
                }
            }
        }
    }
}

