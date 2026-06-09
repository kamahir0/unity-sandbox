using System;
using Lilja.ScreenManagement;
using ScreenManagementSample.Domain;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// スキル選択Overlay（MVP - Presenter）
    /// TResult が null の場合はキャンセル（戻る）を意味する
    /// </summary>
    public class SkillSelectOverlay : AwaitableGameScreen<ValueTuple, Skill>
    {
        protected override IViewHandle ViewHandle { get; } = PrefabViewHandle.Default;

        [View]
        private SkillSelectView _view;

        private readonly Skill[] _skills;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="skills">選択可能なスキル配列</param>
        public SkillSelectOverlay(Skill[] skills)
        {
            _skills = skills ?? Array.Empty<Skill>();
        }

        protected override void OnViewLoaded()
        {
            // 動的にスキルボタンを設定
            _view.SetSkills(_skills, OnSkillSelected);
            _view.BackButton.onClick.AddListener(OnClickBack);

            _view.SetDescription("スキルを選んでください");
        }

        protected override void OnViewUnload()
        {
            _view.ClearSkillButtons();
            _view.BackButton.onClick.RemoveListener(OnClickBack);
        }

        private void OnSkillSelected(Skill skill)
        {
            Debug.Log($"[SkillSelectOverlay] {skill.Name}を選択");
            Complete(skill);
        }

        private void OnClickBack()
        {
            Debug.Log("[SkillSelectOverlay] キャンセル");
            Complete(null); // nullを返してキャンセルを表現
        }
    }
}
