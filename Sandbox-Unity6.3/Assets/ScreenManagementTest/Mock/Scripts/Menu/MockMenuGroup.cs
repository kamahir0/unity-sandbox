using System;
using Lilja.ScreenManagement;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// メニュー画面用のグループ（MenuTop と MenuCharacter を管理）
    /// </summary>
    public class MockMenuGroup : GameScreenGroup
    {
        protected override void Configure(IGameScreenGroupBuilder builder)
        {
            builder.Register<MockMenuTopOverlay, ValueTuple>();
            builder.Register<MockMenuCharacterOverlay, ValueTuple>();

            builder.OverrideTransition<MockMenuTopOverlay, MockMenuCharacterOverlay>(
                ITransition.None
            );
            builder.OverrideTransition<MockMenuCharacterOverlay, MockMenuTopOverlay>(
                ITransition.None
            );
        }
    }
}
