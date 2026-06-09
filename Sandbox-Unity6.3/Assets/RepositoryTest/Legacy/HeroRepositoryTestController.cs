using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.DebugUI;
using Lilja.Repository;
using RepositoryTest.Repositories;
using UnityEngine;
using UnityEngine.UI;
using UIDocument = UnityEngine.UIElements.UIDocument;

namespace RepositoryTest
{
    /// <summary>
    /// Lilja.Repository の Hero（Singleton）テスト用 UI コントローラ。
    /// </summary>
    public class HeroRepositoryTestController : MonoBehaviour
    {
        [SerializeField]
        private Dropdown _storageTypeDropdown;

        [SerializeField]
        private InputField _nameInput;

        [SerializeField]
        private InputField _levelInput;

        [SerializeField]
        private InputField _posXInput;

        [SerializeField]
        private InputField _posYInput;

        [SerializeField]
        private Button _createButton;

        [SerializeField]
        private Button _readButton;

        [SerializeField]
        private Button _updateButton;

        [SerializeField]
        private Button _deleteButton;

        [SerializeField]
        private Text _logText;

        [SerializeField]
        private Text _listText;

        [SerializeField]
        private Text _listHeaderText;

        private IHeroRepository _repository;
        private TxManager _txManager;
        private IDisposable _debugMenuRegistration;
        private StorageType _currentStorageType = StorageType.Json;
        private HeroDraft _debugDraft = HeroDraft.Default;
        private string _lastLogMessage = "未実行";

        internal string CurrentStorageLabel => GetStorageTypeLabel(_currentStorageType);

        private void Start()
        {
            _txManager = new TxManager();

            SetupStorageDropdown();
            SetupButtons();
            ApplyDraftToUi(_debugDraft);
            RegisterDebugMenu();

            InitializeRepositoryAsync(_currentStorageType, destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            _debugMenuRegistration?.Dispose();

            if (DebugMenu.IsInitialized)
            {
                DebugMenu.BackToRoot();
            }
        }

        internal HeroDraft GetDebugDraft()
        {
            return TryGetInputDraft(out var draft, false) ? draft : _debugDraft;
        }

        internal void ApplyDebugDraft(HeroDraft draft)
        {
            _debugDraft = draft;
            ApplyDraftToUi(draft);
        }

        internal Hero GetHeroSnapshot()
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

        internal string GetHeroDebugStatusText()
        {
            return $"Storage: {CurrentStorageLabel} / Exists: {(GetHeroSnapshot() == null ? "No" : "Yes")}\nLast: {_lastLogMessage}";
        }

        internal string DescribeHero(Hero hero)
        {
            return hero == null
                ? "なし"
                : $"{hero.Name}  Lv.{hero.Level}  Pos{hero.Position}";
        }

        internal async UniTask SwitchStorageAsync(StorageType storageType, CancellationToken ct)
        {
            await InitializeRepositoryAsync(storageType, ct);
        }

        internal UniTask ReloadCurrentRepositoryAsync(CancellationToken ct)
        {
            return InitializeRepositoryAsync(_currentStorageType, ct);
        }

        internal async UniTask CreateHeroAsync(HeroDraft draft, CancellationToken ct)
        {
            if (!EnsureRepositoryReady() || !ValidateDraft(draft))
            {
                return;
            }

            ApplyDebugDraft(draft);

            try
            {
                var hero = draft.ToHero();
                await _txManager.BeginRWTransactionAsync(tx =>
                {
                    _repository.Create(tx, hero);
                }, ct);
                SetLog($"作成: {DescribeHero(hero)}");
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"作成エラー: {ex.Message}");
            }
        }

        internal UniTask ReadHeroAsync(CancellationToken ct)
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
                    ApplyDebugDraft(HeroDraft.FromHero(hero));
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

        internal async UniTask UpdateHeroAsync(HeroDraft draft, CancellationToken ct)
        {
            if (!EnsureRepositoryReady() || !ValidateDraft(draft))
            {
                return;
            }

            ApplyDebugDraft(draft);

            try
            {
                var hero = draft.ToHero();
                await _txManager.BeginRWTransactionAsync(tx =>
                {
                    _repository.Update(tx, hero);
                }, ct);
                SetLog($"更新: {DescribeHero(hero)}");
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"更新エラー: {ex.Message}");
            }
        }

