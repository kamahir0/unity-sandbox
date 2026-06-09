using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Kamahir0.ScreenManagement.Mock
{
    public sealed class MockMiniGameResult : AwaitableGameScreen<int, bool>
    {
        [View]
        private Text _scoreText;

        [View]
        private Button _closeButton;

        private int _score;

        public override ITransition OverrideTransition => null;

        protected override IViewHandle ViewHandle =>
            new PrefabViewHandle("Screens/MockMiniGameResult");

        protected override UniTask InitializeAsync(int score, CancellationToken cancellationToken)
        {
            _score = score;
            return UniTask.CompletedTask;
        }

        protected override void OnViewLoaded()
        {
            _scoreText.text = $"Game Over\nScore: {_score}";
            _closeButton.onClick.AddListener(OnClose);
        }

        protected override void OnViewUnload()
        {
            _closeButton.onClick.RemoveListener(OnClose);
        }

        private void OnClose()
        {
            Complete(true);
        }
    }
}
