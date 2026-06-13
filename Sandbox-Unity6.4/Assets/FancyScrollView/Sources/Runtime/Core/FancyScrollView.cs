/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using EasingCore;

namespace FancyScrollView
{
    /// <summary>
    /// Core implementation that owns lifecycle, pooling, scroller wiring, and edit-mode preview.
    /// Public scroll view types expose item-data oriented APIs on top of this class.
    /// </summary>
    /// <typeparam name="TCellData">Data type consumed by each pooled cell.</typeparam>
    /// <typeparam name="TContext"><see cref="Context"/> の型.</typeparam>
    public abstract class FancyScrollViewCore<TCellData, TContext> : FancyScrollViewBase
        where TContext : class, new()
    {
        /// <summary>
        /// セル同士の間隔.
        /// </summary>
        [SerializeField, Range(1e-2f, 1f)] protected float cellInterval = 0.2f;

        /// <summary>
        /// スクロール位置の基準.
        /// </summary>
        /// <remarks>
        /// たとえば、 <c>0.5</c> を指定してスクロール位置が <c>0</c> の場合, 中央に最初のセルが配置されます.
        /// </remarks>
        [SerializeField, Range(0f, 1f)] protected float scrollOffset = 0.5f;

        /// <summary>
        /// セルを循環して配置させるどうか.
        /// </summary>
        /// <remarks>
        /// <c>true</c> にすると最後のセルの後に最初のセル, 最初のセルの前に最後のセルが並ぶようになります.
        /// 無限スクロールを実装する場合は <c>true</c> を指定します.
        /// </remarks>
        [SerializeField] protected bool loop = false;

        /// <summary>
        /// セルの親要素となる <c>Transform</c>.
        /// </summary>
        [SerializeField] protected Transform cellContainer = default;

        readonly List<FancyCell<TCellData, TContext>> pool = new List<FancyCell<TCellData, TContext>>();
        readonly IList<TCellData> emptyItems = new List<TCellData>();

        /// <summary>
        /// 初期化済みかどうか.
        /// </summary>
        protected bool initialized;

        /// <summary>
        /// 現在のスクロール位置.
        /// </summary>
        protected float currentPosition;

        /// <summary>
        /// セルの Prefab.
        /// </summary>
        protected abstract FancyCell<TCellData, TContext> CellPrefab { get; }

        /// <summary>
        /// アイテム一覧のデータ.
        /// </summary>
        protected IList<TCellData> ItemsSource { get; private set; } = new List<TCellData>();

        /// <summary>
        /// <typeparamref name="TContext"/> のインスタンス.
        /// セルとスクロールビュー間で同じインスタンスが共有されます. 情報の受け渡しや状態の保持に使用します.
        /// </summary>
        protected TContext Context { get; } = new TContext();

#if UNITY_EDITOR
        IList<TCellData> itemsSourceBeforePreview;
        bool initializedBeforePreview;
        bool loopBeforePreview;
        bool editorPreviewing;
        float cellIntervalBeforePreview;
        float currentPositionBeforePreview;
        float scrollOffsetBeforePreview;
        int cachedEditorPreviewItemCount = -1;

        internal override bool EditorPreviewing => editorPreviewing;

        /// <summary>
        /// Edit-mode preview is currently active.
        /// </summary>
        protected bool IsEditorPreviewing => editorPreviewing;
#endif

        /// <summary>
        /// セルとスクロールビュー間で共有する context を設定します.
        /// </summary>
        /// <param name="context">共有 context.</param>
        protected virtual void SetupContext(TContext context) { }

        /// <summary>
        /// セル生成直後に呼び出されます.
        /// </summary>
        /// <param name="cell">生成されたセル.</param>
        protected virtual void OnCellCreated(FancyCell<TCellData, TContext> cell) { }

        /// <summary>
        /// <see cref="Scroller"/> の選択インデックスが変更された際に呼び出されます.
        /// </summary>
        /// <param name="index">選択インデックス.</param>
        protected virtual void OnScrollerSelectionChanged(int index) { }

        /// <summary>
        /// <see cref="Scroller"/> に設定する総要素数.
        /// </summary>
        private protected virtual int ScrollerItemCount => ItemsSource.Count;

        /// <summary>
        /// 指定された item index が表すスクロール位置.
        /// </summary>
        /// <param name="itemIndex">アイテムのインデックス.</param>
        /// <returns>スクロール位置.</returns>
        private protected virtual float GetScrollPositionForItem(int itemIndex) => itemIndex;

        /// <summary>
        /// <see cref="Scroller"/> が扱うスクロール位置をこの view が扱う位置に変換します.
        /// </summary>
        /// <param name="position"><see cref="Scroller"/> が扱うスクロール位置.</param>
        /// <returns>この view が扱うスクロール位置.</returns>
        private protected virtual float ToFancyScrollViewPosition(float position) => position;

        /// <summary>
        /// この view が扱うスクロール位置を <see cref="Scroller"/> が扱う位置に変換します.
        /// </summary>
        /// <param name="position">この view が扱うスクロール位置.</param>
        /// <param name="alignment">ビューポート内におけるセル位置の基準. 0f(先頭) ~ 1f(末尾).</param>
        /// <returns><see cref="Scroller"/> が扱うスクロール位置.</returns>
        private protected virtual float ToScrollerPosition(float position, float alignment = 0.5f) => position;

        /// <summary>
        /// ItemsSource が更新された直後に呼び出されます.
        /// </summary>
        /// <param name="items">更新後の items.</param>
        private protected virtual void OnItemsSourceChanged(IList<TCellData> items) { }



        /// <summary>
        /// レイアウト更新の直前に呼び出されます.
        /// </summary>
        private protected virtual void OnBeforeRefresh() { }

        /// <summary>
        /// セルの表示内容を再適用します.
        /// </summary>
        public void RefreshItems() => RefreshInternal(true);

        /// <summary>
        /// セルの表示内容を再適用せず、レイアウトだけを更新します.
        /// </summary>
        public void RefreshLayout() => RefreshInternal(false);



        /// <summary>
        /// 渡されたアイテム一覧に基づいて表示内容を更新します.
        /// </summary>
        /// <param name="itemsSource">アイテム一覧.</param>
        private protected void SetItemsCore(IList<TCellData> itemsSource)
        {
            EnsureInitialized();

            ItemsSource = itemsSource ?? emptyItems;
            OnItemsSourceChanged(ItemsSource);

            RefreshItems();
        }

        void RefreshInternal(bool forceRefresh)
        {
            EnsureInitialized();
            OnBeforeRefresh();
            UpdatePositionInternal(currentPosition, forceRefresh);
        }

        protected void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            ValidateContainer();
            SetupContext(Context);
            ValidateCellPrefab();
            Initialize();
            InitializeCore();
            initialized = true;
        }

