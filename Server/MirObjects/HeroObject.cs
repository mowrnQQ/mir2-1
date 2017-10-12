using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects.Monsters;
using S = ServerPackets;

namespace Server.MirObjects
{
    public sealed class HeroObject : MapObject
    {
        public override ObjectType Race => ObjectType.Hero;
        public override string Name
        {
            get { return $"{Info.Name}\n{Player.Name}的英雄";  }
            set {  }
        }

        public override int CurrentMapIndex { get; set; }
        public override Point CurrentLocation { get; set; }
        public override MirDirection Direction { get; set; }
        public override ushort Level
        {
            get { return Info.Level; }
            set { Info.Level = value; }
        }
        public override uint Health => HP;

        public override uint MaxHealth => MaxHP;

        public ushort HP
        {
            get { return Info.HP; }
            set { Info.HP = value; }
        }
        public ushort MP
        {
            get { return Info.MP; }
            set { Info.MP = value; }
        }
        public byte Hair
        {
            get { return Info.Hair; }
            set { Info.Hair = value; }
        }

        public HeroMode Mode
        {
            get => Info.HeroMode;
            set => Info.HeroMode = value;
        }

        public MirClass Class => Info.Class;
        public MirGender Gender => Info.Gender;

        public LevelEffects LevelEffects = LevelEffects.None;

        public ushort MaxHP, MaxMP;

        public bool HasTeleportRing, HasProtectionRing, HasRevivalRing;
        public bool HasMuscleRing, HasClearRing, HasParalysisRing, HasProbeNecklace, NoDuraLoss;

        public long LastRecallTime, LastRevivalTime, LastTeleportTime, LastProbeTime, MenteeEXP;

        public bool HasUpdatedBaseStats = false;

        public bool MagicShield;
        public byte MagicShieldLv;
        public long MagicShieldTime;

        public bool ElementalBarrier;
        public byte ElementalBarrierLv;
        public long ElementalBarrierTime;

        public bool HasElemental;
        public int ElementsLevel;

        public float ExpRateOffset = 0;

        private bool _concentrating;
        public bool Concentrating
        {
            get
            {
                return _concentrating;
            }
            set
            {
                if (_concentrating == value) return;
                _concentrating = value;
            }

        }
        public bool ConcentrateInterrupted;
        public long ConcentrateInterruptTime;

        public bool Stacking;

        private int _stepCounter, _runCounter, _fishCounter, _restedCounter;

        public const long TurnDelay = 350, MoveDelay = 600, HarvestDelay = 350, RegenDelay = 10000, PotDelay = 200, HealDelay = 600, DuraDelay = 10000, VampDelay = 500, LoyaltyDelay = 1000, FishingCastDelay = 750, FishingDelay = 200, CreatureTimeLeftDelay = 1000, ItemExpireDelay = 60000, MovementDelay = 2000;
        public long ActionTime, RunTime, RegenTime, PotTime, HealTime, AttackTime, StruckTime, TorchTime, DuraTime, DecreaseLoyaltyTime, IncreaseLoyaltyTime, ChatTime, ShoutTime, SpellTime, VampTime, SearchTime, FishingTime, LogTime, FishingFoundTime, CreatureTimeLeftTicker, StackingTime, ItemExpireTime, RestedTime, MovementTime;

        public const int EXPOwnerDelay = 5000, SearchDelay = 3000, RoamDelay = 1000, RevivalDelay = 2000;
        public long MoveTime, DeadTime, RoamTime;
        public long ShockTime, RageTime, HallucinationTime;

        public byte ViewRange = 7;

