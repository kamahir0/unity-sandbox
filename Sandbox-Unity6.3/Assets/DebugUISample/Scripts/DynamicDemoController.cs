using System;
using Lilja.DebugUI;
using UnityEngine;

namespace Lilja.DebugUI.Sample
{
    /// <summary>
    /// DynamicDemo シーンの制御用コンポーネント。
    /// 既存のデバッグメニューに動的に項目を追加するデモ。
    /// </summary>
    public class DynamicDemoController : MonoBehaviour
    {
        private IDisposable _handle;

        private void Start()
        {
            // RootPage を取得して、そこに DynamicDemoPage へのナビゲーションボタンを動的に追加する
            var root = DebugMenu.GetPage<SampleDebugMenu.RootPage>();
            if (root != null)
            {
                _handle = root.AddDebugUI(builder =>
                {
                    builder.NavigationButton<SampleDebugMenu.DynamicDemoPage>();
                });
            }
            else
            {
                Debug.LogWarning("[DynamicDemo] RootPage が見つからないため、デバッグメニュー項目を追加できませんでした。");
            }
        }

        private void OnDestroy()
        {
            // OnDestroy で追加した項目を破棄する
            _handle?.Dispose();
            _handle = null;
        }
    }
}
