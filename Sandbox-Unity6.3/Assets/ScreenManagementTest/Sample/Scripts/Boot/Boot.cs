using System;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement;
using ScreenManagementSample.Application;
using ScreenManagementSample.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScreenManagementSample
{
    /// <summary>
    /// ゲーム起動クラス
    /// </summary>
    public class Boot : MonoBehaviour
    {
        private void Awake()
        {
            // サービスを初期化
            GameServices.Initialize();

            Debug.Log("[Boot] ゲームを開始します...");

            UniTask.Void(async () =>
            {
                try
                {
                    var rootContext = GameScreenContext.CreateRoot(
                        transition: SampleFade.Instance
                    );

                    var group = new SampleScreenGroup();
                    await group.CallAsync<TitleWorld, ValueTuple>(
                        rootContext,
                        new ValueTuple(),
                        destroyCancellationToken
                    );
                }
                finally
                {
                    // Bootシーンをアンロード
                    await SceneManager.UnloadSceneAsync(gameObject.scene);
                }
            });
        }
    }
}
