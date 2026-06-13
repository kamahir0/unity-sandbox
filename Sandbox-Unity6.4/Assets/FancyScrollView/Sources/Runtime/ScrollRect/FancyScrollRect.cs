/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System.Collections.Generic;
using UnityEngine;

namespace FancyScrollView
{
    /// <summary>
    /// Core implementation for ScrollRect-style views.
    /// </summary>
    /// <typeparam name="TCellData">Data type consumed by each pooled cell.</typeparam>
    /// <typeparam name="TContext"><see cref="FancyScrollViewCore{TCellData,TContext}.Context"/> の型.</typeparam>
    public abstract class FancyScrollRectCore<TCellData, TContext> : FancyScrollViewCore<TCellData, TContext>
        where TContext : class, IFancyScrollRectContext, new()
    {
        /// <summary>
        /// スクロール中にセルが再利用されるまでの余白のセル数.
        /// </summary>
        /// <remarks>
        /// <c>0</c> を指定するとセルが完全に隠れた直後に再利用されます.
        /// <c>1</c> 以上を指定すると, そのセル数だけ余分にスクロールしてから再利用されます.
        /// </remarks>
        [SerializeField] protected float reuseCellMarginCount = 0f;

        /// <summary>
        /// コンテンツ先頭の余白.
        /// </summary>
        [SerializeField] protected float paddingHead = 0f;

        /// <summary>
        /// コンテンツ末尾の余白.
        /// </summary>
        [SerializeField] protected float paddingTail = 0f;

        /// <summary>
        /// スクロール軸方向のセル同士の余白.
        /// </summary>
        [SerializeField] protected float spacing = 0f;

        /// <summary>
        /// セルのサイズ.
        /// </summary>
        protected abstract float CellSize { get; }

        /// <summary>
        /// スクロール可能かどうか.
        /// </summary>
        /// <remarks>
        /// アイテム数が十分少なくビューポート内に全てのセルが収まっている場合は <c>false</c>, それ以外は <c>true</c> になります.
        /// </remarks>
        protected virtual bool Scrollable => MaxScrollPosition > 0f;

#if UNITY_EDITOR
        bool previewScrollerStateStored;
        bool previewScrollerDraggable;
        bool previewScrollbarActive;
        float previewScrollSensitivity;
        float previewScrollbarSize;
#endif

        float ScrollLength => 1f / Mathf.Max(cellInterval, 1e-2f) - 1f;

        float ViewportLength => ScrollLength - reuseCellMarginCount * 2f;

        float PaddingHeadLength => (paddingHead - spacing * 0.5f) / (CellSize + spacing);

        float MaxScrollPosition => ItemsSource.Count
            - ScrollLength
            + reuseCellMarginCount * 2f
            + (paddingHead + paddingTail - spacing) / (CellSize + spacing);

        /// <inheritdoc/>
        protected sealed override void SetupContext(TContext context)
        {
            context.ScrollDirection = Scroller.ScrollDirection;
            context.CalculateScrollSize = () =>
            {
                var interval = CellSize + spacing;
                var reuseMargin = interval * reuseCellMarginCount;
                var scrollSize = Scroller.ViewportSize + interval + reuseMargin * 2f;
                return (scrollSize, reuseMargin);
            };

            SetupScrollRectContext(context);
        }

        /// <summary>
        /// ScrollRect 用 context が設定された後に呼び出されます.
        /// </summary>
        /// <param name="context">共有 context.</param>
        protected virtual void SetupScrollRectContext(TContext context) { }

        /// <inheritdoc/>
        private protected override void ApplyScrollerPosition(float position)
        {
            UpdateScrollPosition(position);

            if (Scroller.Scrollbar)
            {
                if (position > ItemsSource.Count - 1)
                {
                    ShrinkScrollbar(position - (ItemsSource.Count - 1));
                }
                else if (position < 0f)
                {
                    ShrinkScrollbar(-position);
                }
            }
        }

        void UpdateScrollPosition(float scrollerPosition)
        {
            var position = ToFancyScrollViewPosition(Scrollable ? scrollerPosition : 0f);
            ApplyScrollRectPosition(position, false);
        }

        /// <summary>
        /// ScrollRect 変換済み位置をレイアウトに適用します.
        /// </summary>
        /// <param name="position">Scroll view position.</param>
        /// <param name="forceRefresh">セル内容も強制更新するかどうか.</param>
        protected void ApplyScrollRectPosition(float position, bool forceRefresh)
        {
            UpdatePositionInternal(position, forceRefresh);
        }

        /// <summary>
        /// スクロール範囲を超えてスクロールされた量に基づいて, スクロールバーのサイズを縮小します.
        /// </summary>
        /// <param name="offset">スクロール範囲を超えてスクロールされた量.</param>
        void ShrinkScrollbar(float offset)
        {
            var scale = 1f - ToFancyScrollViewPosition(offset) / (ViewportLength - PaddingHeadLength);
            UpdateScrollbarSize((ViewportLength - PaddingHeadLength) * scale);
        }

        /// <inheritdoc/>
        private protected override void OnItemsSourceChanged(IList<TCellData> items)
        {
            AdjustCellIntervalAndScrollOffset();
        }

        /// <inheritdoc/>
        private protected override void OnScrollerItemCountChanged()
        {
            RefreshScroller();
        }

        /// <inheritdoc/>
        private protected override void OnBeforeRefresh()
        {
            AdjustCellIntervalAndScrollOffset();
            RefreshScroller();
        }

        /// <summary>
        /// <see cref="Scroller"/> の各種状態を更新します.
        /// </summary>
        protected void RefreshScroller()
        {
            Scroller.Draggable = Scrollable;
            Scroller.ScrollSensitivity = ToRawScrollerPosition(ViewportLength - PaddingHeadLength);
            Scroller.Position = ToRawScrollerPosition(currentPosition);

            if (Scroller.Scrollbar)
            {
                Scroller.Scrollbar.gameObject.SetActive(Scrollable);
                UpdateScrollbarSize(ViewportLength);
            }
        }

        /// <summary>
        /// ビューポートとコンテンツの長さに基づいてスクロールバーのサイズを更新します.
        /// </summary>
        /// <param name="viewportLength">ビューポートのサイズ.</param>
        protected void UpdateScrollbarSize(float viewportLength)
        {
            var contentLength = Mathf.Max(ItemsSource.Count + (paddingHead + paddingTail - spacing) / (CellSize + spacing), 1);
            Scroller.Scrollbar.size = Scrollable ? Mathf.Clamp01(viewportLength / contentLength) : 1f;
        }

        /// <inheritdoc/>
        private protected override float ToFancyScrollViewPosition(float position)
        {
            return position / Mathf.Max(ItemsSource.Count - 1, 1) * MaxScrollPosition - PaddingHeadLength;
        }

        /// <inheritdoc/>
        private protected override float ToScrollerPosition(float position, float alignment = 0.5f)
        {
            var offset = alignment * (ScrollLength - (1f + reuseCellMarginCount * 2f))
                + (1f - alignment - 0.5f) * spacing / (CellSize + spacing);
            return ToRawScrollerPosition(Mathf.Clamp(position - offset, 0f, MaxScrollPosition));
        }

        float ToRawScrollerPosition(float position)
        {
            if (Mathf.Approximately(MaxScrollPosition, 0f))
            {
                return 0f;
            }

            return (position + PaddingHeadLength) / MaxScrollPosition * Mathf.Max(ItemsSource.Count - 1, 1);
        }

        /// <summary>
        /// 指定された設定を実現するための
        /// <see cref="FancyScrollViewCore{TCellData,TContext}.cellInterval"/> と
        /// <see cref="FancyScrollViewCore{TCellData,TContext}.scrollOffset"/> を計算して適用します.
        /// </summary>
        protected void AdjustCellIntervalAndScrollOffset()
        {
            var totalSize = Scroller.ViewportSize + (CellSize + spacing) * (1f + reuseCellMarginCount * 2f);
            cellInterval = (CellSize + spacing) / totalSize;
            scrollOffset = cellInterval * (1f + reuseCellMarginCount);
        }

#if UNITY_EDITOR
        private protected override void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            var scrollerPosition = Scrollable ? ToScrollerPosition(position, 0.5f) : 0f;
            Scroller.Position = scrollerPosition;

            if (forceRefresh)
            {
                ApplyScrollRectPosition(ToFancyScrollViewPosition(Scrollable ? scrollerPosition : 0f), true);
            }
        }

