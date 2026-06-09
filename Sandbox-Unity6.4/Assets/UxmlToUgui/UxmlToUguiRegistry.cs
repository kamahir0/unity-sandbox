#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UxmlToUgui
{
    /// <summary>
    /// UxmlToUgui で生成する UGUI コンポーネント型の上書き設定を管理するレジストリクラス。
    /// </summary>
    public static class UxmlToUguiRegistry
    {
        private static readonly Dictionary<Type, Type> TypeMap = new Dictionary<Type, Type>();

        /// <summary>
        /// 指定した標準のコンポーネント型（TSource）を、カスタムコンポーネント型（TTarget）へ上書き登録します。
        /// </summary>
        /// <typeparam name="TSource">Unity標準のUIコンポーネント（Button, TextMeshProUGUI 等）</typeparam>
        /// <typeparam name="TTarget">TSource を継承したカスタムUIコンポーネント</typeparam>
        public static void OverrideComponent<TSource, TTarget>() 
            where TSource : Component 
            where TTarget : TSource
        {
            TypeMap[typeof(TSource)] = typeof(TTarget);
        }

        /// <summary>
        /// GameObject に登録された上書き型（または標準型 T）のコンポーネントを追加して返します。
        /// </summary>
        public static T AddComponent<T>(GameObject go) where T : Component
        {
            var sourceType = typeof(T);
            if (TypeMap.TryGetValue(sourceType, out var targetType))
            {
                return (T)go.AddComponent(targetType);
            }
            return go.AddComponent<T>();
        }
    }
}
#endif
