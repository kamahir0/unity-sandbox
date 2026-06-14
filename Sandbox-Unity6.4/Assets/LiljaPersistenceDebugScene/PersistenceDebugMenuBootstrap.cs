#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Lilja.DebugUI;
using Persistence.Sample;
using Persistence.Sample.Repositories;
using UnityEngine;
using UnityEngine.UIElements;

namespace Persistence.DebugScene
{
    public sealed class PersistenceDebugMenuBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _showOnStart = true;

        private void Start()
        {
            DebugMenu.Initialize(new PersistenceDebugPage());
            DebugMenuOpenButton.Instantiate();

            if (_showOnStart)
            {
                DebugMenu.Show();
            }
        }
    }

    internal sealed class PersistenceDebugPage : DebugPage
    {
        private readonly IGameSaveDataRepository _repository = GameSaveDataRepository.Json.Create();

        private DebugTextField _slotField = null!;
        private DebugLabel _statusLabel = null!;
        private OrderedGroup<SaveRecordView> _recordsGroup = null!;
        private readonly Dictionary<string, SaveRecordView> _recordViews = new();
        private readonly Dictionary<string, int> _recordOrders = new();
        private int _nextRecordOrder;

        public override void Configure(IDebugUIBuilder builder)
        {
            _statusLabel = builder.Label("Ready");

            builder.Foldout("Repository", repository =>
            {
                _slotField = repository.TextField("Slot Id", "slot-a");
                repository.HorizontalScope(row =>
                {
                    row.PrimaryButton("Create", () => CreateAsync().Forget());
                    row.PrimaryButton("Save", () => SaveRepositoryAsync().Forget());
                    row.SecondaryButton("Exists", () => CheckExistsAsync().Forget());
                });

                repository.HorizontalScope(row =>
                {
                    row.PrimaryButton("Load All", () => LoadAllAsync().Forget());
                    row.SecondaryButton("Show Folder", ShowFolder);
                    row.DangerButton("Clear Files", ClearFiles);
                });
            });

            _recordsGroup = builder.FoldoutWithOrder<SaveRecordView>(
                "Records",
                (view, recordBuilder) => view.Build(recordBuilder));
        }

        private async UniTaskVoid CreateAsync()
        {
            try
            {
                var save = GameSaveScenario.CreateNewGame(CurrentSlotId());
                await _repository.LoadAsync();
                _repository.Update(save);
                SyncRecordControls(_repository.All());
                SetStatus("Created new game in repository memory.");
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private async UniTaskVoid SaveRepositoryAsync()
        {
            try
            {
                await _repository.SaveAsync();
                SetStatus("Saved repository.");
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private void ApplyBattleAndSave(GameSaveData save)
        {
            try
            {
                GameSaveScenario.ApplyBattleResult(save);
                save.Gold += 25;
                SaveRecordAsync(save, "Applied battle result and saved.").Forget();
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private async UniTask LoadAllAsync()
        {
            try
            {
                await _repository.LoadAsync();
                var saves = _repository.All();
                SyncRecordControls(saves);
                SetStatus($"All returned {saves.Count} save(s).");
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private async UniTask DeleteSlotAsync(SaveSlotId slotId)
        {
            await _repository.LoadAsync();
            _repository.Delete(slotId);
            await _repository.SaveAsync();

            SetStatus($"Deleted slot: {slotId}");
            await LoadAllAsync();
        }

        private async UniTaskVoid DeleteLoadedSlotAsync(GameSaveData save)
        {
            try
            {
                await DeleteSlotAsync(save.SlotId);
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private async UniTaskVoid CheckExistsAsync()
        {
            try
            {
                await _repository.LoadAsync();
                var exists = _repository.Exists(CurrentSlotId());
                SetStatus(exists ? "Save file exists." : "Save file does not exist.");
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private void ClearFiles()
        {
            try
            {
                var directoryPath = GetSaveDirectoryPath();
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive: true);
                }

                _repository.Clear();
                ClearRecordControls();
                SetStatus("Deleted persistence sample files.");
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private void ShowFolder()
        {
            var directoryPath = GetSaveDirectoryPath();
            Directory.CreateDirectory(directoryPath);
            Application.OpenURL(directoryPath);
            SetStatus(directoryPath);
        }

        private void AddActor(GameSaveData save)
        {
            var index = save.Actors.Count + 1;
            var actor = new Actor(new ActorId($"actor-{index}"), $"Actor {index}");
            save.Actors.Upsert(actor);
            SaveRecordAsync(save, "Added actor.").Forget();
        }

        private void AddInventoryItem(GameSaveData save)
        {
            var index = save.Inventory.Count + 1;
            save.Inventory.Upsert(new InventoryItem(new ItemId($"item-{index}"), $"Item {index}", 1));
            SaveRecordAsync(save, "Added inventory item.").Forget();
        }

        private async UniTaskVoid SaveRecordAsync(GameSaveData save, string status)
        {
            try
            {
                _repository.Update(save);
                await _repository.SaveAsync();
                await _repository.LoadAsync();
                SyncRecordControls(_repository.All());
                SetStatus(status);
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private void SyncRecordControls(IReadOnlyList<GameSaveData> saves)
        {
            if (_recordsGroup is null)
            {
                return;
            }

            var activeSlotIds = new HashSet<string>(saves.Select(save => save.SlotId.Value));
            foreach (var slotId in _recordViews.Keys.Where(slotId => !activeSlotIds.Contains(slotId)).ToArray())
            {
                RemoveRecordControl(slotId);
            }

            foreach (var save in saves.OrderBy(save => save.SlotId.Value))
            {
                var slotId = save.SlotId.Value;
                if (_recordViews.TryGetValue(slotId, out var existingView))
                {
                    existingView.Update(save);
                    continue;
                }

                var view = new SaveRecordView(this, save);
                view.SetHandle(_recordsGroup.Add(GetRecordOrder(slotId), view));
                _recordViews.Add(slotId, view);
            }
        }

        private static void UpsertInventoryItem(GameSaveData save, InventoryItem item, Actor? owner)
        {
            if (owner is null)
            {
                save.Inventory.Upsert(item);
                return;
            }

            owner.EquippedItems.Upsert(item);
            save.Actors.Upsert(owner);
        }

        private static void RemoveInventoryItem(GameSaveData save, InventoryItem item, Actor? owner)
        {
            if (owner is null)
            {
                save.Inventory.Remove(item);
                return;
            }

            owner.EquippedItems.Remove(item);
            save.Actors.Upsert(owner);
        }

        private void ClearRecordControls()
        {
            foreach (var view in _recordViews.Values)
            {
                view.Dispose();
            }

            _recordViews.Clear();
            _recordOrders.Clear();
            _nextRecordOrder = 0;
        }

        private void RemoveRecordControl(string slotId)
        {
            if (!_recordViews.Remove(slotId, out var view))
            {
                return;
            }

            view.Dispose();
        }

        private int GetRecordOrder(string slotId)
        {
            if (_recordOrders.TryGetValue(slotId, out var order))
            {
                return order;
            }

            order = _nextRecordOrder++;
            _recordOrders.Add(slotId, order);
            return order;
        }

        private sealed class SaveRecordView : IDisposable
        {
            private readonly PersistenceDebugPage _page;
            private readonly Dictionary<string, ActorRecordView> _actorViews = new();
            private readonly Dictionary<string, InventoryItemRecordView> _inventoryViews = new();
            private readonly Dictionary<string, int> _actorOrders = new();
            private readonly Dictionary<string, int> _inventoryOrders = new();
            private int _nextActorOrder;
            private int _nextInventoryOrder;
            private GameSaveData _save;
            private IDisposable? _handle;
            private DebugIntegerField? _chapterField;
            private DebugLongField? _goldField;
            private DebugLabel? _actorsEmptyLabel;
            private DebugLabel? _inventoryEmptyLabel;
            private OrderedGroup<ActorRecordView>? _actorsGroup;
            private OrderedGroup<InventoryItemRecordView>? _inventoryGroup;

            public SaveRecordView(PersistenceDebugPage page, GameSaveData save)
            {
                _page = page;
                _save = save;
            }

            public GameSaveData Save => _save;

            public void SetHandle(IDisposable handle)
            {
                _handle = handle;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.Foldout($"Save: {_save.SlotId}", saveBuilder =>
                {
                    _chapterField = saveBuilder.IntegerField("Chapter", _save.Chapter);
                    _goldField = saveBuilder.LongField("Gold", _save.Gold);

                    saveBuilder.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Apply Properties + Save", () =>
                        {
                            _save.Chapter = Math.Max(1, _chapterField.value);
                            _save.Gold = Math.Max(0L, _goldField.value);
                            _page.SaveRecordAsync(_save, "Saved save properties.").Forget();
                        });

                        row.SecondaryButton("+100 Gold", () =>
                        {
                            _save.Gold += 100;
                            _page.SaveRecordAsync(_save, "Added gold.").Forget();
                        });
                    });

                    saveBuilder.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Battle + Save", () => _page.ApplyBattleAndSave(_save));
                        row.SecondaryButton("Add Actor", () => _page.AddActor(_save));
                    });

                    saveBuilder.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Add Inventory", () => _page.AddInventoryItem(_save));
                        row.DangerButton("Delete This Slot", () => _page.DeleteLoadedSlotAsync(_save).Forget());
                    });

                    saveBuilder.Foldout("Actors", actorsBuilder =>
                    {
                        _actorsEmptyLabel = actorsBuilder.Label("No actors.");
                        _actorsGroup = actorsBuilder.OrderedGroup<ActorRecordView>((view, actorBuilder) => view.Build(actorBuilder));
                        SyncActorControls();
                    });

                    saveBuilder.Foldout("Inventory", inventoryBuilder =>
                    {
                        _inventoryEmptyLabel = inventoryBuilder.Label("No inventory items.");
                        _inventoryGroup = inventoryBuilder.OrderedGroup<InventoryItemRecordView>((view, itemBuilder) => view.Build(itemBuilder));
                        SyncInventoryControls();
                    });
                });
            }

            public void Update(GameSaveData save)
            {
                _save = save;
                _chapterField?.SetValueWithoutNotify(save.Chapter);
                _goldField?.SetValueWithoutNotify(save.Gold);
                SyncActorControls();
                SyncInventoryControls();
            }

            public void Dispose()
            {
                foreach (var view in _actorViews.Values)
                {
                    view.Dispose();
                }

                foreach (var view in _inventoryViews.Values)
                {
                    view.Dispose();
                }

                _actorViews.Clear();
                _inventoryViews.Clear();
                _handle?.Dispose();
                _handle = null;
            }

            private void SyncActorControls()
            {
                if (_actorsGroup is null)
                {
                    return;
                }

                var activeIds = new HashSet<string>(_save.Actors.Select(actor => actor.Id.Value));
                foreach (var actorId in _actorViews.Keys.Where(actorId => !activeIds.Contains(actorId)).ToArray())
                {
                    RemoveActorControl(actorId);
                }

                foreach (var actor in _save.Actors.OrderBy(actor => actor.Id.Value))
                {
                    var actorId = actor.Id.Value;
                    if (_actorViews.TryGetValue(actorId, out var existingView))
                    {
                        existingView.Update(this, actor);
                        continue;
                    }

                    var view = new ActorRecordView(_page, this, actor);
                    view.SetHandle(_actorsGroup.Add(GetActorOrder(actorId), view));
                    _actorViews.Add(actorId, view);
                }

                SetVisible(_actorsEmptyLabel, _actorViews.Count == 0);
            }

            private void SyncInventoryControls()
            {
                if (_inventoryGroup is null)
                {
                    return;
                }

                var activeIds = new HashSet<string>(_save.Inventory.Select(item => item.Id.Value));
                foreach (var itemId in _inventoryViews.Keys.Where(itemId => !activeIds.Contains(itemId)).ToArray())
                {
                    RemoveInventoryControl(itemId);
                }

                foreach (var item in _save.Inventory.OrderBy(item => item.Id.Value))
                {
                    var itemId = item.Id.Value;
                    if (_inventoryViews.TryGetValue(itemId, out var existingView))
                    {
                        existingView.Update(this, item, null);
                        continue;
                    }

                    var view = new InventoryItemRecordView(_page, this, item, "Item");
                    view.SetHandle(_inventoryGroup.Add(GetInventoryOrder(itemId), view));
                    _inventoryViews.Add(itemId, view);
                }

                SetVisible(_inventoryEmptyLabel, _inventoryViews.Count == 0);
            }

            private void RemoveActorControl(string actorId)
            {
                if (!_actorViews.Remove(actorId, out var view))
                {
                    return;
                }

                view.Dispose();
            }

            private void RemoveInventoryControl(string itemId)
            {
                if (!_inventoryViews.Remove(itemId, out var view))
                {
                    return;
                }

                view.Dispose();
            }

            private int GetActorOrder(string actorId)
            {
                if (_actorOrders.TryGetValue(actorId, out var order))
                {
                    return order;
                }

                order = _nextActorOrder++;
                _actorOrders.Add(actorId, order);
                return order;
            }

            private int GetInventoryOrder(string itemId)
            {
                if (_inventoryOrders.TryGetValue(itemId, out var order))
                {
                    return order;
                }

                order = _nextInventoryOrder++;
                _inventoryOrders.Add(itemId, order);
                return order;
            }
        }

        private sealed class ActorRecordView : IDisposable
        {
            private readonly PersistenceDebugPage _page;
            private readonly Dictionary<string, SkillRecordView> _skillViews = new();
            private readonly Dictionary<string, InventoryItemRecordView> _equippedItemViews = new();
            private readonly Dictionary<string, int> _skillOrders = new();
            private readonly Dictionary<string, int> _equippedItemOrders = new();
            private int _nextSkillOrder;
            private int _nextEquippedItemOrder;
            private SaveRecordView _record;
            private Actor _actor;
            private IDisposable? _handle;
            private DebugIntegerField? _damageField;
            private DebugLabel? _skillsEmptyLabel;
            private DebugLabel? _equippedItemsEmptyLabel;
            private OrderedGroup<SkillRecordView>? _skillsGroup;
            private OrderedGroup<InventoryItemRecordView>? _equippedItemsGroup;

            public ActorRecordView(PersistenceDebugPage page, SaveRecordView record, Actor actor)
            {
                _page = page;
                _record = record;
                _actor = actor;
            }

            public Actor Actor => _actor;

            public void SetHandle(IDisposable handle)
            {
                _handle = handle;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.Foldout($"Actor: {_actor.DisplayName} ({_actor.Id})", actorBuilder =>
                {
                    _damageField = actorBuilder.IntegerField("Damage", 10);

                    actorBuilder.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Level Up + Save", () =>
                        {
                            _actor.LevelUp();
                            _record.Save.Actors.Upsert(_actor);
                            _page.SaveRecordAsync(_record.Save, "Leveled actor.").Forget();
                        });

                        row.SecondaryButton("Damage + Save", () =>
                        {
                            _actor.Damage(Math.Max(0, _damageField.value));
                            _record.Save.Actors.Upsert(_actor);
                            _page.SaveRecordAsync(_record.Save, "Damaged actor.").Forget();
                        });
                    });

                    actorBuilder.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Add Skill", () =>
                        {
                            var index = _actor.Skills.Count + 1;
                            _actor.Skills.Upsert(new Skill(new SkillId($"skill-{index}"), $"Skill {index}", 1));
                            _record.Save.Actors.Upsert(_actor);
                            _page.SaveRecordAsync(_record.Save, "Added actor skill.").Forget();
                        });

                        row.SecondaryButton("Add Equip", () =>
                        {
                            var index = _actor.EquippedItems.Count + 1;
                            _actor.EquippedItems.Upsert(new InventoryItem(new ItemId($"equip-{index}"), $"Equip {index}", 1));
                            _record.Save.Actors.Upsert(_actor);
                            _page.SaveRecordAsync(_record.Save, "Added equipped item.").Forget();
                        });
                    });

                    actorBuilder.DangerButton("Remove Actor + Save", () =>
                    {
                        _record.Save.Actors.Remove(_actor);
                        _page.SaveRecordAsync(_record.Save, "Removed actor.").Forget();
                    });

                    actorBuilder.Foldout("Skills", skillsBuilder =>
                    {
                        _skillsEmptyLabel = skillsBuilder.Label("No skills.");
                        _skillsGroup = skillsBuilder.OrderedGroup<SkillRecordView>((view, skillBuilder) => view.Build(skillBuilder));
                        SyncSkillControls();
                    });

                    actorBuilder.Foldout("Equipped Items", equippedBuilder =>
                    {
                        _equippedItemsEmptyLabel = equippedBuilder.Label("No equipped items.");
                        _equippedItemsGroup = equippedBuilder.OrderedGroup<InventoryItemRecordView>((view, itemBuilder) => view.Build(itemBuilder));
                        SyncEquippedItemControls();
                    });
                });
            }

            public void Update(SaveRecordView record, Actor actor)
            {
                _record = record;
                _actor = actor;
                SyncSkillControls();
                SyncEquippedItemControls();
            }

            public void Dispose()
            {
                foreach (var view in _skillViews.Values)
                {
                    view.Dispose();
                }

                foreach (var view in _equippedItemViews.Values)
                {
                    view.Dispose();
                }

                _skillViews.Clear();
                _equippedItemViews.Clear();
                _handle?.Dispose();
                _handle = null;
            }

            private void SyncSkillControls()
            {
                if (_skillsGroup is null)
                {
                    return;
                }

                var activeIds = new HashSet<string>(_actor.Skills.Select(skill => skill.Id.Value));
                foreach (var skillId in _skillViews.Keys.Where(skillId => !activeIds.Contains(skillId)).ToArray())
                {
                    RemoveSkillControl(skillId);
                }

                foreach (var skill in _actor.Skills.OrderBy(skill => skill.Id.Value))
                {
                    var skillId = skill.Id.Value;
                    if (_skillViews.TryGetValue(skillId, out var existingView))
                    {
                        existingView.Update(_record, this, skill);
                        continue;
                    }

                    var view = new SkillRecordView(_page, _record, this, skill);
                    view.SetHandle(_skillsGroup.Add(GetSkillOrder(skillId), view));
                    _skillViews.Add(skillId, view);
                }

                SetVisible(_skillsEmptyLabel, _skillViews.Count == 0);
            }

            private void SyncEquippedItemControls()
            {
                if (_equippedItemsGroup is null)
                {
                    return;
                }

                var activeIds = new HashSet<string>(_actor.EquippedItems.Select(item => item.Id.Value));
                foreach (var itemId in _equippedItemViews.Keys.Where(itemId => !activeIds.Contains(itemId)).ToArray())
                {
                    RemoveEquippedItemControl(itemId);
                }

                foreach (var item in _actor.EquippedItems.OrderBy(item => item.Id.Value))
                {
                    var itemId = item.Id.Value;
                    if (_equippedItemViews.TryGetValue(itemId, out var existingView))
                    {
                        existingView.Update(_record, item, this);
                        continue;
                    }

                    var view = new InventoryItemRecordView(_page, _record, item, "Equip", this);
                    view.SetHandle(_equippedItemsGroup.Add(GetEquippedItemOrder(itemId), view));
                    _equippedItemViews.Add(itemId, view);
                }

                SetVisible(_equippedItemsEmptyLabel, _equippedItemViews.Count == 0);
            }

            private void RemoveSkillControl(string skillId)
            {
                if (!_skillViews.Remove(skillId, out var view))
                {
                    return;
                }

                view.Dispose();
            }

            private void RemoveEquippedItemControl(string itemId)
            {
                if (!_equippedItemViews.Remove(itemId, out var view))
                {
                    return;
                }

                view.Dispose();
            }

            private int GetSkillOrder(string skillId)
            {
                if (_skillOrders.TryGetValue(skillId, out var order))
                {
                    return order;
                }

                order = _nextSkillOrder++;
                _skillOrders.Add(skillId, order);
                return order;
            }

            private int GetEquippedItemOrder(string itemId)
            {
                if (_equippedItemOrders.TryGetValue(itemId, out var order))
                {
                    return order;
                }

                order = _nextEquippedItemOrder++;
                _equippedItemOrders.Add(itemId, order);
                return order;
            }
        }

        private sealed class SkillRecordView : IDisposable
        {
            private readonly PersistenceDebugPage _page;
            private SaveRecordView _record;
            private ActorRecordView _actorView;
            private Skill _skill;
            private IDisposable? _handle;

            public SkillRecordView(PersistenceDebugPage page, SaveRecordView record, ActorRecordView actorView, Skill skill)
            {
                _page = page;
                _record = record;
                _actorView = actorView;
                _skill = skill;
            }

            public void SetHandle(IDisposable handle)
            {
                _handle = handle;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.Foldout($"Skill: {_skill.DisplayName} ({_skill.Id})", skillBuilder =>
                {
                    skillBuilder.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Level Up + Save", () =>
                        {
                            _skill.LevelUp();
                            _actorView.Actor.Skills.Upsert(_skill);
                            _record.Save.Actors.Upsert(_actorView.Actor);
                            _page.SaveRecordAsync(_record.Save, "Leveled skill.").Forget();
                        });

                        row.DangerButton("Remove + Save", () =>
                        {
                            _actorView.Actor.Skills.Remove(_skill);
                            _record.Save.Actors.Upsert(_actorView.Actor);
                            _page.SaveRecordAsync(_record.Save, "Removed skill.").Forget();
                        });
                    });
                });
            }

            public void Update(SaveRecordView record, ActorRecordView actorView, Skill skill)
            {
                _record = record;
                _actorView = actorView;
                _skill = skill;
            }

            public void Dispose()
            {
                _handle?.Dispose();
                _handle = null;
            }
        }

        private sealed class InventoryItemRecordView : IDisposable
        {
            private readonly PersistenceDebugPage _page;
            private readonly string _title;
            private SaveRecordView _record;
            private InventoryItem _item;
            private ActorRecordView? _owner;
            private IDisposable? _handle;
            private DebugIntegerField? _addField;

            public InventoryItemRecordView(
                PersistenceDebugPage page,
                SaveRecordView record,
                InventoryItem item,
                string title,
                ActorRecordView? owner = null)
            {
                _page = page;
                _record = record;
                _item = item;
                _title = title;
                _owner = owner;
            }

            public void SetHandle(IDisposable handle)
            {
                _handle = handle;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.Foldout($"{_title}: {_item.DisplayName} ({_item.Id})", itemBuilder =>
                {
                    _addField = itemBuilder.IntegerField("Add Count", 1);

                    itemBuilder.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Add + Save", () =>
                        {
                            _item.Add(Math.Max(0, _addField.value));
                            UpsertInventoryItem(_record.Save, _item, _owner?.Actor);
                            _page.SaveRecordAsync(_record.Save, "Added item count.").Forget();
                        });

                        row.DangerButton("Remove + Save", () =>
                        {
                            RemoveInventoryItem(_record.Save, _item, _owner?.Actor);
                            _page.SaveRecordAsync(_record.Save, "Removed item.").Forget();
                        });
                    });
                });
            }

            public void Update(SaveRecordView record, InventoryItem item, ActorRecordView? owner)
            {
                _record = record;
                _item = item;
                _owner = owner;
            }

            public void Dispose()
            {
                _handle?.Dispose();
                _handle = null;
            }
        }

        private static void SetVisible(VisualElement? element, bool visible)
        {
            if (element is null)
            {
                return;
            }

            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private SaveSlotId CurrentSlotId()
        {
            var value = string.IsNullOrWhiteSpace(_slotField.value) ? "slot-a" : _slotField.value.Trim();
            return new SaveSlotId(value);
        }

        private static string GetSaveDirectoryPath()
        {
            return Path.Combine(Application.persistentDataPath, "Persistence.Sample.GameSaveData");
        }

        private void SetStatus(string message)
        {
            _statusLabel.text = message;
        }

        private void SetError(Exception ex)
        {
            _statusLabel.text = ex.GetType().Name + ": " + ex.Message;
            Debug.LogException(ex);
        }
    }
}
