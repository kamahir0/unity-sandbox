/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using UnityEngine;

namespace Lilja.FancyScrollView.Example10
{
    class ItemData
    {
        public int Index { get; }
        public Color Color { get; }

        public ItemData(int index, Color color)
        {
            Index = index;
            Color = color;
        }
    }
}
