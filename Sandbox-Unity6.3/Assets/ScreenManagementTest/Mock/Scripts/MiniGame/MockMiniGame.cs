using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Kamahir0.ScreenManagement.Mock
{
    public sealed class MockMiniGame : AwaitableGameScreen<string, int>
    {
        [View]
        private Text _missionText;

        [View]
        private Button _clearButton;

        private string _mission;

        public override ITransition OverrideTransition => null;

        protected override IViewHandle ViewHandle => new PrefabViewHandle("Screens/MockMiniGame");

        protected override UniTask InitializeAsync(
            string mission,
            CancellationToken cancellationToken
        )
        {
            _mission = mission;
            return UniTask.CompletedTask;
        }

        protected override void OnViewLoaded()
        {
            _missionText.text = $"Mission: {_mission}";
            _clearButton.onClick.AddListener(OnClear);
        }

        protected override void OnViewUnload()
        {
            _clearButton.onClick.RemoveListener(OnClear);
        }

        private void OnClear()
        {
            // スコア100点でクリア
            Complete(100);
        }
    }
}
