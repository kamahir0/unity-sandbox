using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement.Dialog;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// キャラ詳細画面の Overlay
    /// </summary>
    public class MockMenuCharacterOverlay : GameScreen<ValueTuple>
    {
        protected override IViewHandle ViewHandle { get; } =
            new PrefabViewHandle("Overlay/MockMenuCharacter");

        [View]
        private MockMenuCharacterView _view;

        /// <inheritdoc/>
        protected override async UniTask EnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        )
        {
            Debug.Log("[MenuCharacterOverlay] キャラ画面を表示しました");
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

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.TestButton.onClick.AddListener(OnClickTest);
            _view.CloseButton.onClick.AddListener(OnClickClose);
        }

        /// <inheritdoc/>
        protected override void OnViewUnload()
        {
            _view.TestButton.onClick.RemoveListener(OnClickTest);
            _view.CloseButton.onClick.RemoveListener(OnClickClose);
        }

        /// <summary> テストボタンクリック時 </summary>
        private void OnClickTest()
        {
            UniTask.Void(async () =>
            {
                var result = await DefaultDialog
                    .Create<bool>("test")
                    .AddText("test")
                    .AddButton("OK", true)
                    .CallAsync(Context, CancellationToken.None);

                Debug.Log($"[MenuCharacterOverlay] result: {result}");
            });
        }

        /// <summary> 閉じるボタンクリック時 </summary>
        private void OnClickClose()
        {
            Debug.Log("[MenuCharacterOverlay] キャラ詳細画面を閉じます");
            Group.SwitchBackAsync().Forget();
        }
    }
}
