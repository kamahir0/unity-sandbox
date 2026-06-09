using System;
using Cysharp.Threading.Tasks;
using Lilja.ScreenManagement.Dialog;

namespace Lilja.ScreenManagement.Mock
{
    public class TestDialog : DefaultDialog<ValueTuple, bool>
    {
        public int Version;

        /// <summary>
        /// Backdrop クリックで閉じる
        /// </summary>
        protected override bool EnableOutsideButton => true;

        /// <summary>
        /// Backdrop クリック時は false を返す
        /// </summary>
        protected override bool OutsideButtonResult => false;

        /// <inheritdoc/>
        protected override void Build()
        {
            Frame.SetTitle($"Version {Version}");
            Content.AddText("Body");
            Frame.AddButton("Back", () => Complete(false));
            Frame.AddButton("Go", () => OnClickGoAsync().Forget());
            Frame.AddButton("Battle", () => new MockBattleOverlay().CallAsync(Context, new ValueTuple(), default).Forget());
            Frame.AddButton("Character", () => new MockMenuGroup().CallAsync<MockMenuCharacterOverlay, ValueTuple>(Context, new ValueTuple(), default));
            Frame.AddButton("Title", () => MockBoot.GotoTitle());
        }

        private async UniTask OnClickGoAsync()
        {
            var result = await new TestDialog { Version = Version + 1 }.CallAsync(Context, default, default);
            if (result)
            {
                Complete(true);
            }
        }
    }
}
