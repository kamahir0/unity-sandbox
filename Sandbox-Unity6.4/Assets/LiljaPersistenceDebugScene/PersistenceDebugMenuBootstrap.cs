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
        private DebugFoldout _recordsFoldout = null!;
        private readonly List<IDisposable> _recordHandles = new();

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

            _recordsFoldout = builder.Foldout("Records", _ =>
            {
            });
        }

        private async UniTaskVoid CreateAsync()
        {
            try
            {
                var save = GameSaveScenario.CreateNewGame(CurrentSlotId());
                await _repository.LoadAsync();
                _repository.Update(save);
                RebuildRecordControls(_repository.All());
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
                RebuildRecordControls(saves);
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
                RebuildRecordControls(_repository.All());
                SetStatus(status);
            }
            catch (Exception ex)
            {
                SetError(ex);
            }
        }

        private void RebuildRecordControls(IReadOnlyList<GameSaveData> saves)
        {
            if (_recordsFoldout is null)
            {
                return;
            }

            ClearRecordControls();

            foreach (var save in saves.OrderBy(save => save.SlotId.Value))
            {
                _recordHandles.Add(_recordsFoldout.AddDebugUI(builder =>
                {
                    BuildGameSaveRecordControls(builder, save);
                }));
            }
        }

        private void BuildGameSaveRecordControls(IDebugUIBuilder builder, GameSaveData save)
        {
            builder.Foldout($"Save: {save.SlotId}", saveBuilder =>
            {
                var chapterField = saveBuilder.IntegerField("Chapter", save.Chapter);
                var goldField = saveBuilder.LongField("Gold", save.Gold);

                saveBuilder.HorizontalScope(row =>
                {
                    row.PrimaryButton("Apply Properties + Save", () =>
                    {
                        save.Chapter = Math.Max(1, chapterField.value);
                        save.Gold = Math.Max(0L, goldField.value);
                        SaveRecordAsync(save, "Saved save properties.").Forget();
                    });

                    row.SecondaryButton("+100 Gold", () =>
                    {
                        save.Gold += 100;
                        SaveRecordAsync(save, "Added gold.").Forget();
                    });
                });

                saveBuilder.HorizontalScope(row =>
                {
                    row.SecondaryButton("Battle + Save", () =>
                    {
                        ApplyBattleAndSave(save);
                    });

                    row.SecondaryButton("Add Actor", () =>
                    {
                        AddActor(save);
                    });
                });

                saveBuilder.HorizontalScope(row =>
                {
                    row.SecondaryButton("Add Inventory", () =>
                    {
                        AddInventoryItem(save);
                    });

                    row.DangerButton("Delete This Slot", () => DeleteLoadedSlotAsync(save).Forget());
                });

                saveBuilder.Foldout("Actors", actorsBuilder =>
                {
                    if (save.Actors.Count == 0)
                    {
                        actorsBuilder.Label("No actors.");
                        return;
                    }

                    foreach (var actor in save.Actors.OrderBy(actor => actor.Id.Value))
                    {
                        BuildActorRecordControls(actorsBuilder, save, actor);
                    }
                });

                saveBuilder.Foldout("Inventory", inventoryBuilder =>
                {
                    if (save.Inventory.Count == 0)
                    {
                        inventoryBuilder.Label("No inventory items.");
                        return;
                    }

                    foreach (var item in save.Inventory.OrderBy(item => item.Id.Value))
                    {
                        BuildInventoryItemRecordControls(inventoryBuilder, save, item, "Item");
                    }
                });
            });
        }

        private void BuildActorRecordControls(IDebugUIBuilder builder, GameSaveData save, Actor actor)
        {
            builder.Foldout($"Actor: {actor.DisplayName} ({actor.Id})", actorBuilder =>
            {
                var damageField = actorBuilder.IntegerField("Damage", 10);

                actorBuilder.HorizontalScope(row =>
                {
                    row.PrimaryButton("Level Up + Save", () =>
                    {
                        actor.LevelUp();
                        save.Actors.Upsert(actor);
                        SaveRecordAsync(save, "Leveled actor.").Forget();
                    });

                    row.SecondaryButton("Damage + Save", () =>
                    {
                        actor.Damage(Math.Max(0, damageField.value));
                        save.Actors.Upsert(actor);
                        SaveRecordAsync(save, "Damaged actor.").Forget();
                    });
                });

                actorBuilder.HorizontalScope(row =>
                {
                    row.SecondaryButton("Add Skill", () =>
                    {
                        var index = actor.Skills.Count + 1;
                        actor.Skills.Upsert(new Skill(new SkillId($"skill-{index}"), $"Skill {index}", 1));
                        save.Actors.Upsert(actor);
                        SaveRecordAsync(save, "Added actor skill.").Forget();
                    });

                    row.SecondaryButton("Add Equip", () =>
                    {
                        var index = actor.EquippedItems.Count + 1;
                        actor.EquippedItems.Upsert(new InventoryItem(new ItemId($"equip-{index}"), $"Equip {index}", 1));
                        save.Actors.Upsert(actor);
                        SaveRecordAsync(save, "Added equipped item.").Forget();
                    });
                });

                actorBuilder.DangerButton("Remove Actor + Save", () =>
                {
                    save.Actors.Remove(actor);
                    SaveRecordAsync(save, "Removed actor.").Forget();
                });

                actorBuilder.Foldout("Skills", skillsBuilder =>
                {
                    if (actor.Skills.Count == 0)
                    {
                        skillsBuilder.Label("No skills.");
                        return;
                    }

                    foreach (var skill in actor.Skills.OrderBy(skill => skill.Id.Value))
                    {
                        BuildSkillRecordControls(skillsBuilder, save, actor, skill);
                    }
                });

                actorBuilder.Foldout("Equipped Items", equippedBuilder =>
                {
                    if (actor.EquippedItems.Count == 0)
                    {
                        equippedBuilder.Label("No equipped items.");
                        return;
                    }

                    foreach (var item in actor.EquippedItems.OrderBy(item => item.Id.Value))
                    {
                        BuildInventoryItemRecordControls(equippedBuilder, save, item, "Equip", actor);
                    }
                });
            });
        }

        private void BuildSkillRecordControls(IDebugUIBuilder builder, GameSaveData save, Actor actor, Skill skill)
        {
            builder.Foldout($"Skill: {skill.DisplayName} ({skill.Id})", skillBuilder =>
            {
                skillBuilder.HorizontalScope(row =>
                {
                    row.PrimaryButton("Level Up + Save", () =>
                    {
                        skill.LevelUp();
                        actor.Skills.Upsert(skill);
                        save.Actors.Upsert(actor);
                        SaveRecordAsync(save, "Leveled skill.").Forget();
                    });

                    row.DangerButton("Remove + Save", () =>
                    {
                        actor.Skills.Remove(skill);
                        save.Actors.Upsert(actor);
                        SaveRecordAsync(save, "Removed skill.").Forget();
                    });
                });
            });
        }

        private void BuildInventoryItemRecordControls(IDebugUIBuilder builder, GameSaveData save, InventoryItem item, string title, Actor? owner = null)
        {
            builder.Foldout($"{title}: {item.DisplayName} ({item.Id})", itemBuilder =>
            {
                var addField = itemBuilder.IntegerField("Add Count", 1);

                itemBuilder.HorizontalScope(row =>
                {
                    row.PrimaryButton("Add + Save", () =>
                    {
                        item.Add(Math.Max(0, addField.value));
                        UpsertInventoryItem(save, item, owner);
                        SaveRecordAsync(save, "Added item count.").Forget();
                    });

                    row.DangerButton("Remove + Save", () =>
                    {
                        RemoveInventoryItem(save, item, owner);
                        SaveRecordAsync(save, "Removed item.").Forget();
                    });
                });
            });
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
            foreach (var handle in _recordHandles)
            {
                handle.Dispose();
            }

            _recordHandles.Clear();
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
