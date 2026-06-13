/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace FancyScrollView
{
    /// <summary>
    /// グリッドレイアウトのスクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップには対応していません.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> が不要な場合は
    /// 代わりに <see cref="FancyGridView{TItemData}"/> を使用します.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <typeparam name="TContext"><see cref="FancyScrollView{TItemData, TContext}.Context"/> の型.</typeparam>
    public abstract class FancyGridView<TItemData, TContext> : FancyScrollRectCore<TItemData[], TContext>
        where TContext : class, IFancyGridViewContext, new()
    {
        /// <summary>
        /// Grid view が内部で使用する非 generic のセルグループ基底クラス.
        /// </summary>
        protected abstract class DefaultCellGroup : FancyCellGroup<TItemData, TContext> { }

        /// <summary>
        /// 最初にセルを配置する軸方向のセル同士の余白.
        /// </summary>
        [SerializeField] protected float startAxisSpacing = 0f;

        /// <summary>
        /// 最初にセルを配置する軸方向のセル数.
        /// </summary>
        [SerializeField] protected int startAxisCellCount = 4;

        /// <summary>
        /// セルのサイズ.
        /// </summary>
        [SerializeField] protected Vector2 cellSize = new Vector2(100f, 100f);

        FancyCell<TItemData[], TContext> cellGroupTemplate;

        /// <inheritdoc/>
        protected sealed override FancyCell<TItemData[], TContext> CellPrefab => cellGroupTemplate;

        /// <inheritdoc/>
        protected sealed override float CellSize => Scroller.ScrollDirection == ScrollDirection.Horizontal
            ? cellSize.x
            : cellSize.y;

        /// <summary>
        /// Edit-mode preview item count.
        /// </summary>
        protected virtual int PreviewItemCount => EditorPreviewItemCount;

        /// <summary>
        /// アイテムの総数.
        /// </summary>
        public int DataCount { get; private set; }

        /// <summary>
        /// 渡された flat item 一覧に基づいて表示内容を更新します.
        /// </summary>
        /// <param name="items">Flat item 一覧.</param>
        public void SetItems(IList<TItemData> items)
        {
            DataCount = items != null ? items.Count : 0;
            SetItemsCore(CreateGroups(items));
        }

        /// <summary>
        /// Edit-mode preview 用の flat item data を作成します.
        /// </summary>
        /// <param name="context">Preview item context.</param>
        /// <returns>Preview item data.</returns>
        protected abstract TItemData CreatePreviewItem(FancyScrollPreviewItemContext context);

        /// <summary>
        /// 最初にセルが生成される直前に呼び出されます.
        /// <see cref="Setup{TGroup}(FancyCell{TItemData, TContext})"/> メソッドを使用してセルテンプレートのセットアップを行ってください.
        /// </summary>
        protected abstract void SetupCellTemplate();

        /// <summary>
        /// セルテンプレートのセットアップを行います.
        /// </summary>
        /// <param name="cellTemplate">セルのテンプレート.</param>
        /// <typeparam name="TGroup">セルグループの型.</typeparam>
        protected virtual void Setup<TGroup>(FancyCell<TItemData, TContext> cellTemplate)
            where TGroup : FancyCell<TItemData[], TContext>
        {
            if (cellTemplate == null)
            {
                throw new InvalidOperationException("Cell template is not assigned.");
            }

            Context.CellTemplate = cellTemplate.gameObject;

            cellGroupTemplate = new GameObject("Group").AddComponent<TGroup>();
            cellGroupTemplate.transform.SetParent(cellContainer, false);
            cellGroupTemplate.SetVisible(false);

#if UNITY_EDITOR
            if (IsEditorPreviewing)
            {
                MarkEditorPreviewObject(cellGroupTemplate.gameObject);
            }
#endif
        }

        /// <inheritdoc/>
        protected sealed override void SetupScrollRectContext(TContext context)
        {
            context.ScrollDirection = Scroller.ScrollDirection;
            context.GetGroupCount = () => Mathf.Max(1, startAxisCellCount);
            context.GetStartAxisSpacing = () => startAxisSpacing;
            context.GetCellSize = () => Scroller.ScrollDirection == ScrollDirection.Horizontal
                ? cellSize.y
                : cellSize.x;

            SetupCellTemplate();
        }

        /// <inheritdoc/>
        private protected override float GetScrollPositionForItem(int itemIndex)
        {
            return itemIndex / Mathf.Max(1, startAxisCellCount);
        }

        IList<TItemData[]> CreateGroups(IList<TItemData> items)
        {
            var source = items ?? Array.Empty<TItemData>();
            var groupSize = Mathf.Max(1, startAxisCellCount);

            return source
                .Select((item, index) => (item, index))
                .GroupBy(
                    x => x.index / groupSize,
                    x => x.item)
                .Select(group => group.ToArray())
                .ToArray();
        }

#if UNITY_EDITOR
        protected override string EditorPreviewCellDataTypeName => typeof(TItemData).Name;

        private protected override string GetEditorPreviewCellPrefabError()
        {
            return null;
        }

        protected sealed override int GetEditorPreviewItemCount() => Mathf.Max(0, PreviewItemCount);

        internal override float GetEditorPreviewMaxPosition() => Mathf.Max(0, GetEditorPreviewItemCount() - 1);

        protected sealed override IList<TItemData[]> CreateEditorPreviewItems(int itemCount)
        {
            var previewItems = Enumerable.Range(0, itemCount)
                .Select(index => CreatePreviewItem(new FancyScrollPreviewItemContext(index, itemCount)))
                .ToArray();

            DataCount = previewItems.Length;
            return CreateGroups(previewItems);
        }

        private protected override void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            var groupSize = Mathf.Max(1, startAxisCellCount);
            base.ApplyEditorPreviewPosition(position / groupSize, forceRefresh);
        }

        protected override void OnPreviewEnd()
        {
            if (cellGroupTemplate != null)
            {
                DestroyImmediate(cellGroupTemplate.gameObject);
            }

            cellGroupTemplate = null;
            base.OnPreviewEnd();
        }
#endif
    }

    /// <summary>
    /// グリッドレイアウトのスクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップには対応していません.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <seealso cref="FancyGridView{TItemData, TContext}"/>
    public abstract class FancyGridView<TItemData> : FancyGridView<TItemData, FancyGridViewContext> { }
}
