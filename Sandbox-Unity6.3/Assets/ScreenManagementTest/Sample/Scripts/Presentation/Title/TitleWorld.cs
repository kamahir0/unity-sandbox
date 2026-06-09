using System;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// タイトル画面World（MVP - Presenter）
    /// </summary>
    public class TitleWorld : GameScreen<ValueTuple>
    {
        protected override IViewHandle ViewHandle { get; } = PrefabViewHandle.Default;

        [View]
        private TitleView _view;

        protected override void OnViewLoaded()
        {
            _view.StartButton.onClick.AddListener(OnClickStart);
        }

        protected override void OnViewUnload()
        {
            _view.StartButton.onClick.RemoveListener(OnClickStart);
        }

        private void OnClickStart()
        {
            Debug.Log("[TitleWorld] マップ画面へ遷移します");
            // ゲームをリセットして新規開始
            Application.GameServices.Reset();
            Group.SwitchAsync<MapWorld, ValueTuple>(new ValueTuple(), default).Forget();
        }
    }
}
