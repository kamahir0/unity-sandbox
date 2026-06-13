/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System.Collections.Generic;
using UnityEngine;

namespace Lilja.FancyScrollView.Example01
{
    class ScrollView : FancyScrollView<ItemData>
    {
        [SerializeField] Cell cellPrefab = default;
        [SerializeField] Scroller scroller = default;

        protected override FancyCell<ItemData, NullContext> CellPrefab => cellPrefab;

        protected override void Initialize()
        {
            base.Initialize();
            scroller.OnValueChanged(UpdatePosition);
        }

        public override void SetItems(IList<ItemData> items)
        {
            base.SetItems(items);
            scroller.SetTotalCount(items.Count);
        }

        protected override ItemData CreatePreviewItem(FancyScrollPreviewItemContext context)
        {
            return new ItemData(string.Format("Preview Cell {0:00}", context.Index));
        }
    }
}
