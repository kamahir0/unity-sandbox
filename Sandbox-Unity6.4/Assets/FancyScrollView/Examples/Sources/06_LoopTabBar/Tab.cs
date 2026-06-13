/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using UnityEngine;
using UnityEngine.UI;

namespace Lilja.FancyScrollView.Example06
{
    class Tab : FancyCell<ItemData, Context>
    {
        [SerializeField] Animator animator = default;
        [SerializeField] Text message = default;
        [SerializeField] Button button = default;

        static class AnimatorHash
        {
            public static readonly int Scroll = Animator.StringToHash("scroll");
        }

        public override void Initialize()
        {
            button.onClick.AddListener(() => Context.OnCellClicked?.Invoke(Index));
        }

        public override void UpdateContent(ItemData itemData)
        {
            message.text = itemData.Message;
        }

        public override void UpdatePosition(float position)
        {
            currentPosition = position;

            if (animator.isActiveAndEnabled)
            {
                animator.Play(AnimatorHash.Scroll, -1, position);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    animator.Update(0f);
                }
#endif
            }

            animator.speed = 0;
        }

        // GameObject 縺碁撼繧｢繧ｯ繝・ぅ繝悶↓縺ｪ繧九→ Animator 縺後Μ繧ｻ繝・ヨ縺輔ｌ縺ｦ縺励∪縺・◆繧・
        // 迴ｾ蝨ｨ菴咲ｽｮ繧剃ｿ晄戟縺励※縺翫＞縺ｦ OnEnable 縺ｮ繧ｿ繧､繝溘Φ繧ｰ縺ｧ迴ｾ蝨ｨ菴咲ｽｮ繧貞・險ｭ螳壹＠縺ｾ縺・
        float currentPosition = 0;

        void OnEnable() => UpdatePosition(currentPosition);
    }
}
