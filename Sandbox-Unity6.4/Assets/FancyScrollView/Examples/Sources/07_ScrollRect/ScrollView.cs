/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System;
using UnityEngine;

namespace Lilja.FancyScrollView.Example07
{
    class ScrollView : FancyScrollRect<ItemData, Context>
    {
        [SerializeField] float cellSize = 100f;
        [SerializeField] Cell cellPrefab = default;

        protected override float CellSize => cellSize;
        protected override FancyCell<ItemData, Context> CellPrefab => cellPrefab;
        public int DataCount => ItemsSource.Count;

        public float PaddingTop
        {
            get => paddingHead;
            set
            {
                paddingHead = value;
                RefreshLayout();
            }
        }

        public float PaddingBottom
        {
            get => paddingTail;
            set
            {
                paddingTail = value;
                RefreshLayout();
            }
        }

        public float Spacing
        {
            get => spacing;
            set
            {
                spacing = value;
                RefreshLayout();
            }
        }

        public void OnCellClicked(Action<int> callback)
        {
            Context.OnCellClicked = callback;
        }

        public void ScrollTo(int index, float duration, Ease easing, Alignment alignment = Alignment.Middle)
        {
            UpdateSelection(index);
            base.ScrollTo(index, duration, easing, GetAlignment(alignment));
        }

        public void JumpTo(int index, Alignment alignment = Alignment.Middle)
        {
            UpdateSelection(index);
            base.JumpTo(index, GetAlignment(alignment));
        }

        float GetAlignment(Alignment alignment)
        {
            switch (alignment)
            {
                case Alignment.Upper: return 0.0f;
                case Alignment.Middle: return 0.5f;
                case Alignment.Lower: return 1.0f;
                default: return GetAlignment(Alignment.Middle);
            }
        }

        void UpdateSelection(int index)
        {
            if (Context.SelectedIndex == index)
            {
                return;
            }

            Context.SelectedIndex = index;
            RefreshItems();
        }

        protected override ItemData CreatePreviewItem(FancyScrollPreviewItemContext context)
        {
            return new ItemData(string.Format("Preview Cell {0:00}", context.Index));
        }
    }
}
