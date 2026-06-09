using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kamahir0.ScreenManagement.Mock;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// メニュー画面の Overlay (MenuTop)
    /// </summary>
    public class MockMenuTopOverlay : GameScreen<ValueTuple>
    {
        protected override IViewHandle ViewHandle { get; } =
            new PrefabViewHandle("Overlay/MockMenuTop");

        [View]
        private MockMenuView _view;

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.CharacterButton.onClick.AddListener(OnClickCharacter);
            _view.BattleButton.onClick.AddListener(OnClickBattle);
            _view.CloseButton.onClick.AddListener(OnClickClose);
            _view.TitleButton.onClick.AddListener(OnClickTitle);
            _view.MiniGameButton.onClick.AddListener(OnClickMiniGame);
        }

        /// <inheritdoc/>
        protected override void OnViewUnload()
        {
            _view.CharacterButton.onClick.RemoveListener(OnClickCharacter);
            _view.BattleButton.onClick.RemoveListener(OnClickBattle);
            _view.CloseButton.onClick.RemoveListener(OnClickClose);
            _view.TitleButton.onClick.RemoveListener(OnClickTitle);
            _view.MiniGameButton.onClick.RemoveListener(OnClickMiniGame);
        }

        /// <inheritdoc/>
        protected override async UniTask EnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        )
        {
            Debug.Log("[MenuOverlay] メニュー画面を表示しました");
            if (context.EnterType == EnterType.OnOpen)
            {
                if (Group.ContainsScreenType(context.PreviousScreenType))
                {
                    await _view.AnimateInAsync(cancellationToken);
                }
            }
        }

        /// <inheritdoc/>
        protected override async UniTask ExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        )
        {
            if (context.ExitType == ExitType.OnClose)
            {
                if (Group.ContainsScreenType(context.NextScreenType))
                {
                    await _view.AnimateOutAsync(cancellationToken);
                }
            }
        }

        /// <summary> キャラ詳細ボタンクリック時 </summary>
        private void OnClickCharacter()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[MenuOverlay] キャラ詳細画面へ遷移します");
                await Group.SwitchAsync<MockMenuCharacterOverlay, ValueTuple>(
                    new ValueTuple(),
                    CancellationToken.None
                );
            });
        }

        /// <summary> バトルボタンクリック時 </summary>
        private void OnClickBattle()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[MenuOverlay] バトル画面へ遷移します");
                await new MockBattleOverlay().CallAsync(
                    Context,
                    new ValueTuple(),
                    CancellationToken.None
                );
            });
        }

        /// <summary> 閉じるボタンクリック時 </summary>
        private void OnClickClose()
        {
            Debug.Log("[MenuOverlay] メニュー画面を閉じます");
            Group.Complete();
        }

        /// <summary> タイトルボタンクリック時 </summary>
        private void OnClickTitle()
        {
            UniTask.Void(async () =>
            {
                // タイトルへ戻る前に確認ダイアログを表示
                var result = await new TestDialog().CallAsync(
                    Context,
                    new ValueTuple(),
                    CancellationToken.None
                );
                if (!result)
                {
                    return;
                }

                Debug.Log("[MenuOverlay] タイトル画面へ遷移します");
                MockBoot.GotoTitle();
            });
        }

        /// <summary> ミニゲームボタンクリック時 </summary>
        private void OnClickMiniGame()
        {
            UniTask.Void(async () =>
            {
                Debug.Log("[MenuOverlay] ミニゲームフローへ遷移します");
                var score = await new MockMiniGameFlow().CallAsync(
                    Context,
                    "Start Mock MiniGame!",
                    CancellationToken.None
                );
                Debug.Log($"[MenuOverlay] ミニゲームフロー終了: スコア = {score}");
            });
        }
    }
}
