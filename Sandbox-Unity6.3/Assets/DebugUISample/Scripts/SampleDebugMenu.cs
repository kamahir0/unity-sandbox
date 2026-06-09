using System;
using System.Collections.Generic;
using Lilja.DebugUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// デバッグメニューのセットアップ例。
/// Bootstrap 用 GameObject にアタッチして Start() で初期化する。
/// DebugMenuOpenButton コンポーネントと組み合わせて使用する。
/// </summary>
public class SampleDebugMenu : MonoBehaviour
{
    [SerializeField] private float _transitionDelay = 5f;

    private void Start()
    {
        if (!DebugMenu.IsInitialized)
        {
            DebugMenu.Initialize(new RootPage());

            // GetPage<T>() のデモ: 初期化後に外部からページへ動的追加
            var root = DebugMenu.GetPage<RootPage>();
            root?.AddDebugUI(b => { b.VisualElement(new DebugLabel($"起動時刻: {DateTime.Now:HH:mm:ss}")); });
        }

        // 5秒（可変）経過後に DynamicDemo シーンへ遷移
        Invoke(nameof(TransitionToDynamicDemo), _transitionDelay);
    }

    private void TransitionToDynamicDemo()
    {
        SceneManager.LoadScene("DynamicDemo");
    }

    // ─── Root ─────────────────────────────────────────────────────

    public class RootPage : DebugPage
    {
        public override void Configure(IDebugUIBuilder builder)
        {
            var playerIcon = Resources.Load<Sprite>("sample_icon_1");
            builder.NavigationButton("Player", () => new PlayerPage(), new StyleBackground(playerIcon));

            var audioIcon = Resources.Load<Texture2D>("sample_icon_2");
            builder.NavigationButton("Audio", () => new AudioPage(), new StyleBackground(audioIcon));

            var settingsIcon = Resources.Load<VectorImage>("sample_icon_3");
            builder.NavigationButton("Settings", () => new SettingsPage(), new StyleBackground(settingsIcon));

            builder.NavigationButton("Scene", b =>
            {
                var titleBtn = new DebugButton("Title");
                titleBtn.clicked += () => SceneManager.LoadScene("Title");
                b.VisualElement(titleBtn);

                var stage1Btn = new DebugButton("Stage 1");
                stage1Btn.clicked += () => SceneManager.LoadScene("Stage1");
                b.VisualElement(stage1Btn);
            });

            builder.Foldout("App Info", b =>
            {
                b.VisualElement(new DebugLabel($"Version: {Application.version}"));
                b.VisualElement(new DebugLabel($"Platform: {Application.platform}"));
            });

            builder.NavigationButton("All Controls", () => new ControlsDemoPage());
        }
    }

    // ─── Dynamic UI Demo ──────────────────────────────────────────

    /// <summary>
    /// AddDebugUI / VirtualFoldout / PlaceBehind のデモページ。
    /// </summary>
    public class DynamicDemoPage : DebugPage
    {
        private VirtualFoldout _enemyFoldout;
        private readonly List<IDisposable> _enemyHandles = new();
        private int _enemyCounter;
        private IDisposable _placedHandle;

        public override void Configure(IDebugUIBuilder builder)
        {
            builder.NavigationButton("Sample シーンをロード", b =>
            {
                var btn = new DebugButton("Sample シーンをロード");
                btn.clicked += () => SceneManager.LoadScene("Sample");
                b.VisualElement(btn);
            });

            // ── VirtualFoldout デモ ──────────────────────────────
            // 子要素が0のとき非表示、1つ以上で表示される Foldout
            _enemyFoldout = new VirtualFoldout("ポップ中エネミー");
            builder.VisualElement(_enemyFoldout);

            var spawnBtn = new DebugButton("エネミーをスポーン");
            spawnBtn.clicked += SpawnEnemy;
            builder.VisualElement(spawnBtn);

            var clearBtn = new DebugDangerButton("全エネミーを撃破");
            clearBtn.clicked += ClearAllEnemies;
            builder.VisualElement(clearBtn);

            // ── PlaceBehind デモ ─────────────────────────────────
            var anchor = new DebugLabel("─── PlaceBehind アンカー ───");
            builder.VisualElement(anchor);

            var insertBtn = new DebugButton("PlaceBehind でボタンを挿入");
            insertBtn.clicked += () =>
            {
                _placedHandle?.Dispose();
                _placedHandle = anchor.PlaceBehind(b =>
                {
                    var btn = new DebugSecondaryButton("動的に挿入されたボタン");
                    btn.clicked += () => Debug.Log("[DynamicDemo] PlaceBehind ボタンがタップされました");
                    b.VisualElement(btn);
                });
            };
            builder.VisualElement(insertBtn);

            var removeBtn = new DebugDangerButton("挿入を削除");
            removeBtn.clicked += () => _placedHandle?.Dispose();
            builder.VisualElement(removeBtn);
        }

