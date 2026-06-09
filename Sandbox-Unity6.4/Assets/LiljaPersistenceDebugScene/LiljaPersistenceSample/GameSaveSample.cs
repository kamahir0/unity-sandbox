#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Lilja.Repository;
using static Lilja.Repository.RepositoryOptions;

namespace Persistence.Sample
{
    public readonly struct SaveSlotId
    {
        public SaveSlotId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        [ToPrimitive]
        public string ToPrimitive()
        {
            return Value;
        }

        [FromPrimitive]
        public static SaveSlotId FromPrimitive(string value)
        {
            return new SaveSlotId(value);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct ActorId
    {
        public ActorId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        [ToPrimitive]
        public string ToPrimitive()
        {
            return Value;
        }

        [FromPrimitive]
        public static ActorId FromPrimitive(string value)
        {
            return new ActorId(value);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct SkillId
    {
        public SkillId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        [ToPrimitive]
        public string ToPrimitive()
        {
            return Value;
        }

        [FromPrimitive]
        public static SkillId FromPrimitive(string value)
        {
            return new SkillId(value);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct ItemId
    {
        public ItemId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        [ToPrimitive]
        public string ToPrimitive()
        {
            return Value;
        }

        [FromPrimitive]
        public static ItemId FromPrimitive(string value)
        {
            return new ItemId(value);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    [Entity]
    public partial class Skill
    {
        [Key]
        [Persist(0)]
        public SkillId Id { get; }

        [Persist(1)]
        public string DisplayName { get; private set; }

        [Persist(2)]
        public int Level { get; private set; }

        public Skill(SkillId id, string displayName, int level)
        {
            Id = id;
            DisplayName = displayName;
            Level = level;
        }

        public void LevelUp()
        {
            Level++;
        }
    }

    [Entity]
    public partial class InventoryItem
    {
        [Key]
        [Persist(0)]
        public ItemId Id { get; }

        [Persist(1)]
        public string DisplayName { get; private set; }

        [Persist(2)]
        public int Count { get; private set; }

        public InventoryItem(ItemId id, string displayName, int count)
        {
            Id = id;
            DisplayName = displayName;
            Count = count;
        }

        public void Add(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Count += count;
        }
    }

    [Entity]
    public partial class Actor
    {
        [Key]
        [Persist(0)]
        public ActorId Id { get; }

        [Persist(1)]
        public string DisplayName { get; private set; }

        [Persist(2)]
        public int Level { get; private set; }

        [Persist(3)]
        public int HitPoint { get; private set; }

        [Persist(4)]
        public List<Skill> Skills { get; }

        [Persist(5)]
        public List<InventoryItem> EquippedItems { get; }

        [Persist(6)]
        public Skill MainSkill { get; }

        public Actor(ActorId id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
            Level = 1;
            HitPoint = 100;
            Skills = new List<Skill>();
            EquippedItems = new List<InventoryItem>();
            MainSkill = new Skill(new SkillId("slash"), "Slash", 1);
        }

        public void Damage(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            HitPoint = Math.Max(0, HitPoint - amount);
        }

        public void LevelUp()
        {
            Level++;
            HitPoint += 10;
        }
    }

    [Entity(InMemory | Json | MsgPack)]
    public partial class GameSaveData
    {
        [Persist, Key] private readonly SaveSlotId _slotId;

        [Persist] public List<Actor> Actors { get; }

        [Persist] public List<InventoryItem> Inventory { get; }

        [Persist] public int Chapter { get; set; }

        [Persist] public long Gold { get; set; }

        public GameSaveData(SaveSlotId slotId)
        {
            _slotId = slotId;
            Actors = new List<Actor>();
            Inventory = new List<InventoryItem>();
            Chapter = 1;
            Gold = 0;
        }

        public SaveSlotId SlotId => _slotId;
    }

    public static class GameSaveScenario
    {
        public static GameSaveData CreateNewGame(SaveSlotId slotId)
        {
            var save = new GameSaveData(slotId);

            var hero = new Actor(new ActorId("hero"), "Hero");
            hero.Skills.Upsert(new Skill(new SkillId("slash"), "Slash", 1));
            hero.Skills.Upsert(new Skill(new SkillId("guard"), "Guard", 1));
            hero.EquippedItems.Add(new InventoryItem(new ItemId("bronze-sword"), "Bronze Sword", 1));
            save.Actors.Upsert(hero);

            save.Inventory.Upsert(new InventoryItem(new ItemId("potion"), "Potion", 5));
            save.Gold = 100;

            return save;
        }

        public static void ApplyBattleResult(GameSaveData save)
        {
            var hero = save.Actors.GetById(new ActorId("hero")) ?? throw new InvalidOperationException("Hero actor was not found.");
            hero.Damage(15);
            hero.LevelUp();

            var slash = hero.Skills.GetById(new SkillId("slash")) ?? throw new InvalidOperationException("Slash skill was not found.");
            slash.LevelUp();
            hero.Skills.Upsert(slash);

            save.Actors.Upsert(hero);

            var potion = save.Inventory.GetById(new ItemId("potion")) ?? throw new InvalidOperationException("Potion item was not found.");
            potion.Add(2);
            save.Inventory.Upsert(potion);
        }
    }

    internal static class SampleListExtensions
    {
        public static Skill? GetById(this List<Skill> values, SkillId id)
        {
            return values.FirstOrDefault(value => value.Id.Equals(id));
        }

        public static Actor? GetById(this List<Actor> values, ActorId id)
        {
            return values.FirstOrDefault(value => value.Id.Equals(id));
        }

        public static InventoryItem? GetById(this List<InventoryItem> values, ItemId id)
        {
            return values.FirstOrDefault(value => value.Id.Equals(id));
        }

        public static void Upsert(this List<Skill> values, Skill value)
        {
            var index = values.FindIndex(item => item.Id.Equals(value.Id));
            if (index >= 0) values[index] = value;
            else values.Add(value);
        }

        public static void Upsert(this List<Actor> values, Actor value)
        {
            var index = values.FindIndex(item => item.Id.Equals(value.Id));
            if (index >= 0) values[index] = value;
            else values.Add(value);
        }

        public static void Upsert(this List<InventoryItem> values, InventoryItem value)
        {
            var index = values.FindIndex(item => item.Id.Equals(value.Id));
            if (index >= 0) values[index] = value;
            else values.Add(value);
        }
    }
}
