using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.DebugUI;
using Lilja.Repository;
using RepositoryTest.Repositories;
using UnityEngine;
using UIDocument = UnityEngine.UIElements.UIDocument;

namespace RepositoryTest
{
    /// <summary>
    /// Lilja.Repository の動作検証を Debug UI だけで行うための専用コントローラ。
    /// </summary>
    public sealed class RepositoryDebugTestController : MonoBehaviour
    {
        [SerializeField]
        private bool _showMenuOnStart = true;

        private MonsterRepositoryTester _monsterTester;
        private HeroRepositoryTester _heroTester;
        private RelicRepositoryTester _relicTester;
        private WorldSettingsRepositoryTester _worldSettingsTester;

        private void Start()
        {
            InitializeAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            if (DebugMenu.IsInitialized)
            {
                DebugMenu.BackToRoot();
            }
        }

        private async UniTask InitializeAsync(CancellationToken ct)
        {
            _monsterTester = new MonsterRepositoryTester();
            _heroTester = new HeroRepositoryTester();
            _relicTester = new RelicRepositoryTester();
            _worldSettingsTester = new WorldSettingsRepositoryTester();

            await _monsterTester.InitializeAsync(RepositoryStorageType.Json, ct);
            await _heroTester.InitializeAsync(RepositoryStorageType.Json, ct);
            await _relicTester.InitializeAsync(RepositoryStorageType.Json, ct);
            await _worldSettingsTester.InitializeAsync(RepositoryStorageType.Json, ct);

            EnsureDebugMenuInitialized();

            if (_showMenuOnStart)
            {
                DebugMenu.Show();
            }
        }

        private void EnsureDebugMenuInitialized()
        {
            if (!DebugMenu.IsInitialized || DebugMenu.GetPage<RootPage>() == null)
            {
                DebugMenu.Initialize(new RootPage(this));
            }

            if (UnityEngine.Object.FindFirstObjectByType<DebugMenuOpenButton>() != null)
            {
                return;
            }

            var go = new GameObject("[RepositoryDebugTestOpenButton]");
            go.AddComponent<UIDocument>();
            go.AddComponent<DebugMenuOpenButton>();
        }

        private sealed class RootPage : DebugPage
        {
            private readonly RepositoryDebugTestController _controller;

            public RootPage(RepositoryDebugTestController controller)
            {
                _controller = controller;
                name = "Repository Debug Test";
            }

            public override void Configure(IDebugUIBuilder builder)
            {
                builder.Label("Lilja.Repository テスト専用シーン");
                builder.Label("Monster / Hero / Relic / WorldSettings を Debug UI だけで検証します。");

                builder.TempNavigationButton(
                    "Monster Repository",
                    tempBuilder => new MonsterPageBuilder(_controller._monsterTester, _controller.destroyCancellationToken).Build(tempBuilder));

                builder.TempNavigationButton(
                    "Hero Repository",
                    tempBuilder => new HeroPageBuilder(_controller._heroTester, _controller.destroyCancellationToken).Build(tempBuilder));

                builder.TempNavigationButton(
                    "Relic Repository",
                    tempBuilder => new RelicPageBuilder(_controller._relicTester, _controller.destroyCancellationToken).Build(tempBuilder));

                builder.TempNavigationButton(
                    "WorldSettings Repository",
                    tempBuilder => new WorldSettingsPageBuilder(_controller._worldSettingsTester, _controller.destroyCancellationToken).Build(tempBuilder));

                builder.Foldout("使い方", foldout =>
                {
                    foldout.Label("起動時にメニューを自動表示します。閉じた後は左下ボタン 3 連打で再表示できます。");
                    foldout.Label("旧シーン / 旧コントローラは Assets/RepositoryTest/Legacy に残しています。");
                });
            }
        }

        private sealed class MonsterPageBuilder
        {
            private readonly MonsterRepositoryTester _tester;
            private readonly CancellationToken _ct;
            private readonly List<IDisposable> _monsterHandles = new();

            private DebugLabel _statusLabel;
            private DebugIntegerField _idField;
            private DebugTextField _nameField;
            private DebugIntegerField _levelField;
            private DebugIntegerField _posXField;
            private DebugIntegerField _posYField;
            private VirtualFoldout _monsterFoldout;

