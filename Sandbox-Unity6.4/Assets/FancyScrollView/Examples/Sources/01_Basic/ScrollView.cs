/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using UnityEngine;

namespace FancyScrollView.Example01
{
    class ScrollView : FancyScrollView<ItemData>
    {
        [SerializeField] Cell cellPrefab = default;

        protected override FancyCell<ItemData, NullContext> CellPrefab => cellPrefab;

        protected override ItemData CreatePreviewItem(FancyScrollPreviewItemContext context)
        {
            return new ItemData(string.Format("Preview Cell {0:00}", context.Index));
        }
    }
}
