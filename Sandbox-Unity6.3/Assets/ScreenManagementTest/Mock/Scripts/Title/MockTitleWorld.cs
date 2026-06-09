using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// タイトル画面の World
    /// </summary>
    public class MockTitleWorld : GameScreen<ValueTuple>
    {
        protected override IViewHandle ViewHandle { get; } = new SceneViewHandle("MockTitle");

        [View]
        private MockTitleView _view;

        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            _view.StartButton.onClick.AddListener(OnClickStart);
        }

        /// <inheritdoc/>
        protected override void OnViewUnload()
        {
            _view.StartButton.onClick.RemoveListener(OnClickStart);
        }

        /// <inheritdoc/>
        protected override UniTask EnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        )
        {
            Debug.Log("[TitleWorld] タイトル画面へ遷移しました");
            return UniTask.CompletedTask;
        }

        /// <summary> スタートボタンクリック時 </summary>
        private void OnClickStart()
        {
            Debug.Log("[TitleWorld] 探索パートへ遷移します");
            Group.SwitchAsync<MockExploreWorld, ValueTuple>(new ValueTuple(), default).Forget();
        }
    }
}
