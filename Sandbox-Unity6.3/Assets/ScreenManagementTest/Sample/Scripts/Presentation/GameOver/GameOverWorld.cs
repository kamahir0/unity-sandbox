using System;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// ゲームオーバー画面World（MVP - Presenter）
    /// </summary>
    public class GameOverWorld : GameScreen<ValueTuple>
    {
        protected override IViewHandle ViewHandle { get; } = PrefabViewHandle.Default;

        [View]
        private GameOverView _view;

        protected override void OnViewLoaded()
        {
            _view.TitleButton.onClick.AddListener(OnClickTitle);
        }

        protected override void OnViewUnload()
        {
            _view.TitleButton.onClick.RemoveListener(OnClickTitle);
        }

        private void OnClickTitle()
        {
            Debug.Log("[GameOverWorld] タイトル画面へ遷移します");
            Group.SwitchAsync<TitleWorld, ValueTuple>(new ValueTuple(), default).Forget();
        }
    }
}