        internal async UniTask DeleteHeroAsync(CancellationToken ct)
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
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"削除エラー: {ex.Message}");
            }
        }

        internal async UniTask SaveHeroAsync(HeroDraft draft, CancellationToken ct)
        {
            if (!EnsureRepositoryReady() || !ValidateDraft(draft))
            {
                return;
            }

            ApplyDebugDraft(draft);

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
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"保存エラー: {ex.Message}");
            }
        }

        internal UniTask SaveSampleHeroAsync(CancellationToken ct)
        {
            return SaveHeroAsync(new HeroDraft("Knight", 12, new Position(3, 2)), ct);
        }

        internal async UniTask RunDuplicateCreateScenarioAsync(HeroDraft draft, CancellationToken ct)
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

            RefreshList();
        }

        internal async UniTask RunMissingUpdateScenarioAsync(HeroDraft draft, CancellationToken ct)
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

            RefreshList();
        }

        internal async UniTask RunRollbackScenarioAsync(HeroDraft draft, CancellationToken ct)
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

                    throw new InvalidOperationException("Intentional rollback from HeroRepositoryTest");
                }, ct);
                SetLog("Rollback テストが想定外に成功しました。");
            }
            catch (Exception ex)
            {
                var after = GetHeroSnapshot();
                SetLog($"Rollback テスト: {ex.Message} / Before={DescribeHero(before)} / After={DescribeHero(after)}");
            }

            RefreshList();
        }

        internal async UniTask RunReadYourWriteScenarioAsync(HeroDraft draft, CancellationToken ct)
        {
            if (!EnsureRepositoryReady() || !ValidateDraft(draft))
            {
                return;
            }

            ApplyDebugDraft(draft);

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
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"RW 内 Read テストエラー: {ex.Message}");
            }
        }

        private void SetupStorageDropdown()
        {
            if (_storageTypeDropdown == null)
            {
                return;
            }

            _storageTypeDropdown.ClearOptions();
            _storageTypeDropdown.AddOptions(new List<string>
            {
                GetStorageTypeLabel(StorageType.Json),
                GetStorageTypeLabel(StorageType.MessagePack),
            });
            _storageTypeDropdown.SetValueWithoutNotify((int)_currentStorageType);
            _storageTypeDropdown.onValueChanged.AddListener(_ => OnStorageTypeChangedAsync(destroyCancellationToken).Forget());
        }

        private void SetupButtons()
        {
            if (_createButton != null) _createButton.onClick.AddListener(() => OnCreateAsync(destroyCancellationToken).Forget());
            if (_readButton != null) _readButton.onClick.AddListener(() => OnReadAsync(destroyCancellationToken).Forget());
            if (_updateButton != null) _updateButton.onClick.AddListener(() => OnUpdateAsync(destroyCancellationToken).Forget());
            if (_deleteButton != null) _deleteButton.onClick.AddListener(() => OnDeleteAsync(destroyCancellationToken).Forget());
        }

        private void RegisterDebugMenu()
        {
            _debugMenuRegistration?.Dispose();
            EnsureDebugMenuInitialized();

            var root = DebugMenu.GetPage<RepositoryDebugRootPage>();
            if (root == null)
            {
                Debug.LogWarning("[HeroRepositoryTest] Debug menu root page is not available.");
                return;
            }

            _debugMenuRegistration = root.AddDebugUI(builder =>
            {
                var button = new DebugNavigationButton("Hero Repository");
                button.clicked += () => DebugMenu.NavigateToTemp(
                    "Hero Repository",
                    tempBuilder => new HeroRepositoryDebugMenuPage(this).Build(tempBuilder));
                builder.VisualElement(button);
            });
        }

        private async UniTask OnStorageTypeChangedAsync(CancellationToken ct)
        {
            if (_storageTypeDropdown == null)
            {
                return;
            }

            await SwitchStorageAsync((StorageType)_storageTypeDropdown.value, ct);
        }

        private async UniTask InitializeRepositoryAsync(StorageType storageType, CancellationToken ct)
        {
            try
            {
                _currentStorageType = storageType;
                UpdateStorageDropdown();

                if (storageType == StorageType.Json)
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

                SetLog($"{CurrentStorageLabel} リポジトリ(Hero)を初期化しました。");
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"初期化エラー: {ex.Message}");
            }
        }

        private async UniTask OnCreateAsync(CancellationToken ct)
        {
            if (!TryGetInputDraft(out var draft, true))
            {
                return;
            }

            await CreateHeroAsync(draft, ct);
        }

        private async UniTask OnReadAsync(CancellationToken ct)
        {
            await ReadHeroAsync(ct);
        }

        private async UniTask OnUpdateAsync(CancellationToken ct)
        {
            if (!TryGetInputDraft(out var draft, true))
            {
                return;
            }

            await UpdateHeroAsync(draft, ct);
        }

        private async UniTask OnDeleteAsync(CancellationToken ct)
        {
            await DeleteHeroAsync(ct);
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

        private void RefreshList()
        {
            if (_listHeaderText == null || _listText == null)
            {
                return;
            }

            if (!EnsureRepositoryReady(false))
            {
                _listHeaderText.text = "現在のデータ";
                _listText.text = "リポジトリ未初期化";
                return;
            }

            var hero = GetHeroSnapshot();
            if (hero == null)
            {
                _listHeaderText.text = "現在のデータ (未作成)";
                _listText.text = "データなし";
                return;
            }

            _listHeaderText.text = "現在のデータ (1件)";
            _listText.text = $"Name: {hero.Name}\nLevel: {hero.Level}\nPos: {hero.Position}";
        }

        private bool TryGetInputDraft(out HeroDraft draft, bool reportErrors)
        {
            draft = _debugDraft;

            var name = _nameInput?.text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                if (reportErrors)
                {
                    SetLog("名前を入力してください。");
                }
                return false;
            }

            if (_levelInput == null || !int.TryParse(_levelInput.text, out var level))
            {
                if (reportErrors)
                {
                    SetLog("レベルを正しく入力してください。");
                }
                return false;
            }

            if (_posXInput == null || _posYInput == null ||
                !int.TryParse(_posXInput.text, out var posX) ||
                !int.TryParse(_posYInput.text, out var posY))
            {
                if (reportErrors)
                {
                    SetLog("座標を正しく入力してください。");
                }
                return false;
            }

            draft = new HeroDraft(name, level, new Position(posX, posY));
            _debugDraft = draft;
            return true;
        }

        private void ApplyDraftToUi(HeroDraft draft)
        {
            if (_nameInput != null) _nameInput.text = draft.Name;
            if (_levelInput != null) _levelInput.text = draft.Level.ToString();
            if (_posXInput != null) _posXInput.text = draft.Position.X.ToString();
            if (_posYInput != null) _posYInput.text = draft.Position.Y.ToString();
        }

        private void UpdateStorageDropdown()
        {
            if (_storageTypeDropdown != null)
            {
                _storageTypeDropdown.SetValueWithoutNotify((int)_currentStorageType);
            }
        }

        private void SetLog(string message)
        {
            _lastLogMessage = message;
            Debug.Log($"[HeroRepositoryTest] {message}");

            if (_logText != null)
            {
                _logText.text = message;
            }
        }

        private void EnsureDebugMenuInitialized()
        {
            if (!DebugMenu.IsInitialized || DebugMenu.GetPage<RepositoryDebugRootPage>() == null)
            {
                DebugMenu.Initialize(new RepositoryDebugRootPage());
            }

            if (UnityEngine.Object.FindFirstObjectByType<DebugMenuOpenButton>() != null)
            {
                return;
            }

            var go = new GameObject("[RepositoryTestDebugMenuOpenButton]");
            go.AddComponent<UIDocument>();
            go.AddComponent<DebugMenuOpenButton>();
        }

        private static string GetStorageTypeLabel(StorageType storageType)
        {
            return storageType == StorageType.MessagePack ? "MessagePack" : "Json";
        }

        internal readonly struct HeroDraft
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

            public Hero ToHero() => new(Name, Level, Position);

            public static HeroDraft FromHero(Hero hero)
            {
                return new HeroDraft(hero.Name, hero.Level, hero.Position);
            }
        }

        internal enum StorageType
        {
            Json = 0,
            MessagePack = 1,
        }

        private sealed class HeroRepositoryDebugMenuPage
        {
            private readonly HeroRepositoryTestController _controller;

            private DebugLabel _statusLabel;
            private DebugLabel _currentHeroLabel;
            private DebugTextField _nameField;
            private DebugIntegerField _levelField;
            private DebugIntegerField _posXField;
            private DebugIntegerField _posYField;

            public HeroRepositoryDebugMenuPage(HeroRepositoryTestController controller)
            {
                _controller = controller;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.VisualElement(new DebugLabel("Hero の Singleton 動作を、CRUD と失敗系シナリオ込みで試せます。"));

                _statusLabel = new DebugLabel();
                builder.VisualElement(_statusLabel);

                _currentHeroLabel = new DebugLabel();
                builder.VisualElement(_currentHeroLabel);

                builder.Foldout("ストレージ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        var jsonButton = new DebugSecondaryButton("Json");
                        jsonButton.clicked += () => Execute(ct => _controller.SwitchStorageAsync(StorageType.Json, ct));
                        row.VisualElement(jsonButton);

                        var messagePackButton = new DebugSecondaryButton("MessagePack");
                        messagePackButton.clicked += () => Execute(ct => _controller.SwitchStorageAsync(StorageType.MessagePack, ct));
                        row.VisualElement(messagePackButton);

                        var reloadButton = new DebugButton("再読込");
                        reloadButton.clicked += () => Execute(_controller.ReloadCurrentRepositoryAsync);
                        row.VisualElement(reloadButton);
                    });
                });

                builder.Foldout("入力", foldout =>
                {
                    _nameField = new DebugTextField("Name");
                    _levelField = new DebugIntegerField("Level");
                    _posXField = new DebugIntegerField("Pos X");
                    _posYField = new DebugIntegerField("Pos Y");

                    foldout.VisualElement(_nameField);
                    foldout.VisualElement(_levelField);
                    foldout.VisualElement(_posXField);
                    foldout.VisualElement(_posYField);

                    foldout.HorizontalScope(row =>
                    {
                        var createButton = new DebugButton("Create");
                        createButton.clicked += () => Execute(ct => _controller.CreateHeroAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(createButton);

                        var readButton = new DebugSecondaryButton("Read");
                        readButton.clicked += () => Execute(_controller.ReadHeroAsync);
                        row.VisualElement(readButton);
                    });

                    foldout.HorizontalScope(row =>
                    {
                        var updateButton = new DebugButton("Update");
                        updateButton.clicked += () => Execute(ct => _controller.UpdateHeroAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(updateButton);

                        var deleteButton = new DebugDangerButton("Delete");
                        deleteButton.clicked += () => Execute(_controller.DeleteHeroAsync);
                        row.VisualElement(deleteButton);
                    });

                    foldout.HorizontalScope(row =>
                    {
                        var saveButton = new DebugSecondaryButton("Create / Update");
                        saveButton.clicked += () => Execute(ct => _controller.SaveHeroAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(saveButton);

                        var sampleButton = new DebugButton("サンプル Hero 保存");
                        sampleButton.clicked += () => Execute(_controller.SaveSampleHeroAsync);
                        row.VisualElement(sampleButton);
                    });
                });

                builder.Foldout("シナリオ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        var duplicateButton = new DebugSecondaryButton("重複 Create");
                        duplicateButton.clicked += () => Execute(ct => _controller.RunDuplicateCreateScenarioAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(duplicateButton);

                        var missingUpdateButton = new DebugSecondaryButton("存在しない Update");
                        missingUpdateButton.clicked += () => Execute(ct => _controller.RunMissingUpdateScenarioAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(missingUpdateButton);
                    });

                    foldout.HorizontalScope(row =>
                    {
                        var rollbackButton = new DebugSecondaryButton("明示 Rollback");
                        rollbackButton.clicked += () => Execute(ct => _controller.RunRollbackScenarioAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(rollbackButton);

                        var readYourWriteButton = new DebugSecondaryButton("RW 内 Read");
                        readYourWriteButton.clicked += () => Execute(ct => _controller.RunReadYourWriteScenarioAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(readYourWriteButton);
                    });
                });

                var refreshButton = new DebugSecondaryButton("状態を再描画");
                refreshButton.clicked += RefreshView;
                builder.VisualElement(refreshButton);

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
                if (_controller == null)
                {
                    if (_statusLabel != null)
                    {
                        _statusLabel.text = "Controller が破棄されたため、このページは更新できません。";
                    }

                    if (_currentHeroLabel != null)
                    {
                        _currentHeroLabel.text = string.Empty;
                    }

                    return;
                }

                var draft = _controller.GetDebugDraft();
                _nameField?.SetValueWithoutNotify(draft.Name);
                _levelField?.SetValueWithoutNotify(draft.Level);
                _posXField?.SetValueWithoutNotify(draft.Position.X);
                _posYField?.SetValueWithoutNotify(draft.Position.Y);

                if (_statusLabel != null)
                {
                    _statusLabel.text = _controller.GetHeroDebugStatusText();
                }

                if (_currentHeroLabel != null)
                {
                    _currentHeroLabel.text = $"現在の Hero: {_controller.DescribeHero(_controller.GetHeroSnapshot())}";
                }
            }

            private void Execute(Func<CancellationToken, UniTask> action)
            {
                ExecuteAsync(action).Forget();
            }

            private async UniTask ExecuteAsync(Func<CancellationToken, UniTask> action)
            {
                if (_controller == null)
                {
                    return;
                }

                try
                {
                    await action(_controller.destroyCancellationToken);
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

        private sealed class RepositoryDebugRootPage : DebugPage
        {
            public override void Configure(IDebugUIBuilder builder)
            {
                builder.VisualElement(new DebugLabel("Lilja.Repository テストメニュー"));
                builder.VisualElement(new DebugLabel("RepositoryTest シーンの追加テスト UI をここから開けます。"));
                builder.Foldout("使い方", foldout =>
                {
                    foldout.VisualElement(new DebugLabel("左下の半透明ボタンを 3 連打すると再度メニューを開けます。"));
                    foldout.VisualElement(new DebugLabel("既存の uGUI を残したまま、失敗系や一括処理の検証を追加しています。"));
                });
            }
        }

    }
}
