using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Mock
{
    /// <summary>
    /// 探索パートのView
    /// </summary>
    public class MockExploreView : MonoBehaviour
    {
        [SerializeField] private Button _menuTopButton;
        [SerializeField] private Button _menuCharacterButton;
        [SerializeField] private Button _battleButton;
        [SerializeField] private RectTransform _header;
        [SerializeField] private float _startPosY = 160f;
        [SerializeField] private float _duration = 0.25f;

        /// <summary> メニュー(Top)ボタン </summary>
        public Button MenuTopButton => _menuTopButton;

        /// <summary> メニュー(Character)ボタン </summary>
        public Button MenuCharacterButton => _menuCharacterButton;

        /// <summary> 戦闘ボタン </summary>
        public Button BattleButton => _battleButton;

        private Tweener _currentTween;

        /// <summary>
        /// 入場アニメーション
        /// </summary>
        public UniTask AnimateInAsync(CancellationToken cancellationToken)
        {
            _currentTween?.Kill();

            _currentTween = _header.DOAnchorPos(Vector2.zero, _duration)
                .From(new Vector2(0, _startPosY))
                .SetEase(Ease.OutExpo);

            return _currentTween.ToUniTask(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 退出アニメーション
        /// </summary>
        public UniTask AnimateOutAsync(CancellationToken cancellationToken)
        {
            _currentTween?.Kill();

            _currentTween = _header.DOAnchorPos(new Vector2(0, _startPosY), _duration)
                .From(Vector2.zero)
                .SetEase(Ease.InExpo);

            return _currentTween.ToUniTask(cancellationToken: cancellationToken);
        }
    }
}