        public bool BindingShotCenter, PoisonStopRegen = true;
        public bool CanMove
        {
            get { return !Dead && Envir.Time >= ActionTime && !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.LRParalysis) && !CurrentPoison.HasFlag(PoisonType.Frozen); }
        }
        public bool CanWalk
        {
            get { return !Dead && Envir.Time >= ActionTime && !InTrapRock && !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.LRParalysis) && !CurrentPoison.HasFlag(PoisonType.Frozen); }
        }
        public bool CanRun
        {
            get { return !Dead && Envir.Time >= ActionTime && (_stepCounter > 0 || FastRun) && (!Sneaking || ActiveSwiftFeet) && CurrentBagWeight <= MaxBagWeight && !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.LRParalysis) && !CurrentPoison.HasFlag(PoisonType.Frozen); }
        }
        public bool CanAttack
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && Envir.Time >= AttackTime && !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.LRParalysis) && !CurrentPoison.HasFlag(PoisonType.Frozen);
            }
        }

        public bool CanRegen
        {
            get { return Envir.Time >= RegenTime && _runCounter == 0; }
        }
        private bool CanCast
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && Envir.Time >= SpellTime && !CurrentPoison.HasFlag(PoisonType.Stun) &&
                    !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.Frozen);
            }
        }

        public long Experience
        {
            get => Info.Experience;
            set => Info.Experience = value;
        }

        public long MaxExperience;
        public byte LifeOnHit;
        public byte HpDrainRate;
        public float HpDrain = 0;

        public byte AttackBonus, MineRate, GemRate, FishRate, CraftRate, SkillNeckBoost;

        public bool FatalSword, Slaying, TwinDrakeBlade, FlamingSword, MPEater, Hemorrhage, CounterAttack;
        public int MPEaterCount, HemorrhageAttackCount;
        public long FlamingSwordTime, CounterAttackTime;
        public bool ActiveBlizzard, ActiveReincarnation, ActiveSwiftFeet, ReincarnationReady;
        public PlayerObject ReincarnationTarget, ReincarnationHost;
        public long ReincarnationExpireTime;
        public byte Reflect;
        public bool UnlockCurse = false;
        public bool FastRun = false;
        public bool CanGainExp = true;

        public short Looks_Armour = 0, Looks_Weapon = -1;
        public byte Looks_Wings = 0;

        

        public List<ItemSets> ItemSets = new List<ItemSets>();
        public List<EquipmentSlot> MirSet = new List<EquipmentSlot>();

        public short TransformType;

        public MapObject[,] ArcherTrapObjectsArray = new MapObject[4, 3];
        public SpellObject[] PortalObjectsArray = new SpellObject[2];

        public HeroInfo Info;
        public PlayerObject Player;

        

        public HeroObject(PlayerObject player)
        {
            Player = player;
            NameColour = Color.SlateBlue;
            CurrentMap = player.CurrentMap;
            CurrentLocation = new Point(player.CurrentLocation.X + 1, player.CurrentLocation.Y);
        }

        public override void SetOperateTime()
        {
            OperateTime = Envir.Time;
        }

        public override void Process()
        {
            if (Player?.Connection == null || Node == null || Info == null) return;

            if (MagicShield && Envir.Time > MagicShieldTime)
            {
                MagicShield = false;
                MagicShieldLv = 0;
                MagicShieldTime = 0;
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.MagicShieldDown }, CurrentLocation);
                RemoveBuff(BuffType.MagicShield);
            }

            if (ElementalBarrier && Envir.Time > ElementalBarrierTime)
            {
                ElementalBarrier = false;
                ElementalBarrierLv = 0;
                ElementalBarrierTime = 0;
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.ElementalBarrierDown }, CurrentLocation);
            }

            for (int i = 0; i <= 3; i++)//Self destruct when out of range (in this case 15 squares)
            {
                if (ArcherTrapObjectsArray[i, 0] == null) continue;
                if (FindObject(ArcherTrapObjectsArray[i, 0].ObjectID, 15) != null) continue;
                bool detonated = true;
                for (int j = 0; j <= 2; j++)
                    if (!((SpellObject)ArcherTrapObjectsArray[i, j]).DetonatedTrap) detonated = false;
                if (detonated) continue;
                for (int j = 0; j <= 2; j++)
                    ((SpellObject)ArcherTrapObjectsArray[i, j]).DetonateTrapNow();
            }

            if (CellTime + 700 < Envir.Time) _stepCounter = 0;

            if (Sneaking) CheckSneakRadius();

            if (CounterAttack && Envir.Time >= CounterAttackTime)
            {
                CounterAttack = false;
            }

            if (ReincarnationReady && Envir.Time >= ReincarnationExpireTime)
            {
                ReincarnationReady = false;
                ActiveReincarnation = false;
                ReincarnationTarget = null;
                ReceiveChat("Reincarnation failed.", ChatType.System);
            }
            if ((ReincarnationReady || ActiveReincarnation) && (ReincarnationTarget == null || !ReincarnationTarget.Dead))
            {
                ReincarnationReady = false;
                ActiveReincarnation = false;
                ReincarnationTarget = null;
            }

            if (Envir.Time > RunTime && _runCounter > 0)
            {
                RunTime = Envir.Time + 1500;
                _runCounter--;
            }

            if (Stacking && Envir.Time > StackingTime)
            {
                Stacking = false;

                for (int i = 0; i < 8; i++)
                {
                    if (Pushed(this, (MirDirection)i, 1) == 1) break;
                }
            }

            if (Envir.Time > ItemExpireTime)
            {
                ItemExpireTime = Envir.Time + ItemExpireDelay;

                ProcessItems();
            }

            for (int i = Pets.Count() - 1; i >= 0; i--)
            {
                MonsterObject pet = Pets[i];
                if (pet.Dead) Pets.Remove(pet);
            }

            ProcessBuffs();
            ProcessInfiniteBuffs();
            ProcessRegen();
            ProcessPoison();

            UserItem item;
            if (Envir.Time > TorchTime)
            {
                TorchTime = Envir.Time + 10000;
                item = Info.Equipment[(int)EquipmentSlot.Torch];
                if (item != null)
                {
                    DamageItem(item, 5);

                    if (item.CurrentDura == 0)
                    {
                        Info.Equipment[(int)EquipmentSlot.Torch] = null;
                        Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        RefreshStats();
                    }
                }
            }

            if (Envir.Time > DuraTime)
            {
                DuraTime = Envir.Time + DuraDelay;

                for (int i = 0; i < Info.Equipment.Length; i++)
                {
                    item = Info.Equipment[i];
                    if (item == null || !item.DuraChanged) continue; // || item.Info.Type == ItemType.Mount
                    item.DuraChanged = false;
                    Player.Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });
                }
            }
            if(Target == null) FindTarget();
            if(Target == null) MoveToPlayer();
            else Attack();

            base.Process();
            RefreshStats();
        }

        public override void Process(DelayedAction action)
        {
            if (action.FlaggedToRemove) return;

            switch (action.Type)
            {
                case DelayedType.Magic:
                    CompleteMagic(action.Params);
                    break;
                case DelayedType.Damage:
                    CompleteAttack(action.Params);
                    break;
                case DelayedType.Poison:
                    CompletePoison(action.Params);
                    break;
                case DelayedType.DamageIndicator:
                    CompleteDamageIndicator(action.Params);
                    break;
            }
        }

        private void CompleteMagic(IList<object> data)
        {
            HeroMagic magic = (HeroMagic)data[0];
            int value;
            MapObject target;
            Point location;
            MonsterObject monster;
            switch (magic.Spell)
            {
                #region FireBall, GreatFireBall, ThunderBolt, SoulFireBall, FlameDisruptor

                case Spell.火球术:
                case Spell.大火球:
                case Spell.雷电术:
                case Spell.灵魂火符:
                case Spell.灭天火:
                case Spell.天日闪:
                case Spell.无我闪:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0) LevelMagic(magic);
                    break;

                #endregion

                #region FrostCrunch
                case Spell.寒冰掌:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0)
                    {
                        if (Level + (target.Race == ObjectType.Player ? 2 : 10) >= target.Level && Envir.Random.Next(target.Race == ObjectType.Player ? 100 : 20) <= magic.Level)
                        {
                            target.ApplyPoison(new Poison
                            {
                                Owner = this,
                                Duration = target.Race == ObjectType.Player ? 4 : 5 + Envir.Random.Next(5),
                                PType = PoisonType.Slow,
                                TickSpeed = 1000,
                            }, this);
                            target.OperateTime = 0;
                        }

                        if (Level + (target.Race == ObjectType.Player ? 2 : 10) >= target.Level && Envir.Random.Next(target.Race == ObjectType.Player ? 100 : 40) <= magic.Level)
                        {
                            target.ApplyPoison(new Poison
                            {
                                Owner = this,
                                Duration = target.Race == ObjectType.Player ? 2 : 5 + Envir.Random.Next(Freezing),
                                PType = PoisonType.Frozen,
                                TickSpeed = 1000,
                            }, this);
                            target.OperateTime = 0;
                        }

                        LevelMagic(magic);
                    }
                    break;

                #endregion

                #region Vampirism

                case Spell.噬血术:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    value = target.Attacked(this, value, DefenceType.MAC, false);
                    if (value == 0) return;
                    LevelMagic(magic);
                    if (VampAmount == 0) VampTime = Envir.Time + 1000;
                    VampAmount += (ushort)(value * (magic.Level + 1) * 0.25F);
                    break;

                #endregion

                #region Healing

                case Spell.治愈术:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsFriendlyTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Health >= target.MaxHealth) return;
                    target.HealAmount = (ushort)Math.Min(ushort.MaxValue, target.HealAmount + value);
                    target.OperateTime = 0;
                    LevelMagic(magic);
                    break;

                #endregion

                #region ElectricShock

                case Spell.诱惑之光:
                    monster = (MonsterObject)data[1];
                    if (monster == null || !monster.IsAttackTarget(this) || monster.CurrentMap != CurrentMap || monster.Node == null) return;
                    ElectricShock(monster, magic);
                    break;

                #endregion

                #region Poisoning

                case Spell.施毒术:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    UserItem item = (UserItem)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

                    switch (item.Info.Shape)
                    {
                        case 1:
                            target.ApplyPoison(new Poison
                            {
                                Duration = (value * 2) + ((magic.Level + 1) * 7),
                                Owner = this,
                                PType = PoisonType.Green,
                                TickSpeed = 2000,
                                Value = value / 15 + magic.Level + 1 + Envir.Random.Next(PoisonAttack)
                            }, this);
                            break;
                        case 2:
                            target.ApplyPoison(new Poison
                            {
                                Duration = (value * 2) + (magic.Level + 1) * 7,
                                Owner = this,
                                PType = PoisonType.Red,
                                TickSpeed = 2000,
                            }, this);
                            break;
                    }
                    target.OperateTime = 0;

                    LevelMagic(magic);
                    break;

                #endregion

                #region StormEscape
                case Spell.StormEscape:
                    location = (Point)data[1];
                    if (CurrentMap.Info.NoTeleport)
                    {
                        ReceiveChat(("You cannot teleport on this map"), ChatType.System);
                        return;
                    }
                    if (!CurrentMap.ValidPoint(location) || Envir.Random.Next(4) >= magic.Level + 1 || !Teleport(CurrentMap, location, false)) return;
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.StormEscape }, CurrentLocation);
                    AddBuff(new Buff { Type = BuffType.TemporalFlux, Caster = this, ExpireTime = Envir.Time + 30000 });
                    LevelMagic(magic);
                    break;
                #endregion

                

                #region Blink

                case Spell.Blink:
                {
                    location = (Point)data[1];
                    if (CurrentMap.Info.NoTeleport)
                    {
                        ReceiveChat(("You cannot teleport on this map"), ChatType.System);
                        return;
                    }
                    if (Functions.InRange(CurrentLocation, location, magic.Info.Range) == false) return;
                    if (!CurrentMap.ValidPoint(location) || Envir.Random.Next(4) >= magic.Level + 1 || !Teleport(CurrentMap, location, false)) return;
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Teleport }, CurrentLocation);
                    LevelMagic(magic);
                    AddBuff(new Buff { Type = BuffType.TemporalFlux, Caster = this, ExpireTime = Envir.Time + 30000 });
                }
                    break;

                #endregion

                #region Hiding

                case Spell.隐身术:
                    for (int i = 0; i < Buffs.Count; i++)
                        if (Buffs[i].Type == BuffType.Hiding) return;

                    value = (int)data[1];
                    AddBuff(new Buff { Type = BuffType.Hiding, Caster = this, ExpireTime = Envir.Time + value * 1000 });
                    LevelMagic(magic);
                    break;

                #endregion

                #region Haste

                case Spell.体迅风:
                    AddBuff(new Buff { Type = BuffType.Haste, Caster = this, ExpireTime = Envir.Time + (magic.Level + 1) * 30000, Values = new int[] { (magic.Level + 1) * 2 } });
                    LevelMagic(magic);
                    break;

                #endregion

                #region Fury

                case Spell.血龙剑法:
                    AddBuff(new Buff { Type = BuffType.Fury, Caster = this, ExpireTime = Envir.Time + 60000 + magic.Level * 10000, Values = new int[] { 4 }, Visible = true });
                    LevelMagic(magic);
                    break;

                #endregion

                #region ImmortalSkin

                case Spell.金刚不坏:
                    int ACvalue = (int)Math.Round(MaxAC * (0.10 + (0.07 * magic.Level)));
                    int DCValue = (int)Math.Round(MaxDC * (0.05 + (0.01 * magic.Level)));
                    AddBuff(new Buff { Type = BuffType.ImmortalSkin, Caster = this, ExpireTime = Envir.Time + 60000 + magic.Level * 1000, Values = new int[] { ACvalue, DCValue }, Visible = true });
                    LevelMagic(magic);
                    break;
                #endregion

                #region LightBody

                case Spell.风身术:
                    AddBuff(new Buff { Type = BuffType.LightBody, Caster = this, ExpireTime = Envir.Time + (magic.Level + 1) * 30000, Values = new int[] { (magic.Level + 1) * 2 } });
                    LevelMagic(magic);
                    break;

                #endregion

                #region MagicShield

                case Spell.魔法盾:

                    if (MagicShield) return;
                    MagicShield = true;
                    MagicShieldLv = magic.Level;
                    MagicShieldTime = Envir.Time + (int)data[1] * 1000;
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.MagicShieldUp }, CurrentLocation);
                    AddBuff(new Buff { Type = BuffType.MagicShield, Caster = this, ExpireTime = MagicShieldTime, Values = new int[] { MagicShieldLv } });
                    LevelMagic(magic);
                    break;

                #endregion

                #region TurnUndead

                case Spell.圣言术:
                    monster = (MonsterObject)data[1];
                    if (monster == null || !monster.IsAttackTarget(this) || monster.CurrentMap != CurrentMap || monster.Node == null) return;
                    monster.LastHitter = this;
                    monster.LastHitTime = Envir.Time + 5000;
                    monster.EXPOwner = this;
                    monster.EXPOwnerTime = Envir.Time + 5000;
                    monster.Die();
                    LevelMagic(magic);
                    break;

                #endregion

                #region MagicBooster

                case Spell.深延术:
                    value = (int)data[1];

                    AddBuff(new Buff { Type = BuffType.MagicBooster, Caster = this, ExpireTime = Envir.Time + 60000, Values = new int[] { value, 6 + magic.Level }, Visible = true });
                    LevelMagic(magic);
                    break;

                #endregion

                #region Purification

                case Spell.净化术:
                    target = (MapObject)data[1];

                    if (target == null || !target.IsFriendlyTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (Envir.Random.Next(4) > magic.Level || target.PoisonList.Count == 0) return;

                    target.ExplosionInflictedTime = 0;
                    target.ExplosionInflictedStage = 0;

                    for (int i = 0; i < target.Buffs.Count; i++)
                    {
                        if (target.Buffs[i].Type == BuffType.Curse)
                        {
                            target.Buffs.RemoveAt(i);
                            break;
                        }
                    }

                    target.PoisonList.Clear();
                    target.OperateTime = 0;

                    //if (target.ObjectID == ObjectID)
                    //    Enqueue(new S.RemoveDelayedExplosion { ObjectID = target.ObjectID });
                    target.Broadcast(new S.RemoveDelayedExplosion { ObjectID = target.ObjectID });

                    LevelMagic(magic);
                    break;

                #endregion

                #region Revelation

                case Spell.心灵启示:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    if (target == null || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) return;
                    if (Envir.Random.Next(4) > magic.Level || Envir.Time < target.RevTime) return;

                    target.RevTime = Envir.Time + value * 1000;
                    target.OperateTime = 0;
                    target.BroadcastHealthChange();

                    LevelMagic(magic);
                    break;

                #endregion

                #region Reincarnation

                case Spell.苏生术:

                    if (ReincarnationReady)
                    {
                        ReincarnationTarget.Enqueue(new S.RequestReincarnation { });
                        LevelMagic(magic);
                        ReincarnationReady = false;
                    }
                    break;

                #endregion

                #region Entrapment

                case Spell.捕绳剑:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || target.Race != ObjectType.Monster ||
                        Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 7 || target.Level >= Level + 5 + Envir.Random.Next(8)) return;

                    MirDirection pulldirection = (MirDirection)((byte)(Direction - 4) % 8);
                    int pulldistance = 0;
                    if ((byte)pulldirection % 2 > 0)
                        pulldistance = Math.Max(0, Math.Min(Math.Abs(CurrentLocation.X - target.CurrentLocation.X), Math.Abs(CurrentLocation.Y - target.CurrentLocation.Y)));
                    else
                        pulldistance = pulldirection == MirDirection.Up || pulldirection == MirDirection.Down ? Math.Abs(CurrentLocation.Y - target.CurrentLocation.Y) - 2 : Math.Abs(CurrentLocation.X - target.CurrentLocation.X) - 2;

                    int levelgap = target.Race == ObjectType.Player ? Level - target.Level + 4 : Level - target.Level + 9;
                    if (Envir.Random.Next(30) >= ((magic.Level + 1) * 3) + levelgap) return;

                    int duration = target.Race == ObjectType.Player ? (int)Math.Round((magic.Level + 1) * 1.6) : (int)Math.Round((magic.Level + 1) * 0.8);
                    if (duration > 0) target.ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = duration, TickSpeed = 1000 }, this);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.Entrapment }, target.CurrentLocation);
                    if (target.Pushed(this, pulldirection, pulldistance) > 0) LevelMagic(magic);
                    break;

                #endregion

                #region Hallucination

                case Spell.迷魂术:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null ||
                        Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 7 || Envir.Random.Next(Level + 20 + magic.Level * 5) <= target.Level + 10) return;
                    item = GetAmulet(1);
                    if (item == null) return;

                    ((MonsterObject)target).HallucinationTime = Envir.Time + (Envir.Random.Next(20) + 10) * 1000;
                    target.Target = null;

                    ConsumeItem(item, 1);

                    LevelMagic(magic);
                    break;

                #endregion

                #region PetEnhancer

                case Spell.血龙水:
                    value = (int)data[1];
                    target = (MonsterObject)data[2];

                    int dcInc = 2 + target.Level * 2;
                    int acInc = 4 + target.Level;

                    target.AddBuff(new Buff { Type = BuffType.PetEnhancer, Caster = this, ExpireTime = Envir.Time + value * 1000, Values = new int[] { dcInc, acInc }, Visible = true });
                    LevelMagic(magic);
                    break;

                #endregion

                #region ElementalBarrier, ElementalShot

                case Spell.金刚术:
                    if (ElementalBarrier) return;
                    if (!HasElemental)
                    {
                        ObtainElement(true);//gather orb through casting
                        LevelMagic(magic);
                        return;
                    }

                    int barrierPower = GetElementalOrbPower(true);//defensive orbpower
                    //destroy orbs
                    ElementsLevel = 0;
                    ObtainElement(false);
                    LevelMagic(magic);
                    //
                    ElementalBarrier = true;
                    ElementalBarrierLv = (byte)((int)magic.Level);//compensate for lower mc then wizard
                    ElementalBarrierTime = Envir.Time + ((int)data[1] + barrierPower) * 1000;
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.ElementalBarrierUp }, CurrentLocation);
                    break;

                case Spell.万斤闪:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null)
                    {
                        //destroy orbs
                        ElementsLevel = 0;
                        ObtainElement(false);//update and send to client
                        return;
                    }
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0)
                        LevelMagic(magic);
                    DoKnockback(target, magic);//ElementalShot - Knockback

                    //destroy orbs
                    ElementsLevel = 0;
                    ObtainElement(false);//update and send to client
                    break;

                #endregion

                #region DelayedExplosion

                case Spell.爆闪:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0) LevelMagic(magic);

                    target.ApplyPoison(new Poison
                    {
                        Duration = (value * 2) + (magic.Level + 1) * 7,
                        Owner = this,
                        PType = PoisonType.DelayedExplosion,
                        TickSpeed = 2000,
                        Value = value
                    }, this);

                    target.OperateTime = 0;
                    LevelMagic(magic);
                    break;

                #endregion

                #region BindingShot

                case Spell.BindingShot:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (((MonsterObject)target).ShockTime >= Envir.Time) return;//Already shocked

                    Point place = target.CurrentLocation;
                    MonsterObject centerTarget = null;

                    for (int y = place.Y - 1; y <= place.Y + 1; y++)
                    {
                        if (y < 0) continue;
                        if (y >= CurrentMap.Height) break;

                        for (int x = place.X - 1; x <= place.X + 1; x++)
                        {
                            if (x < 0) continue;
                            if (x >= CurrentMap.Width) break;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject targetob = cell.Objects[i];

                                if (y == place.Y && x == place.X && targetob.Race == ObjectType.Monster)
                                {
                                    centerTarget = (MonsterObject)targetob;
                                }

                                switch (targetob.Race)
                                {
                                    case ObjectType.Monster:
                                        if (targetob == null || !targetob.IsAttackTarget(this) || targetob.Node == null || targetob.Level > this.Level + 2) continue;

                                        MonsterObject mobTarget = (MonsterObject)targetob;

                                        if (centerTarget == null) centerTarget = mobTarget;

                                        mobTarget.ShockTime = Envir.Time + value;
                                        mobTarget.Target = null;
                                        break;
                                }
                            }
                        }
                    }

                    if (centerTarget == null) return;

                    //only the centertarget holds the effect
                    centerTarget.BindingShotCenter = true;
                    centerTarget.Broadcast(new S.SetBindingShot { ObjectID = centerTarget.ObjectID, Enabled = true, Value = value });

                    LevelMagic(magic);
                    break;

                #endregion

                #region VampireShot, PoisonShot, CrippleShot
                case Spell.吸血地闪:
                case Spell.毒魔闪:
                case Spell.邪爆闪:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) == 0) return;

                    int buffTime = 5 + (5 * magic.Level);

                    bool hasVampBuff = (Buffs.Where(x => x.Type == BuffType.VampireShot).ToList().Count() > 0);
                    bool hasPoisonBuff = (Buffs.Where(x => x.Type == BuffType.PoisonShot).ToList().Count() > 0);

                    bool doVamp = false, doPoison = false;
                    if (magic.Spell == Spell.吸血地闪)
                    {
                        doVamp = true;
                        if (!hasVampBuff && !hasPoisonBuff && (Envir.Random.Next(20) >= 8))//40% chance
                        {
                            AddBuff(new Buff { Type = BuffType.VampireShot, Caster = this, ExpireTime = Envir.Time + (buffTime * 1000), Values = new int[] { value }, Visible = true, ObjectID = this.ObjectID });
                            BroadcastInfo();
                        }
                    }
                    if (magic.Spell == Spell.毒魔闪)
                    {
                        doPoison = true;
                        if (!hasPoisonBuff && !hasVampBuff && (Envir.Random.Next(20) >= 8))//40% chance
                        {
                            AddBuff(new Buff { Type = BuffType.PoisonShot, Caster = this, ExpireTime = Envir.Time + (buffTime * 1000), Values = new int[] { value }, Visible = true, ObjectID = this.ObjectID });
                            BroadcastInfo();
                        }
                    }
                    if (magic.Spell == Spell.邪爆闪)
                    {
                        if (hasVampBuff || hasPoisonBuff)
                        {
                            place = target.CurrentLocation;
                            for (int y = place.Y - 1; y <= place.Y + 1; y++)
                            {
                                if (y < 0) continue;
                                if (y >= CurrentMap.Height) break;
                                for (int x = place.X - 1; x <= place.X + 1; x++)
                                {
                                    if (x < 0) continue;
                                    if (x >= CurrentMap.Width) break;
                                    Cell cell = CurrentMap.GetCell(x, y);
                                    if (!cell.Valid || cell.Objects == null) continue;
                                    for (int i = 0; i < cell.Objects.Count; i++)
                                    {
                                        MapObject targetob = cell.Objects[i];
                                        if (targetob.Race != ObjectType.Monster && targetob.Race != ObjectType.Player) continue;
                                        if (targetob == null || !targetob.IsAttackTarget(this) || targetob.Node == null) continue;
                                        if (targetob.Dead) continue;

                                        if (hasVampBuff)//Vampire Effect
                                        {
                                            //cancel out buff
                                            AddBuff(new Buff { Type = BuffType.VampireShot, Caster = this, ExpireTime = Envir.Time + 1000, Values = new int[] { value }, Visible = true, ObjectID = this.ObjectID });

                                            target.Attacked(this, value, DefenceType.MAC, false);
                                            if (VampAmount == 0) VampTime = Envir.Time + 1000;
                                            VampAmount += (ushort)(value * (magic.Level + 1) * 0.25F);
                                        }
                                        if (hasPoisonBuff)//Poison Effect
                                        {
                                            //cancel out buff
                                            AddBuff(new Buff { Type = BuffType.PoisonShot, Caster = this, ExpireTime = Envir.Time + 1000, Values = new int[] { value }, Visible = true, ObjectID = this.ObjectID });

                                            targetob.ApplyPoison(new Poison
                                            {
                                                Duration = (value * 2) + (magic.Level + 1) * 7,
                                                Owner = this,
                                                PType = PoisonType.Green,
                                                TickSpeed = 2000,
                                                Value = value / 25 + magic.Level + 1 + Envir.Random.Next(PoisonAttack)
                                            }, this);
                                            targetob.OperateTime = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (doVamp)//Vampire Effect
                        {
                            if (VampAmount == 0) VampTime = Envir.Time + 1000;
                            VampAmount += (ushort)(value * (magic.Level + 1) * 0.25F);
                        }
                        if (doPoison)//Poison Effect
                        {
                            target.ApplyPoison(new Poison
                            {
                                Duration = (value * 2) + (magic.Level + 1) * 7,
                                Owner = this,
                                PType = PoisonType.Green,
                                TickSpeed = 2000,
                                Value = value / 25 + magic.Level + 1 + Envir.Random.Next(PoisonAttack)
                            }, this);
                            target.OperateTime = 0;
                        }
                    }

                    LevelMagic(magic);
                    break;
                #endregion

                #region ArcherSummons
                case Spell.吸血地精:
                case Spell.痹魔阱:
                case Spell.蛇柱阱:
                    value = (int)data[1];
                    location = (Point)data[2];
                    target = (MapObject)data[3];

                    int SummonType = 0;
                    switch (magic.Spell)
                    {
                        case Spell.吸血地精:
                            SummonType = 1;
                            break;
                        case Spell.痹魔阱:
                            SummonType = 2;
                            break;
                        case Spell.蛇柱阱:
                            SummonType = 3;
                            break;
                    }
                    if (SummonType == 0) return;

                    for (int i = 0; i < Pets.Count; i++)
                    {
                        monster = Pets[i];
                        if ((monster.Info.Name != (SummonType == 1 ? Settings.VampireName : (SummonType == 2 ? Settings.ToadName : Settings.SnakeTotemName))) || monster.Dead) continue;
                        if (monster.Node == null) continue;
                        monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500, target));
                        monster.Target = target;
                        return;
                    }

                    if (Pets.Where(x => x.Race == ObjectType.Monster).Count() > 1) return;

                    //left it in for future summon amulets
                    //UserItem item = GetAmulet(5);
                    //if (item == null) return;

                    MonsterInfo info = Envir.GetMonsterInfo((SummonType == 1 ? Settings.VampireName : (SummonType == 2 ? Settings.ToadName : Settings.SnakeTotemName)));
                    if (info == null) return;

                    LevelMagic(magic);
                    //ConsumeItem(item, 5);

                    monster = MonsterObject.GetMonster(info);
                    monster.PetLevel = magic.Level;
                    monster.Master = this;
                    monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
                    monster.Direction = Direction;
                    monster.ActionTime = Envir.Time + 1000;
                    monster.Target = target;

                    if (SummonType == 1)
                        ((Monsters.VampireSpider)monster).AliveTime = Envir.Time + ((magic.Level * 1500) + 15000);
                    if (SummonType == 2)
                        ((Monsters.SpittingToad)monster).AliveTime = Envir.Time + ((magic.Level * 2000) + 25000);
                    if (SummonType == 3)
                        ((Monsters.SnakeTotem)monster).AliveTime = Envir.Time + ((magic.Level * 1500) + 20000);

                    //Pets.Add(monster);

                    DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, location);
                    CurrentMap.ActionList.Add(action);
                    break;
                #endregion

            }


        }
        private void CompleteAttack(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            int damage = (int)data[1];
            DefenceType defence = (DefenceType)data[2];
            bool damageWeapon = (bool)data[3];

            if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

            if (target.Attacked(this, damage, defence, damageWeapon) <= 0) return;

            //Level Fencing / SpiritSword
            foreach (HeroMagic magic in Info.Magics)
            {
                switch (magic.Spell)
                {
                    case Spell.基本剑术:
                    case Spell.精神力战法:
                        LevelMagic(magic);
                        break;
                }
            }
        }
        private void CompleteDamageIndicator(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            DamageType type = (DamageType)data[1];

            if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

            target.BroadcastDamageIndicator(type);
        }
        private void CompletePoison(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            PoisonType pt = (PoisonType)data[1];
            SpellEffect sp = (SpellEffect)data[2];
            int duration = (int)data[3];
            int tickSpeed = (int)data[4];

            if (target == null) return;

            target.ApplyPoison(new Poison { PType = pt, Duration = duration, TickSpeed = tickSpeed }, this);
            target.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = sp });
        }


        public override bool IsAttackTarget(PlayerObject attacker)
        {
            if (attacker == null || attacker.Node == null) return false;
            if (Dead || InSafeZone || attacker.InSafeZone|| attacker.ObjectID == Player.ObjectID ) return false;
            if (CurrentMap.Info.NoFight) return false;

            return Player.IsAttackTarget(attacker);
        }

        public override bool IsAttackTarget(HeroObject attacker)
        {
            if (attacker == null || attacker.Node == null) return false;
            if (Dead || InSafeZone || attacker.InSafeZone || attacker == this) return false;
            if (CurrentMap.Info.NoFight) return false;

            switch (attacker.AMode)
            {
                case AttackMode.All:
                    return true;
                case AttackMode.Group:
                    return Player.GroupMembers == null || !GroupMembers.Contains(attacker.Player);
                case AttackMode.Guild:
                    return Player.MyGuild == null || Player.MyGuild != attacker.Player.MyGuild;
                case AttackMode.EnemyGuild:
                    return Player.MyGuild != null && Player.MyGuild.IsEnemy(attacker.Player.MyGuild);
                case AttackMode.Peace:
                    return false;
                case AttackMode.RedBrown:
                    return PKPoints >= 200 || Envir.Time < BrownTime;
            }

            return true;
        }

        public override bool IsAttackTarget(MonsterObject attacker)
        {
            if (attacker == null || attacker.Node == null) return false;
            if (Dead || attacker.Master == this) return false;
            if (attacker.Info.AI == 6 || attacker.Info.AI == 58) return PKPoints >= 200;
            if (attacker.Master == null) return true;
            if (InSafeZone || attacker.InSafeZone || attacker.Master.InSafeZone) return false;

            if (LastHitter != attacker.Master && attacker.Master.LastHitter != this)
            {
                bool target = false;

                for (int i = 0; i < attacker.Master.Pets.Count; i++)
                {
                    if (attacker.Master.Pets[i].Target != this) continue;

                    target = true;
                    break;
                }

                if (!target)
                    return false;
            }

            switch (attacker.Master.AMode)
            {
                case AttackMode.All:
                    return true;
                case AttackMode.Group:
                    return GroupMembers == null || !GroupMembers.Contains(attacker.Master);
                case AttackMode.Guild:
                    return true;
                case AttackMode.EnemyGuild:
                    return false;
                case AttackMode.Peace:
                    return false;
                case AttackMode.RedBrown:
                    return PKPoints >= 200 || Envir.Time < BrownTime;
            }

            return true;
        }

        private void SetHP(ushort amount)
        {
            if (HP == amount) return;

            HP = amount <= MaxHP ? amount : MaxHP;

            if (!Dead && HP == 0) Die();

            //HealthChanged = true;
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.HeroInfos.Attach(Info);
                    ctx.Entry(Info).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }
            //Player.Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }
        private void SetMP(ushort amount)
        {
            if (MP == amount) return;
            //was info.MP
            MP = amount <= MaxMP ? amount : MaxMP;

            // HealthChanged = true;
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.HeroInfos.Attach(Info);
                    ctx.Entry(Info).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }
            //Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }

        public void ChangeHP(int amount)
        {
            //if (amount < 0) amount = (int)(amount * PoisonRate);

            if (HasProtectionRing && MP > 0 && amount < 0)
            {
                ChangeMP(amount);
                return;
            }

            ushort value = (ushort)Math.Max(ushort.MinValue, Math.Min(MaxHP, HP + amount));

            if (value == HP) return;

            HP = value;

            if (!Dead && HP == 0) Die();
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.HeroInfos.Attach(Info);
                    ctx.Entry(Info).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }
            // HealthChanged = true;
            //Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }
        //use this so you can have mobs take no/reduced poison damage
        public void PoisonDamage(int amount, MapObject Attacker)
        {
            ChangeHP(amount);
        }
        public void ChangeMP(int amount)
        {
            ushort value = (ushort)Math.Max(ushort.MinValue, Math.Min(MaxMP, MP + amount));

            if (value == MP) return;

            MP = value;

            // HealthChanged = true;
            //Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }
        public override void Die()
        {
            if (HasRevivalRing && Envir.Time > LastRevivalTime)
            {
                LastRevivalTime = Envir.Time + 300000;

                for (var i = (int)EquipmentSlot.RingL; i <= (int)EquipmentSlot.RingR; i++)
                {
                    var item = Info.Equipment[i];

                    if (item == null) continue;
                    if (!(item.Info.Unique.HasFlag(SpecialItemMode.Revival)) || item.CurrentDura < 1000) continue;
                    SetHP(MaxHP);
                    item.CurrentDura = (ushort)(item.CurrentDura - 1000);
                    Player.Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });
                    RefreshStats();
                    //ReceiveChat("You have been given a second chance at life", ChatType.System);
                    return;
                }
            }

            if (LastHitter != null && LastHitter.Race == ObjectType.Player)
            {
                PlayerObject hitter = (PlayerObject)LastHitter;

                if (Player.AtWar(hitter) || Player.WarZone)
                {
                    //hitter.ReceiveChat(string.Format("You've been protected by the law"), ChatType.System);
                }
                else if (Envir.Time > BrownTime && PKPoints < 200)
                {
                    UserItem weapon = hitter.Info.Equipment[(byte)EquipmentSlot.Weapon];

                    hitter.PKPoints = Math.Min(int.MaxValue, LastHitter.PKPoints + 100);
                    //hitter.ReceiveChat(string.Format("You have murdered {0}", Name), ChatType.System);
                    //ReceiveChat(string.Format("You have been murdered by {0}", LastHitter.Name), ChatType.System);

                    if (weapon != null && weapon.Luck > (Settings.MaxLuck * -1) && Envir.Random.Next(4) == 0)
                    {
                        weapon.Luck--;
                        hitter.ReceiveChat("Your weapon has been cursed.", ChatType.System);
                        hitter.Enqueue(new S.RefreshItem { Item = weapon });
                    }
                }
            }

            for (int i = Pets.Count - 1; i >= 0; i--)
            {
                if (Pets[i].Dead) continue;
                Pets[i].Die();
            }

            if (MagicShield)
            {
                MagicShield = false;
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.MagicShieldDown }, CurrentLocation);
            }
            if (ElementalBarrier)
            {
                ElementalBarrier = false;
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.ElementalBarrierDown }, CurrentLocation);
            }

            //if (PKPoints > 200)
            //    RedDeathDrop(LastHitter);
            //else if (!InSafeZone)
            //    DeathDrop(LastHitter);

            HP = 0;
            Dead = true;

            LogTime = Envir.Time;
            BrownTime = Envir.Time;

            //Enqueue(new S.Death { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectDied { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].Type == BuffType.Curse)
                {
                    Buffs.RemoveAt(i);
                    break;
                }
            }

            PoisonList.Clear();
            InTrapRock = false;

            Player.CallDefaultNPC(DefaultNPCType.Die);

            Player.Report.Died(CurrentMap.Info.FileName);
        }

        public override int Attacked(PlayerObject attacker, int damage, DefenceType type = DefenceType.ACAgility, bool damageWeapon = true)
        {
            int armour = 0;

            for (int i = 0; i < Buffs.Count; i++)
            {
                switch (Buffs[i].Type)
                {
                    case BuffType.MoonLight:
                    case BuffType.DarkBody:
                        Buffs[i].ExpireTime = 0;
                        break;
                    case BuffType.EnergyShield:
                        int rate = Buffs[i].Values[0];

                        if (Envir.Random.Next(rate) == 0)
                        {
                            if (HP + ((ushort)Buffs[i].Values[1]) >= MaxHP)
                                SetHP(MaxHP);
                            else
                                ChangeHP(Buffs[i].Values[1]);
                        }
                        break;
                }
            }

            switch (type)
            {
                case DefenceType.ACAgility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.AC:
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.MACAgility:
                    if ((Settings.PvpCanResistMagic) && (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist))
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    armour = GetDefencePower(MinMAC, MaxMAC);
                    break;
                case DefenceType.MAC:
                    if ((Settings.PvpCanResistMagic) && (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist))
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    armour = GetDefencePower(MinMAC, MaxMAC);
                    break;
                case DefenceType.Agility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    break;
            }

            armour = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(armour * ArmourRate))));
            damage = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(damage * DamageRate))));

            if (damageWeapon)
                attacker.DamageWeapon();

            damage += attacker.AttackBonus;

            if (Envir.Random.Next(100) < Reflect)
            {
                if (attacker.IsAttackTarget(this))
                {
                    attacker.Attacked(this, damage, type, false);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Reflect }, CurrentLocation);
                }
                return 0;
            }

            if (MagicShield)
                damage -= damage * (MagicShieldLv + 2) / 10;

            if (ElementalBarrier)
                damage -= damage * (ElementalBarrierLv + 1) / 10;

            if (armour >= damage)
            {
                BroadcastDamageIndicator(DamageType.Miss);
                return 0;
            }

            if ((attacker.CriticalRate * Settings.CriticalRateWeight) > Envir.Random.Next(100))
            {
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Critical }, CurrentLocation);
                damage = Math.Min(int.MaxValue, damage + (int)Math.Floor(damage * (((double)attacker.CriticalDamage / (double)Settings.CriticalDamageWeight) * 10)));
                BroadcastDamageIndicator(DamageType.Critical);
            }

            if (MagicShield)
            {
                MagicShieldTime -= (damage - armour) * 60;
                AddBuff(new Buff { Type = BuffType.MagicShield, Caster = this, ExpireTime = MagicShieldTime, Values = new int[] { MagicShieldLv } });
            }

            ElementalBarrierTime -= (damage - armour) * 60;

            if (attacker.LifeOnHit > 0)
                attacker.ChangeHP(attacker.LifeOnHit);

            if (attacker.HpDrainRate > 0)
            {
                attacker.HpDrain += Math.Max(0, ((float)(damage - armour) / 100) * attacker.HpDrainRate);
                if (attacker.HpDrain > 2)
                {
                    int HpGain = (int)Math.Floor(attacker.HpDrain);
                    attacker.ChangeHP(HpGain);
                    attacker.HpDrain -= HpGain;

                }
            }

            for (int i = PoisonList.Count - 1; i >= 0; i--)
            {
                if (PoisonList[i].PType != PoisonType.LRParalysis) continue;

                PoisonList.RemoveAt(i);
                OperateTime = 0;
            }


            LastHitter = attacker;
            LastHitTime = Envir.Time + 10000;
            RegenTime = Envir.Time + RegenDelay;
            LogTime = Envir.Time + Globals.LogDelay;

            if (Envir.Time > BrownTime && PKPoints < 200 && !Player.AtWar(attacker))
                attacker.BrownTime = Envir.Time + Settings.Minute;

            ushort LevelOffset = (byte)(Level > attacker.Level ? 0 : Math.Min(10, attacker.Level - Level));

            if (attacker.HasParalysisRing && type != DefenceType.MAC && type != DefenceType.MACAgility && 1 == Envir.Random.Next(1, 15))
            {
                ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = 5, TickSpeed = 1000 }, attacker);
            }
            if ((attacker.Freezing > 0) && (Settings.PvpCanFreeze) && type != DefenceType.MAC && type != DefenceType.MACAgility)
            {
                if ((Envir.Random.Next(Settings.FreezingAttackWeight) < attacker.Freezing) && (Envir.Random.Next(LevelOffset) == 0))
                    ApplyPoison(new Poison { PType = PoisonType.Slow, Duration = Math.Min(10, (3 + Envir.Random.Next(attacker.Freezing))), TickSpeed = 1000 }, attacker);
            }

            if (attacker.PoisonAttack > 0 && type != DefenceType.MAC && type != DefenceType.MACAgility)
            {
                if ((Envir.Random.Next(Settings.PoisonAttackWeight) < attacker.PoisonAttack) && (Envir.Random.Next(LevelOffset) == 0))
                    ApplyPoison(new Poison { PType = PoisonType.Green, Duration = 5, TickSpeed = 1000, Value = Math.Min(10, 3 + Envir.Random.Next(attacker.PoisonAttack)) }, attacker);
            }

            attacker.GatherElement();

            DamageDura();
            ActiveBlizzard = false;
            ActiveReincarnation = false;

            CounterAttackCast(GetMagic(Spell.天务), LastHitter);

            //Enqueue(new S.Struck { AttackerID = attacker.ObjectID });
            Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = attacker.ObjectID, Direction = Direction, Location = CurrentLocation });

            BroadcastDamageIndicator(DamageType.Hit, armour - damage);

            ChangeHP(armour - damage);
            return damage - armour;
        }

        public override int Attacked(HeroObject attacker, int damage, DefenceType type = DefenceType.ACAgility, bool damageWeapon = true)
        {
            int armour = 0;

            for (int i = 0; i < Buffs.Count; i++)
            {
                switch (Buffs[i].Type)
                {
                    case BuffType.MoonLight:
                    case BuffType.DarkBody:
                        Buffs[i].ExpireTime = 0;
                        break;
                    case BuffType.EnergyShield:
                        int rate = Buffs[i].Values[0];

                        if (Envir.Random.Next(rate) == 0)
                        {
                            if (HP + ((ushort)Buffs[i].Values[1]) >= MaxHP)
                                SetHP(MaxHP);
                            else
                                ChangeHP(Buffs[i].Values[1]);
                        }
                        break;
                }
            }

            switch (type)
            {
                case DefenceType.ACAgility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.AC:
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.MACAgility:
                    if ((Settings.PvpCanResistMagic) && (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist))
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    armour = GetDefencePower(MinMAC, MaxMAC);
                    break;
                case DefenceType.MAC:
                    if ((Settings.PvpCanResistMagic) && (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist))
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    armour = GetDefencePower(MinMAC, MaxMAC);
                    break;
                case DefenceType.Agility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    break;
            }

            armour = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(armour * ArmourRate))));
            damage = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(damage * DamageRate))));

            if (damageWeapon)
                attacker.DamageWeapon();

            damage += attacker.AttackBonus;

            if (Envir.Random.Next(100) < Reflect)
            {
                if (attacker.IsAttackTarget(this))
                {
                    attacker.Attacked(this, damage, type, false);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Reflect }, CurrentLocation);
                }
                return 0;
            }

            if (MagicShield)
                damage -= damage * (MagicShieldLv + 2) / 10;

            if (ElementalBarrier)
                damage -= damage * (ElementalBarrierLv + 1) / 10;

            if (armour >= damage)
            {
                BroadcastDamageIndicator(DamageType.Miss);
                return 0;
            }

            if ((attacker.CriticalRate * Settings.CriticalRateWeight) > Envir.Random.Next(100))
            {
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Critical }, CurrentLocation);
                damage = Math.Min(int.MaxValue, damage + (int)Math.Floor(damage * (((double)attacker.CriticalDamage / (double)Settings.CriticalDamageWeight) * 10)));
                BroadcastDamageIndicator(DamageType.Critical);
            }

            if (MagicShield)
            {
                MagicShieldTime -= (damage - armour) * 60;
                AddBuff(new Buff { Type = BuffType.MagicShield, Caster = this, ExpireTime = MagicShieldTime, Values = new int[] { MagicShieldLv } });
            }

            ElementalBarrierTime -= (damage - armour) * 60;

            if (attacker.LifeOnHit > 0)
                attacker.ChangeHP(attacker.LifeOnHit);

            if (attacker.HpDrainRate > 0)
            {
                attacker.HpDrain += Math.Max(0, ((float)(damage - armour) / 100) * attacker.HpDrainRate);
                if (attacker.HpDrain > 2)
                {
                    int HpGain = (int)Math.Floor(attacker.HpDrain);
                    attacker.ChangeHP(HpGain);
                    attacker.HpDrain -= HpGain;

                }
            }

            for (int i = PoisonList.Count - 1; i >= 0; i--)
            {
                if (PoisonList[i].PType != PoisonType.LRParalysis) continue;

                PoisonList.RemoveAt(i);
                OperateTime = 0;
            }


            LastHitter = attacker;
            LastHitTime = Envir.Time + 10000;
            RegenTime = Envir.Time + RegenDelay;
            LogTime = Envir.Time + Globals.LogDelay;

            if (Envir.Time > BrownTime && PKPoints < 200 && !Player.AtWar(attacker.Player))
                attacker.BrownTime = Envir.Time + Settings.Minute;

            ushort LevelOffset = (byte)(Level > attacker.Level ? 0 : Math.Min(10, attacker.Level - Level));

            if (attacker.HasParalysisRing && type != DefenceType.MAC && type != DefenceType.MACAgility && 1 == Envir.Random.Next(1, 15))
            {
                ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = 5, TickSpeed = 1000 }, attacker);
            }
            if ((attacker.Freezing > 0) && (Settings.PvpCanFreeze) && type != DefenceType.MAC && type != DefenceType.MACAgility)
            {
                if ((Envir.Random.Next(Settings.FreezingAttackWeight) < attacker.Freezing) && (Envir.Random.Next(LevelOffset) == 0))
                    ApplyPoison(new Poison { PType = PoisonType.Slow, Duration = Math.Min(10, (3 + Envir.Random.Next(attacker.Freezing))), TickSpeed = 1000 }, attacker);
            }

            if (attacker.PoisonAttack > 0 && type != DefenceType.MAC && type != DefenceType.MACAgility)
            {
                if ((Envir.Random.Next(Settings.PoisonAttackWeight) < attacker.PoisonAttack) && (Envir.Random.Next(LevelOffset) == 0))
                    ApplyPoison(new Poison { PType = PoisonType.Green, Duration = 5, TickSpeed = 1000, Value = Math.Min(10, 3 + Envir.Random.Next(attacker.PoisonAttack)) }, attacker);
            }

            attacker.GatherElement();

            DamageDura();
            ActiveBlizzard = false;
            ActiveReincarnation = false;

            CounterAttackCast(GetMagic(Spell.天务), LastHitter);

            //Enqueue(new S.Struck { AttackerID = attacker.ObjectID });
            Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = attacker.ObjectID, Direction = Direction, Location = CurrentLocation });

            BroadcastDamageIndicator(DamageType.Hit, armour - damage);

            ChangeHP(armour - damage);
            return damage - armour;
        }

        public override int Attacked(MonsterObject attacker, int damage, DefenceType type = DefenceType.ACAgility)
        {
            int armour = 0;

            for (int i = 0; i < Buffs.Count; i++)
            {
                switch (Buffs[i].Type)
                {
                    case BuffType.MoonLight:
                    case BuffType.DarkBody:
                        Buffs[i].ExpireTime = 0;
                        break;
                    case BuffType.EnergyShield:
                        int rate = Buffs[i].Values[0];

                        if (Envir.Random.Next(rate < 2 ? 2 : rate) == 0)
                        {
                            if (HP + ((ushort)Buffs[i].Values[1]) >= MaxHP)
                                SetHP(MaxHP);
                            else
                                ChangeHP(Buffs[i].Values[1]);
                        }
                        break;
                }
            }

            switch (type)
            {
                case DefenceType.ACAgility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.AC:
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.MACAgility:
                    if (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        return 0;
                    }
                    armour = GetDefencePower(MinMAC, MaxMAC);
                    break;
                case DefenceType.MAC:
                    if (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.Agility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy)
                    {
                        BroadcastDamageIndicator(DamageType.Miss);
                        return 0;
                    }
                    break;
            }

            if (Envir.Random.Next(100) < Reflect)
            {
                if (attacker.IsAttackTarget(this))
                {
                    attacker.Attacked(this, damage, type, false);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Reflect }, CurrentLocation);
                }
                return 0;
            }

            armour = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(armour * ArmourRate))));
            damage = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(damage * DamageRate))));

            if (MagicShield)
                damage -= damage * (MagicShieldLv + 2) / 10;

            if (ElementalBarrier)
                damage -= damage * (ElementalBarrierLv + 1) / 10;

            if (armour >= damage)
            {
                BroadcastDamageIndicator(DamageType.Miss);
                return 0;
            }

            if (MagicShield)
            {
                MagicShieldTime -= (damage - armour) * 60;
                AddBuff(new Buff { Type = BuffType.MagicShield, Caster = this, ExpireTime = MagicShieldTime, Values = new int[] { MagicShieldLv } });
            }

            ElementalBarrierTime -= (damage - armour) * 60;

            for (int i = PoisonList.Count - 1; i >= 0; i--)
            {
                if (PoisonList[i].PType != PoisonType.LRParalysis) continue;

                PoisonList.RemoveAt(i);
                OperateTime = 0;
            }

            LastHitter = attacker.Master ?? attacker;
            LastHitTime = Envir.Time + 10000;
            RegenTime = Envir.Time + RegenDelay;
            LogTime = Envir.Time + Globals.LogDelay;

            DamageDura();
            ActiveBlizzard = false;
            ActiveReincarnation = false;

            CounterAttackCast(GetMagic(Spell.天务), LastHitter);

            if (StruckTime < Envir.Time)
            {
                //Enqueue(new S.Struck { AttackerID = attacker.ObjectID });
                Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = attacker.ObjectID, Direction = Direction, Location = CurrentLocation });
                StruckTime = Envir.Time + 500;
            }

            BroadcastDamageIndicator(DamageType.Hit, armour - damage);

            ChangeHP(armour - damage);
            return damage - armour;
        }

        public override int Struck(int damage, DefenceType type = DefenceType.ACAgility)
        {
            int armour = 0;
            if (Hidden)
            {
                for (int i = 0; i < Buffs.Count; i++)
                {
                    switch (Buffs[i].Type)
                    {
                        case BuffType.MoonLight:
                        case BuffType.DarkBody:
                            Buffs[i].ExpireTime = 0;
                            break;
                    }
                }
            }

            switch (type)
            {
                case DefenceType.ACAgility:
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.AC:
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.MACAgility:
                    armour = GetDefencePower(MinMAC, MaxMAC);
                    break;
                case DefenceType.MAC:
                    armour = GetDefencePower(MinAC, MaxAC);
                    break;
                case DefenceType.Agility:
                    break;
            }

            armour = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(armour * ArmourRate))));
            damage = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(damage * DamageRate))));

            if (MagicShield)
                damage -= damage * (MagicShieldLv + 2) / 10;

            if (ElementalBarrier)
                damage -= damage * (ElementalBarrierLv + 1) / 10;

            if (armour >= damage) return 0;

            if (MagicShield)
            {
                MagicShieldTime -= (damage - armour) * 60;
                AddBuff(new Buff { Type = BuffType.MagicShield, Caster = this, ExpireTime = MagicShieldTime, Values = new int[] { MagicShieldLv } });
            }

            ElementalBarrierTime -= (damage - armour) * 60;
            RegenTime = Envir.Time + RegenDelay;
            LogTime = Envir.Time + Globals.LogDelay;

            DamageDura();
            ActiveBlizzard = false;
            ActiveReincarnation = false;
            //Enqueue(new S.Struck { AttackerID = 0 });
            Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = 0, Direction = Direction, Location = CurrentLocation });

            ChangeHP(armour - damage);
            return damage - armour;
        }

        public override bool IsFriendlyTarget(HeroObject ally)
        {
            if (ally.Player == this.Player) return true;

            switch (ally.Player.AMode)
            {
                case AttackMode.Group:
                    return GroupMembers != null && GroupMembers.Contains(ally.Player);
                case AttackMode.RedBrown:
                    return PKPoints < 200 & Envir.Time > BrownTime;
                case AttackMode.Guild:
                    return Player.MyGuild != null && Player.MyGuild == ally.Player.MyGuild;
                case AttackMode.EnemyGuild:
                    return true;
            }
            return true;
        }

        public override bool IsFriendlyTarget(PlayerObject ally)
        {
            if (ally == this.Player) return true;

            switch (ally.AMode)
            {
                case AttackMode.Group:
                    return GroupMembers != null && GroupMembers.Contains(ally);
                case AttackMode.RedBrown:
                    return PKPoints < 200 & Envir.Time > BrownTime;
                case AttackMode.Guild:
                    return Player.MyGuild != null && Player.MyGuild == ally.MyGuild;
                case AttackMode.EnemyGuild:
                    return true;
            }
            return true;
        }

        public override bool IsFriendlyTarget(MonsterObject ally)
        {
            if (ally.Race != ObjectType.Monster) return false;
            if (ally.Master == null) return false;

            switch (ally.Master.Race)
            {
                case ObjectType.Player:
                    if (!ally.Master.IsFriendlyTarget(Player)) return false;
                    break;
                case ObjectType.Monster:
                    return false;
            }

            return true;
        }

        public override void ReceiveChat(string text, ChatType type)
        {
            Player.ReceiveChat(Info.Name + ":" + text,type);
        }

        public override Packet GetInfo()
        {
            return new S.ObjectPlayer
            {
                ObjectID = ObjectID,
                Name = CurrentMap.Info.NoNames ? "?????" : Name,
                NameColour = NameColour,
                GuildName = "",
                GuildRankName = "",
                Class = Class,
                Gender = Gender,
                Level = Level,
                Location = CurrentLocation,
                Direction = Direction,
                Hair = Hair,
                Weapon = Looks_Weapon,
                Armour = Looks_Armour,
                Light = Light,
                Poison = CurrentPoison,
                Dead = Dead,
                Hidden = Hidden,
                Effect = MagicShield ? SpellEffect.MagicShieldUp : ElementalBarrier ? SpellEffect.ElementalBarrierUp : SpellEffect.None,
                WingEffect = Looks_Wings,

                TransformType = TransformType,

                ElementOrbEffect = (uint)GetElementalOrbCount(),
                ElementOrbLvl = (uint)ElementsLevel,
                ElementOrbMax = (uint)Settings.OrbsExpList[Settings.OrbsExpList.Count - 1],

                Buffs = Buffs.Where(d => d.Visible).Select(e => e.Type).ToList(),

                LevelEffects = LevelEffects
            };
        }

        public override void ApplyPoison(Poison p, MapObject Caster = null, bool NoResist = false, bool ignoreDefence = true)
        {
            if ((Caster != null) && (!NoResist))
                if (((Caster.Race != ObjectType.Player) || Settings.PvpCanResistPoison) && (Envir.Random.Next(Settings.PoisonResistWeight) < PoisonResist))
                    return;

            if (!ignoreDefence && (p.PType == PoisonType.Green))
            {
                int armour = GetDefencePower(MinMAC, MaxMAC);

                if (p.Value < armour)
                    p.PType = PoisonType.None;
                else
                    p.Value -= armour;
            }

            if (p.Owner != null && p.Owner.Race == ObjectType.Player && Envir.Time > BrownTime && PKPoints < 200)
                p.Owner.BrownTime = Envir.Time + Settings.Minute;

            if ((p.PType == PoisonType.Green) || (p.PType == PoisonType.Red)) p.Duration = Math.Max(0, p.Duration - PoisonRecovery);
            if (p.Duration == 0) return;
            if (p.PType == PoisonType.None) return;

            for (int i = 0; i < PoisonList.Count; i++)
            {
                if (PoisonList[i].PType != p.PType) continue;
                if ((PoisonList[i].PType == PoisonType.Green) && (PoisonList[i].Value > p.Value)) return;//cant cast weak poison to cancel out strong poison
                if ((PoisonList[i].PType != PoisonType.Green) && ((PoisonList[i].Duration - PoisonList[i].Time) > p.Duration)) return;//cant cast 1 second poison to make a 1minute poison go away!
                if ((PoisonList[i].PType == PoisonType.Frozen) || (PoisonList[i].PType == PoisonType.Slow) || (PoisonList[i].PType == PoisonType.Paralysis) || (PoisonList[i].PType == PoisonType.LRParalysis)) return;//prevents mobs from being perma frozen/slowed
                if (p.PType == PoisonType.DelayedExplosion) return;
                ReceiveChat("You have been poisoned.", ChatType.System2);
                PoisonList[i] = p;
                return;
            }

            if (p.PType == PoisonType.DelayedExplosion)
            {
                ExplosionInflictedTime = Envir.Time + 4000;
                //Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion });
                Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion });
                ReceiveChat("You are a walking explosive.", ChatType.System);
            }
            else
                ReceiveChat("You have been poisoned.", ChatType.System2);

            PoisonList.Add(p);
        }

        public override int Pushed(MapObject pusher, MirDirection dir, int distance)
        {
            throw new NotImplementedException();
        }

        public override void SendHealth(PlayerObject player)
        {
            throw new NotImplementedException();
        }


        #region Refresh Stats

        public void RefreshStats()
        {
            
            RefreshLevelStats();
            RefreshBagWeight();
            RefreshEquipmentStats();
            RefreshItemSetStats();
            RefreshMirSetStats();
            RefreshSkills();
            RefreshBuffs();
            RefreshStatCaps();
            RefreshMountStats();
            RefreshGuildBuffs();

            //Location Stats ?

            if (HP > MaxHP) SetHP(MaxHP);
            if (MP > MaxMP) SetMP(MaxMP);

            AttackSpeed = 1400 - ((ASpeed * 60) + Math.Min(370, (Level * 14)));

            if (AttackSpeed < 550) AttackSpeed = 550;
            if (HasUpdatedBaseStats == false)
            {
                Player.Enqueue(new S.HeroInformation()
                {
                    ObjectID = ObjectID,
                    RealId = Info.Index,
                    Name = Info.Name,
                    NameColour = NameColour,
                    Class = Class,
                    Gender = Gender,
                    Level = Level,
                    Location = CurrentLocation,
                    Direction = Direction,
                    Hair = Hair,
                    HP = HP,
                    MP = MP,
                    Experience = 0,
                    MaxExperience = MaxExperience,
                    LevelEffects = LevelEffects,
                    Magics = new List<ClientMagic>(),
                    MaxHP = MaxHP,
                    MaxMP = MaxMP,
                    MinAC = MinAC,
                    MaxAC = MaxAC,
                    MinMAC = MinMAC,
                    MaxMAC = MaxMAC,
                    MinDC = MinDC,
                    MaxDC = MaxDC,
                    MinMC = MinMC,
                    MaxMC = MaxMC,
                    MinSC = MinSC,
                    MaxSC = MaxSC,
                    Accuracy = Accuracy,
                    Agility = Agility,
                    ASpeed = ASpeed,
                    Luck = Luck,
                    AttackSpeed = AttackSpeed,
                    CurrentHandWeight = CurrentHandWeight,
                    MaxHandWeight = MaxHandWeight,
                    CurrentWearWeight = CurrentWearWeight,
                    MaxWearWeight = MaxWearWeight,
                    CurrentBagWeight = CurrentBagWeight,
                    MaxBagWeight = MaxBagWeight,
                    LifeOnHit = LifeOnHit,
                    MagicResist = MagicResist,
                    PoisonResist = PoisonResist,
                    HealthRecovery = HealthRecovery,
                    SpellRecovery = SpellRecovery,
                    PoisonRecovery = PoisonRecovery,
                    CriticalRate = CriticalRate,
                    CriticalDamage = CriticalDamage,
                    Holy = Holy,
                    Freezing = Freezing,
                    PoisonAttack = PoisonAttack,
                    HpDrainRate = HpDrainRate,
                    Mode = Mode,
                    Inventory = Info.Inventory,
                    Equipment = Info.Equipment
                });
                HasUpdatedBaseStats = true;
            }
            else
            {
                Player.Enqueue(new S.HeroInformation()
                {
                    ObjectID = ObjectID,
                    RealId = Info.Index,
                    Name = Info.Name,
                    NameColour = NameColour,
                    Class = Class,
                    Gender = Gender,
                    Level = Level,
                    Location = CurrentLocation,
                    Direction = Direction,
                    Hair = Hair,
                    HP = HP,
                    MP = MP,
                    Experience = 0,
                    MaxExperience = MaxExperience,
                    LevelEffects = LevelEffects,
                    Magics = new List<ClientMagic>(),
                    MaxHP = MaxHP,
                    MaxMP = MaxMP,
                    MinAC = MinAC,
                    MaxAC = MaxAC,
                    MinMAC = MinMAC,
                    MaxMAC = MaxMAC,
                    MinDC = MinDC,
                    MaxDC = MaxDC,
                    MinMC = MinMC,
                    MaxMC = MaxMC,
                    MinSC = MinSC,
                    MaxSC = MaxSC,
                    Accuracy = Accuracy,
                    Agility = Agility,
                    ASpeed = ASpeed,
                    Luck = Luck,
                    AttackSpeed = AttackSpeed,
                    CurrentHandWeight = CurrentHandWeight,
                    MaxHandWeight = MaxHandWeight,
                    CurrentWearWeight = CurrentWearWeight,
                    MaxWearWeight = MaxWearWeight,
                    CurrentBagWeight = CurrentBagWeight,
                    MaxBagWeight = MaxBagWeight,
                    LifeOnHit = LifeOnHit,
                    MagicResist = MagicResist,
                    PoisonResist = PoisonResist,
                    HealthRecovery = HealthRecovery,
                    SpellRecovery = SpellRecovery,
                    PoisonRecovery = PoisonRecovery,
                    CriticalRate = CriticalRate,
                    CriticalDamage = CriticalDamage,
                    Holy = Holy,
                    Freezing = Freezing,
                    PoisonAttack = PoisonAttack,
                    HpDrainRate = HpDrainRate,
                    Mode = Mode,
                });
            }
        }

        private void RefreshLevelStats()
        {
            MaxExperience = Level < Settings.ExperienceList.Count ? Settings.ExperienceList[Level - 1] : 0;
            MaxHP = 0; MaxMP = 0;
            MinAC = 0; MaxAC = 0;
            MinMAC = 0; MaxMAC = 0;
            MinDC = 0; MaxDC = 0;
            MinMC = 0; MaxMC = 0;
            MinSC = 0; MaxSC = 0;

            Accuracy = Settings.ClassBaseStats[(byte)Class].StartAccuracy;
            Agility = Settings.ClassBaseStats[(byte)Class].StartAgility;
            CriticalRate = Settings.ClassBaseStats[(byte)Class].StartCriticalRate;
            CriticalDamage = Settings.ClassBaseStats[(byte)Class].StartCriticalDamage;
            //Other Stats;
            MaxBagWeight = 0;
            MaxWearWeight = 0;
            MaxHandWeight = 0;
            ASpeed = 0;
            Luck = 0;
            LifeOnHit = 0;
            HpDrainRate = 0;
            Reflect = 0;
            MagicResist = 0;
            PoisonResist = 0;
            HealthRecovery = 0;
            SpellRecovery = 0;
            PoisonRecovery = 0;
            Holy = 0;
            Freezing = 0;
            PoisonAttack = 0;

            ExpRateOffset = 0;
            ItemDropRateOffset = 0;
            MineRate = 0;
            GemRate = 0;
            FishRate = 0;
            CraftRate = 0;
            GoldDropRateOffset = 0;

            AttackBonus = 0;

            MaxHP = (ushort)Math.Min(ushort.MaxValue, 14 + (Level / Settings.ClassBaseStats[(byte)Class].HpGain + Settings.ClassBaseStats[(byte)Class].HpGainRate) * Level);

            MinAC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MinAc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinAc : 0);
            MaxAC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxAc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxAc : 0);
            MinMAC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MinMac > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinMac : 0);
            MaxMAC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxMac > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxMac : 0);
            MinDC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MinDc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinDc : 0);
            MaxDC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxDc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxDc : 0);
            MinMC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MinMc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinMc : 0);
            MaxMC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxMc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxMc : 0);
            MinSC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MinSc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinSc : 0);
            MaxSC = (ushort)Math.Min(ushort.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxSc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxSc : 0);
            CriticalRate = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].CritialRateGain > 0 ? CriticalRate + (Level / Settings.ClassBaseStats[(byte)Class].CritialRateGain) : CriticalRate);
            CriticalDamage = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].CriticalDamageGain > 0 ? CriticalDamage + (Level / Settings.ClassBaseStats[(byte)Class].CriticalDamageGain) : CriticalDamage);

            MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, (50 + Level / Settings.ClassBaseStats[(byte)Class].BagWeightGain * Level));
            MaxWearWeight = (ushort)Math.Min(ushort.MaxValue, 15 + Level / Settings.ClassBaseStats[(byte)Class].WearWeightGain * Level);
            MaxHandWeight = (ushort)Math.Min(ushort.MaxValue, 12 + Level / Settings.ClassBaseStats[(byte)Class].HandWeightGain * Level);
            switch (Class)
            {
                case MirClass.Warrior:
                    MaxHP = (ushort)Math.Min(ushort.MaxValue, 14 + (Level / Settings.ClassBaseStats[(byte)Class].HpGain + Settings.ClassBaseStats[(byte)Class].HpGainRate + Level / 20F) * Level);
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, 11 + (Level * 3.5F) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
                case MirClass.Wizard:
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, 13 + ((Level / 5F + 2F) * 2.2F * Level) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
                case MirClass.Taoist:
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, (13 + Level / 8F * 2.2F * Level) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
                case MirClass.Assassin:
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, (11 + Level * 5F) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
                case MirClass.Archer:
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, (11 + Level * 4F) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
            }

        }

        private void RefreshBagWeight()
        {
            CurrentBagWeight = 0;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null)
                {
                    if (item.Info == null)
                    {
                        using (var ctx = new DataContext())
                        {
                            item.Info = ctx.ItemInfos.FirstOrDefault(info => info.Index == item.ItemIndex);
                        }
                    }
                    CurrentBagWeight = (ushort)Math.Min(ushort.MaxValue, CurrentBagWeight + item.Weight);
                }

            }
        }

        private void RefreshEquipmentStats()
        {
            short OldLooks_Weapon = Looks_Weapon;
            short OldLooks_Armour = Looks_Armour;
            byte OldLooks_Wings = Looks_Wings;
            byte OldLight = Light;

            Looks_Armour = 0;
            Looks_Weapon = -1;
            Looks_Wings = 0;
            Light = 0;
            CurrentWearWeight = 0;
            CurrentHandWeight = 0;

            HasTeleportRing = false;
            HasProtectionRing = false;
            HasRevivalRing = false;
            HasClearRing = false;
            HasMuscleRing = false;
            HasParalysisRing = false;
            HasProbeNecklace = false;
            SkillNeckBoost = 1;
            NoDuraLoss = false;
            FastRun = false;

            var skillsToAdd = new List<string>();
            var skillsToRemove = new List<string> { Settings.HealRing, Settings.FireRing };
            short Macrate = 0, Acrate = 0, HPrate = 0, MPrate = 0;
            ItemSets.Clear();
            MirSet.Clear();

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem temp = Info.Equipment[i];
                if (temp == null) continue;
                if (temp.Info == null)
                {
                    using (var ctx = new DataContext())
                    {
                        temp.Info = ctx.ItemInfos.FirstOrDefault(info => info.Index == temp.ItemIndex);
                    }
                }
                ItemInfo RealItem = Functions.GetRealItem(temp.Info, Info.Level, Info.Class, Envir.ItemInfoList);
                if (RealItem.Type == ItemType.武器 || RealItem.Type == ItemType.火把)
                    CurrentHandWeight = (ushort)Math.Min(byte.MaxValue, CurrentHandWeight + temp.Weight);
                else
                    CurrentWearWeight = (ushort)Math.Min(byte.MaxValue, CurrentWearWeight + temp.Weight);

                if (temp.CurrentDura == 0 && temp.Info.Durability > 0) continue;


                MinAC = (ushort)Math.Min(ushort.MaxValue, MinAC + RealItem.MinAC + temp.Awake.getAC());
                MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + RealItem.MaxAC + temp.AC + temp.Awake.getAC());
                MinMAC = (ushort)Math.Min(ushort.MaxValue, MinMAC + RealItem.MinMAC + temp.Awake.getMAC());
                MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + RealItem.MaxMAC + temp.MAC + temp.Awake.getMAC());

                MinDC = (ushort)Math.Min(ushort.MaxValue, MinDC + RealItem.MinDC + temp.Awake.getDC());
                MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + RealItem.MaxDC + temp.DC + temp.Awake.getDC());
                MinMC = (ushort)Math.Min(ushort.MaxValue, MinMC + RealItem.MinMC + temp.Awake.getMC());
                MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + RealItem.MaxMC + temp.MC + temp.Awake.getMC());
                MinSC = (ushort)Math.Min(ushort.MaxValue, MinSC + RealItem.MinSC + temp.Awake.getSC());
                MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + RealItem.MaxSC + temp.SC + temp.Awake.getSC());

                Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + RealItem.Accuracy + temp.Accuracy);
                Agility = (byte)Math.Min(byte.MaxValue, Agility + RealItem.Agility + temp.Agility);

                MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + RealItem.HP + temp.HP + temp.Awake.getHPMP());
                MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + RealItem.MP + temp.MP + temp.Awake.getHPMP());

                ASpeed = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, ASpeed + temp.AttackSpeed + RealItem.AttackSpeed)));
                Luck = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, Luck + temp.Luck + RealItem.Luck)));

                MaxBagWeight = (ushort)Math.Max(ushort.MinValue, (Math.Min(ushort.MaxValue, MaxBagWeight + RealItem.BagWeight)));
                MaxWearWeight = (ushort)Math.Max(ushort.MinValue, (Math.Min(byte.MaxValue, MaxWearWeight + RealItem.WearWeight)));
                MaxHandWeight = (ushort)Math.Max(ushort.MinValue, (Math.Min(byte.MaxValue, MaxHandWeight + RealItem.HandWeight)));
                HPrate = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, HPrate + RealItem.HPrate));
                MPrate = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, MPrate + RealItem.MPrate));
                Acrate = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Acrate + RealItem.MaxAcRate));
                Macrate = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Macrate + RealItem.MaxMacRate));
                MagicResist = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, MagicResist + temp.MagicResist + RealItem.MagicResist)));
                PoisonResist = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, PoisonResist + temp.PoisonResist + RealItem.PoisonResist)));
                HealthRecovery = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, HealthRecovery + temp.HealthRecovery + RealItem.HealthRecovery)));
                SpellRecovery = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, SpellRecovery + temp.ManaRecovery + RealItem.SpellRecovery)));
                PoisonRecovery = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, PoisonRecovery + temp.PoisonRecovery + RealItem.PoisonRecovery)));
                CriticalRate = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, CriticalRate + temp.CriticalRate + RealItem.CriticalRate)));
                CriticalDamage = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, CriticalDamage + temp.CriticalDamage + RealItem.CriticalDamage)));
                Holy = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, Holy + RealItem.Holy)));
                Freezing = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, Freezing + temp.Freezing + RealItem.Freezing)));
                PoisonAttack = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, PoisonAttack + temp.PoisonAttack + RealItem.PoisonAttack)));
                Reflect = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, Reflect + RealItem.Reflect)));
                HpDrainRate = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, HpDrainRate + RealItem.HpDrainRate)));

                if (RealItem.Light > Light) Light = RealItem.Light;
                if (RealItem.Unique != SpecialItemMode.None)
                {
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Paralize)) HasParalysisRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Teleport)) HasTeleportRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Clearring)) HasClearRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Protection)) HasProtectionRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Revival)) HasRevivalRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Muscle)) HasMuscleRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Flame))
                    {
                        skillsToAdd.Add(Settings.FireRing);
                        skillsToRemove.Remove(Settings.FireRing);
                    }
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Healing))
                    {
                        skillsToAdd.Add(Settings.HealRing);
                        skillsToRemove.Remove(Settings.HealRing);
                    }
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Probe)) HasProbeNecklace = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Skill)) SkillNeckBoost = 3;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.NoDuraLoss)) NoDuraLoss = true;
                }

                if (RealItem.CanFastRun)
                {
                    FastRun = true;
                }

                if (RealItem.Type == ItemType.护甲)
                {
                    Looks_Armour = RealItem.Shape;
                    Looks_Wings = RealItem.Effect;
                }

                if (RealItem.Type == ItemType.武器)
                    Looks_Weapon = RealItem.Shape;

                if (RealItem.Type == ItemType.坐骑)
                {
                    //MountType = RealItem.Shape;
                    //RealItem.Effect;
                }

                if (RealItem.Set == ItemSet.None) continue;

                ItemSets itemSet = ItemSets.Where(set => set.Set == RealItem.Set && !set.Type.Contains(RealItem.Type) && !set.SetComplete).FirstOrDefault();

                if (itemSet != null)
                {
                    itemSet.Type.Add(RealItem.Type);
                    itemSet.Count++;
                }
                else
                {
                    ItemSets.Add(new ItemSets { Count = 1, Set = RealItem.Set, Type = new List<ItemType> { RealItem.Type } });
                }

                //Mir Set
                if (RealItem.Set == ItemSet.Mir)
                {
                    if (!MirSet.Contains((EquipmentSlot)i))
                        MirSet.Add((EquipmentSlot)i);
                }
            }

            MaxHP = (ushort)Math.Min(ushort.MaxValue, (((double)HPrate / 100) + 1) * MaxHP);
            MaxMP = (ushort)Math.Min(ushort.MaxValue, (((double)MPrate / 100) + 1) * MaxMP);
            MaxAC = (ushort)Math.Min(ushort.MaxValue, (((double)Acrate / 100) + 1) * MaxAC);
            MaxMAC = (ushort)Math.Min(ushort.MaxValue, (((double)Macrate / 100) + 1) * MaxMAC);

            AddTempSkills(skillsToAdd);
            RemoveTempSkills(skillsToRemove);

            if (HasMuscleRing)
            {
                MaxBagWeight = (ushort)(MaxBagWeight * 2);
                MaxWearWeight = Math.Min(ushort.MaxValue, (ushort)(MaxWearWeight * 2));
                MaxHandWeight = Math.Min(ushort.MaxValue, (ushort)(MaxHandWeight * 2));
            }
            if ((OldLooks_Armour != Looks_Armour) || (OldLooks_Weapon != Looks_Weapon) || (OldLooks_Wings != Looks_Wings) || (OldLight != Light))
            {
                Broadcast(GetUpdateInfo());

                if ((OldLooks_Weapon == 49 || OldLooks_Weapon == 50) && (Looks_Weapon != 49 && Looks_Weapon != 50))
                {
                    //Enqueue(GetFishInfo());
                }
            }
        }

        private void RefreshItemSetStats()
        {
            foreach (var s in ItemSets)
            {
                if ((s.Set == ItemSet.Smash) && (s.Type.Contains(ItemType.戒指)) && (s.Type.Contains(ItemType.手镯)))
                    ASpeed = (sbyte)Math.Min(sbyte.MaxValue, ASpeed + 2);
                if ((s.Set == ItemSet.Purity) && (s.Type.Contains(ItemType.戒指)) && (s.Type.Contains(ItemType.手镯)))
                    Holy = Math.Min(byte.MaxValue, (byte)(Holy + 3));
                if ((s.Set == ItemSet.HwanDevil) && (s.Type.Contains(ItemType.戒指)) && (s.Type.Contains(ItemType.手镯)))
                {
                    MaxWearWeight = (ushort)Math.Min(ushort.MaxValue, MaxWearWeight + 5);
                    MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 20);
                }

                if (!s.SetComplete) continue;
                switch (s.Set)
                {
                    case ItemSet.Mundane:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 50);
                        break;
                    case ItemSet.NokChi:
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 50);
                        break;
                    case ItemSet.TaoProtect:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 30);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 30);
                        break;
                    case ItemSet.RedOrchid:
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + 2);
                        HpDrainRate = (byte)Math.Min(byte.MaxValue, HpDrainRate + 10);
                        break;
                    case ItemSet.RedFlower:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 50);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP - 25);
                        break;
                    case ItemSet.Smash:
                        MinDC = (ushort)Math.Min(ushort.MaxValue, MinDC + 1);
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 3);
                        ASpeed = (sbyte)Math.Min(sbyte.MaxValue, ASpeed + 2);
                        break;
                    case ItemSet.HwanDevil:
                        MinMC = (ushort)Math.Min(ushort.MaxValue, MinMC + 1);
                        MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 2);
                        MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 20);
                        MaxWearWeight = (ushort)Math.Min(ushort.MaxValue, MaxWearWeight + 5);
                        break;
                    case ItemSet.Purity:
                        MinSC = (ushort)Math.Min(ushort.MaxValue, MinSC + 1);
                        MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 2);
                        Holy = (byte)Math.Min(ushort.MaxValue, Holy + 3);
                        break;
                    case ItemSet.FiveString:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + (((double)MaxHP / 100) * 30));
                        MinAC = (ushort)Math.Min(ushort.MaxValue, MinAC + 2);
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 2);
                        break;
                    case ItemSet.Spirit:
                        MinDC = (ushort)Math.Min(ushort.MaxValue, MinDC + 2);
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 5);
                        ASpeed = (sbyte)Math.Min(sbyte.MaxValue, ASpeed + 2);
                        break;
                    case ItemSet.Bone:
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 2);
                        MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 1);
                        MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 1);
                        break;
                    case ItemSet.Bug:
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 1);
                        MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 1);
                        MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 1);
                        MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + 1);
                        PoisonResist = (byte)Math.Min(byte.MaxValue, PoisonResist + 1);
                        break;
                    case ItemSet.WhiteGold:
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 2);
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 2);
                        break;
                    case ItemSet.WhiteGoldH:
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 3);
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 30);
                        ASpeed = (sbyte)Math.Min(int.MaxValue, ASpeed + 2);
                        break;
                    case ItemSet.RedJade:
                        MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 2);
                        MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + 2);
                        break;
                    case ItemSet.RedJadeH:
                        MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 2);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 40);
                        Agility = (byte)Math.Min(byte.MaxValue, Agility + 2);
                        break;
                    case ItemSet.Nephrite:
                        MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 2);
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 1);
                        MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + 1);
                        break;
                    case ItemSet.NephriteH:
                        MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 2);
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 15);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 20);
                        Holy = (byte)Math.Min(byte.MaxValue, Holy + 1);
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + 1);
                        break;
                    case ItemSet.Whisker1:
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 1);
                        MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 25);
                        break;
                    case ItemSet.Whisker2:
                        MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 1);
                        MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 17);
                        break;
                    case ItemSet.Whisker3:
                        MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 1);
                        MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 17);
                        break;
                    case ItemSet.Whisker4:
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 1);
                        MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 20);
                        break;
                    case ItemSet.Whisker5:
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 1);
                        MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 17);
                        break;
                    case ItemSet.Hyeolryong:
                        MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 2);
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 15);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 20);
                        Holy = (byte)Math.Min(byte.MaxValue, Holy + 1);
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + 1);
                        break;
                    case ItemSet.Monitor:
                        MagicResist = (byte)Math.Min(byte.MaxValue, MagicResist + 1);
                        PoisonResist = (byte)Math.Min(byte.MaxValue, PoisonResist + 1);
                        break;
                    case ItemSet.Oppressive:
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 1);
                        Agility = (byte)Math.Min(byte.MaxValue, Agility + 1);
                        break;
                }
            }
        }

        private void RefreshMirSetStats()
        {
            if (MirSet.Count() == 10)
            {
                MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 1);
                MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + 1);
                MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 70);
                Luck = (sbyte)Math.Min(sbyte.MaxValue, Luck + 2);
                ASpeed = (sbyte)Math.Min(int.MaxValue, ASpeed + 2);
                MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 70);
                MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 80);
                MagicResist = (byte)Math.Min(byte.MaxValue, MagicResist + 6);
                PoisonResist = (byte)Math.Min(byte.MaxValue, PoisonResist + 6);
            }

            if (MirSet.Contains(EquipmentSlot.RingL) && MirSet.Contains(EquipmentSlot.RingR))
            {
                MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + 1);
                MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 1);
            }
            if (MirSet.Contains(EquipmentSlot.BraceletL) && MirSet.Contains(EquipmentSlot.BraceletR))
            {
                MinAC = (ushort)Math.Min(ushort.MaxValue, MinAC + 1);
                MinMAC = (ushort)Math.Min(ushort.MaxValue, MinMAC + 1);
            }
            if ((MirSet.Contains(EquipmentSlot.RingL) | MirSet.Contains(EquipmentSlot.RingR)) && (MirSet.Contains(EquipmentSlot.BraceletL) | MirSet.Contains(EquipmentSlot.BraceletR)) && MirSet.Contains(EquipmentSlot.Necklace))
            {
                MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + 1);
                MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 1);
                MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 30);
                MaxWearWeight = (ushort)Math.Min(ushort.MaxValue, MaxWearWeight + 17);
            }
            if (MirSet.Contains(EquipmentSlot.RingL) && MirSet.Contains(EquipmentSlot.RingR) && MirSet.Contains(EquipmentSlot.BraceletL) && MirSet.Contains(EquipmentSlot.BraceletR) && MirSet.Contains(EquipmentSlot.Necklace))
            {
                MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + 1);
                MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + 1);
                MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + 20);
                MaxWearWeight = (ushort)Math.Min(ushort.MaxValue, MaxWearWeight + 10);
            }
            if (MirSet.Contains(EquipmentSlot.Armour) && MirSet.Contains(EquipmentSlot.Helmet) && MirSet.Contains(EquipmentSlot.Weapon))
            {
                MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 2);
                MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 1);
                MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 1);
                Agility = (byte)Math.Min(byte.MaxValue, Agility + 1);
            }
            if (MirSet.Contains(EquipmentSlot.Armour) && MirSet.Contains(EquipmentSlot.Boots) && MirSet.Contains(EquipmentSlot.Belt))
            {
                MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 1);
                MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 1);
                MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 1);
                MaxHandWeight = (ushort)Math.Min(ushort.MaxValue, MaxHandWeight + 17);
            }
            if (MirSet.Contains(EquipmentSlot.Armour) && MirSet.Contains(EquipmentSlot.Boots) && MirSet.Contains(EquipmentSlot.Belt) && MirSet.Contains(EquipmentSlot.Helmet) && MirSet.Contains(EquipmentSlot.Weapon))
            {
                MinDC = (ushort)Math.Min(ushort.MaxValue, MinDC + 1);
                MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + 1);
                MinMC = (ushort)Math.Min(ushort.MaxValue, MinMC + 1);
                MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + 1);
                MinSC = (ushort)Math.Min(ushort.MaxValue, MinSC + 1);
                MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + 1);
                MaxHandWeight = (ushort)Math.Min(ushort.MaxValue, MaxHandWeight + 17);
            }
        }

        public void RefreshStatCaps()
        {
            MagicResist = Math.Min(Settings.MaxMagicResist, MagicResist);
            PoisonResist = Math.Min(Settings.MaxPoisonResist, PoisonResist);
            CriticalRate = Math.Min(Settings.MaxCriticalRate, CriticalRate);
            CriticalDamage = Math.Min(Settings.MaxCriticalDamage, CriticalDamage);
            Freezing = Math.Min(Settings.MaxFreezing, Freezing);
            PoisonAttack = Math.Min(Settings.MaxPoisonAttack, PoisonAttack);
            HealthRecovery = Math.Min(Settings.MaxHealthRegen, HealthRecovery);
            PoisonRecovery = Math.Min(Settings.MaxPoisonRecovery, PoisonRecovery);
            SpellRecovery = Math.Min(Settings.MaxManaRegen, SpellRecovery);
            HpDrainRate = Math.Min((byte)100, HpDrainRate);
        }

        public void RefreshMountStats()
        {

        }

        private void RefreshSkills()
        {
            for (int i = 0; i < Info.Magics.Count; i++)
            {
                HeroMagic magic = Info.Magics[i];
                switch (magic.Spell)
                {
                    case Spell.基本剑术:
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + magic.Level * 3);
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + (magic.Level + 1) * 3);
                        break;
                    case Spell.绝命剑法:
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + magic.Level);
                        break;
                    case Spell.精神力战法:
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + magic.Level);
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + MaxSC * (magic.Level + 1) * 0.1F);
                        break;
                }
            }
        }
        private void RefreshBuffs()
        {
            short Old_TransformType = TransformType;

            TransformType = -1;

            for (int i = 0; i < Buffs.Count; i++)
            {
                Buff buff = Buffs[i];

                if (buff.Values == null || buff.Values.Length < 1 || buff.Paused) continue;

                switch (buff.Type)
                {
                    case BuffType.Haste:
                    case BuffType.Fury:
                        ASpeed = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, ASpeed + buff.Values[0])));
                        break;
                    case BuffType.ImmortalSkin:
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + buff.Values[0]);
                        MaxDC = (ushort)Math.Max(ushort.MinValue, MaxDC - buff.Values[1]);
                        break;
                    case BuffType.SwiftFeet:
                        ActiveSwiftFeet = true;
                        break;
                    case BuffType.LightBody:
                        Agility = (byte)Math.Min(byte.MaxValue, Agility + buff.Values[0]);
                        break;
                    case BuffType.SoulShield:
                        MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + buff.Values[0]);
                        break;
                    case BuffType.BlessedArmour:
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + buff.Values[0]);
                        break;
                    case BuffType.UltimateEnhancer:
                        if (Class == MirClass.Wizard || Class == MirClass.Archer)
                        {
                            MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + buff.Values[0]);
                        }
                        else if (Class == MirClass.Taoist)
                        {
                            MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + buff.Values[0]);
                        }
                        else
                        {
                            MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + buff.Values[0]);
                        }
                        break;
                    case BuffType.ProtectionField:
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + buff.Values[0]);
                        break;
                    case BuffType.Rage:
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + buff.Values[0]);
                        break;
                    case BuffType.CounterAttack:
                        MinAC = (ushort)Math.Min(ushort.MaxValue, MinAC + buff.Values[0]);
                        MinMAC = (ushort)Math.Min(ushort.MaxValue, MinMAC + buff.Values[0]);
                        MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + buff.Values[0]);
                        MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + buff.Values[0]);
                        break;
                    case BuffType.Curse:

                        ushort rMaxDC = (ushort)(((int)MaxDC / 100) * buff.Values[0]);
                        ushort rMaxMC = (ushort)(((int)MaxMC / 100) * buff.Values[0]);
                        ushort rMaxSC = (ushort)(((int)MaxSC / 100) * buff.Values[0]);
                        byte rASpeed = (byte)(((int)ASpeed / 100) * buff.Values[0]);

                        MaxDC = (ushort)Math.Max(ushort.MinValue, MaxDC - rMaxDC);
                        MaxMC = (ushort)Math.Max(ushort.MinValue, MaxMC - rMaxMC);
                        MaxSC = (ushort)Math.Max(ushort.MinValue, MaxSC - rMaxSC);
                        ASpeed = (sbyte)Math.Min(sbyte.MaxValue, (Math.Max(sbyte.MinValue, ASpeed - rASpeed)));
                        break;
                    case BuffType.MagicBooster:
                        MinMC = (ushort)Math.Min(ushort.MaxValue, MinMC + buff.Values[0]);
                        MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + buff.Values[0]);
                        break;

                    case BuffType.General:
                        ExpRateOffset = (float)Math.Min(float.MaxValue, ExpRateOffset + buff.Values[0]);

                        if (buff.Values.Length > 1)
                            ItemDropRateOffset = (float)Math.Min(float.MaxValue, ItemDropRateOffset + buff.Values[1]);
                        if (buff.Values.Length > 2)
                            GoldDropRateOffset = (float)Math.Min(float.MaxValue, GoldDropRateOffset + buff.Values[2]);
                        break;
                    case BuffType.Rested:
                    case BuffType.Exp:
                        ExpRateOffset = (float)Math.Min(float.MaxValue, ExpRateOffset + buff.Values[0]);
                        break;
                    case BuffType.Drop:
                        ItemDropRateOffset = (float)Math.Min(float.MaxValue, ItemDropRateOffset + buff.Values[0]);
                        break;
                    case BuffType.Gold:
                        GoldDropRateOffset = (float)Math.Min(float.MaxValue, GoldDropRateOffset + buff.Values[0]);
                        break;
                    case BuffType.Knapsack:
                    case BuffType.BagWeight:
                        MaxBagWeight = (ushort)Math.Min(ushort.MaxValue, MaxBagWeight + buff.Values[0]);
                        break;
                    case BuffType.Transform:
                        TransformType = (short)buff.Values[0];
                        break;

                    case BuffType.Impact:
                        MaxDC = (ushort)Math.Min(ushort.MaxValue, MaxDC + buff.Values[0]);
                        break;
                    case BuffType.Magic:
                        MaxMC = (ushort)Math.Min(ushort.MaxValue, MaxMC + buff.Values[0]);
                        break;
                    case BuffType.Taoist:
                        MaxSC = (ushort)Math.Min(ushort.MaxValue, MaxSC + buff.Values[0]);
                        break;
                    case BuffType.Storm:
                        ASpeed = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, ASpeed + buff.Values[0])));
                        break;
                    case BuffType.HealthAid:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + buff.Values[0]);
                        break;
                    case BuffType.ManaAid:
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + buff.Values[0]);
                        break;
                    case BuffType.WonderDrug:
                        switch (buff.Values[0])
                        {
                            case 0:
                                ExpRateOffset = (float)Math.Min(float.MaxValue, ExpRateOffset + buff.Values[1]);
                                break;
                            case 1:
                                ItemDropRateOffset = (float)Math.Min(float.MaxValue, ItemDropRateOffset + buff.Values[1]);
                                break;
                            case 2:
                                MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + buff.Values[1]);
                                break;
                            case 3:
                                MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + buff.Values[1]);
                                break;
                            case 4:
                                MinAC = (ushort)Math.Min(ushort.MaxValue, MinAC + buff.Values[1]);
                                MaxAC = (ushort)Math.Min(ushort.MaxValue, MaxAC + buff.Values[1]);
                                break;
                            case 5:
                                MinMAC = (ushort)Math.Min(ushort.MaxValue, MinMAC + buff.Values[1]);
                                MaxMAC = (ushort)Math.Min(ushort.MaxValue, MaxMAC + buff.Values[1]);
                                break;
                            case 6:
                                ASpeed = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, ASpeed + buff.Values[1])));
                                break;
                        }
                        break;
                }
            }

            if (Old_TransformType != TransformType)
            {
                Broadcast(new S.TransformUpdate { ObjectID = ObjectID, TransformType = TransformType });
            }
        }
        public void RefreshGuildBuffs()
        {
            
        }
        #endregion


        #region Elemental System
        private void Concentration(HeroMagic magic)
        {
            int duration = 45 + (15 * magic.Level);
            int count = Buffs.Where(x => x.Type == BuffType.Concentration).ToList().Count();
            if (count > 0) return;

            AddBuff(new Buff { Type = BuffType.Concentration, Caster = this, ExpireTime = Envir.Time + duration * 1000, Values = new int[] { magic.Level } });

            LevelMagic(magic);

            ConcentrateInterruptTime = 0;
            ConcentrateInterrupted = false;
            Concentrating = true;
            UpdateConcentration();//Update & send to client

            OperateTime = 0;
        }
        public void UpdateConcentration()
        {
            //Enqueue(new S.SetConcentration { ObjectID = ObjectID, Enabled = Concentrating, Interrupted = ConcentrateInterrupted });
            Broadcast(new S.SetObjectConcentration { ObjectID = ObjectID, Enabled = Concentrating, Interrupted = ConcentrateInterrupted });
        }
        private bool ElementalShot(MapObject target, HeroMagic magic)
        {
            if (HasElemental)
            {
                if (target == null || !target.IsAttackTarget(this)) return false;
                if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return false;

                int orbPower = GetElementalOrbPower(false);//base power + orbpower

                int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC) + orbPower);
                int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

                DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);
                ActionList.Add(action);
            }
            else
            {
                ObtainElement(true);//gather orb through casting
                LevelMagic(magic);
                return false;
            }
            return true;
        }

        public void GatherElement()
        {
            HeroMagic magic = GetMagic(Spell.气功术);

            if (magic == null) return;

            int MeditationLvl = magic.Level;

            magic = GetMagic(Spell.气流术);

            int ConcentrateLvl = magic != null ? magic.Level : -1;

            int MeditateChance = 0;
            int ConcentrateChance = 0;

            if (Concentrating && !ConcentrateInterrupted && ConcentrateLvl >= 0)
                ConcentrateChance = 1 + ConcentrateLvl;

            if (MeditationLvl >= 0)
            {
                MeditateChance = (8 - MeditationLvl);
                int rnd = Envir.Random.Next(10);
                if (rnd >= (MeditateChance - ConcentrateChance))
                {
                    ObtainElement(false);
                    LevelMagic(GetMagic(Spell.气功术));
                }
            }
        }
        public void ObtainElement(bool cast)
        {
            int orbType = 0;
            int meditateLevel = 0;

            HeroMagic spell = GetMagic(Spell.气功术);

            if (spell == null)
            {
                ReceiveChat("Skill requires meditation.", ChatType.System);
                return;
            }

            meditateLevel = spell.Level;

            int maxOrbs = (int)Settings.OrbsExpList[Settings.OrbsExpList.Count - 1];

            if (cast)
            {
                ElementsLevel = (int)Settings.OrbsExpList[0];
                orbType = 1;
                if (Settings.GatherOrbsPerLevel)//Meditation Orbs per level
                    if (meditateLevel == 3)
                    {
                        //Enqueue(new S.SetElemental { ObjectID = ObjectID, Enabled = true, Value = (uint)Settings.OrbsExpList[0], ElementType = 1, ExpLast = (uint)maxOrbs });
                        Broadcast(new S.SetObjectElemental { ObjectID = ObjectID, Enabled = true, Casted = true, Value = (uint)Settings.OrbsExpList[0], ElementType = 1, ExpLast = (uint)maxOrbs });
                        ElementsLevel = (int)Settings.OrbsExpList[1];
                        orbType = 2;
                    }

                HasElemental = true;
            }
            else
            {
                HasElemental = false;
                ElementsLevel++;

                if (Settings.GatherOrbsPerLevel)//Meditation Orbs per level
                    if (ElementsLevel > Settings.OrbsExpList[GetMagic(Spell.气功术).Level])
                    {
                        HasElemental = true;
                        ElementsLevel = (int)Settings.OrbsExpList[GetMagic(Spell.气功术).Level];
                        return;
                    }

                if (ElementsLevel >= Settings.OrbsExpList[0]) HasElemental = true;
                for (int i = 0; i <= Settings.OrbsExpList.Count - 1; i++)
                {
                    if (Settings.OrbsExpList[i] != ElementsLevel) continue;
                    orbType = i + 1;
                    break;
                }
            }

            //Enqueue(new S.SetElemental { ObjectID = ObjectID, Enabled = HasElemental, Value = (uint)ElementsLevel, ElementType = (uint)orbType, ExpLast = (uint)maxOrbs });
            Broadcast(new S.SetObjectElemental { ObjectID = ObjectID, Enabled = HasElemental, Casted = cast, Value = (uint)ElementsLevel, ElementType = (uint)orbType, ExpLast = (uint)maxOrbs });
        }

        public int GetElementalOrbCount()
        {
            int OrbCount = 0;
            for (int i = Settings.OrbsExpList.Count - 1; i >= 0; i--)
            {
                if (ElementsLevel >= Settings.OrbsExpList[i])
                {
                    OrbCount = i + 1;
                    break;
                }
            }
            return OrbCount;
        }
        public int GetElementalOrbPower(bool defensive)
        {
            if (!HasElemental) return 0;

            if (defensive)
                return (int)Settings.OrbsDefList[GetElementalOrbCount() - 1];

            if (!defensive)
                return (int)Settings.OrbsDmgList[GetElementalOrbCount() - 1];

            return 0;
        }
        #endregion

        #region Wizard Skills
        private bool Fireball(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);

            //if(magic.Info.Spell == Spell.GreatFireBall && magic.Level >= 3 && target.Race == ObjectType.Monster)
            //{
            //    List<MapObject> targets = ((MonsterObject)target).FindAllNearby(3, target.CurrentLocation);

            //    int secondaryTargetCount = targets.Count > 3 ? 3 : targets.Count;

            //    for (int i = 0; i < secondaryTargetCount; i++)
            //    {
            //        if (!target.IsAttackTarget(this)) continue;
            //        DelayedAction action2 = new DelayedAction(DelayedType.Magic, Envir.Time + delay + 200, magic, damage / 2, targets[i]);
            //        ActionList.Add(action2);

            //        Enqueue(new S.Magic { Spell = magic.Info.Spell, TargetID = targets[i].ObjectID, Target = targets[i].CurrentLocation, Cast = true, Level = magic.Level });
            //        Broadcast(new S.ObjectMagic { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = magic.Info.Spell, TargetID = targets[i].ObjectID, Target = targets[i].CurrentLocation, Cast = true, Level = magic.Level });
            //    }
            //}

            ActionList.Add(action);

            return true;
        }
        private void Repulsion(HeroMagic magic)
        {
            bool result = false;
            for (int d = 0; d <= 1; d++)
            {
                for (int y = CurrentLocation.Y - d; y <= CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= CurrentMap.Height) break;

                    for (int x = CurrentLocation.X - d; x <= CurrentLocation.X + d; x += Math.Abs(y - CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= CurrentMap.Width) break;

                        Cell cell = CurrentMap.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; cell.Objects != null && i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player) continue;

                            if (!ob.IsAttackTarget(this) || ob.Level >= Level) continue;

                            if (Envir.Random.Next(20) >= 6 + magic.Level * 3 + Level - ob.Level) continue;

                            int distance = 1 + Math.Max(0, magic.Level - 1) + Envir.Random.Next(2);
                            MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, ob.CurrentLocation);

                            if (ob.Pushed(this, dir, distance) == 0) continue;

                            if (ob.Race == ObjectType.Player)
                                ob.Attacked(this, magic.GetDamage(0), DefenceType.None, false);
                            result = true;
                        }
                    }
                }
            }

            if (result) LevelMagic(magic);
        }
        private void ElectricShock(MonsterObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            if (Envir.Random.Next(4 - magic.Level) > 0)
            {
                if (Envir.Random.Next(2) == 0) LevelMagic(magic);
                return;
            }

            LevelMagic(magic);

            if (target.Master == this)
            {
                target.ShockTime = Envir.Time + (magic.Level * 5 + 10) * 1000;
                target.Target = null;
                return;
            }

            if (Envir.Random.Next(2) > 0)
            {
                target.ShockTime = Envir.Time + (magic.Level * 5 + 10) * 1000;
                target.Target = null;
                return;
            }

            if (target.Level > Level + 2 || !target.Info.CanTame) return;

            if (Envir.Random.Next(Level + 20 + magic.Level * 5) <= target.Level + 10)
            {
                if (Envir.Random.Next(5) > 0 && target.Master == null)
                {
                    target.RageTime = Envir.Time + (Envir.Random.Next(20) + 10) * 1000;
                    target.Target = null;
                }
                return;
            }

            if (Pets.Count(t => !t.Dead) >= magic.Level + 2) return;
            int rate = (int)(target.MaxHP / 100);
            if (rate <= 2) rate = 2;
            else rate *= 2;

            if (Envir.Random.Next(rate) != 0) return;
            //else if (Envir.Random.Next(20) == 0) target.Die();

            if (target.Master != null)
            {
                target.SetHP(target.MaxHP / 10);
                target.Master.Pets.Remove(target);
            }
            else if (target.Respawn != null)
            {
                target.Respawn.Count--;
                Envir.MonsterCount--;
                CurrentMap.MonsterCount--;
                target.Respawn = null;
            }

            target.Master = this;
            //target.HealthChanged = true;
            target.BroadcastHealthChange();
            Pets.Add(target);
            target.Target = null;
            target.RageTime = 0;
            target.ShockTime = 0;
            target.OperateTime = 0;
            target.MaxPetLevel = (byte)(1 + magic.Level * 2);
            //target.TameTime = Envir.Time + (Settings.Minute * 60);

            target.Broadcast(new S.ObjectName { ObjectID = target.ObjectID, Name = target.Name });
        }
        private void HellFire(HeroMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction, 4);
            CurrentMap.ActionList.Add(action);

            if (magic.Level != 3) return;

            MirDirection dir = (MirDirection)(((int)Direction + 1) % 8);
            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, dir, 4);
            CurrentMap.ActionList.Add(action);

            dir = (MirDirection)(((int)Direction - 1 + 8) % 8);
            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, dir, 4);
            CurrentMap.ActionList.Add(action);
        }
        private void ThunderBolt(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            if (target.Undead) damage = (int)(damage * 1.5F);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void Vampirism(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void FireBang(HeroMagic magic, Point location)
        {
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);
            CurrentMap.ActionList.Add(action);
        }
        private void FireWall(HeroMagic magic, Point location)
        {
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);
            CurrentMap.ActionList.Add(action);
        }
        private void Lightning(HeroMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction);
            CurrentMap.ActionList.Add(action);
        }
        private void TurnUndead(MapObject target, HeroMagic magic)
        {
            if (target == null || target.Race != ObjectType.Monster || !target.Undead || !target.IsAttackTarget(this)) return;

            if (Envir.Random.Next(2) + Level - 1 <= target.Level)
            {
                target.Target = this;
                return;
            }

            int dif = Level - target.Level + 15;

            if (Envir.Random.Next(100) >= (magic.Level + 1 << 3) + dif)
            {
                target.Target = this;
                return;
            }

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, target);
            ActionList.Add(action);
        }
        private void FlameDisruptor(MapObject target, HeroMagic magic)
        {
            if (target == null || (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) || !target.IsAttackTarget(this)) return;

            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            if (!target.Undead) damage = (int)(damage * 1.5F);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void ThunderStorm(HeroMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation);
            CurrentMap.ActionList.Add(action);
        }
        private void Mirroring(HeroMagic magic)
        {
            MonsterObject monster;
            DelayedAction action;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.CloneName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front, true);
                CurrentMap.ActionList.Add(action);
                return;
            }

            MonsterInfo info = Envir.GetMonsterInfo(Settings.CloneName);
            if (info == null) return;


            LevelMagic(magic);

            monster = MonsterObject.GetMonster(info);
            monster.Master = this;
            monster.ActionTime = Envir.Time + 1000;
            monster.RefreshNameColour(false);

            Pets.Add(monster);

            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front, false);
            CurrentMap.ActionList.Add(action);
        }
        private void Blizzard(HeroMagic magic, Point location, out bool cast)
        {
            cast = false;

            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);

            ActiveBlizzard = true;
            CurrentMap.ActionList.Add(action);
            cast = true;
        }
        private void MeteorStrike(HeroMagic magic, Point location, out bool cast)
        {
            cast = false;

            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);

            ActiveBlizzard = true;
            CurrentMap.ActionList.Add(action);
            cast = true;
        }

        private void IceThrust(HeroMagic magic)
        {
            int damageBase = GetAttackPower(MinMC, MaxMC);
            if (Envir.Random.Next(100) <= (1 + Luck))
                damageBase += damageBase;
            int damageFinish = magic.GetDamage(damageBase);

            Point location = Functions.PointMove(CurrentLocation, Direction, 1);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 1500, this, magic, location, Direction, damageFinish, (int)(damageFinish * 0.6));

            CurrentMap.ActionList.Add(action);
        }

        private void MagicBooster(HeroMagic magic)
        {
            int bonus = 6 + magic.Level * 6;

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, bonus));
        }

        #endregion

        #region Taoist Skills
        private void Healing(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsFriendlyTarget(this)) return;

            int health = magic.GetDamage(GetAttackPower(MinSC, MaxSC) * 2) + Level;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, health, target);

            ActionList.Add(action);
        }
        private bool Poisoning(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return false;

            UserItem item = GetPoison(1);
            if (item == null) return false;

            int power = magic.GetDamage(GetAttackPower(MinSC, MaxSC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, power, target, item);
            ActionList.Add(action);
            ConsumeItem(item, 1);
            return true;
        }
        private bool SoulFireball(MapObject target, HeroMagic magic, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return false;
            cast = true;

            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int damage = magic.GetDamage(GetAttackPower(MinSC, MaxSC));

            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);

            ActionList.Add(action);
            ConsumeItem(item, 1);

            return true;
        }
        private void SummonSkeleton(HeroMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.SkeletonName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500));
                return;
            }

            if (Pets.Where(x => x.Race == ObjectType.Monster).Count() > 1) return;

            UserItem item = GetAmulet(1);
            if (item == null) return;

            MonsterInfo info = Envir.GetMonsterInfo(Settings.SkeletonName);
            if (info == null) return;


            LevelMagic(magic);
            ConsumeItem(item, 1);

            monster = MonsterObject.GetMonster(info);
            monster.PetLevel = magic.Level;
            monster.Master = this;
            monster.MaxPetLevel = (byte)(4 + magic.Level);
            monster.ActionTime = Envir.Time + 1000;
            monster.RefreshNameColour(false);

            //Pets.Add(monster);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front);
            CurrentMap.ActionList.Add(action);
        }
        private void Purification(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsFriendlyTarget(this)) return;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, target);

            ActionList.Add(action);
        }
        private void SummonShinsu(HeroMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.ShinsuName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500));
                return;
            }

            if (Pets.Where(x => x.Race == ObjectType.Monster).Count() > 1) return;

            UserItem item = GetAmulet(5);
            if (item == null) return;

            MonsterInfo info = Envir.GetMonsterInfo(Settings.ShinsuName);
            if (info == null) return;


            LevelMagic(magic);
            ConsumeItem(item, 5);


            monster = MonsterObject.GetMonster(info);
            monster.PetLevel = magic.Level;
            monster.Master = this;
            monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
            monster.Direction = Direction;
            monster.ActionTime = Envir.Time + 1000;

            //Pets.Add(monster);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front);
            CurrentMap.ActionList.Add(action);
        }
        private void Hiding(HeroMagic magic)
        {
            UserItem item = GetAmulet(1);
            if (item == null) return;

            ConsumeItem(item, 1);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, GetAttackPower(MinSC, MaxSC) + (magic.Level + 1) * 5);
            ActionList.Add(action);

        }
        private void MassHiding(HeroMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, GetAttackPower(MinSC, MaxSC) / 2 + (magic.Level + 1) * 2, location);
            CurrentMap.ActionList.Add(action);
        }
        private void SoulShield(HeroMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, GetAttackPower(MinSC, MaxSC) * 2 + (magic.Level + 1) * 10, location);
            CurrentMap.ActionList.Add(action);

            ConsumeItem(item, 1);
        }
        private void MassHealing(HeroMagic magic, Point location)
        {
            int value = magic.GetDamage(GetAttackPower(MinSC, MaxSC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, location);
            CurrentMap.ActionList.Add(action);
        }
        private void Revelation(MapObject target, HeroMagic magic)
        {
            if (target == null) return;

            int value = GetAttackPower(MinSC, MaxSC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, value, target);

            ActionList.Add(action);
        }
        private void PoisonCloud(HeroMagic magic, Point location, out bool cast)
        {
            cast = false;

            UserItem amulet = GetAmulet(5);
            if (amulet == null) return;

            UserItem poison = GetPoison(5, 1);
            if (poison == null) return;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step
            int damage = magic.GetDamage(GetAttackPower(MinSC, MaxSC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, damage, location, (byte)Envir.Random.Next(PoisonAttack));

            ConsumeItem(amulet, 5);
            ConsumeItem(poison, 5);

            CurrentMap.ActionList.Add(action);
            cast = true;
        }
        private void TrapHexagon(HeroMagic magic, MapObject target, out bool cast)
        {
            cast = false;

            if (target == null || !target.IsAttackTarget(this) || !(target is MonsterObject)) return;
            if (target.Level > Level + 2) return;

            UserItem item = GetAmulet(1);
            Point location = target.CurrentLocation;

            if (item == null) return;

            LevelMagic(magic);
            uint duration = (uint)((magic.Level * 5 + 10) * 1000);
            int value = (int)duration;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, location);
            CurrentMap.ActionList.Add(action);

            ConsumeItem(item, 1);
            cast = true;
        }
        private void Reincarnation(HeroMagic magic, PlayerObject target, out bool cast)
        {
            cast = true;

            
            return;
        }
        private void SummonHolyDeva(HeroMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.AngelName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500));
                return;
            }

            if (Pets.Where(x => x.Race == ObjectType.Monster).Count() > 1) return;

            UserItem item = GetAmulet(2);
            if (item == null) return;


            MonsterInfo info = Envir.GetMonsterInfo(Settings.AngelName);
            if (info == null) return;

            LevelMagic(magic);
            ConsumeItem(item, 2);

            monster = MonsterObject.GetMonster(info);
            monster.PetLevel = magic.Level;
            monster.Master = this;
            monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
            monster.Direction = Direction;
            monster.ActionTime = Envir.Time + 1000;

            //Pets.Add(monster);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 1500, this, magic, monster, Front);
            CurrentMap.ActionList.Add(action);
        }
        private void Hallucination(MapObject target, HeroMagic magic)
        {
            if (target == null || target.Race != ObjectType.Monster || !target.IsAttackTarget(this)) return;

            int damage = 0;
            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, delay, magic, damage, target);

            ActionList.Add(action);
        }
        private void EnergyShield(MapObject target, HeroMagic magic, out bool cast)
        {
            cast = false;

            if (target == null || !target.IsFriendlyTarget(this)) target = this; //offical is only party target

            int duration = 30 + 50 * magic.Level;
            int power = magic.GetPower(GetAttackPower(MinSC, MaxSC));
            int chance = 9 - (Luck / 3 + magic.Level);

            int[] values = { chance < 2 ? 2 : chance, power };

            switch (target.Race)
            {
                case ObjectType.Player:
                    //Only targets
                    if (target.IsFriendlyTarget(this))
                    {
                        target.AddBuff(new Buff { Type = BuffType.EnergyShield, Caster = this, ExpireTime = Envir.Time + duration * 1000, Visible = true, Values = values });
                        target.OperateTime = 0;
                        LevelMagic(magic);
                        cast = true;
                    }
                    break;
            }
        }
        private void UltimateEnhancer(MapObject target, HeroMagic magic, out bool cast)
        {
            cast = false;

            if (target == null || !target.IsFriendlyTarget(this)) return;
            UserItem item = GetAmulet(1);
            if (item == null) return;

            long expiretime = GetAttackPower(MinSC, MaxSC) * 2 + (magic.Level + 1) * 10;
            int value = MaxSC >= 5 ? Math.Min(8, MaxSC / 5) : 1;

            switch (target.Race)
            {
                case ObjectType.Monster:
                case ObjectType.Player:
                    //Only targets
                    if (target.IsFriendlyTarget(this))
                    {
                        target.AddBuff(new Buff { Type = BuffType.UltimateEnhancer, Caster = this, ExpireTime = Envir.Time + expiretime * 1000, Values = new int[] { value } });
                        target.OperateTime = 0;
                        LevelMagic(magic);
                        ConsumeItem(item, 1);
                        cast = true;
                    }
                    break;
            }
        }
        private void Plague(HeroMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step


            PoisonType pType = PoisonType.None;

            UserItem itemp = GetPoison(1, 1);

            if (itemp != null)
                pType = PoisonType.Green;
            else
            {
                itemp = GetPoison(1, 2);

                if (itemp != null)
                    pType = PoisonType.Red;
            }

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, magic.GetDamage(GetAttackPower(MinSC, MaxSC)), location, pType);
            CurrentMap.ActionList.Add(action);

            ConsumeItem(item, 1);
            if (itemp != null) ConsumeItem(itemp, 1);
        }
        private void Curse(HeroMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            ConsumeItem(item, 1);

            if (Envir.Random.Next(10 - ((magic.Level + 1) * 2)) > 2) return;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, magic.GetDamage(GetAttackPower(MinSC, MaxSC)), location, 1 + ((magic.Level + 1) * 2));
            CurrentMap.ActionList.Add(action);

        }


        private void PetEnhancer(MapObject target, HeroMagic magic, out bool cast)
        {
            cast = false;

            if (target == null || target.Race != ObjectType.Monster || !target.IsFriendlyTarget(this)) return;

            int duration = GetAttackPower(MinSC, MaxSC) + magic.GetPower();

            cast = true;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, duration, target);

            ActionList.Add(action);
        }
        #endregion

        #region Warrior Skills
        private void Entrapment(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = 0;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void BladeAvalanche(HeroMagic magic)
        {
            int damageBase = GetAttackPower(MinDC, MaxDC);
            if (Envir.Random.Next(0, 100) <= (1 + Luck))
                damageBase += damageBase;//crit should do something like double dmg, not double max dc dmg!
            int damageFinal = magic.GetDamage(damageBase);

            int col = 3;
            int row = 3;

            Point[] loc = new Point[col]; //0 = left 1 = center 2 = right
            loc[0] = Functions.PointMove(CurrentLocation, Functions.PreviousDir(Direction), 1);
            loc[1] = Functions.PointMove(CurrentLocation, Direction, 1);
            loc[2] = Functions.PointMove(CurrentLocation, Functions.NextDir(Direction), 1);

            for (int i = 0; i < col; i++)
            {
                Point startPoint = loc[i];
                for (int j = 0; j < row; j++)
                {
                    Point hitPoint = Functions.PointMove(startPoint, Direction, j);

                    if (!CurrentMap.ValidPoint(hitPoint)) continue;

                    Cell cell = CurrentMap.GetCell(hitPoint);

                    if (cell.Objects == null) continue;

                    for (int k = 0; k < cell.Objects.Count; k++)
                    {
                        MapObject target = cell.Objects[k];
                        switch (target.Race)
                        {
                            case ObjectType.Monster:
                            case ObjectType.Player:
                                //Only targets
                                if (target.IsAttackTarget(this))
                                {
                                    if (target.Attacked(this, j <= 1 ? damageFinal : (int)(damageFinal * 0.6), DefenceType.MAC, false) > 0)
                                        LevelMagic(magic);
                                }
                                break;
                        }
                    }
                }
            }
        }
        private void ProtectionField(HeroMagic magic)
        {
            int count = Buffs.Where(x => x.Type == BuffType.ProtectionField).ToList().Count();
            if (count > 0) return;

            int duration = 45 + (15 * magic.Level);
            int value = (int)Math.Round(MaxAC * (0.2 + (0.03 * magic.Level)));

            AddBuff(new Buff { Type = BuffType.ProtectionField, Caster = this, ExpireTime = Envir.Time + duration * 1000, Values = new int[] { value } });
            OperateTime = 0;
            LevelMagic(magic);
        }
        private void Rage(HeroMagic magic)
        {
            int count = Buffs.Where(x => x.Type == BuffType.Rage).ToList().Count();
            if (count > 0) return;

            int duration = 48 + (6 * magic.Level);
            int value = (int)Math.Round(MaxDC * (0.12 + (0.03 * magic.Level)));

            AddBuff(new Buff { Type = BuffType.Rage, Caster = this, ExpireTime = Envir.Time + duration * 1000, Values = new int[] { value } });
            OperateTime = 0;
            LevelMagic(magic);
        }
        private void ShoulderDash(HeroMagic magic)
        {
            if (InTrapRock) return;
            if (!CanWalk) return;
            ActionTime = Envir.Time + MoveDelay;

            int dist = Envir.Random.Next(2) + magic.Level + 2;
            int travel = 0;
            bool wall = true;
            Point location = CurrentLocation;
            MapObject target = null;
            for (int i = 0; i < dist; i++)
            {
                location = Functions.PointMove(location, Direction, 1);

                if (!CurrentMap.ValidPoint(location)) break;


                Cell cell = CurrentMap.GetCell(location);

                bool blocking = false;
                if (cell.Objects != null)
                {
                    for (int c = cell.Objects.Count - 1; c >= 0; c--)
                    {
                        MapObject ob = cell.Objects[c];
                        if (!ob.Blocking) continue;
                        wall = false;
                        if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player)
                        {
                            blocking = true;
                            break;
                        }

                        if (target == null && ob.Race == ObjectType.Player)
                            target = ob;

                        if (Envir.Random.Next(20) >= 6 + magic.Level * 3 + Level - ob.Level || !ob.IsAttackTarget(this) || ob.Level >= Level || ob.Pushed(this, Direction, 1) == 0)
                        {
                            if (target == ob)
                                target = null;
                            blocking = true;
                            break;
                        }

                        if (cell.Objects == null) break;

                    }
                }

                if (blocking)
                {
                    if (magic.Level != 3) break;

                    Point location2 = Functions.PointMove(location, Direction, 1);

                    if (!CurrentMap.ValidPoint(location2)) break;

                    cell = CurrentMap.GetCell(location2);

                    blocking = false;
                    if (cell.Objects != null)
                    {
                        for (int c = cell.Objects.Count - 1; c >= 0; c--)
                        {
                            MapObject ob = cell.Objects[c];
                            if (!ob.Blocking) continue;
                            if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player)
                            {
                                blocking = true;
                                break;
                            }

                            if (!ob.IsAttackTarget(this) || ob.Level >= Level || ob.Pushed(this, Direction, 1) == 0)
                            {
                                blocking = true;
                                break;
                            }

                            if (cell.Objects == null) break;
                        }
                    }

                    if (blocking) break;

                    cell = CurrentMap.GetCell(location);

                    if (cell.Objects != null)
                    {
                        for (int c = cell.Objects.Count - 1; c >= 0; c--)
                        {
                            MapObject ob = cell.Objects[c];
                            if (!ob.Blocking) continue;
                            if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player)
                            {
                                blocking = true;
                                break;
                            }

                            if (Envir.Random.Next(20) >= 6 + magic.Level * 3 + Level - ob.Level || !ob.IsAttackTarget(this) || ob.Level >= Level || ob.Pushed(this, Direction, 1) == 0)
                            {
                                blocking = true;
                                break;
                            }

                            if (cell.Objects == null) break;
                        }
                    }

                    if (blocking) break;
                }

                travel++;
                CurrentMap.GetCell(CurrentLocation).Remove(this);
                RemoveObjects(Direction, 1);

                CurrentLocation = location;

                //Enqueue(new S.UserDash { Direction = Direction, Location = location });
                Broadcast(new S.ObjectDash { ObjectID = ObjectID, Direction = Direction, Location = location });

                CurrentMap.GetCell(CurrentLocation).Add(this);
                AddObjects(Direction, 1);
            }

            if (travel > 0 && !wall)
            {
                if (target != null) target.Attacked(this, magic.GetDamage(0), DefenceType.None, false);
                LevelMagic(magic);
            }

            if (travel > 0)
            {
                ActionTime = Envir.Time + (travel * MoveDelay);

                Cell cell = CurrentMap.GetCell(CurrentLocation);
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    if (cell.Objects[i].Race != ObjectType.Spell) continue;
                    SpellObject ob = (SpellObject)cell.Objects[i];

                    if (ob.Spell != Spell.火墙 || !IsAttackTarget(ob.Caster)) continue;
                    Attacked(ob.Caster, ob.Value, DefenceType.MAC, false);
                    break;
                }
            }

            if (travel == 0 || wall && dist != travel)
            {
                if (travel > 0)
                {
                    //Enqueue(new S.UserDash { Direction = Direction, Location = Front });
                    Broadcast(new S.ObjectDash { ObjectID = ObjectID, Direction = Direction, Location = Front });

                    SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

                    if (szi != null)
                    {
                        //BindLocation = szi.Location;
                        //BindMapIndex = CurrentMapIndex;
                        InSafeZone = true;
                    }
                    else
                        InSafeZone = false;
                }
                else
                    Broadcast(new S.ObjectDash { ObjectID = ObjectID, Direction = Direction, Location = Front });

                //Enqueue(new S.UserDashFail { Direction = Direction, Location = CurrentLocation });
                Broadcast(new S.ObjectDashFail { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
                ReceiveChat("Not enough pushing Power.", ChatType.System);
            }


            magic.CastTime = Envir.Time;
            _stepCounter = 0;
            //ActionTime = Envir.Time + GetDelayTime(MoveDelay);

            //Player.Enqueue(new S.MagicCast { Spell = magic.Spell });
            Broadcast(new S.ObjectMagic() {Spell = magic.Spell});
            CellTime = Envir.Time + 500;
        }
        private void SlashingBurst(HeroMagic magic, out bool cast)
        {
            cast = true;

            // damage
            int damageBase = GetAttackPower(MinDC, MaxDC);
            int damageFinal = magic.GetDamage(damageBase);

            // objects = this, magic, damage, currentlocation, direction, attackRange
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damageFinal, CurrentLocation, Direction, 1);
            CurrentMap.ActionList.Add(action);

            // telpo location
            Point location = Functions.PointMove(CurrentLocation, Direction, 2);

            if (!CurrentMap.ValidPoint(location)) return;

            Cell cInfo = CurrentMap.GetCell(location);

            bool blocked = false;
            if (cInfo.Objects != null)
            {
                for (int c = 0; c < cInfo.Objects.Count; c++)
                {
                    MapObject ob = cInfo.Objects[c];
                    if (!ob.Blocking) continue;
                    blocked = true;
                    if ((cInfo.Objects == null) || blocked) break;
                }
            }

            // blocked telpo cancel
            if (blocked) return;

            Teleport(CurrentMap, location, false);

            //// move character
            //CurrentMap.GetCell(CurrentLocation).Remove(this);
            //RemoveObjects(Direction, 1);

            //CurrentLocation = location;

            //CurrentMap.GetCell(CurrentLocation).Add(this);
            //AddObjects(Direction, 1);

            //Enqueue(new S.UserAttackMove { Direction = Direction, Location = location });
        }
        private void FurySpell(HeroMagic magic, out bool cast)
        {
            cast = true;

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic));
        }
        private void ImmortalSkin(HeroMagic magic, out bool cast)
        {
            cast = true;

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic));

        }
        private void CounterAttackCast(HeroMagic magic, MapObject target)
        {
            if (target == null || magic == null) return;

            if (CounterAttack == false) return;

            int damageBase = GetAttackPower(MinDC, MaxDC);
            if (Envir.Random.Next(0, 100) <= Accuracy)
                damageBase += damageBase;//crit should do something like double dmg, not double max dc dmg!
            int damageFinal = magic.GetDamage(damageBase);


            MirDirection dir = Functions.ReverseDirection(target.Direction);
            Direction = dir;

            if (Functions.InRange(CurrentLocation, target.CurrentLocation, 1) == false) return;
            if (Envir.Random.Next(10) > magic.Level + 6) return;
            Player.Enqueue(new S.ObjectMagic { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = Spell.天务, TargetID = target.ObjectID, Target = target.CurrentLocation, Cast = true, Level = GetMagic(Spell.天务).Level, SelfBroadcast = true });
            DelayedAction action = new DelayedAction(DelayedType.Damage, AttackTime, target, damageFinal, DefenceType.AC, true);
            ActionList.Add(action);
            LevelMagic(magic);
            CounterAttack = false;
        }
        #endregion

        #region Assassin Skills

        private void HeavenlySword(HeroMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(MinDC, MaxDC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction);
            CurrentMap.ActionList.Add(action);
        }
        private void SwiftFeet(HeroMagic magic, out bool cast)
        {
            cast = true;

            AddBuff(new Buff { Type = BuffType.SwiftFeet, Caster = this, ExpireTime = Envir.Time + 25000 + magic.Level * 5000, Values = new int[] { 1 }, Visible = true });
            LevelMagic(magic);
        }
        private void MoonLight(HeroMagic magic)
        {
            for (int i = 0; i < Buffs.Count; i++)
                if (Buffs[i].Type == BuffType.MoonLight) return;

            AddBuff(new Buff { Type = BuffType.MoonLight, Caster = this, ExpireTime = Envir.Time + (GetAttackPower(MinAC, MaxAC) + (magic.Level + 1) * 5) * 500, Visible = true });
            LevelMagic(magic);
        }
        private void Trap(HeroMagic magic, MapObject target, out bool cast)
        {
            cast = false;

            if (target == null || !target.IsAttackTarget(this) || !(target is MonsterObject)) return;
            if (target.Level >= Level + 2) return;

            Point location = target.CurrentLocation;

            LevelMagic(magic);
            uint duration = 60000;
            int value = (int)duration;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, location);
            CurrentMap.ActionList.Add(action);
            cast = true;
        }
        private bool PoisonSword(HeroMagic magic)
        {
            UserItem item = GetPoison(1);
            if (item == null) return false;

            Point hitPoint;
            Cell cell;
            MirDirection dir = Functions.PreviousDir(Direction);
            int power = magic.GetDamage(GetAttackPower(MinDC, MaxDC));

            for (int i = 0; i < 5; i++)
            {
                hitPoint = Functions.PointMove(CurrentLocation, dir, 1);
                dir = Functions.NextDir(dir);

                if (!CurrentMap.ValidPoint(hitPoint)) continue;
                cell = CurrentMap.GetCell(hitPoint);

                if (cell.Objects == null) continue;

                for (int o = 0; o < cell.Objects.Count; o++)
                {
                    MapObject target = cell.Objects[o];
                    if (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) continue;
                    if (target == null || !target.IsAttackTarget(this) || target.Node == null) continue;

                    target.ApplyPoison(new Poison
                    {
                        Duration = 3 + power / 10 + magic.Level * 3,
                        Owner = this,
                        PType = PoisonType.Green,
                        TickSpeed = 1000,
                        Value = power / 10 + magic.Level + 1 + Envir.Random.Next(PoisonAttack)
                    }, this);

                    target.OperateTime = 0;
                    break;
                }
            }

            LevelMagic(magic);
            ConsumeItem(item, 1);
            return true;
        }
        private void DarkBody(MapObject target, HeroMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.AssassinCloneName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.Die();
                return;
            }

            MonsterInfo info = Envir.GetMonsterInfo(Settings.AssassinCloneName);
            if (info == null) return;

            if (target == null) return;

            LevelMagic(magic);

            monster = MonsterObject.GetMonster(info);
            monster.Master = this;
            monster.Direction = Direction;
            monster.ActionTime = Envir.Time + 500;
            monster.RefreshNameColour(false);
            monster.Target = target;
            Pets.Add(monster);

            monster.Spawn(CurrentMap, CurrentLocation);

            for (int i = 0; i < Buffs.Count; i++)
                if (Buffs[i].Type == BuffType.DarkBody) return;

            AddBuff(new Buff { Type = BuffType.DarkBody, Caster = this, ExpireTime = Envir.Time + (GetAttackPower(MinAC, MaxAC) + (magic.Level + 1) * 5) * 500, Visible = true });
            LevelMagic(magic);
        }
        private void CrescentSlash(HeroMagic magic)
        {
            int damageBase = GetAttackPower(MinDC, MaxDC);
            if (Envir.Random.Next(0, 100) <= Accuracy)
                damageBase += damageBase;//crit should do something like double dmg, not double max dc dmg!
            int damageFinal = magic.GetDamage(damageBase);

            MirDirection backDir = Functions.ReverseDirection(Direction);
            MirDirection preBackDir = Functions.PreviousDir(backDir);
            MirDirection nextBackDir = Functions.NextDir(backDir);

            for (int i = 0; i < 8; i++)
            {
                MirDirection dir = (MirDirection)i;
                Point hitPoint = Functions.PointMove(CurrentLocation, dir, 1);

                if (dir != backDir && dir != preBackDir && dir != nextBackDir)
                {

                    if (!CurrentMap.ValidPoint(hitPoint)) continue;

                    Cell cell = CurrentMap.GetCell(hitPoint);

                    if (cell.Objects == null) continue;


                    for (int j = 0; j < cell.Objects.Count; j++)
                    {
                        MapObject target = cell.Objects[j];
                        switch (target.Race)
                        {
                            case ObjectType.Monster:
                            case ObjectType.Player:
                                //Only targets
                                if (target.IsAttackTarget(this))
                                {
                                    DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + AttackSpeed, target, damageFinal, DefenceType.AC, true);
                                    ActionList.Add(action);
                                }
                                break;
                        }
                    }
                    LevelMagic(magic);
                }
            }
        }

        private void FlashDash(HeroMagic magic)
        {
            bool success = false;
            ActionTime = Envir.Time;

            int travel = 0;
            bool blocked = false;
            int jumpDistance = (magic.Level <= 1) ? 0 : 1;//3 max
            Point location = CurrentLocation;
            for (int i = 0; i < jumpDistance; i++)
            {
                location = Functions.PointMove(location, Direction, 1);
                if (!CurrentMap.ValidPoint(location)) break;

                Cell cInfo = CurrentMap.GetCell(location);
                if (cInfo.Objects != null)
                {
                    for (int c = 0; c < cInfo.Objects.Count; c++)
                    {
                        MapObject ob = cInfo.Objects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.Objects == null) || blocked) break;
                    }
                }
                if (blocked) break;
                travel++;
            }

            jumpDistance = travel;

            if (jumpDistance > 0)
            {
                location = Functions.PointMove(CurrentLocation, Direction, jumpDistance);
                CurrentMap.GetCell(CurrentLocation).Remove(this);
                RemoveObjects(Direction, 1);
                CurrentLocation = location;
                CurrentMap.GetCell(CurrentLocation).Add(this);
                AddObjects(Direction, 1);
                //Enqueue(new S.UserDashAttack { Direction = Direction, Location = location });
                Broadcast(new S.ObjectDashAttack { ObjectID = ObjectID, Direction = Direction, Location = location, Distance = jumpDistance });
            }
            else
            {
                Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
            }

            if (travel == 0) location = CurrentLocation;

            int attackDelay = (AttackSpeed - 120) <= 300 ? 300 : (AttackSpeed - 120);
            AttackTime = Envir.Time + attackDelay;
            SpellTime = Envir.Time + 300;

            location = Functions.PointMove(location, Direction, 1);
            if (CurrentMap.ValidPoint(location))
            {
                Cell cInfo = CurrentMap.GetCell(location);
                if (cInfo.Objects != null)
                {
                    for (int c = 0; c < cInfo.Objects.Count; c++)
                    {
                        MapObject ob = cInfo.Objects[c];
                        switch (ob.Race)
                        {
                            case ObjectType.Monster:
                            case ObjectType.Player:
                                //Only targets
                                if (ob.IsAttackTarget(this))
                                {
                                    DelayedAction action = new DelayedAction(DelayedType.Damage, AttackTime, ob, magic.GetDamage(GetAttackPower(MinDC, MaxDC)), DefenceType.AC, true);
                                    ActionList.Add(action);
                                    success = true;
                                    if ((((ob.Race != ObjectType.Player) || Settings.PvpCanResistPoison) && (Envir.Random.Next(Settings.PoisonAttackWeight) >= ob.PoisonResist)) && (Envir.Random.Next(15) <= magic.Level + 1))
                                    {
                                        DelayedAction pa = new DelayedAction(DelayedType.Poison, AttackTime, ob, PoisonType.Stun, SpellEffect.TwinDrakeBlade, magic.Level + 1, 1000);
                                        ActionList.Add(pa);
                                    }
                                }
                                break;
                        }
                    }
                }
            }
            if (success) //technicaly this makes flashdash lvl when it casts rather then when it hits (it wont lvl if it's not hitting!)
                LevelMagic(magic);

            magic.CastTime = Envir.Time;
            //Player.Enqueue(new S.MagicCast { Spell = magic.Spell });
            Broadcast(new S.ObjectMagic(){Spell = magic.Spell});
        }
        #endregion

        #region Archer Skills

        private int ApplyArcherState(int damage)
        {
            HeroMagic magic = GetMagic(Spell.MentalState);
            if (magic != null)
                LevelMagic(magic);
            int dmgpenalty = 100;
            switch (Info.MentalState)
            {
                case 1: //trickshot
                    dmgpenalty = 55 + (Info.MentalStateLvl * 5);
                    break;
                case 2: //group attack
                    dmgpenalty = 80;
                    break;
            }
            return (damage * dmgpenalty) / 100;
        }

        private bool StraightShot(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return false;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return false;
            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));
            damage = (int)(damage * Math.Max(1, (distance * 0.45)));//range boost
            damage = ApplyArcherState(damage);
            int delay = distance * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);

            ActionList.Add(action);

            return true;
        }
        private bool DoubleShot(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return false;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return false;
            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));
            damage = (int)(damage * Math.Max(1, (distance * 0.25)));//range boost
            damage = ApplyArcherState(damage);
            int delay = distance * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);

            ActionList.Add(action);

            action = new DelayedAction(DelayedType.Magic, Envir.Time + delay + 50, magic, damage, target);

            ActionList.Add(action);

            return true;
        }
        private void BackStep(HeroMagic magic)
        {
            ActionTime = Envir.Time;
            if (!CanWalk) return;

            int travel = 0;
            bool blocked = false;
            int jumpDistance = (magic.Level == 0) ? 1 : magic.Level;//3 max
            MirDirection jumpDir = Functions.ReverseDirection(Direction);
            Point location = CurrentLocation;
            for (int i = 0; i < jumpDistance; i++)
            {
                location = Functions.PointMove(location, jumpDir, 1);
                if (!CurrentMap.ValidPoint(location)) break;

                Cell cInfo = CurrentMap.GetCell(location);
                if (cInfo.Objects != null)
                    for (int c = 0; c < cInfo.Objects.Count; c++)
                    {
                        MapObject ob = cInfo.Objects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.Objects == null) || blocked) break;
                    }
                if (blocked) break;
                travel++;
            }

            jumpDistance = travel;
            if (jumpDistance > 0)
            {
                for (int i = 0; i < jumpDistance; i++)
                {
                    location = Functions.PointMove(CurrentLocation, jumpDir, 1);
                    CurrentMap.GetCell(CurrentLocation).Remove(this);
                    RemoveObjects(jumpDir, 1);
                    CurrentLocation = location;
                    CurrentMap.GetCell(CurrentLocation).Add(this);
                    AddObjects(jumpDir, 1);
                }
                //Enqueue(new S.UserBackStep { Direction = Direction, Location = location });
                Broadcast(new S.ObjectBackStep { ObjectID = ObjectID, Direction = Direction, Location = location, Distance = jumpDistance });
                LevelMagic(magic);
            }
            else
            {
                Broadcast(new S.ObjectBackStep { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Distance = jumpDistance });
                ReceiveChat("Not enough jumping power.", ChatType.System);
            }

            magic.CastTime = Envir.Time;
            //Enqueue(new S.MagicCast { Spell = magic.Spell });
            Broadcast(new S.ObjectMagic(){ Spell = magic.Spell});
            CellTime = Envir.Time + 500;
        }
        private bool DelayedExplosion(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int power = magic.GetDamage(GetAttackPower(MinMC, MaxMC));
            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, power, target);
            ActionList.Add(action);
            return true;
        }
        private void ExplosiveTrap(HeroMagic magic, Point location)
        {
            int trapCount = 0;
            for (int i = 0; i <= 3; i++)
                if (ArcherTrapObjectsArray[i, 0] != null) trapCount++;
            if (trapCount >= magic.Level + 1) return;//max 4 traps

            int freeTrapSpot = -1;
            for (int i = 0; i <= 3; i++)
                if (ArcherTrapObjectsArray[i, 0] == null)
                {
                    freeTrapSpot = i;
                    break;
                }
            if (freeTrapSpot == -1) return;

            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location, freeTrapSpot);
            CurrentMap.ActionList.Add(action);
        }
        public void ExplosiveTrapDetonated(int obIDX, int Trapnr)
        {
            SpellObject ArcherTrap;
            if (ArcherTrapObjectsArray[obIDX, Trapnr] == null) return;
            for (int j = 0; j <= 2; j++)
            {
                if (j != Trapnr)
                {
                    ArcherTrap = (SpellObject)ArcherTrapObjectsArray[obIDX, j];
                    //this should technicaly remove them without explosion but it crashes server so leaving it for now
                    //ArcherTrap.CurrentMap.RemoveObject(ArcherTrap);
                    //ArcherTrap.Despawn();
                    ArcherTrap.DetonateTrapNow();
                }
                ArcherTrapObjectsArray[obIDX, j] = null;
            }
        }
        public void DoKnockback(MapObject target, HeroMagic magic)//ElementalShot - knockback
        {
            Cell cell = CurrentMap.GetCell(target.CurrentLocation);
            if (!cell.Valid || cell.Objects == null) return;

            if (target.CurrentLocation.Y < 0 || target.CurrentLocation.Y >= CurrentMap.Height || target.CurrentLocation.X < 0 || target.CurrentLocation.X >= CurrentMap.Height) return;

            if (target.Race != ObjectType.Monster && target.Race != ObjectType.Player) return;
            if (!target.IsAttackTarget(this) || target.Level >= Level) return;

            if (Envir.Random.Next(20) >= 6 + magic.Level * 3 + ElementsLevel + Level - target.Level) return;
            int distance = 1 + Math.Max(0, magic.Level - 1) + Envir.Random.Next(2);
            MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, target.CurrentLocation);

            target.Pushed(this, dir, distance);
        }
        public void BindingShot(HeroMagic magic, MapObject target, out bool cast)
        {
            cast = false;

            if (target == null || !target.IsAttackTarget(this) || !(target is MonsterObject)) return;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return;
            if (target.Level > Level + 2) return;
            if (((MonsterObject)target).ShockTime >= Envir.Time) return;//Already shocked


            uint duration = (uint)((magic.Level * 5 + 10) * 1000);
            int value = (int)duration;
            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, value, target);
            ActionList.Add(action);

            cast = true;
        }
        public void SpecialArrowShot(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return;
            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));
            if (magic.Spell != Spell.邪爆闪)
                damage = (int)(damage * Math.Max(1, (distance * 0.4)));//range boost
            damage = ApplyArcherState(damage);

            int delay = distance * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);
            ActionList.Add(action);
        }
        public void NapalmShot(MapObject target, HeroMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return;

            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));
            damage = ApplyArcherState(damage);

            int delay = distance * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, damage, target.CurrentLocation);
            CurrentMap.ActionList.Add(action);
        }
        public void ArcherSummon(HeroMagic magic, MapObject target, Point location)
        {
            if (target != null && target.IsAttackTarget(this))
                location = target.CurrentLocation;
            if (!CanFly(location)) return;

            uint duration = (uint)((magic.Level * 5 + 10) * 1000);
            int value = (int)duration;
            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, value, location, target);
            ActionList.Add(action);
        }

        public void OneWithNature(MapObject target, HeroMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(MinMC, MaxMC));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation);
            CurrentMap.ActionList.Add(action);
        }
        #endregion

        #region Custom

        private void Portal(HeroMagic magic, Point location, out bool cast)
        {
            cast = false;

            if (!CurrentMap.ValidPoint(location)) return;

            if (PortalObjectsArray[1] != null && PortalObjectsArray[1].Node != null)
            {
                PortalObjectsArray[0].ExpireTime = 0;
                PortalObjectsArray[0].Process();
            }

            if (!CanFly(location)) return;

            int duration = 30 + (magic.Level * 30);
            int value = duration;
            int passthroughCount = (magic.Level * 2) - 1;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, location, passthroughCount);
            CurrentMap.ActionList.Add(action);
            cast = true;
        }

        #endregion

        private void DamageDura()
        {
            if (!NoDuraLoss)
                for (int i = 0; i < Info.Equipment.Length; i++) DamageItem(Info.Equipment[i], Envir.Random.Next(1) + 1);
        }
        public void DamageWeapon()
        {
            if (!NoDuraLoss)
                DamageItem(Info.Equipment[(int)EquipmentSlot.Weapon], Envir.Random.Next(4) + 1);
        }
        private void DamageItem(UserItem item, int amount, bool isChanged = false)
        {
            if (item != null && item.Info == null)
            {
                using (var ctx = new DataContext())
                {
                    item.Info = ctx.ItemInfos.FirstOrDefault(i => i.Index == item.ItemIndex);
                }
            }
            if (item == null || item.CurrentDura == 0 || item.Info.Type == ItemType.护身符) return;
            //if ((item.WeddingRing == Info.Married) && (Info.Equipment[(int)EquipmentSlot.RingL].UniqueID == item.UniqueID)) return;
            if (item.Info.Strong > 0) amount = Math.Max(1, amount - item.Info.Strong);
            item.CurrentDura = (ushort)Math.Max(ushort.MinValue, item.CurrentDura - amount);
            item.DuraChanged = true;

            if (item.CurrentDura > 0 && isChanged != true) return;
            Player.Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.UserItems.Attach(item);
                    ctx.Entry(item).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }
            item.DuraChanged = false;
            RefreshStats();
        }
        public HeroMagic GetMagic(Spell spell)
        {
            for (int i = 0; i < Info.Magics.Count; i++)
            {
                HeroMagic magic = Info.Magics[i];
                if (magic.Spell != spell) continue;
                return magic;
            }


            return null;
        }

        private void AddTempSkills(IEnumerable<string> skillsToAdd)
        {
            foreach (var skill in skillsToAdd)
            {
                Spell spelltype;
                bool hasSkill = false;

                if (!Enum.TryParse(skill, out spelltype)) return;

                for (var i = Info.Magics.Count - 1; i >= 0; i--)
                    if (Info.Magics[i].Spell == spelltype) hasSkill = true;

                if (hasSkill) continue;

                var magic = new HeroMagic(spelltype) { IsTempSpell = true };
                Info.Magics.Add(magic);
                Player.Enqueue(magic.GetInfo());
            }
        }
        private void RemoveTempSkills(IEnumerable<string> skillsToRemove)
        {
            foreach (var skill in skillsToRemove)
            {
                Spell spelltype;
                if (!Enum.TryParse(skill, out spelltype)) return;

                for (var i = Info.Magics.Count - 1; i >= 0; i--)
                {
                    if (!Info.Magics[i].IsTempSpell || Info.Magics[i].Spell != spelltype) continue;

                    Info.Magics.RemoveAt(i);
                    Player.Enqueue(new S.RemoveMagic { PlaceId = i , IsHeroMagic = true});
                }
            }
        }

        private Packet GetUpdateInfo()
        {
            UpdateConcentration();
            return new S.PlayerUpdate
            {
                ObjectID = ObjectID,
                Weapon = Looks_Weapon,
                Armour = Looks_Armour,
                Light = Light,
                WingEffect = Looks_Wings
            };
        }

        public void LevelMagic(HeroMagic magic)
        {
            byte exp = (byte)(Envir.Random.Next(3) + 1);

            exp *= SkillNeckBoost;

            if (Level == 65535) exp = byte.MaxValue;

            int oldLevel = magic.Level;

            switch (magic.Level)
            {
                case 0:
                    if (Level < magic.Info.Level1)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need1)
                    {
                        magic.Level++;
                        magic.Experience = (ushort)(magic.Experience - magic.Info.Need1);
                        RefreshStats();
                    }
                    break;
                case 1:
                    if (Level < magic.Info.Level2)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need2)
                    {
                        magic.Level++;
                        magic.Experience = (ushort)(magic.Experience - magic.Info.Need2);
                        RefreshStats();
                    }
                    break;
                case 2:
                    if (Level < magic.Info.Level3)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need3)
                    {
                        magic.Level++;
                        magic.Experience = 0;
                        RefreshStats();
                    }
                    break;
                default:
                    return;
            }

            if (oldLevel != magic.Level)
            {
                long delay = magic.GetDelay();
                Player.Enqueue(new S.MagicDelay { Spell = magic.Spell, Delay = delay });
            }
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.HeroMagics.Attach(magic);
                    ctx.Entry(magic).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }
            Player.Enqueue(new S.MagicLeveled { Spell = magic.Spell, Level = magic.Level, Experience = magic.Experience });

        }

        private UserItem GetPoison(int count, byte shape = 0)
        {
            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem item = Info.Equipment[i];
                if (item != null && item.Info == null)
                {
                    using (var ctx = new DataContext())
                    {
                        item.Info = ctx.ItemInfos.FirstOrDefault(info => info.Index == item.ItemIndex);
                    }
                }
                if (item != null && item.Info.Type == ItemType.护身符 && item.Count >= count)
                {
                    if (shape == 0)
                    {
                        if (item.Info.Shape == 1 || item.Info.Shape == 2)
                            return item;
                    }
                    else
                    {
                        if (item.Info.Shape == shape)
                            return item;
                    }
                }
            }

            return null;
        }

        private void ConsumeItem(UserItem item, uint cost)
        {
            item.Count -= cost;
            Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = cost });


            if (item.Count != 0) return;

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                if (Info.Equipment[i] != null && Info.Equipment[i].Slots.Length > 0)
                {
                    for (int j = 0; j < Info.Equipment[i].Slots.Length; j++)
                    {
                        if (Info.Equipment[i].Slots[j] != item) continue;
                        Info.Equipment[i].Slots[j] = null;
                        return;
                    }
                }

                if (Info.Equipment[i] != item) continue;
                Info.Equipment[i] = null;

                return;
            }

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                if (Info.Inventory[i] != item) continue;
                Info.Inventory[i] = null;
                return;
            }
            //Item not found
        }

        private UserItem GetAmulet(int count, int shape = 0)
        {
            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem item = Info.Equipment[i];
                if (item != null && item.Info == null)
                {
                    using (var ctx = new DataContext())
                    {
                        item.Info = ctx.ItemInfos.FirstOrDefault(info => info.Index == item.ItemIndex);
                    }
                }
                if (item != null && item.Info.Type == ItemType.护身符 && item.Info.Shape == shape && item.Count >= count)
                    return item;
            }

            return null;
        }

        public void RemoveObjects(MirDirection dir, int count)
        {
            switch (dir)
            {
                case MirDirection.Up:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpRight:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Right:
                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownRight:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Down:
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownLeft:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Left:
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpLeft:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
            }
        }
        public void AddObjects(MirDirection dir, int count)
        {
            switch (dir)
            {
                case MirDirection.Up:
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpRight:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Right:
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownRight:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Down:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownLeft:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Left:
                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpLeft:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
            }
        }

        private void CheckSneakRadius()
        {
            if (!Sneaking) return;

            for (int y = CurrentLocation.Y - 3; y <= CurrentLocation.Y + 3; y++)
            {
                if (y < 0) continue;
                if (y >= CurrentMap.Height) break;

                for (int x = CurrentLocation.X - 3; x <= CurrentLocation.X + 3; x++)
                {
                    if (x < 0) continue;
                    if (x >= CurrentMap.Width) break;

                    Cell cell = CurrentMap.GetCell(x, y);
                    if (!cell.Valid || cell.Objects == null) continue;

                    for (int i = 0; cell.Objects != null && i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if ((ob.Race != ObjectType.Player) || ob == this) continue;

                        SneakingActive = false;
                        return;
                    }
                }
            }

            SneakingActive = true;
        }

        private void ProcessBuffs()
        {
            bool refresh = false;

            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                Buff buff = Buffs[i];

                if (Envir.Time <= buff.ExpireTime || buff.Infinite || buff.Paused) continue;

                Buffs.RemoveAt(i);
                //Enqueue(new S.RemoveBuff { Type = buff.Type, ObjectID = ObjectID });

                if (buff.Visible)
                    Broadcast(new S.RemoveBuff { Type = buff.Type, ObjectID = ObjectID });

                switch (buff.Type)
                {
                    case BuffType.MoonLight:
                    case BuffType.Hiding:
                    case BuffType.DarkBody:
                        if (!HasClearRing) Hidden = false;
                        Sneaking = false;
                        for (int j = 0; j < Buffs.Count; j++)
                        {
                            switch (Buffs[j].Type)
                            {
                                case BuffType.Hiding:
                                case BuffType.MoonLight:
                                case BuffType.DarkBody:
                                    if (Buffs[j].Type != buff.Type)
                                        Buffs[j].ExpireTime = 0;
                                    break;
                            }
                        }
                        break;
                    case BuffType.Concentration:
                        ConcentrateInterrupted = false;
                        ConcentrateInterruptTime = 0;
                        Concentrating = false;
                        UpdateConcentration();
                        break;
                    case BuffType.SwiftFeet:
                        ActiveSwiftFeet = false;
                        break;
                }

                refresh = true;
            }

            if (Concentrating && !ConcentrateInterrupted && (ConcentrateInterruptTime != 0))
            {
                //check for reenable
                if (ConcentrateInterruptTime <= SMain.Envir.Time)
                {
                    ConcentrateInterruptTime = 0;
                    UpdateConcentration();//Update & send to client
                }
            }

            if (refresh) RefreshStats();
        }
        private void ProcessInfiniteBuffs()
        {
            bool hiding = false;
            bool isGM = false;
            bool mentalState = false;

            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                Buff buff = Buffs[i];

                if (!buff.Infinite) continue;

                bool removeBuff = false;

                switch (buff.Type)
                {
                    case BuffType.Hiding:
                        hiding = true;
                        if (!HasClearRing) removeBuff = true;
                        break;
                    case BuffType.MentalState:
                        mentalState = true;
                        break;
                }

                if (removeBuff)
                {
                    Buffs.RemoveAt(i);
                    //Enqueue(new S.RemoveBuff { Type = buff.Type, ObjectID = ObjectID });

                    switch (buff.Type)
                    {
                        case BuffType.Hiding:
                            Hidden = false;
                            break;
                    }
                }
            }

            if (HasClearRing && !hiding)
            {
                AddBuff(new Buff { Type = BuffType.Hiding, Caster = this, ExpireTime = Envir.Time + 100, Infinite = true });
            }

            if (GetMagic(Spell.MentalState) != null && !mentalState)
            {
                AddBuff(new Buff { Type = BuffType.MentalState, Caster = this, ExpireTime = Envir.Time + 100, Values = new int[] { Info.MentalState }, Infinite = true });
            }
        }
        private void ProcessRegen()
        {
            if (Dead) return;

            int healthRegen = 0, manaRegen = 0;

            if (CanRegen)
            {
                RegenTime = Envir.Time + RegenDelay;


                if (HP < MaxHP)
                {
                    healthRegen += (int)(MaxHP * 0.03F) + 1;
                    healthRegen += (int)(healthRegen * ((double)HealthRecovery / Settings.HealthRegenWeight));
                }

                if (MP < MaxMP)
                {
                    manaRegen += (int)(MaxMP * 0.03F) + 1;
                    manaRegen += (int)(manaRegen * ((double)SpellRecovery / Settings.ManaRegenWeight));
                }
            }

            if (Envir.Time > PotTime)
            {
                //PotTime = Envir.Time + Math.Max(50,Math.Min(PotDelay, 600 - (Level * 10)));
                PotTime = Envir.Time + PotDelay;
                int PerTickRegen = 5 + (Level / 10);

                if (PotHealthAmount > PerTickRegen)
                {
                    healthRegen += PerTickRegen;
                    PotHealthAmount -= (ushort)PerTickRegen;
                }
                else
                {
                    healthRegen += PotHealthAmount;
                    PotHealthAmount = 0;
                }

                if (PotManaAmount > PerTickRegen)
                {
                    manaRegen += PerTickRegen;
                    PotManaAmount -= (ushort)PerTickRegen;
                }
                else
                {
                    manaRegen += PotManaAmount;
                    PotManaAmount = 0;
                }
            }

            if (Envir.Time > HealTime)
            {
                HealTime = Envir.Time + HealDelay;

                int incHeal = (Level / 10) + (HealAmount / 10);
                if (HealAmount > (5 + incHeal))
                {
                    healthRegen += (5 + incHeal);
                    HealAmount -= (ushort)Math.Min(HealAmount, 5 + incHeal);
                }
                else
                {
                    healthRegen += HealAmount;
                    HealAmount = 0;
                }
            }

            if (Envir.Time > VampTime)
            {
                VampTime = Envir.Time + VampDelay;

                if (VampAmount > 10)
                {
                    healthRegen += 10;
                    VampAmount -= 10;
                }
                else
                {
                    healthRegen += VampAmount;
                    VampAmount = 0;
                }
            }

            if (healthRegen > 0) ChangeHP(healthRegen);
            if (HP == MaxHP)
            {
                PotHealthAmount = 0;
                HealAmount = 0;
            }

            if (manaRegen > 0) ChangeMP(manaRegen);
            if (MP == MaxMP) PotManaAmount = 0;
        }
        private void ProcessPoison()
        {
            PoisonType type = PoisonType.None;
            ArmourRate = 1F;
            DamageRate = 1F;

            for (int i = PoisonList.Count - 1; i >= 0; i--)
            {
                if (Dead) return;

                Poison poison = PoisonList[i];

                if (poison.Owner != null && poison.Owner.Node == null)
                {
                    PoisonList.RemoveAt(i);
                    continue;
                }

                if (Envir.Time > poison.TickTime)
                {
                    poison.Time++;
                    poison.TickTime = Envir.Time + poison.TickSpeed;

                    if (poison.Time >= poison.Duration)
                        PoisonList.RemoveAt(i);

                    if (poison.PType == PoisonType.Green || poison.PType == PoisonType.Bleeding)
                    {
                        LastHitter = poison.Owner;
                        LastHitTime = Envir.Time + 10000;

                        if (poison.PType == PoisonType.Bleeding)
                        {
                            Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Bleeding, EffectType = 0 });
                        }

                        //ChangeHP(-poison.Value);
                        PoisonDamage(-poison.Value, poison.Owner);

                        if (Dead) break;
                        RegenTime = Envir.Time + RegenDelay;
                    }

                    if (poison.PType == PoisonType.DelayedExplosion)
                    {
                        if (Envir.Time > ExplosionInflictedTime) ExplosionInflictedStage++;

                        if (!ProcessDelayedExplosion(poison))
                        {
                            if (Dead) break;

                            ExplosionInflictedStage = 0;
                            ExplosionInflictedTime = 0;

                            PoisonList.RemoveAt(i);
                            continue;
                        }
                    }
                }

                switch (poison.PType)
                {
                    case PoisonType.Red:
                        ArmourRate -= 0.10F;
                        break;
                    case PoisonType.Stun:
                        DamageRate += 0.20F;
                        break;
                }
                type |= poison.PType;
                /*
                if ((int)type < (int)poison.PType)
                    type = poison.PType;
                */
            }

            if (type == CurrentPoison) return;

            //Enqueue(new S.Poisoned { Poison = type });
            Broadcast(new S.ObjectPoisoned { ObjectID = ObjectID, Poison = type });

            CurrentPoison = type;
        }
        private bool ProcessDelayedExplosion(Poison poison)
        {
            if (Dead) return false;

            if (ExplosionInflictedStage == 0)
            {
                //Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 0 });
                Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 0 });
                return true;
            }
            if (ExplosionInflictedStage == 1)
            {
                if (Envir.Time > ExplosionInflictedTime)
                    ExplosionInflictedTime = poison.TickTime + 3000;
                //Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 1 });
                Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 1 });
                return true;
            }
            if (ExplosionInflictedStage == 2)
            {
                //Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 2 });
                Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 2 });
                if (poison.Owner != null)
                {
                    switch (poison.Owner.Race)
                    {
                        case ObjectType.Player:
                            PlayerObject caster = (PlayerObject)poison.Owner;
                            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time, poison.Owner, caster.GetMagic(Spell.爆闪), poison.Value, this.CurrentLocation);
                            CurrentMap.ActionList.Add(action);
                            //Attacked((PlayerObject)poison.Owner, poison.Value, DefenceType.MAC, false);
                            break;
                        case ObjectType.Monster://this is in place so it could be used by mobs if one day someone chooses to
                            Attacked((MonsterObject)poison.Owner, poison.Value, DefenceType.MAC);
                            break;

                    }

                    LastHitter = poison.Owner;
                }
                return false;
            }
            return false;
        }

        private void ProcessItems()
        {
            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];

                if (item == null || item.ExpireInfo == null) continue;

                if (DateTime.Now > item.ExpireInfo.ExpiryDate)
                {
                    Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                    using (var ctx = new DataContext())
                    {
                        var dbIvt = ctx.HeroInventoryItems.Skip(i).FirstOrDefault(ivt => ivt.HeroIndex == Info.Index);
                        if (dbIvt != null)
                        {
                            dbIvt.ItemUniqueID = null;
                        }
                        ctx.UserItems.Attach(Info.Inventory[i]);
                        ctx.Entry(Info.Inventory[i] = null).State = EntityState.Deleted;
                        ctx.SaveChanges();
                    }
                    Info.Inventory[i] = null;
                    ReceiveChat(string.Format("{0} has just expired from your inventory.", item.Info.FriendlyName), ChatType.Hint);
                }
            }

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem item = Info.Equipment[i];

                if (item == null || item.ExpireInfo == null) continue;

                if (DateTime.Now > item.ExpireInfo.ExpiryDate)
                {
                    Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                    using (var ctx = new DataContext())
                    {
                        var dbEq = ctx.HeroEquipmentItems.Skip(i).FirstOrDefault(eq => eq.HeroIndex == Info.Index);
                        if (dbEq != null)
                        {
                            dbEq.ItemUniqueID = null;
                        }
                        ctx.UserItems.Attach(Info.Inventory[i]);
                        ctx.Entry(Info.Inventory[i] = null).State = EntityState.Deleted;
                        ctx.SaveChanges();
                    }
                    Info.Equipment[i] = null;
                    ReceiveChat(string.Format("{0} has just expired from your equipment.", item.Info.FriendlyName), ChatType.Hint);
                }
            }
        }

        private void MoveToPlayer()
        {
            if (Player.PMode == PetMode.None) return;
            if (!CanWalk) return;
            if(Target != null) Attack();
            MoveTo(Player.CurrentLocation);
        }

        public void MoveTo(Point point)
        {
            if (Functions.InRange(CurrentLocation, point, 1)) return;
            if (!Functions.InRange(CurrentLocation, point, Globals.DataRange) ||
                CurrentMap != Player.CurrentMap)
            {
                if (Player == null) return;
                if (!Teleport(Player.CurrentMap, Player.Back))
                    Teleport(Player.CurrentMap, point);
                return;
            }
            var dir = Functions.DirectionFromPoint(CurrentLocation, point);
            if (!(Functions.InRange(CurrentLocation, point, 2) ? Walk(dir) : Run(dir)))
            {
                switch (Envir.Random.Next(2)) //No favour
                {
                    case 0:
                        for (int i = 0; i < 7; i++)
                        {
                            dir = Functions.NextDir(dir);

                            if (Walk(dir))
                                return;
                        }
                        break;
                    default:
                        for (int i = 0; i < 7; i++)
                        {
                            dir = Functions.PreviousDir(dir);

                            if (Walk(dir))
                                return;
                        }
                        break;
                }
            }
        }

        public bool Run(MirDirection dir)
        {
            if (!CanMove) return false;

            Point location = Functions.PointMove(CurrentLocation, dir, 2);

            if (!CurrentMap.ValidPoint(location)) return false;

            Cell cell = CurrentMap.GetCell(location);

            if (cell.Objects != null)
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];
                    if (!ob.Blocking || Race == ObjectType.Creature) continue;

                    return false;
                }

            CurrentMap.GetCell(CurrentLocation).Remove(this);

            Direction = dir;
            RemoveObjects(dir, 1);
            CurrentLocation = location;
            CurrentMap.GetCell(CurrentLocation).Add(this);
            AddObjects(dir, 1);

            if (Hidden)
            {
                Hidden = false;

                for (int i = 0; i < Buffs.Count; i++)
                {
                    if (Buffs[i].Type != BuffType.Hiding) continue;

                    Buffs[i].ExpireTime = 0;
                    break;
                }
            }

            CellTime = Envir.Time + 500;
            ActionTime = Envir.Time + MoveDelay;

            InSafeZone = CurrentMap.GetSafeZone(CurrentLocation) != null;

            Broadcast(new S.ObjectRun { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });


            cell = CurrentMap.GetCell(CurrentLocation);

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                if (cell.Objects[i].Race != ObjectType.Spell) continue;
                SpellObject ob = (SpellObject)cell.Objects[i];

                ob.ProcessSpell(this);
                //break;
            }

            return true;
        }

        public bool Walk(MirDirection dir)
        {
            if (!CanMove) return false;

            Point location = Functions.PointMove(CurrentLocation, dir, 1);

            if (!CurrentMap.ValidPoint(location)) return false;

            Cell cell = CurrentMap.GetCell(location);

            if (cell.Objects != null)
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];
                    if (!ob.Blocking || Race == ObjectType.Creature) continue;

                    return false;
                }

            CurrentMap.GetCell(CurrentLocation).Remove(this);

            Direction = dir;
            RemoveObjects(dir, 1);
            CurrentLocation = location;
            CurrentMap.GetCell(CurrentLocation).Add(this);
            AddObjects(dir, 1);

            if (Hidden)
            {
                Hidden = false;

                for (int i = 0; i < Buffs.Count; i++)
                {
                    if (Buffs[i].Type != BuffType.Hiding) continue;

                    Buffs[i].ExpireTime = 0;
                    break;
                }
            }


            CellTime = Envir.Time + 500;
            ActionTime = Envir.Time + MoveDelay;

            InSafeZone = CurrentMap.GetSafeZone(CurrentLocation) != null;

            Broadcast(new S.ObjectWalk { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });


            cell = CurrentMap.GetCell(CurrentLocation);

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                if (cell.Objects[i].Race != ObjectType.Spell) continue;
                SpellObject ob = (SpellObject)cell.Objects[i];

                ob.ProcessSpell(this);
                //break;
            }

            return true;
        }

        private bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            return Target.CurrentLocation != CurrentLocation && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
        }

        private void Attack()
        {
            ShockTime = 0;

            if (!Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            if (!CanAttack) return;
            if (!InAttackRange())
            {
                MoveTo(Target.CurrentLocation);
                return;
            }
            if (Target.Dead)
            {
                FindTarget();
                return;
            }
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;

            int damage = GetAttackPower(MinDC, MaxDC);

            if (damage == 0) return;

            Target.Attacked(this, damage);
        }

        private void FindTarget()
        {
            //if (CurrentMap.Players.Count < 1) return;
            Map Current = CurrentMap;

            for (int d = 0; d <= ViewRange; d++)
            {
                for (int y = CurrentLocation.Y - d; y <= CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= Current.Height) break;

                    for (int x = CurrentLocation.X - d; x <= CurrentLocation.X + d; x += Math.Abs(y - CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= Current.Width) break;
                        Cell cell = Current.Cells[x, y];
                        if (cell.Objects == null || !cell.Valid) continue;
                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            switch (ob.Race)
                            {
                                case ObjectType.Monster:
                                    if (!ob.IsAttackTarget(this)) continue;
                                    if (ob.Hidden && (!CoolEye || Level < ob.Level)) continue;
                                    Target = ob;
                                    return;
                                case ObjectType.Player:
                                    PlayerObject playerob = (PlayerObject)ob;
                                    if (!ob.IsAttackTarget(this)) continue;
                                    if (playerob.GMGameMaster || ob.Hidden && (!CoolEye || Level < ob.Level) || Envir.Time < HallucinationTime) continue;

                                    Target = ob;

                                    if (Master != null)
                                    {
                                        for (int j = 0; j < playerob.Pets.Count; j++)
                                        {
                                            MonsterObject pet = playerob.Pets[j];

                                            if (!pet.IsAttackTarget(this)) continue;
                                            Target = pet;
                                            break;
                                        }
                                    }
                                    return;
                                case ObjectType.Hero:
                                    if (!ob.IsAttackTarget(this)) continue;
                                    Target = ob;
                                    return;
                                default:
                                    continue;
                            }
                        }
                    }
                }
            }
        }

        public void GainExp(uint amount)
        {
            if (!CanGainExp) return;

            if (amount == 0) return;

            if (ExpRateOffset > 0)
                amount += (uint)(amount * (ExpRateOffset / 100));

            Experience += amount;

            //Enqueue(new S.GainExperience { Amount = amount });


            for (int i = 0; i < Pets.Count; i++)
            {
                MonsterObject monster = Pets[i];
                if (monster.CurrentMap == CurrentMap && Functions.InRange(monster.CurrentLocation, CurrentLocation, Globals.DataRange) && !monster.Dead)
                    monster.PetExp(amount);
            }

            if (Experience < MaxExperience) return;
            if (Level >= ushort.MaxValue) return;

            //Calculate increased levels
            var experience = Experience;

            while (experience >= MaxExperience)
            {
                Level++;
                experience -= MaxExperience;

                RefreshLevelStats();

                if (Level >= ushort.MaxValue) break;
            }

            Experience = experience;

            LevelUp();
            if (Settings.UseSQLServer)
            {
                using (var ctx = new DataContext())
                {
                    ctx.HeroInfos.Attach(Info);
                    ctx.Entry(Info).State = EntityState.Modified;
                    ctx.SaveChanges();
                }
            }
            if (Player.IsGM) return;
        }

        public void LevelUp()
        {
            RefreshStats();
            SetHP(MaxHP);
            SetMP(MaxMP);

            //CallDefaultNPC(DefaultNPCType.LevelUp);

            //Enqueue(new S.LevelChanged { Level = Level, Experience = Experience, MaxExperience = MaxExperience });
            Broadcast(new S.ObjectLeveled { ObjectID = ObjectID });

            //Player.Report.Levelled(Level);
        }
    }
}
