using System;
using Lilja.ScreenManagement;
using ScreenManagementSample.Application;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// メニュー画面Overlay（MVP - Presenter）
    /// </summary>
    public class MenuOverlay : AwaitableGameScreen<ValueTuple, ValueTuple>
    {
        protected override IViewHandle ViewHandle { get; } = PrefabViewHandle.Default;

        [View]
        private MenuView _view;

        protected override void OnViewLoaded()
        {
            _view.CloseButton.onClick.AddListener(OnClickClose);

            // ステータス表示を同期
            SyncDisplay();
        }

        protected override void OnViewUnload()
        {
            _view.CloseButton.onClick.RemoveListener(OnClickClose);
        }

        /// <summary>
        /// 表示を同期
        /// </summary>
        private void SyncDisplay()
        {
            var player = GameServices.PlayerRepository.Get();
            _view.SetStatus(
                player.Name,
                player.CurrentHp,
                player.MaxHp,
                player.Attack,
                player.Defense
            );
        }

        private void OnClickClose()
        {
            Debug.Log("[MenuOverlay] メニューを閉じます");
            Complete(new ValueTuple());
        }
    }
}
