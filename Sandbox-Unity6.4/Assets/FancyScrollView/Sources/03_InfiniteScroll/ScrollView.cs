/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System.Collections.Generic;
using UnityEngine;

namespace Lilja.FancyScrollView.Example03
{
    class ScrollView : FancyScrollView<ItemData, Context>
    {
        [SerializeField] Cell cellPrefab = default;
        [SerializeField] Scroller scroller = default;

        protected override FancyCell<ItemData, Context> CellPrefab => cellPrefab;

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

        void UpdateSelection(int index)
        {
            if (Context.SelectedIndex == index)
            {
                return;
            }

            Context.SelectedIndex = index;
            Refresh();
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

        protected override bool TryCreatePreviewItem(FancyScrollPreviewItemContext context, out ItemData item)
        {
            item = new ItemData(string.Format("Preview Cell {0:00}", context.Index));
            return true;
        }
    }
}