        private void SpawnEnemy()
        {
            var id = ++_enemyCounter;
            var enemyName = $"Enemy-{id:D3}";

            IDisposable handle = null;
            handle = _enemyFoldout.AddDebugUI(b =>
            {
                b.HorizontalScope(hb =>
                {
                    hb.VisualElement(new DebugLabel(enemyName));

                    var killBtn = new DebugDangerButton("即死");
                    killBtn.clicked += () =>
                    {
                        Debug.Log($"[DynamicDemo] {enemyName} を撃破");
                        handle?.Dispose();
                        _enemyHandles.Remove(handle);
                    };
                    hb.VisualElement(killBtn);
                });
            });

            _enemyHandles.Add(handle);
            Debug.Log($"[DynamicDemo] {enemyName} がスポーンしました");
        }

        private void ClearAllEnemies()
        {
            foreach (var h in _enemyHandles) h.Dispose();
            _enemyHandles.Clear();
            Debug.Log("[DynamicDemo] 全エネミーを撃破しました");
        }
    }

    // ─── Player ───────────────────────────────────────────────────

    class PlayerPage : DebugPage
    {
        public override void Configure(IDebugUIBuilder builder)
        {
            var hpField = new DebugIntegerField("HP") { value = 100 };
            var setHpBtn = new DebugButton("HP をセット");
            setHpBtn.clicked += () => Debug.Log($"[Debug] HP → {hpField.value}");
            builder.VisualElement(hpField);
            builder.VisualElement(setHpBtn);

            builder.Foldout("チート", b =>
            {
                var fullHpBtn = new DebugButton("HP 最大化");
                fullHpBtn.clicked += () => Debug.Log("[Debug] HP 最大化");
                b.VisualElement(fullHpBtn);

                var invincibleBtn = new DebugButton("無敵モード");
                invincibleBtn.clicked += () => Debug.Log("[Debug] 無敵モード");
                b.VisualElement(invincibleBtn);
            });

            builder.NavigationButton("Player", () => new PlayerPage());
            builder.NavigationButton("Audio", () => new AudioPage());
        }
    }

    // ─── Settings ────────────────────────────────────────────────

    class SettingsPage : DebugPage
    {
        public override void Configure(IDebugUIBuilder builder)
        {
            var nameField = new DebugTextField("プレイヤー名") { value = "Player1" };
            builder.VisualElement(nameField);

            builder.NavigationButton<PlayerPage>();

            builder.VisualElement(new DebugLabel("プレイヤー設定"));

            var themeGroup = new DebugRadioButtonGroup("テーマ") { choices = new List<string> { "ライト", "ダーク" } };
            builder.VisualElement(themeGroup);

            var featureGroup = new DebugToggleGroup("有効機能");
            featureGroup.Add(new DebugToggleGroupItem("デバッグログ"));
            featureGroup.Add(new DebugToggleGroupItem("FPS表示"));
            builder.VisualElement(featureGroup);

            builder.Foldout("詳細設定", b =>
            {
                var outputGroup = new DebugToggleGroup("出力先");
                outputGroup.Add(new DebugToggleGroupItem("コンソール"));
                outputGroup.Add(new DebugToggleGroupItem("ファイル"));
                b.VisualElement(outputGroup);

                b.VisualElement(new DebugIntegerField("回数"));
            });

            builder.HorizontalScope(b =>
            {
                var resetBtn = new DebugSecondaryButton("リセット");
                resetBtn.clicked += () => Debug.Log("[Debug] リセット");
                b.VisualElement(resetBtn);

                var applyBtn = new DebugButton("適用");
                applyBtn.clicked += () => Debug.Log($"[Debug] 名前={nameField.value}");
                b.VisualElement(applyBtn);
            });

            var deleteBtn = new DebugDangerButton("設定を削除");
            deleteBtn.clicked += () => Debug.Log("[Debug] 設定を削除");
            builder.VisualElement(deleteBtn);
        }
    }