        protected override void OnPreviewBegin()
        {
            base.OnPreviewBegin();

            previewScrollerDraggable = Scroller.Draggable;
            previewScrollSensitivity = Scroller.ScrollSensitivity;

            if (Scroller.Scrollbar)
            {
                previewScrollbarActive = Scroller.Scrollbar.gameObject.activeSelf;
                previewScrollbarSize = Scroller.Scrollbar.size;
            }

            previewScrollerStateStored = true;
        }

        protected override void OnPreviewEnd()
        {
            if (previewScrollerStateStored)
            {
                Scroller.Draggable = previewScrollerDraggable;
                Scroller.ScrollSensitivity = previewScrollSensitivity;

                if (Scroller.Scrollbar)
                {
                    Scroller.Scrollbar.gameObject.SetActive(previewScrollbarActive);
                    Scroller.Scrollbar.size = previewScrollbarSize;
                }
            }

            previewScrollerStateStored = false;
            base.OnPreviewEnd();
        }
#endif

        protected virtual void OnValidate()
        {
            if (Scroller != null)
            {
                AdjustCellIntervalAndScrollOffset();
            }

            if (loop)
            {
                loop = false;
                Debug.LogError("Loop is currently not supported in FancyScrollRect.");
            }

            if (Scroller != null && Scroller.SnapEnabled)
            {
                Scroller.SnapEnabled = false;
                Debug.LogError("Snap is currently not supported in FancyScrollRect.");
            }

            if (Scroller != null && Scroller.MovementType == MovementType.Unrestricted)
            {
                Scroller.MovementType = MovementType.Elastic;
                Debug.LogError("MovementType.Unrestricted is currently not supported in FancyScrollRect.");
            }
        }
    }

    /// <summary>
    /// ScrollRect スタイルのスクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップには対応していません.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> が不要な場合は
    /// 代わりに <see cref="FancyScrollRect{TItemData}"/> を使用します.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <typeparam name="TContext"><see cref="FancyScrollView{TItemData, TContext}.Context"/> の型.</typeparam>
    public abstract class FancyScrollRect<TItemData, TContext> : FancyScrollRectCore<TItemData, TContext>
        where TContext : class, IFancyScrollRectContext, new()
    {
        /// <summary>
        /// Edit-mode preview item count.
        /// </summary>
        protected virtual int PreviewItemCount => EditorPreviewItemCount;

        /// <summary>
        /// 渡されたアイテム一覧に基づいて表示内容を更新します.
        /// </summary>
        /// <param name="items">アイテム一覧.</param>
        public void SetItems(IList<TItemData> items) => SetItemsCore(items);

        /// <summary>
        /// Edit-mode preview 用の item data を作成します.
        /// </summary>
        /// <param name="context">Preview item context.</param>
        /// <returns>Preview item data.</returns>
        protected abstract TItemData CreatePreviewItem(FancyScrollPreviewItemContext context);

#if UNITY_EDITOR
        protected sealed override int GetEditorPreviewItemCount() => Mathf.Max(0, PreviewItemCount);

        protected sealed override IList<TItemData> CreateEditorPreviewItems(int itemCount)
        {
            var items = new List<TItemData>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                items.Add(CreatePreviewItem(new FancyScrollPreviewItemContext(i, itemCount)));
            }

            return items;
        }
#endif
    }

    /// <summary>
    /// ScrollRect スタイルのスクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップには対応していません.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <seealso cref="FancyScrollRect{TItemData, TContext}"/>
    public abstract class FancyScrollRect<TItemData> : FancyScrollRect<TItemData, FancyScrollRectContext> { }
}
