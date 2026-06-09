using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Lilja.Repository の Monster テスト用 UI コントローラ。
    /// </summary>
    public class RepositoryTestController : MonoBehaviour
    {
        [SerializeField]
        private Dropdown _storageTypeDropdown;

        [SerializeField]
        private InputField _idInput;

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

        private IMonsterRepository _repository;
        private TxManager _txManager;
        private IDisposable _debugMenuRegistration;
        private StorageType _currentStorageType = StorageType.Json;
        private MonsterDraft _debugDraft = MonsterDraft.Default;
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

        internal MonsterDraft GetDebugDraft()
        {
            return TryGetInputDraft(out var draft, false) ? draft : _debugDraft;
        }

        internal void ApplyDebugDraft(MonsterDraft draft)
        {
            _debugDraft = draft;
            ApplyDraftToUi(draft);
        }

        internal IReadOnlyList<Monster> GetMonsterSnapshot()
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

        internal string GetMonsterDebugStatusText()
        {
            return $"Storage: {CurrentStorageLabel} / Count: {GetMonsterSnapshot().Count}\nLast: {_lastLogMessage}";
        }

        internal string FormatMonster(Monster monster)
        {
            return monster == null
                ? "null"
                : $"[{monster.Id}] {monster.Name}  Lv.{monster.Level}  Pos{monster.Position}";
        }

        internal async UniTask SwitchStorageAsync(StorageType storageType, CancellationToken ct)
        {
            await InitializeRepositoryAsync(storageType, ct);
        }

        internal UniTask ReloadCurrentRepositoryAsync(CancellationToken ct)
        {
            return InitializeRepositoryAsync(_currentStorageType, ct);
        }

        internal async UniTask CreateMonsterAsync(MonsterDraft draft, CancellationToken ct)
        {
            if (!EnsureRepositoryReady() || !ValidateDraft(draft))
            {
                return;
            }

            ApplyDebugDraft(draft);

            try
            {
                var monster = draft.ToMonster();
                await _txManager.BeginRWTransactionAsync(tx =>
                {
                    _repository.Create(tx, monster);
                }, ct);
                SetLog($"作成: {FormatMonster(monster)}");
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"作成エラー: {ex.Message}");
            }
        }

        internal UniTask ReadMonsterAsync(int id, CancellationToken ct)
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
                    ApplyDebugDraft(MonsterDraft.FromMonster(monster));
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

        internal async UniTask UpdateMonsterAsync(MonsterDraft draft, CancellationToken ct)
        {
            if (!EnsureRepositoryReady() || !ValidateDraft(draft))
            {
                return;
            }

            ApplyDebugDraft(draft);

            try
            {
                var monster = draft.ToMonster();
                await _txManager.BeginRWTransactionAsync(tx =>
                {
                    _repository.Update(tx, monster);
                }, ct);
                SetLog($"更新: {FormatMonster(monster)}");
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"更新エラー: {ex.Message}");
            }
        }

        internal async UniTask DeleteMonsterAsync(int id, CancellationToken ct)
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
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"削除エラー: {ex.Message}");
            }
        }

        internal async UniTask SeedSampleMonstersAsync(CancellationToken ct)
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

                ApplyDebugDraft(samples[0]);
                SetLog($"サンプル {samples.Length} 体を投入または更新しました。");
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"サンプル投入エラー: {ex.Message}");
            }
        }

        internal async UniTask ReplaceWithSampleMonstersAsync(CancellationToken ct)
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

                ApplyDebugDraft(samples[0]);
                SetLog($"サンプル {samples.Length} 体で置き換えました。");
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"サンプル置換エラー: {ex.Message}");
            }
        }

        internal async UniTask CreateMonsterWaveAsync(int count, CancellationToken ct)
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

                ApplyDebugDraft(lastCreated);
                SetLog($"連番モンスターを {count} 体追加しました。");
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"一括追加エラー: {ex.Message}");
            }
        }

        internal async UniTask ClearAllMonstersAsync(CancellationToken ct)
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
                RefreshList();
            }
            catch (Exception ex)
            {
                SetLog($"一括削除エラー: {ex.Message}");
            }
        }

        internal async UniTask RunDuplicateCreateScenarioAsync(CancellationToken ct)
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

            RefreshList();
        }

        internal async UniTask RunMissingUpdateScenarioAsync(CancellationToken ct)
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

            RefreshList();
        }

        internal async UniTask RunRollbackScenarioAsync(CancellationToken ct)
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
                    throw new InvalidOperationException("Intentional rollback from RepositoryTest");
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

            RefreshList();
        }

        internal async UniTask RunReadYourWriteScenarioAsync(MonsterDraft draft, CancellationToken ct)
        {
            if (!EnsureRepositoryReady() || !ValidateDraft(draft))
            {
                return;
            }

            ApplyDebugDraft(draft);

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
                Debug.LogWarning("[RepositoryTest] Debug menu root page is not available.");
                return;
            }

            _debugMenuRegistration = root.AddDebugUI(builder =>
            {
                var button = new DebugNavigationButton("Monster Repository");
                button.clicked += () => DebugMenu.NavigateToTemp(
                    "Monster Repository",
                    tempBuilder => new MonsterRepositoryDebugMenuPage(this).Build(tempBuilder));
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

                SetLog($"{CurrentStorageLabel} リポジトリを初期化しました。");
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

            await CreateMonsterAsync(draft, ct);
        }

        private async UniTask OnReadAsync(CancellationToken ct)
        {
            if (!TryParseIdFromInput(out var id, true))
            {
                return;
            }

            await ReadMonsterAsync(id, ct);
        }

        private async UniTask OnUpdateAsync(CancellationToken ct)
        {
            if (!TryGetInputDraft(out var draft, true))
            {
                return;
            }

            await UpdateMonsterAsync(draft, ct);
        }

        private async UniTask OnDeleteAsync(CancellationToken ct)
        {
            if (!TryParseIdFromInput(out var id, true))
            {
                return;
            }

            await DeleteMonsterAsync(id, ct);
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

        private void RefreshList()
        {
            if (_listHeaderText == null || _listText == null)
            {
                return;
            }

            if (!EnsureRepositoryReady(false))
            {
                _listHeaderText.text = "一覧";
                _listText.text = "リポジトリ未初期化";
                return;
            }

            var monsters = GetMonsterSnapshot();
            if (monsters.Count == 0)
            {
                _listHeaderText.text = "一覧 (0件)";
                _listText.text = "データなし";
                return;
            }

            _listHeaderText.text = $"一覧 ({monsters.Count}件)";
            var sb = new StringBuilder();
            foreach (var monster in monsters.OrderBy(monster => monster.Id))
            {
                sb.AppendLine(FormatMonster(monster));
            }

            _listText.text = sb.ToString();
        }

        private bool TryGetInputDraft(out MonsterDraft draft, bool reportErrors)
        {
            draft = _debugDraft;

            if (!TryParseIdFromInput(out var id, reportErrors))
            {
                return false;
            }

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

            draft = new MonsterDraft(id, name, level, new Position(posX, posY));
            _debugDraft = draft;
            return true;
        }

        private bool TryParseIdFromInput(out int id, bool reportErrors)
        {
            id = 0;

            if (_idInput != null && int.TryParse(_idInput.text, out id))
            {
                return true;
            }

            if (reportErrors)
            {
                SetLog("IDを正しく入力してください。");
            }

            return false;
        }

        private void ApplyDraftToUi(MonsterDraft draft)
        {
            if (_idInput != null) _idInput.text = draft.Id.ToString();
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

        private MonsterDraft CreateIsolatedScenarioDraft()
        {
            return GetDebugDraft().WithId(GetNextMonsterId());
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

        private void SetLog(string message)
        {
            _lastLogMessage = message;
            Debug.Log($"[RepositoryTest] {message}");

            if (_logText != null)
            {
                _logText.text = message;
            }
        }

        internal readonly struct MonsterDraft
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

            public Monster ToMonster() => new(Id, Name, Level, Position);

            public MonsterDraft WithId(int id) => new(id, Name, Level, Position);

            public static MonsterDraft FromMonster(Monster monster)
            {
                return new MonsterDraft(monster.Id, monster.Name, monster.Level, monster.Position);
            }
        }

        internal enum StorageType
        {
            Json = 0,
            MessagePack = 1,
        }

        private sealed class MonsterRepositoryDebugMenuPage
        {
            private readonly RepositoryTestController _controller;
            private readonly List<IDisposable> _monsterHandles = new();

            private DebugLabel _statusLabel;
            private DebugIntegerField _idField;
            private DebugTextField _nameField;
            private DebugIntegerField _levelField;
            private DebugIntegerField _posXField;
            private DebugIntegerField _posYField;
            private VirtualFoldout _monsterFoldout;

            public MonsterRepositoryDebugMenuPage(RepositoryTestController controller)
            {
                _controller = controller;
            }

            public void Build(IDebugUIBuilder builder)
            {
                builder.VisualElement(new DebugLabel("Monster Entity の CRUD / 一括投入 / 失敗系 / Tx 可視性を試せます。"));

                _statusLabel = new DebugLabel();
                builder.VisualElement(_statusLabel);

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
                    _idField = new DebugIntegerField("Id");
                    _nameField = new DebugTextField("Name");
                    _levelField = new DebugIntegerField("Level");
                    _posXField = new DebugIntegerField("Pos X");
                    _posYField = new DebugIntegerField("Pos Y");

                    foldout.VisualElement(_idField);
                    foldout.VisualElement(_nameField);
                    foldout.VisualElement(_levelField);
                    foldout.VisualElement(_posXField);
                    foldout.VisualElement(_posYField);

                    foldout.HorizontalScope(row =>
                    {
                        var createButton = new DebugButton("Create");
                        createButton.clicked += () => Execute(ct => _controller.CreateMonsterAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(createButton);

                        var readButton = new DebugSecondaryButton("Read");
                        readButton.clicked += () => Execute(ct => _controller.ReadMonsterAsync(_idField.value, ct));
                        row.VisualElement(readButton);
                    });

                    foldout.HorizontalScope(row =>
                    {
                        var updateButton = new DebugButton("Update");
                        updateButton.clicked += () => Execute(ct => _controller.UpdateMonsterAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(updateButton);

                        var deleteButton = new DebugDangerButton("Delete");
                        deleteButton.clicked += () => Execute(ct => _controller.DeleteMonsterAsync(_idField.value, ct));
                        row.VisualElement(deleteButton);
                    });
                });

                builder.Foldout("シナリオ", foldout =>
                {
                    foldout.HorizontalScope(row =>
                    {
                        var seedButton = new DebugButton("サンプル 3 体投入");
                        seedButton.clicked += () => Execute(_controller.SeedSampleMonstersAsync);
                        row.VisualElement(seedButton);

                        var replaceButton = new DebugSecondaryButton("サンプルで置換");
                        replaceButton.clicked += () => Execute(_controller.ReplaceWithSampleMonstersAsync);
                        row.VisualElement(replaceButton);
                    });

                    foldout.HorizontalScope(row =>
                    {
                        var batchButton = new DebugButton("連番 5 体追加");
                        batchButton.clicked += () => Execute(ct => _controller.CreateMonsterWaveAsync(5, ct));
                        row.VisualElement(batchButton);

                        var clearButton = new DebugDangerButton("全削除");
                        clearButton.clicked += () => Execute(_controller.ClearAllMonstersAsync);
                        row.VisualElement(clearButton);
                    });

                    foldout.HorizontalScope(row =>
                    {
                        var duplicateButton = new DebugSecondaryButton("重複 Create");
                        duplicateButton.clicked += () => Execute(_controller.RunDuplicateCreateScenarioAsync);
                        row.VisualElement(duplicateButton);

                        var missingUpdateButton = new DebugSecondaryButton("存在しない Update");
                        missingUpdateButton.clicked += () => Execute(_controller.RunMissingUpdateScenarioAsync);
                        row.VisualElement(missingUpdateButton);
                    });

                    foldout.HorizontalScope(row =>
                    {
                        var rollbackButton = new DebugSecondaryButton("明示 Rollback");
                        rollbackButton.clicked += () => Execute(_controller.RunRollbackScenarioAsync);
                        row.VisualElement(rollbackButton);

                        var readYourWriteButton = new DebugSecondaryButton("RW 内 Read");
                        readYourWriteButton.clicked += () => Execute(ct => _controller.RunReadYourWriteScenarioAsync(ReadDraftFromFields(), ct));
                        row.VisualElement(readYourWriteButton);
                    });
                });

                _monsterFoldout = new VirtualFoldout("現在のモンスター一覧");
                builder.VisualElement(_monsterFoldout);

                var refreshButton = new DebugSecondaryButton("一覧を再描画");
                refreshButton.clicked += RefreshView;
                builder.VisualElement(refreshButton);

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
                if (_controller == null)
                {
                    if (_statusLabel != null)
                    {
                        _statusLabel.text = "Controller が破棄されたため、このページは更新できません。";
                    }

                    ClearMonsterList();
                    return;
                }

                var draft = _controller.GetDebugDraft();
                _idField?.SetValueWithoutNotify(draft.Id);
                _nameField?.SetValueWithoutNotify(draft.Name);
                _levelField?.SetValueWithoutNotify(draft.Level);
                _posXField?.SetValueWithoutNotify(draft.Position.X);
                _posYField?.SetValueWithoutNotify(draft.Position.Y);

                if (_statusLabel != null)
                {
                    _statusLabel.text = _controller.GetMonsterDebugStatusText();
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

                foreach (var monster in _controller.GetMonsterSnapshot().OrderBy(monster => monster.Id))
                {
                    _monsterHandles.Add(_monsterFoldout.AddDebugUI(builder =>
                    {
                        builder.HorizontalScope(row =>
                        {
                            row.VisualElement(new DebugLabel(_controller.FormatMonster(monster)));

                            var loadButton = new DebugSecondaryButton("入力へ反映");
                            loadButton.clicked += () =>
                            {
                                _controller.ApplyDebugDraft(MonsterDraft.FromMonster(monster));
                                RefreshView();
                            };
                            row.VisualElement(loadButton);

                            var deleteButton = new DebugDangerButton("削除");
                            deleteButton.clicked += () => Execute(ct => _controller.DeleteMonsterAsync(monster.Id, ct));
                            row.VisualElement(deleteButton);
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