    // ─── Audio ────────────────────────────────────────────────────

    class AudioPage : DebugPage
    {
        public override void Configure(IDebugUIBuilder builder)
        {
            var bgmField = new DebugFloatField("BGM") { value = AudioListener.volume };
            var seField = new DebugFloatField("SE") { value = 1f };
            var applyBtn = new DebugButton("適用");
            applyBtn.clicked += () =>
            {
                AudioListener.volume = bgmField.value;
                Debug.Log($"[Debug] BGM={bgmField.value} SE={seField.value}");
            };
            builder.VisualElement(bgmField);
            builder.VisualElement(seField);
            builder.VisualElement(applyBtn);
            builder.NavigationButton("Player", () => new PlayerPage());
        }
    }

    // ─── Controls Demo ────────────────────────────────────────────

    public enum SampleEnum
    {
        None,
        Easy,
        Normal,
        Hard,
        Legendary
    }

    class ControlsDemoPage : DebugPage
    {
        public override void Configure(IDebugUIBuilder builder)
        {
            builder.VisualElement(new DebugLabel("─── Basic Fields ───"));
            builder.TextField("Text", "Hello World", v => Debug.Log($"Text: {v}"));
            builder.IntegerField("Int", 42, v => Debug.Log($"Int: {v}"));
            builder.LongField("Long", 1234567890L, v => Debug.Log($"Long: {v}"));
            builder.FloatField("Float", 3.14f, v => Debug.Log($"Float: {v}"));
            builder.DoubleField("Double", 3.1415926535, v => Debug.Log($"Double: {v}"));

            builder.VisualElement(new DebugLabel("─── Sliders ───"));
            builder.Slider("Slider", 0.5f, 0f, 1f, v => Debug.Log($"Slider: {v}"));
            builder.SliderInt("SliderInt", 5, 0, 10, v => Debug.Log($"SliderInt: {v}"));
            builder.MinMaxSlider("MinMax", new Vector2(0.2f, 0.8f), 0f, 1f, v => Debug.Log($"MinMax: {v}"));

            builder.VisualElement(new DebugLabel("─── Progress ───"));
            builder.ProgressBar("Loading Assets", 75f);

            builder.VisualElement(new DebugLabel("─── Enum ───"));
            builder.EnumField("Difficulty", SampleEnum.Normal, v => Debug.Log($"Enum: {v}"));

            builder.VisualElement(new DebugLabel("─── Vector Fields ───"));
            builder.Vector2Field("Vector2", Vector2.one, v => Debug.Log($"Vector2: {v}"));
            builder.Vector2IntField("Vector2Int", Vector2Int.one, v => Debug.Log($"Vector2Int: {v}"));
            builder.Vector3Field("Vector3", Vector3.one, v => Debug.Log($"Vector3: {v}"));
            builder.Vector3IntField("Vector3Int", Vector3Int.one, v => Debug.Log($"Vector3Int: {v}"));
            builder.Vector4Field("Vector4", Vector4.one, v => Debug.Log($"Vector4: {v}"));

            builder.VisualElement(new DebugLabel("─── Rect & Bounds ───"));
            builder.RectField("Rect", new Rect(0, 0, 100, 100), v => Debug.Log($"Rect: {v}"));
            builder.RectIntField("RectInt", new RectInt(0, 0, 100, 100), v => Debug.Log($"RectInt: {v}"));
            builder.BoundsField("Bounds", new Bounds(Vector3.zero, Vector3.one), v => Debug.Log($"Bounds: {v}"));
            builder.BoundsIntField("BoundsInt", new BoundsInt(Vector3Int.zero, Vector3Int.one), v => Debug.Log($"BoundsInt: {v}"));

            builder.VisualElement(new DebugLabel("─── Others ───"));
        }
    }
}
