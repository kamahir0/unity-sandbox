using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement.Mock
{
    public class MockBoot : MonoBehaviour
    {
        private static MockScreenGroup _mockScreenGroup;

        /// <summary>
        /// タイトル画面（MockTitleWorld）へ遷移します。
        /// </summary>
        public static void GotoTitle()
        {
            if (_mockScreenGroup != null)
            {
                _mockScreenGroup
                    .SwitchAsync<MockTitleWorld, ValueTuple>(new ValueTuple(), default)
                    .Forget();
            }
            else
            {
                Debug.LogWarning("[MockBoot] MockScreenGroup が初期化されていません。");
            }
        }

        [SerializeField]
        private bool _useAddressables;

        private void Awake()
        {
            Debug.Log("タイトル画面を開きます...");

            UniTask.Void(async () =>
            {
                var rootContext = GameScreenContext.CreateRoot(transition: Fade.Instance);

                _mockScreenGroup = new MockScreenGroup();
                
                // ブートシーンが破棄されてもグループが終了しないように CancellationToken.None を指定して開始
                var handle = _mockScreenGroup.CallAsync<MockTitleWorld, ValueTuple>(
                    rootContext,
                    new ValueTuple(),
                    CancellationToken.None
                );

                // 最初の画面のロードと表示（入場演出完了）が完了するまで、このブートシーンの生存期間トークンで待つ
                await handle.WaitForInitialScreenEnterAsync(destroyCancellationToken);

                // 表示が完了したので、ブートシーン自体を即座に破棄（アンロード）
                var op = SceneManager.UnloadSceneAsync(gameObject.scene);
                if (op != null)
                {
                    await op.ToUniTask();
                }
            });
        }
    }
}
