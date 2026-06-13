/*
 * FancyScrollView Test Implementation
 */

using UnityEngine;
using System.Collections.Generic;

namespace ScrollViewTest
{
    public class EntryPoint : MonoBehaviour
    {
        [SerializeField] ScrollView scrollView = default;
        [SerializeField] int numberOfItems = 30;

        void Start()
        {
            if (scrollView == null)
            {
                scrollView = FindAnyObjectByType<ScrollView>();
            }

            if (scrollView != null)
            {
                var items = new List<ItemData>();
                for (int i = 0; i < numberOfItems; i++)
                {
                    items.Add(new ItemData($"Item {i:D2}"));
                }
                scrollView.SetItems(items);
            }
            else
            {
                Debug.LogError("ScrollView is not assigned or found in the scene.");
            }
        }
    }
}
