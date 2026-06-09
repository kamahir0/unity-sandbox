using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// 探索パートの World
    /// </summary>
    public class MockExploreWorld : GameScreen<ValueTuple>
    {
        protected override IViewHandle ViewHandle { get; } = new SceneViewHandle("MockExplore");

        [View]
        private MockExploreView _view;

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.MenuTopButton.onClick.AddListener(OnClickMenuTop);
            _view.MenuCharacterButton.onClick.AddListener(OnClickMenuCharacter);
            _view.BattleButton.onClick.AddListener(OnClickBattle);
        }

        /// <inheritdoc/>
        protected override void OnViewUnload()
        {
            _view.MenuTopButton.onClick.RemoveListener(OnClickMenuTop);
            _view.MenuCharacterButton.onClick.RemoveListener(OnClickMenuCharacter);
            _view.BattleButton.onClick.RemoveListener(OnClickBattle);
        }

        /// <inheritdoc/>
        protected override UniTask EnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        )
        {
            Debug.Log("[ExploreWorld] 探索パートへ遷移しました");
            if (context.EnterType == EnterType.OnResume)
            {
                return _view.AnimateInAsync(cancellationToken);
            }

            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        protected override UniTask ExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        )
        {
            if (context.ExitType == ExitType.OnPause)
            {
                return _view.AnimateOutAsync(cancellationToken);
            }

            return UniTask.CompletedTask;
        }

        /// <summary> メニュー(Top)ボタンクリック時 </summary>
        private void OnClickMenuTop()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[ExploreWorld] メニュー(Top)画面グループへ遷移します");
                await new MockMenuGroup().CallAsync<MockMenuTopOverlay, ValueTuple>(
                    Context,
                    new ValueTuple(),
                    CancellationToken.None
                );
            });
        }

        /// <summary> メニュー(Character)ボタンクリック時 </summary>
        private void OnClickMenuCharacter()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[ExploreWorld] メニュー(Character)画面グループへ遷移します");
                await new MockMenuGroup().CallAsync<MockMenuCharacterOverlay, ValueTuple>(
                    Context,
                    new ValueTuple(),
                    CancellationToken.None
                );
            });
        }

        /// <summary> 戦闘ボタンクリック時 </summary>
        private void OnClickBattle()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[ExploreWorld] 戦闘画面へ遷移します");
                await new MockBattleOverlay().CallAsync(
                    Context,
                    new ValueTuple(),
                    CancellationToken.None
                );
            });
        }
    }
}