        void ValidateContainer()
        {
            if (cellContainer == null)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} requires Cell Container.",
                    GetType().Name));
            }
        }

        /// <summary>
        /// 初期化を行います.
        /// </summary>
        protected virtual void Initialize() { }

        /// <summary>
        /// 追加の初期化処理を行います.
        /// </summary>
        protected virtual void InitializeCore() { }

        void ValidateCellPrefab()
        {
            if (CellPrefab == null)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} requires a cell prefab of type FancyCell<{1}, {2}>.",
                    GetType().Name,
                    typeof(TCellData).Name,
                    typeof(TContext).Name));
            }
        }



        private protected void UpdatePositionInternal(float position, bool forceRefresh)
        {
            currentPosition = position;

            var p = position - scrollOffset / cellInterval;
            var firstIndex = Mathf.CeilToInt(p);
            var firstPosition = (Mathf.Ceil(p) - p) * cellInterval;

            if (firstPosition + pool.Count * cellInterval < 1f)
            {
                ResizePool(firstPosition);
            }

            UpdateCells(firstPosition, firstIndex, forceRefresh);
        }

        /// <summary>
        /// スクロール位置を更新します.
        /// </summary>
        /// <param name="position">スクロール位置.</param>
        protected virtual void UpdatePosition(float position)
        {
            UpdatePositionInternal(position, false);
        }

        void ResizePool(float firstPosition)
        {
            var addCount = Mathf.CeilToInt((1f - firstPosition) / cellInterval) - pool.Count;
            for (var i = 0; i < addCount; i++)
            {
                var cell = Instantiate(CellPrefab, cellContainer);

#if UNITY_EDITOR
                if (editorPreviewing)
                {
                    MarkEditorPreviewObject(cell.gameObject);
                }
#endif

                cell.SetContext(Context);
                cell.Initialize();
                OnCellCreated(cell);

#if UNITY_EDITOR
                if (editorPreviewing)
                {
                    MarkEditorPreviewObject(cell.gameObject);
                }
#endif

                cell.SetVisible(false);
                pool.Add(cell);
            }
        }

        /// <summary>
        /// Destroys all pooled cells and resets initialization state.
        /// </summary>
        /// <param name="destroyImmediately">Use immediate destruction. This is required for edit-mode cleanup.</param>
        private protected void ClearCellPool(bool destroyImmediately)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var cell = pool[i];
                if (cell != null)
                {
                    DestroyGameObject(cell.gameObject, destroyImmediately);
                }
            }

            pool.Clear();
            initialized = false;
        }

        static void DestroyGameObject(GameObject gameObject, bool destroyImmediately)
        {
            if (gameObject == null)
            {
                return;
            }

            if (destroyImmediately)
            {
                DestroyImmediate(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void UpdateCells(float firstPosition, int firstIndex, bool forceRefresh)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var index = firstIndex + i;
                var position = firstPosition + i * cellInterval;
                var cell = pool[CircularIndex(index, pool.Count)];

                if (loop)
                {
                    index = CircularIndex(index, ItemsSource.Count);
                }

                if (index < 0 || index >= ItemsSource.Count || position > 1f)
                {
                    cell.SetVisible(false);
                    continue;
                }

                if (forceRefresh || cell.Index != index || !cell.IsVisible)
                {
                    cell.Index = index;
                    cell.SetVisible(true);
                    cell.UpdateContent(ItemsSource[index]);
                }

                cell.UpdatePosition(position);
            }
        }

        int CircularIndex(int i, int size) => size < 1 ? 0 : i < 0 ? size - 1 + (i + 1) % size : i % size;

#if UNITY_EDITOR
        internal override string GetEditorPreviewError()
        {
            if (Application.isPlaying)
            {
                return "Preview is only available in Edit Mode.";
            }

            var cellPrefabError = GetEditorPreviewCellPrefabError();
            if (!string.IsNullOrEmpty(cellPrefabError))
            {
                return cellPrefabError;
            }

            if (cellContainer == null)
            {
                return "Cell Container is not assigned.";
            }

            if (GetEditorPreviewItemCount() <= 0)
            {
                return "Preview Item Count must be greater than 0.";
            }

            return GetAdditionalEditorPreviewError();
        }

        internal override float GetEditorPreviewMaxPosition() => Mathf.Max(0, GetEditorPreviewItemCount() - 1);

        internal override void BeginEditorPreview()
        {
            if (editorPreviewing)
            {
                return;
            }

            itemsSourceBeforePreview = ItemsSource;
            initializedBeforePreview = initialized;
            loopBeforePreview = loop;
            cellIntervalBeforePreview = cellInterval;
            currentPositionBeforePreview = currentPosition;
            scrollOffsetBeforePreview = scrollOffset;
            cachedEditorPreviewItemCount = -1;
            editorPreviewing = true;

            OnPreviewBegin();

            ClearCellPool(true);
            ClearEditorPreviewObjects();
        }

        internal override void UpdateEditorPreview(float position, bool forceRefresh)
        {
            if (!editorPreviewing)
            {
                BeginEditorPreview();
            }

            if (forceRefresh)
            {
                ClearCellPool(true);
                ClearEditorPreviewObjects();
            }

            var itemCount = GetEditorPreviewItemCount();
            if (forceRefresh || itemCount != cachedEditorPreviewItemCount)
            {
                cachedEditorPreviewItemCount = itemCount;
                ApplyEditorPreviewItems(CreateEditorPreviewItems(itemCount));
            }

            ApplyEditorPreviewPosition(position, forceRefresh);
            MarkEditorPreviewCells();
        }

        internal override void EndEditorPreview()
        {
            if (!editorPreviewing)
            {
                return;
            }

            ClearCellPool(true);
            ClearEditorPreviewObjects();

            ItemsSource = itemsSourceBeforePreview ?? emptyItems;
            initialized = initializedBeforePreview && pool.Count > 0;
            loop = loopBeforePreview;
            cellInterval = cellIntervalBeforePreview;
            currentPosition = currentPositionBeforePreview;
            scrollOffset = scrollOffsetBeforePreview;
            cachedEditorPreviewItemCount = -1;
            editorPreviewing = false;

            OnPreviewEnd();
        }

        protected virtual string EditorPreviewCellDataTypeName => typeof(TCellData).Name;

        private protected virtual string GetEditorPreviewCellPrefabError()
        {
            return CellPrefab == null
                ? string.Format(
                    "Assign a cell prefab of type FancyCell<{0}, {1}>.",
                    EditorPreviewCellDataTypeName,
                    typeof(TContext).Name)
                : null;
        }

        protected abstract int GetEditorPreviewItemCount();

        protected abstract IList<TCellData> CreateEditorPreviewItems(int itemCount);

        private protected virtual string GetAdditionalEditorPreviewError() => null;

        private protected virtual void ApplyEditorPreviewItems(IList<TCellData> items) => SetItemsCore(items);

        private protected virtual void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            UpdatePositionInternal(position, forceRefresh);
        }

        protected virtual void OnPreviewBegin() { }

        protected virtual void OnPreviewEnd() { }

        private protected void MarkEditorPreviewObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            SetHideFlagsRecursively(gameObject.transform, FancyScrollViewBase.EditorPreviewHideFlags);
        }

        void MarkEditorPreviewCells()
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var cell = pool[i];
                if (cell != null)
                {
                    MarkEditorPreviewObject(cell.gameObject);
                }
            }
        }

        void ClearEditorPreviewObjects()
        {
            if (cellContainer == null)
            {
                return;
            }

            for (var i = cellContainer.childCount - 1; i >= 0; i--)
            {
                var child = cellContainer.GetChild(i);
                if (IsEditorPreviewObject(child.gameObject))
                {
                    DestroyGameObject(child.gameObject, true);
                }
            }
        }

        static bool IsEditorPreviewObject(GameObject gameObject)
        {
            return (gameObject.hideFlags & FancyScrollViewBase.EditorPreviewHideFlags) ==
                FancyScrollViewBase.EditorPreviewHideFlags;
        }

        static void SetHideFlagsRecursively(Transform target, HideFlags hideFlags)
        {
            target.gameObject.hideFlags = hideFlags;

            for (var i = 0; i < target.childCount; i++)
            {
                SetHideFlagsRecursively(target.GetChild(i), hideFlags);
            }
        }

        bool cachedLoop;
        float cachedCellInterval, cachedScrollOffset;

        void LateUpdate()
        {
            if (editorPreviewing)
            {
                return;
            }

            if (cachedLoop != loop ||
                cachedCellInterval != cellInterval ||
                cachedScrollOffset != scrollOffset)
            {
                cachedLoop = loop;
                cachedCellInterval = cellInterval;
                cachedScrollOffset = scrollOffset;

                RefreshLayout();
            }
        }
#endif
    }

    /// <summary>
    /// スクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップに対応しています.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> が不要な場合は
    /// 代わりに <see cref="FancyScrollView{TItemData}"/> を使用します.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <typeparam name="TContext"><see cref="Context"/> の型.</typeparam>
    public abstract class FancyScrollView<TItemData, TContext> : FancyScrollViewCore<TItemData, TContext>
        where TContext : class, new()
    {
        /// <summary>
        /// Edit-mode preview item count.
        /// </summary>
        protected virtual int PreviewItemCount => EditorPreviewItemCount;

        /// <summary>
        /// 渡されたアイテム一覧に基づいて表示内容を更新します.
        /// </summary>
        /// <param name="items">アイテム一覧.</param>
        public virtual void SetItems(IList<TItemData> items) => SetItemsCore(items);

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
    /// <see cref="FancyScrollView{TItemData}"/> のコンテキストクラス.
    /// </summary>
    public sealed class NullContext { }

    /// <summary>
    /// スクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップに対応しています.
    /// </summary>
    /// <typeparam name="TItemData"></typeparam>
    /// <seealso cref="FancyScrollView{TItemData, TContext}"/>
    public abstract class FancyScrollView<TItemData> : FancyScrollView<TItemData, NullContext> { }
}
