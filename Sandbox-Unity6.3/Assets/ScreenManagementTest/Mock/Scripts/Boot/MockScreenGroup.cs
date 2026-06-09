using System;
using Lilja.ScreenManagement;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// Mock 用の画面遷移グループ
    /// </summary>
    public class MockScreenGroup : GameScreenGroup
    {
        protected override void Configure(IGameScreenGroupBuilder builder)
        {
            builder.Register<MockTitleWorld, ValueTuple>();
            builder.Register<MockExploreWorld, ValueTuple>();
        }
    }
}
