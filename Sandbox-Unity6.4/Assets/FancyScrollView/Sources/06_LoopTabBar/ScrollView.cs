/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lilja.FancyScrollView.Example06
{
    class ScrollView : FancyScrollView<ItemData, Context>
    {
        [SerializeField] Tab cellPrefab = default;
        [SerializeField] Scroller scroller = default;

        Action<int, MovementDirection> onSelectionChanged;

        protected override FancyCell<ItemData, Context> CellPrefab => cellPrefab;

        protected override void Initialize()
        {
            base.Initialize();
            Context.OnCellClicked = SelectCell;
            scroller.OnValueChanged(UpdatePosition);
            scroller.OnSelectionChanged(UpdateSelection);
        }

        public override void SetItems(IList<ItemData> items)
        {
            base.SetItems(items);
            scroller.SetTotalCount(items.Count);
        }

        void UpdateSelection(int index)
        {
            if (Context.SelectedIndex == index)
            {
                return;
            }

            var direction = scroller.GetMovementDirection(Context.SelectedIndex, index);

            Context.SelectedIndex = index;
            RefreshItems();

            onSelectionChanged?.Invoke(index, direction);
        }

        public void OnSelectionChanged(Action<int, MovementDirection> callback)
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

            scroller.ScrollTo(index, 0.35f, Ease.OutCubic);
        }

        protected override ItemData CreatePreviewItem(FancyScrollPreviewItemContext context)
        {
            return new ItemData(string.Format("Preview Tab {0:00}", context.Index));
        }
    }
}
