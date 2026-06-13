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
    /// スクロールビューを実装するための抽象基底クラス.
    /// 無限スクロールおよびスナップに対応しています.
    /// <see cref="FancyScrollView{TItemData, TContext}.Context"/> が不要な場合は
    /// 代わりに <see cref="FancyScrollView{TItemData}"/> を使用します.
    /// </summary>
    /// <typeparam name="TItemData">アイテムのデータ型.</typeparam>
    /// <typeparam name="TContext"><see cref="Context"/> の型.</typeparam>
    public abstract class FancyScrollView<TItemData, TContext> : FancyScrollViewBase where TContext : class, new()
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

        readonly List<FancyCell<TItemData, TContext>> pool = new List<FancyCell<TItemData, TContext>>();

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
        protected abstract GameObject CellPrefab { get; }

        /// <summary>
        /// アイテム一覧のデータ.
        /// </summary>
        protected IList<TItemData> ItemsSource { get; set; } = new List<TItemData>();

        /// <summary>
        /// <typeparamref name="TContext"/> のインスタンス.
        /// セルとスクロールビュー間で同じインスタンスが共有されます. 情報の受け渡しや状態の保持に使用します.
        /// </summary>
        protected TContext Context { get; } = new TContext();

#if UNITY_EDITOR
        IList<TItemData> itemsSourceBeforePreview;
        bool initializedBeforePreview;
        bool loopBeforePreview;
        bool editorPreviewing;
        float cellIntervalBeforePreview;
        float currentPositionBeforePreview;
        float scrollOffsetBeforePreview;
        int editorPreviewItemCount = -1;

        internal override bool EditorPreviewing => editorPreviewing;

        /// <summary>
        /// Edit-mode preview is currently active.
        /// </summary>
        protected bool IsEditorPreviewing => editorPreviewing;
#endif

        /// <summary>
        /// 初期化を行います.
        /// </summary>
        /// <remarks>
        /// 最初にセルが生成される直前に呼び出されます.
        /// </remarks>
        protected virtual void Initialize() { }

        /// <summary>
        /// 渡されたアイテム一覧に基づいて表示内容を更新します.
        /// </summary>
        /// <param name="itemsSource">アイテム一覧.</param>
        protected virtual void UpdateContents(IList<TItemData> itemsSource)
        {
            ItemsSource = itemsSource;
            Refresh();
        }

        /// <summary>
        /// セルのレイアウトを強制的に更新します.
        /// </summary>
        protected virtual void Relayout() => UpdatePositionInternal(currentPosition, false);

        /// <summary>
        /// セルのレイアウトと表示内容を強制的に更新します.
        /// </summary>
        protected virtual void Refresh() => UpdatePositionInternal(currentPosition, true);

        /// <summary>
        /// スクロール位置を更新します.
        /// </summary>
        /// <param name="position">スクロール位置.</param>
        protected virtual void UpdatePosition(float position) => UpdatePositionInternal(position, false);

        protected void UpdatePositionInternal(float position, bool forceRefresh)
        {
            if (!initialized)
            {
                Initialize();
                initialized = true;
            }

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

        void ResizePool(float firstPosition)
        {
            Debug.Assert(CellPrefab != null);
            Debug.Assert(cellContainer != null);

            var addCount = Mathf.CeilToInt((1f - firstPosition) / cellInterval) - pool.Count;
            for (var i = 0; i < addCount; i++)
            {
                var cell = Instantiate(CellPrefab, cellContainer).GetComponent<FancyCell<TItemData, TContext>>();
                if (cell == null)
                {
                    throw new MissingComponentException(string.Format(
                        "FancyCell<{0}, {1}> component not found in {2}.",
                        typeof(TItemData).FullName, typeof(TContext).FullName, CellPrefab.name));
                }

#if UNITY_EDITOR
                if (editorPreviewing)
                {
                    MarkEditorPreviewObject(cell.gameObject);
                }
#endif

                cell.SetContext(Context);
                cell.Initialize();

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
        protected void ClearCellPool(bool destroyImmediately)
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

            if (!HasEditorPreviewDataSource())
            {
                return string.Format(
                    "Implement IFancyScrollPreviewDataSource<{0}> to provide preview data.",
                    EditorPreviewItemDataTypeName);
            }

            if (!HasEditorPreviewCellPrefab())
            {
                return "Cell Prefab is not assigned.";
            }

            if (cellContainer == null)
            {
                return "Cell Container is not assigned.";
            }

            if (GetEditorPreviewItemCount() <= 0)
            {
                return "PreviewItemCount must be greater than 0.";
            }

            return null;
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
            editorPreviewItemCount = -1;
            editorPreviewing = true;

            (this as IFancyScrollPreviewLifecycle)?.OnBeginPreview();
            OnEditorPreviewBegin();

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
            if (forceRefresh || itemCount != editorPreviewItemCount)
            {
                editorPreviewItemCount = itemCount;
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

            ItemsSource = itemsSourceBeforePreview ?? new List<TItemData>();
            initialized = initializedBeforePreview && pool.Count > 0;
            loop = loopBeforePreview;
            cellInterval = cellIntervalBeforePreview;
            currentPosition = currentPositionBeforePreview;
            scrollOffset = scrollOffsetBeforePreview;
            editorPreviewItemCount = -1;
            editorPreviewing = false;

            OnEditorPreviewEnd();
            (this as IFancyScrollPreviewLifecycle)?.OnEndPreview();
        }

        protected virtual string EditorPreviewItemDataTypeName => typeof(TItemData).Name;

        protected virtual bool HasEditorPreviewDataSource() => this is IFancyScrollPreviewDataSource<TItemData>;

        protected virtual bool HasEditorPreviewCellPrefab() => CellPrefab != null;

        protected virtual int GetEditorPreviewItemCount()
        {
            return this is IFancyScrollPreviewDataSource<TItemData> source
                ? Mathf.Max(0, source.PreviewItemCount)
                : 0;
        }

        protected virtual IList<TItemData> CreateEditorPreviewItems(int itemCount)
        {
            var source = (IFancyScrollPreviewDataSource<TItemData>)this;
            var items = new List<TItemData>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                items.Add(source.CreatePreviewItem(new FancyScrollPreviewItemContext(i, itemCount)));
            }

            return items;
        }

        protected virtual void ApplyEditorPreviewItems(IList<TItemData> items) => UpdateContents(items);

        protected virtual void ApplyEditorPreviewPosition(float position, bool forceRefresh)
        {
            UpdatePositionInternal(position, forceRefresh);
        }

        protected virtual void OnEditorPreviewBegin() { }

        protected virtual void OnEditorPreviewEnd() { }

        protected void MarkEditorPreviewObject(GameObject gameObject)
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

                UpdatePosition(currentPosition);
            }
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
