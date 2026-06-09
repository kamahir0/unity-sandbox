using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Lilja.ScreenManagement;

namespace Kamahir0.ScreenManagement.Mock
{
    public sealed class MockMiniGameFlow : GameFlow<string, int>
    {
        protected override async UniTask<int> RunAsync(
            GameScreenContext context,
            string mission,
            CancellationToken cancellationToken
        )
        {
            // 1. ミニゲーム画面のロード・起動 ＆ 結果待ち
            var score = await new MockMiniGame().CallAsync(context, mission, cancellationToken);

            // 2. 暗転維持のまま、リザルト画面のロード・起動 ＆ 完了待ち
            await new MockMiniGameResult().CallAsync(context, score, cancellationToken);

            return score;
        }
    }
}
