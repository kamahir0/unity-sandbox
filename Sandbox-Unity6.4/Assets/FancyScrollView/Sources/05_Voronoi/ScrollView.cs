/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lilja.FancyScrollView.Example05
{
    class ScrollView : FancyScrollView<ItemData, Context>
    {
        [SerializeField] Cell cellPrefab = default;
        [SerializeField] Scroller scroller = default;

        Action<int> onSelectionChanged;

        protected override FancyCell<ItemData, Context> CellPrefab => cellPrefab;

        public int CellInstanceCount => Mathf.CeilToInt(1f / Mathf.Max(cellInterval, 1e-3f));

        protected override void Initialize()
        {
            base.Initialize();
            Context.OnCellClicked = SelectCell;
            scroller.OnValueChanged(UpdatePosition);
            scroller.OnSelectionChanged(UpdateSelection);
        }

        public void UpdateData(IList<ItemData> items)
        {
            UpdateContents(items);
            scroller.SetTotalCount(items.Count);
        }

        public void UpdateSelection(int index)
        {
            if (Context.SelectedIndex == index)
            {
                return;
            }

            Context.SelectedIndex = index;
            Refresh();

            onSelectionChanged?.Invoke(index);
        }

        public void OnSelectionChanged(Action<int> callback)
        {
            onSelectionChanged = callback;
        }

        public void SelectNextCell()
        {
            SelectCell(Context.SelectedIndex + 1);
        }

        public void SelectPrevCell()
        {
            SelectCell(Context.SelectedIndex - 1);
        }

        public void SelectCell(int index)
        {
            if (index < 0 || index >= ItemsSource.Count || index == Context.SelectedIndex)
            {
                return;
            }

            UpdateSelection(index);
            scroller.ScrollTo(index, 0.35f, Ease.OutCubic);
        }

        public void ScrollTo(float position, float duration, Ease easing, Action onComplete = null)
        {
            scroller.ScrollTo(position, duration, easing, onComplete);
        }

        public void JumpTo(int index)
        {
            scroller.JumpTo(index);
        }

        public Vector4[] GetCellState()
        {
            Context.UpdateCellState?.Invoke();
            return Context.CellState;
        }

        public void SetCellState(int cellIndex, int dataIndex, float x, float y, float selectAnimation)
        {
            Context.SetCellState(cellIndex, dataIndex, x, y, selectAnimation);
        }

        protected override bool TryCreatePreviewItem(FancyScrollPreviewItemContext context, out ItemData item)
        {
            item = new ItemData(string.Format("Preview Cell {0:00}", context.Index));
            return true;
        }
    }
}
