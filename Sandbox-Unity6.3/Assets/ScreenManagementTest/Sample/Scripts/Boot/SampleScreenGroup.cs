using System;
using Lilja.ScreenManagement;
using ScreenManagementSample.Presentation;

namespace ScreenManagementSample
{
    /// <summary>
    /// Sample 用の画面遷移グループ
    /// </summary>
    public class SampleScreenGroup : GameScreenGroup
    {
        protected override void Configure(IGameScreenGroupBuilder builder)
        {
            builder.Register<TitleWorld, ValueTuple>();
            builder.Register<MapWorld, ValueTuple>();
            builder.Register<GameOverWorld, ValueTuple>();
        }
    }
}
