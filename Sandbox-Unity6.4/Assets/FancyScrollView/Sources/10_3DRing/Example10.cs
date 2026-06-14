/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System.Linq;
using UnityEngine;

namespace Lilja.FancyScrollView.Example10
{
    class Example10 : MonoBehaviour
    {
        [SerializeField] ScrollView scrollView = default;

        void Start()
        {
            var items = Enumerable.Range(0, 36)
                .Select(ScrollView.CreateItem)
                .ToArray();

            scrollView.UpdateData(items);
        }
    }
}