            public MonsterPageBuilder(MonsterRepositoryTester tester, CancellationToken ct)
            {
                _tester = tester;
                _ct = ct;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.Label("Monster Entity の CRUD / 一括投入 / 失敗系 / Tx 可視性を試せます。");

                _statusLabel = builder.Label();

                builder.Foldout("ストレージ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Json", () => Execute(ct => _tester.InitializeAsync(RepositoryStorageType.Json, ct)));
                        row.SecondaryButton("MessagePack", () => Execute(ct => _tester.InitializeAsync(RepositoryStorageType.MessagePack, ct)));
                        row.PrimaryButton("再読込", () => Execute(_tester.ReloadCurrentRepositoryAsync));
                    });
                });

                builder.Foldout("入力", foldout =>
                {
                    _idField = foldout.IntegerField("Id");
                    _nameField = foldout.TextField("Name");
                    _levelField = foldout.IntegerField("Level");
                    _posXField = foldout.IntegerField("Pos X");
                    _posYField = foldout.IntegerField("Pos Y");

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Create", () => Execute(ct => _tester.CreateMonsterAsync(ReadDraftFromFields(), ct)));
                        row.SecondaryButton("Read", () => Execute(ct => _tester.ReadMonsterAsync(_idField.value, ct)));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Update", () => Execute(ct => _tester.UpdateMonsterAsync(ReadDraftFromFields(), ct)));
                        row.DangerButton("Delete", () => Execute(ct => _tester.DeleteMonsterAsync(_idField.value, ct)));
                    });
                });

                builder.Foldout("シナリオ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("サンプルリスト作成", () => Execute(_tester.SeedSampleMonstersAsync));
                        row.SecondaryButton("サンプルで置換", () => Execute(_tester.ReplaceWithSampleMonstersAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("連番 5 体追加", () => Execute(ct => _tester.CreateMonsterWaveAsync(5, ct)));
                        row.DangerButton("全てクリア", () => Execute(_tester.ClearAllMonstersAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("重複 Create", () => Execute(_tester.RunDuplicateCreateScenarioAsync));
                        row.SecondaryButton("存在しない Update", () => Execute(_tester.RunMissingUpdateScenarioAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("明示 Rollback", () => Execute(_tester.RunRollbackScenarioAsync));
                        row.SecondaryButton("RW 内 Read", () => Execute(ct => _tester.RunReadYourWriteScenarioAsync(ReadDraftFromFields(), ct)));
                    });
                });

                _monsterFoldout = builder.VirtualFoldout("現在のモンスター一覧");
                builder.SecondaryButton("一覧を再描画", RefreshView);

                RefreshView();
            }

            private MonsterDraft ReadDraftFromFields()
            {
                return new MonsterDraft(
                    _idField?.value ?? 0,
                    _nameField?.value ?? string.Empty,
                    _levelField?.value ?? 0,
                    new Position(_posXField?.value ?? 0, _posYField?.value ?? 0));
            }

            private void RefreshView()
            {
                var draft = _tester.CurrentDraft;
                _idField?.SetValueWithoutNotify(draft.Id);
                _nameField?.SetValueWithoutNotify(draft.Name);
                _levelField?.SetValueWithoutNotify(draft.Level);
                _posXField?.SetValueWithoutNotify(draft.Position.X);
                _posYField?.SetValueWithoutNotify(draft.Position.Y);

                if (_statusLabel != null)
                {
                    _statusLabel.text = _tester.GetStatusText();
                }

                RebuildMonsterList();
            }

            private void RebuildMonsterList()
            {
                if (_monsterFoldout == null)
                {
                    return;
                }

                ClearMonsterList();

                foreach (var monster in _tester.GetMonsterSnapshot().OrderBy(monster => monster.Id))
                {
                    _monsterHandles.Add(_monsterFoldout.AddDebugUI(builder =>
                    {
                        builder.HorizontalScope(row =>
                        {
                            row.Label(_tester.FormatMonster(monster));
                            row.SecondaryButton("入力へ反映", () =>
                            {
                                _tester.ApplyDraft(MonsterDraft.FromMonster(monster));
                                RefreshView();
                            });
                            row.DangerButton("削除", () => Execute(ct => _tester.DeleteMonsterAsync(monster.Id, ct)));
                        });
                    }));
                }
            }

            private void ClearMonsterList()
            {
                foreach (var handle in _monsterHandles)
                {
                    handle.Dispose();
                }

                _monsterHandles.Clear();
            }

            private void Execute(Func<CancellationToken, UniTask> action)
            {
                ExecuteAsync(action).Forget();
            }

            private async UniTask ExecuteAsync(Func<CancellationToken, UniTask> action)
            {
                try
                {
                    await action(_ct);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    RefreshView();
                }
            }
        }

        private sealed class HeroPageBuilder
        {
            private readonly HeroRepositoryTester _tester;
            private readonly CancellationToken _ct;

            private DebugLabel _statusLabel;
            private DebugLabel _currentHeroLabel;
            private DebugTextField _nameField;
            private DebugIntegerField _levelField;
            private DebugIntegerField _posXField;
            private DebugIntegerField _posYField;

            public HeroPageBuilder(HeroRepositoryTester tester, CancellationToken ct)
            {
                _tester = tester;
                _ct = ct;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.Label("Hero の Singleton 動作を、CRUD と失敗系シナリオ込みで試せます。");

                _statusLabel = builder.Label();
                _currentHeroLabel = builder.Label();

                builder.Foldout("ストレージ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Json", () => Execute(ct => _tester.InitializeAsync(RepositoryStorageType.Json, ct)));
                        row.SecondaryButton("MessagePack", () => Execute(ct => _tester.InitializeAsync(RepositoryStorageType.MessagePack, ct)));
                        row.PrimaryButton("再読込", () => Execute(_tester.ReloadCurrentRepositoryAsync));
                    });
                });

                builder.Foldout("入力", foldout =>
                {
                    _nameField = foldout.TextField("Name");
                    _levelField = foldout.IntegerField("Level");
                    _posXField = foldout.IntegerField("Pos X");
                    _posYField = foldout.IntegerField("Pos Y");

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Create", () => Execute(ct => _tester.CreateHeroAsync(ReadDraftFromFields(), ct)));
                        row.SecondaryButton("Read", () => Execute(_tester.ReadHeroAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Update", () => Execute(ct => _tester.UpdateHeroAsync(ReadDraftFromFields(), ct)));
                        row.DangerButton("Delete", () => Execute(_tester.DeleteHeroAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Create / Update", () => Execute(ct => _tester.SaveHeroAsync(ReadDraftFromFields(), ct)));
                    });
                });

                builder.Foldout("シナリオ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("サンプル作成", () => Execute(_tester.SaveSampleHeroAsync));
                        row.DangerButton("全てクリア", () => Execute(_tester.ClearAllHeroesAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("重複 Create", () => Execute(ct => _tester.RunDuplicateCreateScenarioAsync(ReadDraftFromFields(), ct)));
                        row.SecondaryButton("存在しない Update", () => Execute(ct => _tester.RunMissingUpdateScenarioAsync(ReadDraftFromFields(), ct)));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("明示 Rollback", () => Execute(ct => _tester.RunRollbackScenarioAsync(ReadDraftFromFields(), ct)));
                        row.SecondaryButton("RW 内 Read", () => Execute(ct => _tester.RunReadYourWriteScenarioAsync(ReadDraftFromFields(), ct)));
                    });
                });

                builder.SecondaryButton("状態を再描画", RefreshView);

                RefreshView();
            }

            private HeroDraft ReadDraftFromFields()
            {
                return new HeroDraft(
                    _nameField?.value ?? string.Empty,
                    _levelField?.value ?? 0,
                    new Position(_posXField?.value ?? 0, _posYField?.value ?? 0));
            }

            private void RefreshView()
            {
                var draft = _tester.CurrentDraft;
                _nameField?.SetValueWithoutNotify(draft.Name);
                _levelField?.SetValueWithoutNotify(draft.Level);
                _posXField?.SetValueWithoutNotify(draft.Position.X);
                _posYField?.SetValueWithoutNotify(draft.Position.Y);

                if (_statusLabel != null)
                {
                    _statusLabel.text = _tester.GetStatusText();
                }

                if (_currentHeroLabel != null)
                {
                    _currentHeroLabel.text = $"現在の Hero: {_tester.DescribeHero(_tester.GetHeroSnapshot())}";
                }
            }

            private void Execute(Func<CancellationToken, UniTask> action)
            {
                ExecuteAsync(action).Forget();
            }

            private async UniTask ExecuteAsync(Func<CancellationToken, UniTask> action)
            {
                try
                {
                    await action(_ct);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    RefreshView();
                }
            }
        }

        private sealed class RelicPageBuilder
        {
            private readonly RelicRepositoryTester _tester;
            private readonly CancellationToken _ct;
            private readonly List<IDisposable> _relicHandles = new();

            private DebugLabel _statusLabel;
            private DebugIntegerField _idField;
            private DebugTextField _nameField;
            private DebugEnumField _rarityField;
            private DebugLongField _priceField;
            private DebugIntegerField _attackField;
            private DebugIntegerField _defenseField;
            private DebugFloatField _criticalRateField;
            private DebugSecondaryButton _equippedToggleButton;
            private VirtualFoldout _relicFoldout;
            private bool _isEquipped;

            public RelicPageBuilder(RelicRepositoryTester tester, CancellationToken ct)
            {
                _tester = tester;
                _ct = ct;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.Label("Relic Entity で enum / bool / long / ValueObject をまとめて検証します。");

                _statusLabel = builder.Label();

                builder.Foldout("ストレージ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Json", () => Execute(ct => _tester.InitializeAsync(RepositoryStorageType.Json, ct)));
                        row.SecondaryButton("MessagePack", () => Execute(ct => _tester.InitializeAsync(RepositoryStorageType.MessagePack, ct)));
                        row.PrimaryButton("再読込", () => Execute(_tester.ReloadCurrentRepositoryAsync));
                    });
                });

                builder.Foldout("入力", foldout =>
                {
                    _idField = foldout.IntegerField("Id");
                    _nameField = foldout.TextField("Name");
                    _rarityField = foldout.EnumField("Rarity", RelicRarity.Common);
                    _priceField = foldout.LongField("Price");
                    _attackField = foldout.IntegerField("Attack");
                    _defenseField = foldout.IntegerField("Defense");
                    _criticalRateField = foldout.FloatField("Critical Rate");
                    _equippedToggleButton = foldout.SecondaryButton("Equipped: false", ToggleEquipped);

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Create", () => Execute(ct => _tester.CreateRelicAsync(ReadDraftFromFields(), ct)));
                        row.SecondaryButton("Read", () => Execute(ct => _tester.ReadRelicAsync(_idField.value, ct)));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Update", () => Execute(ct => _tester.UpdateRelicAsync(ReadDraftFromFields(), ct)));
                        row.DangerButton("Delete", () => Execute(ct => _tester.DeleteRelicAsync(_idField.value, ct)));
                    });
                });

                builder.Foldout("シナリオ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("サンプルリスト作成", () => Execute(_tester.SeedSampleRelicsAsync));
                        row.SecondaryButton("サンプルで置換", () => Execute(_tester.ReplaceWithSampleRelicsAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("連番 5 個追加", () => Execute(ct => _tester.CreateRelicWaveAsync(5, ct)));
                        row.DangerButton("全てクリア", () => Execute(_tester.ClearAllRelicsAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("重複 Create", () => Execute(_tester.RunDuplicateCreateScenarioAsync));
                        row.SecondaryButton("存在しない Update", () => Execute(_tester.RunMissingUpdateScenarioAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("明示 Rollback", () => Execute(_tester.RunRollbackScenarioAsync));
                        row.SecondaryButton("RW 内 Read", () => Execute(ct => _tester.RunReadYourWriteScenarioAsync(ReadDraftFromFields(), ct)));
                    });
                });

                _relicFoldout = builder.VirtualFoldout("現在のレリック一覧");
                builder.SecondaryButton("一覧を再描画", RefreshView);

                RefreshView();
            }

            private RelicDraft ReadDraftFromFields()
            {
                var rarity = _rarityField?.value is RelicRarity relicRarity
                    ? relicRarity
                    : RelicRarity.Common;

                return new RelicDraft(
                    _idField?.value ?? 0,
                    _nameField?.value ?? string.Empty,
                    rarity,
                    _isEquipped,
                    _priceField?.value ?? 0L,
                    new RelicStats(
                        _attackField?.value ?? 0,
                        _defenseField?.value ?? 0,
                        _criticalRateField?.value ?? 0f));
            }

            private void RefreshView()
            {
                var draft = _tester.CurrentDraft;
                _idField?.SetValueWithoutNotify(draft.Id);
                _nameField?.SetValueWithoutNotify(draft.Name);
                _rarityField?.SetValueWithoutNotify((Enum)draft.Rarity);
                _priceField?.SetValueWithoutNotify(draft.Price);
                _attackField?.SetValueWithoutNotify(draft.Stats.Attack);
                _defenseField?.SetValueWithoutNotify(draft.Stats.Defense);
                _criticalRateField?.SetValueWithoutNotify(draft.Stats.CriticalRate);
                _isEquipped = draft.IsEquipped;
                RefreshToggleLabel();

                if (_statusLabel != null)
                {
                    _statusLabel.text = _tester.GetStatusText();
                }

                RebuildRelicList();
            }

            private void RebuildRelicList()
            {
                if (_relicFoldout == null)
                {
                    return;
                }

                ClearRelicList();

                foreach (var relic in _tester.GetRelicSnapshot().OrderBy(relic => relic.Id))
                {
                    _relicHandles.Add(_relicFoldout.AddDebugUI(builder =>
                    {
                        builder.HorizontalScope(row =>
                        {
                            row.Label(_tester.FormatRelic(relic));
                            row.SecondaryButton("入力へ反映", () =>
                            {
                                _tester.ApplyDraft(RelicDraft.FromRelic(relic));
                                RefreshView();
                            });
                            row.DangerButton("削除", () => Execute(ct => _tester.DeleteRelicAsync(relic.Id, ct)));
                        });
                    }));
                }
            }

            private void ClearRelicList()
            {
                foreach (var handle in _relicHandles)
                {
                    handle.Dispose();
                }

                _relicHandles.Clear();
            }

            private void ToggleEquipped()
            {
                _isEquipped = !_isEquipped;
                RefreshToggleLabel();
            }

            private void RefreshToggleLabel()
            {
                if (_equippedToggleButton != null)
                {
                    _equippedToggleButton.text = $"Equipped: {_isEquipped}";
                }
            }

            private void Execute(Func<CancellationToken, UniTask> action)
            {
                ExecuteAsync(action).Forget();
            }

            private async UniTask ExecuteAsync(Func<CancellationToken, UniTask> action)
            {
                try
                {
                    await action(_ct);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    RefreshView();
                }
            }
        }

        private sealed class WorldSettingsPageBuilder
        {
            private readonly WorldSettingsRepositoryTester _tester;
            private readonly CancellationToken _ct;

            private DebugLabel _statusLabel;
            private DebugLabel _currentSettingsLabel;
            private DebugTextField _regionNameField;
            private DebugEnumField _difficultyField;
            private DebugFloatField _spawnRateField;
            private DebugIntegerField _startXField;
            private DebugIntegerField _startYField;
            private DebugSecondaryButton _nightModeToggleButton;
            private bool _nightMode;

            public WorldSettingsPageBuilder(WorldSettingsRepositoryTester tester, CancellationToken ct)
            {
                _tester = tester;
                _ct = ct;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.Label("WorldSettings Entity で singleton + enum / bool / float を検証します。");

                _statusLabel = builder.Label();
                _currentSettingsLabel = builder.Label();

                builder.Foldout("ストレージ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Json", () => Execute(ct => _tester.InitializeAsync(RepositoryStorageType.Json, ct)));
                        row.SecondaryButton("MessagePack", () => Execute(ct => _tester.InitializeAsync(RepositoryStorageType.MessagePack, ct)));
                        row.PrimaryButton("再読込", () => Execute(_tester.ReloadCurrentRepositoryAsync));
                    });
                });

                builder.Foldout("入力", foldout =>
                {
                    _regionNameField = foldout.TextField("Region");
                    _difficultyField = foldout.EnumField("Difficulty", WorldDifficulty.Normal);
                    _spawnRateField = foldout.FloatField("Spawn Rate");
                    _startXField = foldout.IntegerField("Start X");
                    _startYField = foldout.IntegerField("Start Y");
                    _nightModeToggleButton = foldout.SecondaryButton("Night Mode: false", ToggleNightMode);

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Create", () => Execute(ct => _tester.CreateWorldSettingsAsync(ReadDraftFromFields(), ct)));
                        row.SecondaryButton("Read", () => Execute(_tester.ReadWorldSettingsAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("Update", () => Execute(ct => _tester.UpdateWorldSettingsAsync(ReadDraftFromFields(), ct)));
                        row.DangerButton("Delete", () => Execute(_tester.DeleteWorldSettingsAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("Create / Update", () => Execute(ct => _tester.SaveWorldSettingsAsync(ReadDraftFromFields(), ct)));
                    });
                });

                builder.Foldout("シナリオ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        row.PrimaryButton("サンプル作成", () => Execute(_tester.SaveSampleWorldSettingsAsync));
                        row.DangerButton("全てクリア", () => Execute(_tester.ClearAllWorldSettingsAsync));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("重複 Create", () => Execute(ct => _tester.RunDuplicateCreateScenarioAsync(ReadDraftFromFields(), ct)));
                        row.SecondaryButton("存在しない Update", () => Execute(ct => _tester.RunMissingUpdateScenarioAsync(ReadDraftFromFields(), ct)));
                    });

                    foldout.HorizontalScope(row =>
                    {
                        row.SecondaryButton("明示 Rollback", () => Execute(ct => _tester.RunRollbackScenarioAsync(ReadDraftFromFields(), ct)));
                        row.SecondaryButton("RW 内 Read", () => Execute(ct => _tester.RunReadYourWriteScenarioAsync(ReadDraftFromFields(), ct)));
                    });
                });

                builder.SecondaryButton("状態を再描画", RefreshView);

                RefreshView();
            }

            private WorldSettingsDraft ReadDraftFromFields()
            {
                var difficulty = _difficultyField?.value is WorldDifficulty worldDifficulty
                    ? worldDifficulty
                    : WorldDifficulty.Normal;

                return new WorldSettingsDraft(
                    _regionNameField?.value ?? string.Empty,
                    difficulty,
                    _nightMode,
                    _spawnRateField?.value ?? 0f,
                    new Position(_startXField?.value ?? 0, _startYField?.value ?? 0));
            }

            private void RefreshView()
            {
                var draft = _tester.CurrentDraft;
                _regionNameField?.SetValueWithoutNotify(draft.RegionName);
                _difficultyField?.SetValueWithoutNotify((Enum)draft.Difficulty);
                _spawnRateField?.SetValueWithoutNotify(draft.SpawnRate);
                _startXField?.SetValueWithoutNotify(draft.StartPosition.X);
                _startYField?.SetValueWithoutNotify(draft.StartPosition.Y);
                _nightMode = draft.NightMode;
                RefreshNightModeLabel();

                if (_statusLabel != null)
                {
                    _statusLabel.text = _tester.GetStatusText();
                }

                if (_currentSettingsLabel != null)
                {
                    _currentSettingsLabel.text = $"現在の WorldSettings: {_tester.DescribeWorldSettings(_tester.GetWorldSettingsSnapshot())}";
                }
            }

            private void ToggleNightMode()
            {
                _nightMode = !_nightMode;
                RefreshNightModeLabel();
            }

            private void RefreshNightModeLabel()
            {
                if (_nightModeToggleButton != null)
                {
                    _nightModeToggleButton.text = $"Night Mode: {_nightMode}";
                }
            }

            private void Execute(Func<CancellationToken, UniTask> action)
            {
                ExecuteAsync(action).Forget();
            }

            private async UniTask ExecuteAsync(Func<CancellationToken, UniTask> action)
            {
                try
                {
                    await action(_ct);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    RefreshView();
                }
            }
        }

        private sealed class MonsterRepositoryTester
        {
            private IMonsterRepository _repository;
            private readonly TxManager _txManager = new();
            private RepositoryStorageType _currentStorageType = RepositoryStorageType.Json;
            private MonsterDraft _currentDraft = MonsterDraft.Default;
            private string _lastLogMessage = "未実行";

            public MonsterDraft CurrentDraft => _currentDraft;

            public async UniTask InitializeAsync(RepositoryStorageType storageType, CancellationToken ct)
            {
                try
                {
                    _currentStorageType = storageType;

                    if (storageType == RepositoryStorageType.Json)
                    {
                        var repository = new JsonMonsterRepository();
                        await repository.InitializeAsync(ct);
                        _repository = repository;
                    }
                    else
                    {
                        var repository = new MessagePackMonsterRepository();
                        await repository.InitializeAsync(ct);
                        _repository = repository;
                    }

                    SetLog($"{GetStorageLabel()} リポジトリを初期化しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"初期化エラー: {ex.Message}");
                }
            }

            public UniTask ReloadCurrentRepositoryAsync(CancellationToken ct)
            {
                return InitializeAsync(_currentStorageType, ct);
            }

            public void ApplyDraft(MonsterDraft draft)
            {
                _currentDraft = draft;
            }

            public IReadOnlyList<Monster> GetMonsterSnapshot()
            {
                if (!EnsureRepositoryReady(false))
                {
                    return Array.Empty<Monster>();
                }

                try
                {
                    IReadOnlyList<Monster> monsters = Array.Empty<Monster>();
                    _txManager.BeginROTransaction(tx =>
                    {
                        monsters = _repository.All(tx);
                    });
                    return monsters ?? Array.Empty<Monster>();
                }
                catch
                {
                    return Array.Empty<Monster>();
                }
            }

            public string GetStatusText()
            {
                return $"Storage: {GetStorageLabel()} / Count: {GetMonsterSnapshot().Count}\nLast: {_lastLogMessage}";
            }

            public string FormatMonster(Monster monster)
            {
                return monster == null
                    ? "null"
                    : $"[{monster.Id}] {monster.Name}  Lv.{monster.Level}  Pos{monster.Position}";
            }

            public async UniTask CreateMonsterAsync(MonsterDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var monster = draft.ToMonster();
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Create(tx, monster);
                    }, ct);
                    SetLog($"作成: {FormatMonster(monster)}");
                }
                catch (Exception ex)
                {
                    SetLog($"作成エラー: {ex.Message}");
                }
            }

            public UniTask ReadMonsterAsync(int id, CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return UniTask.CompletedTask;
                }

                try
                {
                    Monster monster = null;
                    _txManager.BeginROTransaction(tx =>
                    {
                        monster = _repository.Read(tx, id);
                    });

                    if (monster != null)
                    {
                        ApplyDraft(MonsterDraft.FromMonster(monster));
                        SetLog($"読取: {FormatMonster(monster)}");
                    }
                    else
                    {
                        SetLog($"Id={id} のモンスターは存在しません。");
                    }
                }
                catch (Exception ex)
                {
                    SetLog($"読取エラー: {ex.Message}");
                }

                return UniTask.CompletedTask;
            }

            public async UniTask UpdateMonsterAsync(MonsterDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var monster = draft.ToMonster();
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Update(tx, monster);
                    }, ct);
                    SetLog($"更新: {FormatMonster(monster)}");
                }
                catch (Exception ex)
                {
                    SetLog($"更新エラー: {ex.Message}");
                }
            }

            public async UniTask DeleteMonsterAsync(int id, CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Delete(tx, id);
                    }, ct);
                    SetLog($"削除: Id={id}");
                }
                catch (Exception ex)
                {
                    SetLog($"削除エラー: {ex.Message}");
                }
            }

            public async UniTask SeedSampleMonstersAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var samples = GetSampleMonsterDrafts();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        foreach (var sample in samples)
                        {
                            if (_repository.Read(tx, sample.Id) == null)
                            {
                                _repository.Create(tx, sample.ToMonster());
                            }
                            else
                            {
                                _repository.Update(tx, sample.ToMonster());
                            }
                        }
                    }, ct);

                    ApplyDraft(samples[0]);
                    SetLog($"サンプル {samples.Length} 体を投入または更新しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"サンプル投入エラー: {ex.Message}");
                }
            }

            public async UniTask ReplaceWithSampleMonstersAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var samples = GetSampleMonsterDrafts();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        foreach (var monster in _repository.All(tx).ToArray())
                        {
                            _repository.Delete(tx, monster.Id);
                        }

                        foreach (var sample in samples)
                        {
                            _repository.Create(tx, sample.ToMonster());
                        }
                    }, ct);

                    ApplyDraft(samples[0]);
                    SetLog($"サンプル {samples.Length} 体で置き換えました。");
                }
                catch (Exception ex)
                {
                    SetLog($"サンプル置換エラー: {ex.Message}");
                }
            }

            public async UniTask CreateMonsterWaveAsync(int count, CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var startId = GetNextMonsterId();
                var lastCreated = default(MonsterDraft);

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        for (var i = 0; i < count; i++)
                        {
                            var id = startId + i;
                            lastCreated = new MonsterDraft(
                                id,
                                $"Wave-{id:D3}",
                                i + 1,
                                new Position(i * 2, -i));
                            _repository.Create(tx, lastCreated.ToMonster());
                        }
                    }, ct);

                    ApplyDraft(lastCreated);
                    SetLog($"連番モンスターを {count} 体追加しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"一括追加エラー: {ex.Message}");
                }
            }

            public async UniTask ClearAllMonstersAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var snapshot = GetMonsterSnapshot();
                if (snapshot.Count == 0)
                {
                    SetLog("削除対象のモンスターはありません。");
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        foreach (var monster in snapshot)
                        {
                            _repository.Delete(tx, monster.Id);
                        }
                    }, ct);
                    SetLog($"{snapshot.Count} 体のモンスターを削除しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"一括削除エラー: {ex.Message}");
                }
            }

            public async UniTask RunDuplicateCreateScenarioAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var draft = CreateIsolatedScenarioDraft();
                if (!ValidateDraft(draft))
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        var monster = draft.ToMonster();
                        _repository.Create(tx, monster);
                        _repository.Create(tx, monster);
                    }, ct);
                    SetLog("重複 Create テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var persisted = false;
                    _txManager.BeginROTransaction(tx =>
                    {
                        persisted = _repository.Read(tx, draft.Id) != null;
                    });
                    SetLog($"重複 Create テスト: {ex.Message} / Rollback={(persisted ? "失敗" : "成功")} / Id={draft.Id}");
                }
            }

            public async UniTask RunMissingUpdateScenarioAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var draft = CreateIsolatedScenarioDraft();
                if (!ValidateDraft(draft))
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Update(tx, draft.ToMonster());
                    }, ct);
                    SetLog("存在しない Update テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var exists = false;
                    _txManager.BeginROTransaction(tx =>
                    {
                        exists = _repository.Read(tx, draft.Id) != null;
                    });
                    SetLog($"存在しない Update テスト: {ex.Message} / Persisted={(exists ? "あり" : "なし")} / Id={draft.Id}");
                }
            }

            public async UniTask RunRollbackScenarioAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var draft = CreateIsolatedScenarioDraft();
                if (!ValidateDraft(draft))
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Create(tx, draft.ToMonster());
                        throw new InvalidOperationException("Intentional rollback from RepositoryDebugTest");
                    }, ct);
                    SetLog("Rollback テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var persisted = false;
                    _txManager.BeginROTransaction(tx =>
                    {
                        persisted = _repository.Read(tx, draft.Id) != null;
                    });
                    SetLog($"Rollback テスト: {ex.Message} / Persisted={(persisted ? "あり" : "なし")} / Id={draft.Id}");
                }
            }

            public async UniTask RunReadYourWriteScenarioAsync(MonsterDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var mode = string.Empty;
                    Monster staged = null;
                    var visibleCount = 0;

                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx, draft.Id) == null)
                        {
                            mode = "Create";
                            _repository.Create(tx, draft.ToMonster());
                        }
                        else
                        {
                            mode = "Update";
                            _repository.Update(tx, draft.ToMonster());
                        }

                        staged = _repository.Read(tx, draft.Id);
                        visibleCount = _repository.All(tx).Count;
                    }, ct);

                    Monster committed = null;
                    _txManager.BeginROTransaction(tx =>
                    {
                        committed = _repository.Read(tx, draft.Id);
                    });

                    SetLog($"RW 内 Read: Mode={mode}, VisibleCount={visibleCount}, Staged={FormatMonster(staged)}, Committed={FormatMonster(committed)}");
                }
                catch (Exception ex)
                {
                    SetLog($"RW 内 Read テストエラー: {ex.Message}");
                }
            }

            private bool EnsureRepositoryReady(bool reportError = true)
            {
                if (_repository != null)
                {
                    return true;
                }

                if (reportError)
                {
                    SetLog("リポジトリが未初期化です。");
                }

                return false;
            }

            private bool ValidateDraft(MonsterDraft draft)
            {
                if (string.IsNullOrWhiteSpace(draft.Name))
                {
                    SetLog("名前を入力してください。");
                    return false;
                }

                return true;
            }

            private MonsterDraft CreateIsolatedScenarioDraft()
            {
                return _currentDraft.WithId(GetNextMonsterId());
            }

            private int GetNextMonsterId()
            {
                var nextId = 1;
                foreach (var monster in GetMonsterSnapshot())
                {
                    nextId = Math.Max(nextId, monster.Id + 1);
                }

                return nextId;
            }

            private MonsterDraft[] GetSampleMonsterDrafts()
            {
                return new[]
                {
                    new MonsterDraft(1, "Slime", 1, new Position(0, 0)),
                    new MonsterDraft(2, "Goblin", 4, new Position(2, -1)),
                    new MonsterDraft(10, "Dragon", 20, new Position(8, 5)),
                };
            }

            private string GetStorageLabel()
            {
                return _currentStorageType == RepositoryStorageType.MessagePack ? "MessagePack" : "Json";
            }

            private void SetLog(string message)
            {
                _lastLogMessage = message;
                Debug.Log($"[RepositoryDebugTest/Monster] {message}");
            }
        }

        private sealed class HeroRepositoryTester
        {
            private IHeroRepository _repository;
            private readonly TxManager _txManager = new();
            private RepositoryStorageType _currentStorageType = RepositoryStorageType.Json;
            private HeroDraft _currentDraft = HeroDraft.Default;
            private string _lastLogMessage = "未実行";

            public HeroDraft CurrentDraft => _currentDraft;

            public async UniTask InitializeAsync(RepositoryStorageType storageType, CancellationToken ct)
            {
                try
                {
                    _currentStorageType = storageType;

                    if (storageType == RepositoryStorageType.Json)
                    {
                        var repository = new JsonHeroRepository();
                        await repository.InitializeAsync(ct);
                        _repository = repository;
                    }
                    else
                    {
                        var repository = new MessagePackHeroRepository();
                        await repository.InitializeAsync(ct);
                        _repository = repository;
                    }

                    SetLog($"{GetStorageLabel()} リポジトリ(Hero)を初期化しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"初期化エラー: {ex.Message}");
                }
            }

            public UniTask ReloadCurrentRepositoryAsync(CancellationToken ct)
            {
                return InitializeAsync(_currentStorageType, ct);
            }

            public HeroDraft CurrentHeroDraft => _currentDraft;

            public void ApplyDraft(HeroDraft draft)
            {
                _currentDraft = draft;
            }

            public Hero GetHeroSnapshot()
            {
                if (!EnsureRepositoryReady(false))
                {
                    return null;
                }

                try
                {
                    Hero hero = null;
                    _txManager.BeginROTransaction(tx =>
                    {
                        hero = _repository.Read(tx);
                    });
                    return hero;
                }
                catch
                {
                    return null;
                }
            }

            public string GetStatusText()
            {
                return $"Storage: {GetStorageLabel()} / Exists: {(GetHeroSnapshot() == null ? "No" : "Yes")}\nLast: {_lastLogMessage}";
            }

            public string DescribeHero(Hero hero)
            {
                return hero == null
                    ? "なし"
                    : $"{hero.Name}  Lv.{hero.Level}  Pos{hero.Position}";
            }

            public async UniTask CreateHeroAsync(HeroDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var hero = draft.ToHero();
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Create(tx, hero);
                    }, ct);
                    SetLog($"作成: {DescribeHero(hero)}");
                }
                catch (Exception ex)
                {
                    SetLog($"作成エラー: {ex.Message}");
                }
            }

            public UniTask ReadHeroAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return UniTask.CompletedTask;
                }

                try
                {
                    var hero = GetHeroSnapshot();
                    if (hero != null)
                    {
                        ApplyDraft(HeroDraft.FromHero(hero));
                        SetLog($"読取: {DescribeHero(hero)}");
                    }
                    else
                    {
                        SetLog("Hero データは存在しません。");
                    }
                }
                catch (Exception ex)
                {
                    SetLog($"読取エラー: {ex.Message}");
                }

                return UniTask.CompletedTask;
            }

            public async UniTask UpdateHeroAsync(HeroDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var hero = draft.ToHero();
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Update(tx, hero);
                    }, ct);
                    SetLog($"更新: {DescribeHero(hero)}");
                }
                catch (Exception ex)
                {
                    SetLog($"更新エラー: {ex.Message}");
                }
            }

            public async UniTask DeleteHeroAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Delete(tx);
                    }, ct);
                    SetLog("削除: Hero データを削除しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"削除エラー: {ex.Message}");
                }
            }

            public async UniTask ClearAllHeroesAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                if (GetHeroSnapshot() == null)
                {
                    SetLog("削除対象の Hero データはありません。");
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) != null)
                        {
                            _repository.Delete(tx);
                        }
                    }, ct);
                    SetLog("Hero データを全てクリアしました。");
                }
                catch (Exception ex)
                {
                    SetLog($"全クリアエラー: {ex.Message}");
                }
            }

            public async UniTask SaveHeroAsync(HeroDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) == null)
                        {
                            _repository.Create(tx, draft.ToHero());
                        }
                        else
                        {
                            _repository.Update(tx, draft.ToHero());
                        }
                    }, ct);
                    SetLog($"保存(Create / Update): {DescribeHero(draft.ToHero())}");
                }
                catch (Exception ex)
                {
                    SetLog($"保存エラー: {ex.Message}");
                }
            }

            public UniTask SaveSampleHeroAsync(CancellationToken ct)
            {
                return SaveHeroAsync(new HeroDraft("Knight", 12, new Position(3, 2)), ct);
            }

            public async UniTask RunDuplicateCreateScenarioAsync(HeroDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                var before = GetHeroSnapshot();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) != null)
                        {
                            _repository.Delete(tx);
                        }

                        var hero = draft.ToHero();
                        _repository.Create(tx, hero);
                        _repository.Create(tx, hero);
                    }, ct);
                    SetLog("重複 Create テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var after = GetHeroSnapshot();
                    SetLog($"重複 Create テスト: {ex.Message} / Before={DescribeHero(before)} / After={DescribeHero(after)}");
                }
            }

            public async UniTask RunMissingUpdateScenarioAsync(HeroDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                var before = GetHeroSnapshot();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) != null)
                        {
                            _repository.Delete(tx);
                        }

                        _repository.Update(tx, draft.ToHero());
                    }, ct);
                    SetLog("存在しない Update テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var after = GetHeroSnapshot();
                    SetLog($"存在しない Update テスト: {ex.Message} / Before={DescribeHero(before)} / After={DescribeHero(after)}");
                }
            }

            public async UniTask RunRollbackScenarioAsync(HeroDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                var before = GetHeroSnapshot();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) == null)
                        {
                            _repository.Create(tx, draft.ToHero());
                        }
                        else
                        {
                            _repository.Update(tx, draft.ToHero());
                        }

                        throw new InvalidOperationException("Intentional rollback from RepositoryDebugTest");
                    }, ct);
                    SetLog("Rollback テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var after = GetHeroSnapshot();
                    SetLog($"Rollback テスト: {ex.Message} / Before={DescribeHero(before)} / After={DescribeHero(after)}");
                }
            }

            public async UniTask RunReadYourWriteScenarioAsync(HeroDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var mode = string.Empty;
                    Hero staged = null;

                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) == null)
                        {
                            mode = "Create";
                            _repository.Create(tx, draft.ToHero());
                        }
                        else
                        {
                            mode = "Update";
                            _repository.Update(tx, draft.ToHero());
                        }

                        staged = _repository.Read(tx);
                    }, ct);

                    var committed = GetHeroSnapshot();
                    SetLog($"RW 内 Read: Mode={mode}, Staged={DescribeHero(staged)}, Committed={DescribeHero(committed)}");
                }
                catch (Exception ex)
                {
                    SetLog($"RW 内 Read テストエラー: {ex.Message}");
                }
            }

            private bool EnsureRepositoryReady(bool reportError = true)
            {
                if (_repository != null)
                {
                    return true;
                }

                if (reportError)
                {
                    SetLog("リポジトリが未初期化です。");
                }

                return false;
            }

            private bool ValidateDraft(HeroDraft draft)
            {
                if (string.IsNullOrWhiteSpace(draft.Name))
                {
                    SetLog("名前を入力してください。");
                    return false;
                }

                return true;
            }

            private string GetStorageLabel()
            {
                return _currentStorageType == RepositoryStorageType.MessagePack ? "MessagePack" : "Json";
            }

            private void SetLog(string message)
            {
                _lastLogMessage = message;
                Debug.Log($"[RepositoryDebugTest/Hero] {message}");
            }
        }

        private sealed class RelicRepositoryTester
        {
            private IRelicRepository _repository;
            private readonly TxManager _txManager = new();
            private RepositoryStorageType _currentStorageType = RepositoryStorageType.Json;
            private RelicDraft _currentDraft = RelicDraft.Default;
            private string _lastLogMessage = "未実行";

            public RelicDraft CurrentDraft => _currentDraft;

            public async UniTask InitializeAsync(RepositoryStorageType storageType, CancellationToken ct)
            {
                try
                {
                    _currentStorageType = storageType;

                    if (storageType == RepositoryStorageType.Json)
                    {
                        var repository = new JsonRelicRepository();
                        await repository.InitializeAsync(ct);
                        _repository = repository;
                    }
                    else
                    {
                        var repository = new MessagePackRelicRepository();
                        await repository.InitializeAsync(ct);
                        _repository = repository;
                    }

                    SetLog($"{GetStorageLabel()} リポジトリ(Relic)を初期化しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"初期化エラー: {ex.Message}");
                }
            }

            public UniTask ReloadCurrentRepositoryAsync(CancellationToken ct)
            {
                return InitializeAsync(_currentStorageType, ct);
            }

            public void ApplyDraft(RelicDraft draft)
            {
                _currentDraft = draft;
            }

            public IReadOnlyList<Relic> GetRelicSnapshot()
            {
                if (!EnsureRepositoryReady(false))
                {
                    return Array.Empty<Relic>();
                }

                try
                {
                    IReadOnlyList<Relic> relics = Array.Empty<Relic>();
                    _txManager.BeginROTransaction(tx =>
                    {
                        relics = _repository.All(tx);
                    });
                    return relics ?? Array.Empty<Relic>();
                }
                catch
                {
                    return Array.Empty<Relic>();
                }
            }

            public string GetStatusText()
            {
                return $"Storage: {GetStorageLabel()} / Count: {GetRelicSnapshot().Count}\nLast: {_lastLogMessage}";
            }

            public string FormatRelic(Relic relic)
            {
                return relic == null
                    ? "null"
                    : $"[{relic.Id}] {relic.Name} / {relic.Rarity} / Equipped={relic.IsEquipped} / Price={relic.Price} / Stats{relic.Stats}";
            }

            public async UniTask CreateRelicAsync(RelicDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var relic = draft.ToRelic();
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Create(tx, relic);
                    }, ct);
                    SetLog($"作成: {FormatRelic(relic)}");
                }
                catch (Exception ex)
                {
                    SetLog($"作成エラー: {ex.Message}");
                }
            }

            public UniTask ReadRelicAsync(int id, CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return UniTask.CompletedTask;
                }

                try
                {
                    Relic relic = null;
                    _txManager.BeginROTransaction(tx =>
                    {
                        relic = _repository.Read(tx, id);
                    });

                    if (relic != null)
                    {
                        ApplyDraft(RelicDraft.FromRelic(relic));
                        SetLog($"読取: {FormatRelic(relic)}");
                    }
                    else
                    {
                        SetLog($"Id={id} のレリックは存在しません。");
                    }
                }
                catch (Exception ex)
                {
                    SetLog($"読取エラー: {ex.Message}");
                }

                return UniTask.CompletedTask;
            }

            public async UniTask UpdateRelicAsync(RelicDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var relic = draft.ToRelic();
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Update(tx, relic);
                    }, ct);
                    SetLog($"更新: {FormatRelic(relic)}");
                }
                catch (Exception ex)
                {
                    SetLog($"更新エラー: {ex.Message}");
                }
            }

            public async UniTask DeleteRelicAsync(int id, CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Delete(tx, id);
                    }, ct);
                    SetLog($"削除: Id={id}");
                }
                catch (Exception ex)
                {
                    SetLog($"削除エラー: {ex.Message}");
                }
            }

            public async UniTask SeedSampleRelicsAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var samples = GetSampleRelics();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        foreach (var sample in samples)
                        {
                            if (_repository.Read(tx, sample.Id) == null)
                            {
                                _repository.Create(tx, sample.ToRelic());
                            }
                            else
                            {
                                _repository.Update(tx, sample.ToRelic());
                            }
                        }
                    }, ct);

                    ApplyDraft(samples[0]);
                    SetLog($"サンプル {samples.Length} 個を投入または更新しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"サンプル投入エラー: {ex.Message}");
                }
            }

            public async UniTask ReplaceWithSampleRelicsAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var samples = GetSampleRelics();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        foreach (var relic in _repository.All(tx).ToArray())
                        {
                            _repository.Delete(tx, relic.Id);
                        }

                        foreach (var sample in samples)
                        {
                            _repository.Create(tx, sample.ToRelic());
                        }
                    }, ct);

                    ApplyDraft(samples[0]);
                    SetLog($"サンプル {samples.Length} 個で置き換えました。");
                }
                catch (Exception ex)
                {
                    SetLog($"サンプル置換エラー: {ex.Message}");
                }
            }

            public async UniTask CreateRelicWaveAsync(int count, CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var startId = GetNextRelicId();
                var lastCreated = default(RelicDraft);

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        for (var i = 0; i < count; i++)
                        {
                            var id = startId + i;
                            lastCreated = new RelicDraft(
                                id,
                                $"Relic-{id:D3}",
                                (RelicRarity)(i % Enum.GetValues(typeof(RelicRarity)).Length),
                                i % 2 == 0,
                                250L * (i + 1),
                                new RelicStats(5 + i, 3 + i, 0.05f * (i + 1)));
                            _repository.Create(tx, lastCreated.ToRelic());
                        }
                    }, ct);

                    ApplyDraft(lastCreated);
                    SetLog($"連番レリックを {count} 個追加しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"一括追加エラー: {ex.Message}");
                }
            }

            public async UniTask ClearAllRelicsAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var snapshot = GetRelicSnapshot();
                if (snapshot.Count == 0)
                {
                    SetLog("削除対象のレリックはありません。");
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        foreach (var relic in snapshot)
                        {
                            _repository.Delete(tx, relic.Id);
                        }
                    }, ct);
                    SetLog($"{snapshot.Count} 個のレリックを削除しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"一括削除エラー: {ex.Message}");
                }
            }

            public async UniTask RunDuplicateCreateScenarioAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var draft = CreateIsolatedScenarioDraft();
                if (!ValidateDraft(draft))
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        var relic = draft.ToRelic();
                        _repository.Create(tx, relic);
                        _repository.Create(tx, relic);
                    }, ct);
                    SetLog("重複 Create テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var persisted = false;
                    _txManager.BeginROTransaction(tx =>
                    {
                        persisted = _repository.Read(tx, draft.Id) != null;
                    });
                    SetLog($"重複 Create テスト: {ex.Message} / Rollback={(persisted ? "失敗" : "成功")} / Id={draft.Id}");
                }
            }

            public async UniTask RunMissingUpdateScenarioAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var draft = CreateIsolatedScenarioDraft();
                if (!ValidateDraft(draft))
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Update(tx, draft.ToRelic());
                    }, ct);
                    SetLog("存在しない Update テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var exists = false;
                    _txManager.BeginROTransaction(tx =>
                    {
                        exists = _repository.Read(tx, draft.Id) != null;
                    });
                    SetLog($"存在しない Update テスト: {ex.Message} / Persisted={(exists ? "あり" : "なし")} / Id={draft.Id}");
                }
            }

            public async UniTask RunRollbackScenarioAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                var draft = CreateIsolatedScenarioDraft();
                if (!ValidateDraft(draft))
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Create(tx, draft.ToRelic());
                        throw new InvalidOperationException("Intentional rollback from RepositoryDebugTest");
                    }, ct);
                    SetLog("Rollback テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var persisted = false;
                    _txManager.BeginROTransaction(tx =>
                    {
                        persisted = _repository.Read(tx, draft.Id) != null;
                    });
                    SetLog($"Rollback テスト: {ex.Message} / Persisted={(persisted ? "あり" : "なし")} / Id={draft.Id}");
                }
            }

            public async UniTask RunReadYourWriteScenarioAsync(RelicDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var mode = string.Empty;
                    Relic staged = null;
                    var visibleCount = 0;

                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx, draft.Id) == null)
                        {
                            mode = "Create";
                            _repository.Create(tx, draft.ToRelic());
                        }
                        else
                        {
                            mode = "Update";
                            _repository.Update(tx, draft.ToRelic());
                        }

                        staged = _repository.Read(tx, draft.Id);
                        visibleCount = _repository.All(tx).Count;
                    }, ct);

                    Relic committed = null;
                    _txManager.BeginROTransaction(tx =>
                    {
                        committed = _repository.Read(tx, draft.Id);
                    });

                    SetLog($"RW 内 Read: Mode={mode}, VisibleCount={visibleCount}, Staged={FormatRelic(staged)}, Committed={FormatRelic(committed)}");
                }
                catch (Exception ex)
                {
                    SetLog($"RW 内 Read テストエラー: {ex.Message}");
                }
            }

            private bool EnsureRepositoryReady(bool reportError = true)
            {
                if (_repository != null)
                {
                    return true;
                }

                if (reportError)
                {
                    SetLog("リポジトリが未初期化です。");
                }

                return false;
            }

            private bool ValidateDraft(RelicDraft draft)
            {
                if (string.IsNullOrWhiteSpace(draft.Name))
                {
                    SetLog("名前を入力してください。");
                    return false;
                }

                return true;
            }

            private RelicDraft CreateIsolatedScenarioDraft()
            {
                return _currentDraft.WithId(GetNextRelicId());
            }

            private int GetNextRelicId()
            {
                var nextId = 1;
                foreach (var relic in GetRelicSnapshot())
                {
                    nextId = Math.Max(nextId, relic.Id + 1);
                }

                return nextId;
            }

            private RelicDraft[] GetSampleRelics()
            {
                return new[]
                {
                    new RelicDraft(1, "Bronze Sword", RelicRarity.Common, false, 100, new RelicStats(10, 2, 0.05f)),
                    new RelicDraft(2, "Moon Charm", RelicRarity.Rare, true, 1500, new RelicStats(2, 8, 0.12f)),
                    new RelicDraft(10, "Phoenix Crown", RelicRarity.Legendary, false, 99999, new RelicStats(25, 20, 0.35f)),
                };
            }

            private string GetStorageLabel()
            {
                return _currentStorageType == RepositoryStorageType.MessagePack ? "MessagePack" : "Json";
            }

            private void SetLog(string message)
            {
                _lastLogMessage = message;
                Debug.Log($"[RepositoryDebugTest/Relic] {message}");
            }
        }

        private sealed class WorldSettingsRepositoryTester
        {
            private IWorldSettingsRepository _repository;
            private readonly TxManager _txManager = new();
            private RepositoryStorageType _currentStorageType = RepositoryStorageType.Json;
            private WorldSettingsDraft _currentDraft = WorldSettingsDraft.Default;
            private string _lastLogMessage = "未実行";

            public WorldSettingsDraft CurrentDraft => _currentDraft;

            public async UniTask InitializeAsync(RepositoryStorageType storageType, CancellationToken ct)
            {
                try
                {
                    _currentStorageType = storageType;

                    if (storageType == RepositoryStorageType.Json)
                    {
                        var repository = new JsonWorldSettingsRepository();
                        await repository.InitializeAsync(ct);
                        _repository = repository;
                    }
                    else
                    {
                        var repository = new MessagePackWorldSettingsRepository();
                        await repository.InitializeAsync(ct);
                        _repository = repository;
                    }

                    SetLog($"{GetStorageLabel()} リポジトリ(WorldSettings)を初期化しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"初期化エラー: {ex.Message}");
                }
            }

            public UniTask ReloadCurrentRepositoryAsync(CancellationToken ct)
            {
                return InitializeAsync(_currentStorageType, ct);
            }

            public void ApplyDraft(WorldSettingsDraft draft)
            {
                _currentDraft = draft;
            }

            public WorldSettings GetWorldSettingsSnapshot()
            {
                if (!EnsureRepositoryReady(false))
                {
                    return null;
                }

                try
                {
                    WorldSettings settings = null;
                    _txManager.BeginROTransaction(tx =>
                    {
                        settings = _repository.Read(tx);
                    });
                    return settings;
                }
                catch
                {
                    return null;
                }
            }

            public string GetStatusText()
            {
                return $"Storage: {GetStorageLabel()} / Exists: {(GetWorldSettingsSnapshot() == null ? "No" : "Yes")}\nLast: {_lastLogMessage}";
            }

            public string DescribeWorldSettings(WorldSettings settings)
            {
                return settings == null
                    ? "なし"
                    : $"{settings.RegionName} / {settings.Difficulty} / Night={settings.NightMode} / SpawnRate={settings.SpawnRate:0.00} / Start{settings.StartPosition}";
            }

            public async UniTask CreateWorldSettingsAsync(WorldSettingsDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var settings = draft.ToWorldSettings();
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Create(tx, settings);
                    }, ct);
                    SetLog($"作成: {DescribeWorldSettings(settings)}");
                }
                catch (Exception ex)
                {
                    SetLog($"作成エラー: {ex.Message}");
                }
            }

            public UniTask ReadWorldSettingsAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return UniTask.CompletedTask;
                }

                try
                {
                    var settings = GetWorldSettingsSnapshot();
                    if (settings != null)
                    {
                        ApplyDraft(WorldSettingsDraft.FromWorldSettings(settings));
                        SetLog($"読取: {DescribeWorldSettings(settings)}");
                    }
                    else
                    {
                        SetLog("WorldSettings データは存在しません。");
                    }
                }
                catch (Exception ex)
                {
                    SetLog($"読取エラー: {ex.Message}");
                }

                return UniTask.CompletedTask;
            }

            public async UniTask UpdateWorldSettingsAsync(WorldSettingsDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var settings = draft.ToWorldSettings();
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Update(tx, settings);
                    }, ct);
                    SetLog($"更新: {DescribeWorldSettings(settings)}");
                }
                catch (Exception ex)
                {
                    SetLog($"更新エラー: {ex.Message}");
                }
            }

            public async UniTask DeleteWorldSettingsAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        _repository.Delete(tx);
                    }, ct);
                    SetLog("削除: WorldSettings データを削除しました。");
                }
                catch (Exception ex)
                {
                    SetLog($"削除エラー: {ex.Message}");
                }
            }

            public async UniTask ClearAllWorldSettingsAsync(CancellationToken ct)
            {
                if (!EnsureRepositoryReady())
                {
                    return;
                }

                if (GetWorldSettingsSnapshot() == null)
                {
                    SetLog("削除対象の WorldSettings データはありません。");
                    return;
                }

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) != null)
                        {
                            _repository.Delete(tx);
                        }
                    }, ct);
                    SetLog("WorldSettings データを全てクリアしました。");
                }
                catch (Exception ex)
                {
                    SetLog($"全クリアエラー: {ex.Message}");
                }
            }

            public async UniTask SaveWorldSettingsAsync(WorldSettingsDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) == null)
                        {
                            _repository.Create(tx, draft.ToWorldSettings());
                        }
                        else
                        {
                            _repository.Update(tx, draft.ToWorldSettings());
                        }
                    }, ct);
                    SetLog($"保存(Create / Update): {DescribeWorldSettings(draft.ToWorldSettings())}");
                }
                catch (Exception ex)
                {
                    SetLog($"保存エラー: {ex.Message}");
                }
            }

            public UniTask SaveSampleWorldSettingsAsync(CancellationToken ct)
            {
                return SaveWorldSettingsAsync(new WorldSettingsDraft("AncientRuins", WorldDifficulty.Hard, true, 1.35f, new Position(4, -2)), ct);
            }

            public async UniTask RunDuplicateCreateScenarioAsync(WorldSettingsDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                var before = GetWorldSettingsSnapshot();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) != null)
                        {
                            _repository.Delete(tx);
                        }

                        var settings = draft.ToWorldSettings();
                        _repository.Create(tx, settings);
                        _repository.Create(tx, settings);
                    }, ct);
                    SetLog("重複 Create テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var after = GetWorldSettingsSnapshot();
                    SetLog($"重複 Create テスト: {ex.Message} / Before={DescribeWorldSettings(before)} / After={DescribeWorldSettings(after)}");
                }
            }

            public async UniTask RunMissingUpdateScenarioAsync(WorldSettingsDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                var before = GetWorldSettingsSnapshot();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) != null)
                        {
                            _repository.Delete(tx);
                        }

                        _repository.Update(tx, draft.ToWorldSettings());
                    }, ct);
                    SetLog("存在しない Update テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var after = GetWorldSettingsSnapshot();
                    SetLog($"存在しない Update テスト: {ex.Message} / Before={DescribeWorldSettings(before)} / After={DescribeWorldSettings(after)}");
                }
            }

            public async UniTask RunRollbackScenarioAsync(WorldSettingsDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                var before = GetWorldSettingsSnapshot();

                try
                {
                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) == null)
                        {
                            _repository.Create(tx, draft.ToWorldSettings());
                        }
                        else
                        {
                            _repository.Update(tx, draft.ToWorldSettings());
                        }

                        throw new InvalidOperationException("Intentional rollback from RepositoryDebugTest");
                    }, ct);
                    SetLog("Rollback テストが想定外に成功しました。");
                }
                catch (Exception ex)
                {
                    var after = GetWorldSettingsSnapshot();
                    SetLog($"Rollback テスト: {ex.Message} / Before={DescribeWorldSettings(before)} / After={DescribeWorldSettings(after)}");
                }
            }

            public async UniTask RunReadYourWriteScenarioAsync(WorldSettingsDraft draft, CancellationToken ct)
            {
                if (!EnsureRepositoryReady() || !ValidateDraft(draft))
                {
                    return;
                }

                ApplyDraft(draft);

                try
                {
                    var mode = string.Empty;
                    WorldSettings staged = null;

                    await _txManager.BeginRWTransactionAsync(tx =>
                    {
                        if (_repository.Read(tx) == null)
                        {
                            mode = "Create";
                            _repository.Create(tx, draft.ToWorldSettings());
                        }
                        else
                        {
                            mode = "Update";
                            _repository.Update(tx, draft.ToWorldSettings());
                        }

                        staged = _repository.Read(tx);
                    }, ct);

                    var committed = GetWorldSettingsSnapshot();
                    SetLog($"RW 内 Read: Mode={mode}, Staged={DescribeWorldSettings(staged)}, Committed={DescribeWorldSettings(committed)}");
                }
                catch (Exception ex)
                {
                    SetLog($"RW 内 Read テストエラー: {ex.Message}");
                }
            }

            private bool EnsureRepositoryReady(bool reportError = true)
            {
                if (_repository != null)
                {
                    return true;
                }

                if (reportError)
                {
                    SetLog("リポジトリが未初期化です。");
                }

                return false;
            }

            private bool ValidateDraft(WorldSettingsDraft draft)
            {
                if (string.IsNullOrWhiteSpace(draft.RegionName))
                {
                    SetLog("Region を入力してください。");
                    return false;
                }

                return true;
            }

            private string GetStorageLabel()
            {
                return _currentStorageType == RepositoryStorageType.MessagePack ? "MessagePack" : "Json";
            }

            private void SetLog(string message)
            {
                _lastLogMessage = message;
                Debug.Log($"[RepositoryDebugTest/WorldSettings] {message}");
            }
        }

        private enum RepositoryStorageType
        {
            Json = 0,
            MessagePack = 1,
        }

        private readonly struct MonsterDraft
        {
            public static MonsterDraft Default => new(1, "Slime", 1, new Position(0, 0));

            public MonsterDraft(int id, string name, int level, Position position)
            {
                Id = id;
                Name = name ?? string.Empty;
                Level = level;
                Position = position;
            }

            public int Id { get; }

            public string Name { get; }

            public int Level { get; }

            public Position Position { get; }

            public Monster ToMonster()
            {
                return new Monster(Id, Name, Level, Position);
            }

            public MonsterDraft WithId(int id)
            {
                return new MonsterDraft(id, Name, Level, Position);
            }

            public static MonsterDraft FromMonster(Monster monster)
            {
                return new MonsterDraft(monster.Id, monster.Name, monster.Level, monster.Position);
            }
        }

        private readonly struct HeroDraft
        {
            public static HeroDraft Default => new("Hero", 1, new Position(0, 0));

            public HeroDraft(string name, int level, Position position)
            {
                Name = name ?? string.Empty;
                Level = level;
                Position = position;
            }

            public string Name { get; }

            public int Level { get; }

            public Position Position { get; }

            public Hero ToHero()
            {
                return new Hero(Name, Level, Position);
            }

            public static HeroDraft FromHero(Hero hero)
            {
                return new HeroDraft(hero.Name, hero.Level, hero.Position);
            }
        }

        private readonly struct RelicDraft
        {
            public static RelicDraft Default => new(1, "Bronze Sword", RelicRarity.Common, false, 100, new RelicStats(10, 2, 0.05f));

            public RelicDraft(int id, string name, RelicRarity rarity, bool isEquipped, long price, RelicStats stats)
            {
                Id = id;
                Name = name ?? string.Empty;
                Rarity = rarity;
                IsEquipped = isEquipped;
                Price = price;
                Stats = stats;
            }

            public int Id { get; }

            public string Name { get; }

            public RelicRarity Rarity { get; }

            public bool IsEquipped { get; }

            public long Price { get; }

            public RelicStats Stats { get; }

            public Relic ToRelic()
            {
                return new Relic(Id, Name, Rarity, IsEquipped, Price, Stats);
            }

            public RelicDraft WithId(int id)
            {
                return new RelicDraft(id, Name, Rarity, IsEquipped, Price, Stats);
            }

            public static RelicDraft FromRelic(Relic relic)
            {
                return new RelicDraft(relic.Id, relic.Name, relic.Rarity, relic.IsEquipped, relic.Price, relic.Stats);
            }
        }

        private readonly struct WorldSettingsDraft
        {
            public static WorldSettingsDraft Default => new("Grassland", WorldDifficulty.Normal, false, 1f, new Position(0, 0));

            public WorldSettingsDraft(string regionName, WorldDifficulty difficulty, bool nightMode, float spawnRate, Position startPosition)
            {
                RegionName = regionName ?? string.Empty;
                Difficulty = difficulty;
                NightMode = nightMode;
                SpawnRate = spawnRate;
                StartPosition = startPosition;
            }

            public string RegionName { get; }

            public WorldDifficulty Difficulty { get; }

            public bool NightMode { get; }

            public float SpawnRate { get; }

            public Position StartPosition { get; }

            public WorldSettings ToWorldSettings()
            {
                return new WorldSettings(RegionName, Difficulty, NightMode, SpawnRate, StartPosition);
            }

            public static WorldSettingsDraft FromWorldSettings(WorldSettings settings)
            {
                return new WorldSettingsDraft(settings.RegionName, settings.Difficulty, settings.NightMode, settings.SpawnRate, settings.StartPosition);
            }
        }
    }
}
