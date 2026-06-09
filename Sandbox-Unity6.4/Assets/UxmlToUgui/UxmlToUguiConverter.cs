// UxmlToUguiConverter.cs
// Editorフォルダ配下に配置すること
// 依存: TextMeshPro パッケージ

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UxmlToUgui;

/// <summary>
/// UXML → UGUI Prefab 決定論的変換ライブラリ。
/// 同一入力に対して常に同一出力を保証する。
/// </summary>
public static class UxmlToUguiConverter
{
    // ────────────────────────────────────────────────────────────
    //  公開 API
    // ────────────────────────────────────────────────────────────

    public class UxmlConvertResult
    {
        public int NodeCount;
        public int WarnCount;
        public int TodoCount;
    }

    /// <summary>
    /// UXML ファイルを読み込み UGUI Prefab を生成する。
    /// </summary>
    /// <param name="uxmlPath">プロジェクト相対パス (例: "Assets/UI/Hoge.uxml")</param>
    /// <param name="outputPath">出力 Prefab パス (例: "Assets/Prefabs/HogeUgui.prefab")</param>
    /// <param name="fontScale">フォントサイズの拡大倍率</param>
    public static UxmlConvertResult Convert(string uxmlPath, string outputPath, float fontScale = 1.0f)
    {
        var ctx = new ConvertContext { FontScale = fontScale };

        // ── 1. UXML 読み込み ──────────────────────────────────
        string fullPath = Path.GetFullPath(uxmlPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[UxmlConverter] ファイルが見つかりません: {uxmlPath}");
            return null;
        }

        XmlDocument xmlDoc = new XmlDocument();
        try { xmlDoc.Load(fullPath); }
        catch (Exception e)
        {
            Debug.LogError($"[UxmlConverter] XML パース失敗: {e.Message}");
            return null;
        }

        // ── 2. USS 収集 ──────────────────────────────────────
        var ussRules = CollectUssRules(xmlDoc, uxmlPath);

        // ── 3. UxmlNode ツリー構築 ────────────────────────────
        XmlElement root = xmlDoc.DocumentElement;
        // <UXML> ラッパーを透過する
        XmlElement contentRoot = root.LocalName == "UXML" ? GetFirstChildElement(root) : root;
        if (contentRoot == null)
        {
            Debug.LogError("[UxmlConverter] 変換対象の要素が見つかりません");
            return null;
        }

        UxmlNode rootNode = ParseNode(contentRoot, ussRules, ctx, depth: 0);
        if (rootNode == null) return null;

        // ── 4. GameObject ツリー構築 ──────────────────────────
        var goRoot = new GameObject("UIRoot", typeof(RectTransform));
        StretchFill(goRoot);

        // UIRoot は Canvas 下で 0,0,0,0 にストレッチするよう sizeDelta を 0 にする
        var rootRt = goRoot.GetComponent<RectTransform>();
        rootRt.sizeDelta = Vector2.zero;

        BuildLayerL(goRoot, rootNode,
            parentIsRow:  false,
            parentWidth:  1920f,
            parentHeight: 1080f,
            ctx, depth: 0);

        // ── 5. Prefab 保存 ────────────────────────────────────
        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        PrefabUtility.SaveAsPrefabAsset(goRoot, outputPath);
        GameObject.DestroyImmediate(goRoot);

        AssetDatabase.ImportAsset(outputPath);
        AssetDatabase.Refresh();

        // ── 6. サマリーログ ───────────────────────────────────
        Debug.Log(
            $"[UxmlConverter] 完了: {outputPath}\n" +
            $"  変換ノード数: {ctx.NodeCount}\n" +
            $"  警告数: {ctx.WarnCount}\n" +
            $"  TODOオブジェクト数: {ctx.TodoCount}\n" +
            $"  詳細は Console ウィンドウを確認してください"
        );

        return new UxmlConvertResult
        {
            NodeCount = ctx.NodeCount,
            WarnCount = ctx.WarnCount,
            TodoCount = ctx.TodoCount
        };
    }

    // ────────────────────────────────────────────────────────────
    //  内部定数
    // ────────────────────────────────────────────────────────────

    private const int MaxDepth = 50;

    // ────────────────────────────────────────────────────────────
    //  内部データ型
    // ────────────────────────────────────────────────────────────

    private enum NodeKind { Layout, Leaf, AbsoluteLeaf }

    private enum FlexDir { Row, Column }

    private enum AlignVal { Stretch, Center, FlexStart, FlexEnd, Auto }

    private enum JustifyVal { FlexStart, Center, FlexEnd, SpaceBetween, SpaceAround }

    private class EdgeValues
    {
        public float Top, Right, Bottom, Left;
    }

    private class ResolvedStyle
    {
        public FlexDir FlexDirection = FlexDir.Column;
        public float FlexGrow        = 0f;
        public float? Width, Height, MinWidth, MinHeight, MaxWidth, MaxHeight;
        public AlignVal AlignItems   = AlignVal.Stretch;
        public AlignVal AlignSelf    = AlignVal.Auto;
        public JustifyVal Justify    = JustifyVal.FlexStart;
        public EdgeValues Padding    = new EdgeValues();
        public EdgeValues Margin     = new EdgeValues();
        public bool IsAbsolute       = false;
        public float? Left, Top, Right, Bottom;
        public bool DisplayNone      = false;
        public bool FlexWrap         = false;
        public float? FontSize;
        public Color? BackgroundColor;
        public Color? Color;
    }

    private class UxmlNode
    {
        public string TagName;
        public string Name;
        public ResolvedStyle Style;
        public string TextContent;
        public List<UxmlNode> Children = new List<UxmlNode>();
        public NodeKind Kind;
    }

    private class ConvertContext
    {
        public int NodeCount;
        public int WarnCount;
        public int TodoCount;
        public float FontScale = 1.0f;
        // 同階層内での名前重複管理: parentPath → (name → count)
        public Dictionary<string, Dictionary<string, int>> NameCounters
            = new Dictionary<string, Dictionary<string, int>>();
    }

    // ────────────────────────────────────────────────────────────
    //  USS 解析
    // ────────────────────────────────────────────────────────────

    /// <summary>クラスセレクタ → プロパティ辞書 のマップを返す</summary>
    private static Dictionary<string, Dictionary<string, string>> CollectUssRules(
        XmlDocument xmlDoc, string uxmlPath)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();

        // <Style src="*.uss"> を収集
        foreach (XmlNode node in xmlDoc.GetElementsByTagName("Style"))
        {
            var src = (node as XmlElement)?.GetAttribute("src");
            if (string.IsNullOrEmpty(src)) continue;

            string ussPath = Path.Combine(Path.GetDirectoryName(uxmlPath) ?? "", src);
            if (!File.Exists(ussPath))
            {
                // Assets/Resources も試みる
                ussPath = Path.Combine(Application.dataPath, "Resources", src);
            }
            if (!File.Exists(ussPath)) continue;

            ParseUssFile(File.ReadAllText(ussPath), result);
        }
        return result;
    }

    private static void ParseUssFile(
        string ussText,
        Dictionary<string, Dictionary<string, string>> rules)
    {
        // 簡易パーサー: .class, #id, tag { prop: value; ... }
        var ruleRx = new Regex(@"([\.#]?[a-zA-Z0-9_-]+)\s*\{([^}]*)\}", RegexOptions.Singleline);
        foreach (Match m in ruleRx.Matches(ussText))
        {
            string selector = m.Groups[1].Value.Trim().ToLower();
            string body = m.Groups[2].Value;
            var props = ParseStyleBody(body);
            if (!rules.ContainsKey(selector))
                rules[selector] = new Dictionary<string, string>();
            foreach (var kv in props)
                rules[selector][kv.Key] = kv.Value;
        }
    }

    // ────────────────────────────────────────────────────────────
    //  UXML ノード パース
    // ────────────────────────────────────────────────────────────

    private static UxmlNode ParseNode(
        XmlElement el,
        Dictionary<string, Dictionary<string, string>> ussRules,
        ConvertContext ctx,
        int depth)
    {
        if (depth > MaxDepth)
        {
            Debug.LogError("[UxmlConverter] ネスト深度上限(50)を超えました。処理を中断します。");
            return null;
        }

        string tag = el.LocalName;

        // スタイル解決
        var style = ResolveStyle(el, ussRules);

        if (style.DisplayNone)
        {
            Debug.Log($"[UxmlConverter] display:none のためスキップ: <{tag} name=\"{el.GetAttribute("name")}\">");
            return null;
        }

        // 子要素を再帰パース（absolute を含む全子）
        var children = new List<UxmlNode>();
        foreach (XmlNode child in el.ChildNodes)
        {
            if (child is XmlElement childEl)
            {
                var childNode = ParseNode(childEl, ussRules, ctx, depth + 1);
                if (childNode != null) children.Add(childNode);
            }
        }

        // Kind 決定
        NodeKind kind;
        if (style.IsAbsolute)
            kind = NodeKind.AbsoluteLeaf;
        else if (children.Count > 0)
            kind = NodeKind.Layout;
        else
            kind = NodeKind.Leaf;

        string rawName = el.GetAttribute("name");
        if (string.IsNullOrEmpty(rawName)) rawName = tag;

        string textContent = el.GetAttribute("text");

        ctx.NodeCount++;

        return new UxmlNode
        {
            TagName     = tag,
            Name        = rawName,
            Style       = style,
            TextContent = string.IsNullOrEmpty(textContent) ? null : textContent,
            Children    = children,
            Kind        = kind
        };
    }

    // ────────────────────────────────────────────────────────────
    //  スタイル解決
    // ────────────────────────────────────────────────────────────

    private static ResolvedStyle ResolveStyle(
        XmlElement el,
        Dictionary<string, Dictionary<string, string>> ussRules)
    {
        var props = new Dictionary<string, string>();
        string tag = el.LocalName.ToLower();

        // 1. 要素名 (Tag) セレクタ (優先度: 低)
        if (ussRules.TryGetValue(tag, out var tagProps))
        {
            foreach (var kv in tagProps) props[kv.Key] = kv.Value;
        }

        // 2. USS クラスセレクタ (優先度: 中)
        string classAttr = el.GetAttribute("class");
        if (!string.IsNullOrEmpty(classAttr))
        {
            foreach (string cls in classAttr.Split(' '))
            {
                string trimmed = cls.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (ussRules.TryGetValue("." + trimmed.ToLower(), out var clsProps))
                {
                    foreach (var kv in clsProps) props[kv.Key] = kv.Value;
                }
            }
        }

        // 3. ID セレクタ (優先度: 高)
        string idAttr = el.GetAttribute("name");
        if (!string.IsNullOrEmpty(idAttr))
        {
            if (ussRules.TryGetValue("#" + idAttr.ToLower(), out var idProps))
            {
                foreach (var kv in idProps) props[kv.Key] = kv.Value;
            }
        }

        // 4. インラインスタイル (優先度: 最優先)
        string inlineStyle = el.GetAttribute("style");
        if (!string.IsNullOrEmpty(inlineStyle))
        {
            foreach (var kv in ParseStyleBody(inlineStyle))
                props[kv.Key] = kv.Value;
        }

        return BuildResolvedStyle(props);
    }

    private static Dictionary<string, string> ParseStyleBody(string body)
    {
        var result = new Dictionary<string, string>();
        foreach (string decl in body.Split(';'))
        {
            int colon = decl.IndexOf(':');
            if (colon < 0) continue;
            string key = decl.Substring(0, colon).Trim().ToLower();
            string val = decl.Substring(colon + 1).Trim().ToLower();
            if (!string.IsNullOrEmpty(key)) result[key] = val;
        }
        return result;
    }

    private static ResolvedStyle BuildResolvedStyle(Dictionary<string, string> props)
    {
        var s = new ResolvedStyle();

        if (props.TryGetValue("flex-direction", out string fd))
            s.FlexDirection = fd.Contains("row") ? FlexDir.Row : FlexDir.Column;

        if (props.TryGetValue("flex-grow", out string fg))
            float.TryParse(fg, out s.FlexGrow);

        s.Width     = GetSize(props, "width");
        s.Height    = GetSize(props, "height");
        s.MinWidth  = GetSize(props, "min-width");
        s.MinHeight = GetSize(props, "min-height");
        s.MaxWidth  = GetSize(props, "max-width");
        s.MaxHeight = GetSize(props, "max-height");

        if (props.TryGetValue("align-items", out string ai))
            s.AlignItems = ParseAlign(ai);
        if (props.TryGetValue("align-self", out string as_))
            s.AlignSelf = ParseAlign(as_);
        if (props.TryGetValue("justify-content", out string jc))
            s.Justify = ParseJustify(jc);

        s.Padding = ParseEdge(props, "padding");
        s.Margin  = ParseEdge(props, "margin");

        if (props.TryGetValue("position", out string pos))
            s.IsAbsolute = pos.Contains("absolute");

        s.Left   = GetSize(props, "left");
        s.Top    = GetSize(props, "top");
        s.Right  = GetSize(props, "right");
        s.Bottom = GetSize(props, "bottom");

        if (props.TryGetValue("display", out string disp))
            s.DisplayNone = disp == "none";

        if (props.TryGetValue("flex-wrap", out string fw))
            s.FlexWrap = fw.Contains("wrap");

        if (props.TryGetValue("font-size", out string fs))
            s.FontSize = ParseSize(fs);

        if (props.TryGetValue("background-color", out string bgCol))
            s.BackgroundColor = ParseColor(bgCol);

        if (props.TryGetValue("color", out string col))
            s.Color = ParseColor(col);

        return s;
    }

    private static Color? ParseColor(string css)
    {
        if (string.IsNullOrEmpty(css)) return null;
        css = css.Trim().ToLower();

        if (css == "transparent" || css == "none" || css == "initial")
            return new Color(0f, 0f, 0f, 0f);

        if (css.StartsWith("#") || !css.Contains("("))
        {
            if (ColorUtility.TryParseHtmlString(css, out Color color))
                return color;
        }

        if (css.StartsWith("rgb"))
        {
            var match = Regex.Match(css, @"rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d\.]+)\s*)?\)");
            if (match.Success)
            {
                float r = float.Parse(match.Groups[1].Value) / 255f;
                float g = float.Parse(match.Groups[2].Value) / 255f;
                float b = float.Parse(match.Groups[3].Value) / 255f;
                float a = 1f;
                if (match.Groups[4].Success && float.TryParse(match.Groups[4].Value, out float alpha))
                {
                    a = alpha;
                }
                return new Color(r, g, b, a);
            }
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────
    //  Layer-L 構築（再帰）
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// 兄弟ノード群のサイズを親サイズ・flex-grow重みから計算して返す。
    /// キー: UxmlNode, 値: (resolvedWidth, resolvedHeight)
    /// </summary>
    private static Dictionary<UxmlNode, Vector2> CalcChildSizes(
        IList<UxmlNode> children, float parentWidth, float parentHeight, bool parentIsRow)
    {
        var result = new Dictionary<UxmlNode, Vector2>();

        // パディング・マージンは子側のマージンを簡易考慮（ここでは無視して近似）
        float totalMain   = parentIsRow ? parentWidth  : parentHeight;
        float totalCross  = parentIsRow ? parentHeight : parentWidth;

        // ── Step1: 固定サイズ(preferred)の合計と flex-grow の合計を集計 ──
        float fixedMain   = 0f;
        float totalGrow   = 0f;
        foreach (var child in children)
        {
            if (child.Kind == NodeKind.AbsoluteLeaf) continue;
            var s = child.Style;
            float? mainSize = parentIsRow ? s.Width : s.Height;
            if (mainSize.HasValue)
                fixedMain += mainSize.Value;
            else
                totalGrow += Mathf.Max(s.FlexGrow, s.FlexGrow == 0f ? 1f : s.FlexGrow);
                // flex-grow 未指定(0)の子も等分で余白を受け取る前提で 1 として扱う
        }

        float remainMain = Mathf.Max(0f, totalMain - fixedMain);

        // flex-grow=0 かつ固定サイズ未指定の子の数（等分対象）
        int noGrowNoFixed = 0;
        foreach (var child in children)
        {
            if (child.Kind == NodeKind.AbsoluteLeaf) continue;
            var s = child.Style;
            float? mainSize = parentIsRow ? s.Width : s.Height;
            if (!mainSize.HasValue && s.FlexGrow == 0f) noGrowNoFixed++;
        }

        // ── Step2: 各子のサイズを確定 ────────────────────────────
        // 実 grow 合計 (0子は等分扱いのため noGrowNoFixed 個分を加算)
        float effectiveGrowTotal = 0f;
        foreach (var child in children)
        {
            if (child.Kind == NodeKind.AbsoluteLeaf) continue;
            var s = child.Style;
            float? mainSize = parentIsRow ? s.Width : s.Height;
            if (!mainSize.HasValue)
                effectiveGrowTotal += s.FlexGrow > 0f ? s.FlexGrow : 1f;
        }

        foreach (var child in children)
        {
            if (child.Kind == NodeKind.AbsoluteLeaf)
            {
                result[child] = new Vector2(child.Style.Width ?? 100f, child.Style.Height ?? 100f);
                continue;
            }
            var cs = child.Style;
            float? fixedW = cs.Width;
            float? fixedH = cs.Height;

            float resolvedMain;
            float resolvedCross;

            if (parentIsRow)
            {
                resolvedMain  = fixedW.HasValue
                    ? fixedW.Value
                    : (effectiveGrowTotal > 0f
                        ? remainMain * ((cs.FlexGrow > 0f ? cs.FlexGrow : 1f) / effectiveGrowTotal)
                        : remainMain);
                resolvedCross = fixedH ?? totalCross;
            }
            else
            {
                resolvedMain  = fixedH.HasValue
                    ? fixedH.Value
                    : (effectiveGrowTotal > 0f
                        ? remainMain * ((cs.FlexGrow > 0f ? cs.FlexGrow : 1f) / effectiveGrowTotal)
                        : remainMain);
                resolvedCross = fixedW ?? totalCross;
            }

            float resolvedW = parentIsRow ? resolvedMain : resolvedCross;
            float resolvedH = parentIsRow ? resolvedCross : resolvedMain;
            result[child] = new Vector2(resolvedW, resolvedH);
        }
        return result;
    }

    private static void BuildLayerL(
        GameObject parent,
        UxmlNode node,
        bool parentIsRow,
        float parentWidth,
        float parentHeight,
        ConvertContext ctx,
        int depth)
    {
        if (depth > MaxDepth) return;

        // ── このノード自身の Layer-L GO を作る ──────────────
        string uniqueName = GetUniqueName(parent.name, node.Name, ctx);
        var go = new GameObject($"[Layout] {uniqueName}", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);

        // 親が LayoutGroup を持たない（UIRoot や [Overlay]）かつ非 absolute の場合、
        // 0,0,0,0 ストレッチを適用してサイズ駆動漏れを防ぐ
        if (parent.GetComponent<LayoutGroup>() == null && !node.Style.IsAbsolute)
        {
            StretchFill(go);
        }

        // LayoutElement はLayoutGroup配下のノードには必ず付与する。
        // （AbsoluteLeaf は後段で ignoreLayout を設定する）
        var le = UxmlToUguiRegistry.AddComponent<LayoutElement>(go);
        ApplyLayoutElement(le, node.Style, parentIsRow, parentWidth, parentHeight, node.Name, ctx);

        // 背景色の適用（独自にグラフィックを持つ一部の Leaf ノードを除き、Layout 段階で適用）
        bool isGraphicLeaf = node.Kind == NodeKind.Leaf && 
            (node.TagName.ToLower() == "button" || 
             node.TagName.ToLower() == "textfield" || 
             node.TagName.ToLower() == "integerfield" || 
             node.TagName.ToLower() == "floatfield" || 
             node.TagName.ToLower() == "longfield" || 
             node.TagName.ToLower() == "image");

        if (node.Style.BackgroundColor.HasValue && !isGraphicLeaf)
        {
            var bgImg = UxmlToUguiRegistry.AddComponent<Image>(go);
            bgImg.color = node.Style.BackgroundColor.Value;
        }

        // ── AbsoluteLeaf は専用処理 ──────────────────────────
        if (node.Kind == NodeKind.AbsoluteLeaf)
        {
            ApplyAbsoluteRect(go, node.Style);
            le.ignoreLayout = true;
            AttachLayerG(go, node, ctx);
            return;
        }

        // ── Layout ノード ─────────────────────────────────────
        if (node.Kind == NodeKind.Layout)
        {
            // ScrollView はLayoutGroupではなくScrollRect固有構造に委譲
            if (node.TagName.ToLower() == "scrollview")
            {
                BuildScrollView(go, node, parentWidth, parentHeight, ctx, depth);
                return;
            }

            bool isRow = node.Style.FlexDirection == FlexDir.Row;

            // flex-wrap → GridLayoutGroup で近似
            if (node.Style.FlexWrap)
            {
                var grid = UxmlToUguiRegistry.AddComponent<GridLayoutGroup>(go);
                grid.constraint = GridLayoutGroup.Constraint.Flexible;
                grid.padding    = ConvertPadding(node.Style.Padding);
                
                // 子要素のサイズからセルサイズを自動算出
                Vector2 cellSize = new Vector2(100f, 100f);
                if (node.Children.Count > 0)
                {
                    var firstChild = node.Children[0];
                    float w = firstChild.Style.Width ?? firstChild.Style.MinWidth ?? 100f;
                    float h = firstChild.Style.Height ?? firstChild.Style.MinHeight ?? 100f;
                    cellSize = new Vector2(w, h);
                }
                grid.cellSize = cellSize;
            }
            else
            {
                HorizontalOrVerticalLayoutGroup group = isRow
                    ? (HorizontalOrVerticalLayoutGroup)UxmlToUguiRegistry.AddComponent<HorizontalLayoutGroup>(go)
                    : UxmlToUguiRegistry.AddComponent<VerticalLayoutGroup>(go);

                group.childForceExpandWidth  = false;
                group.childForceExpandHeight = false;
                group.childControlWidth      = true;
                group.childControlHeight     = true;
                group.padding                = ConvertPadding(node.Style.Padding);
                group.spacing                = 0f;

                ApplyAlignment(group, node.Style, isRow, node.Name, ctx, go);
                ApplyAlignItemsExpand(group, node.Style, isRow);
            }

            // absolute な子を先に分離
            var normalChildren   = new List<UxmlNode>();
            var absoluteChildren = new List<UxmlNode>();
            foreach (var child in node.Children)
            {
                if (child.Kind == NodeKind.AbsoluteLeaf) absoluteChildren.Add(child);
                else normalChildren.Add(child);
            }

            bool childIsRow = node.Style.FlexDirection == FlexDir.Row;

            // 子のサイズを親サイズ・flex-grow 重みから事前計算
            var childSizes = CalcChildSizes(normalChildren, parentWidth, parentHeight, childIsRow);

            // 通常フローの子を再帰
            foreach (var child in normalChildren)
            {
                var sz = childSizes.ContainsKey(child)
                    ? childSizes[child]
                    : new Vector2(parentWidth, parentHeight);
                BuildLayerL(go, child, childIsRow, sz.x, sz.y, ctx, depth + 1);
            }

            // absolute な子 → オーバーレイコンテナ経由
            if (absoluteChildren.Count > 0)
            {
                var overlayContainer = new GameObject(
                    $"[Overlay] {uniqueName}", typeof(RectTransform));
                overlayContainer.transform.SetParent(go.transform, false);
                StretchFill(overlayContainer);
                var oLe = UxmlToUguiRegistry.AddComponent<LayoutElement>(overlayContainer);
                oLe.ignoreLayout = true;

                foreach (var absChild in absoluteChildren)
                    BuildLayerL(overlayContainer, absChild, childIsRow,
                        parentWidth, parentHeight, ctx, depth + 1);
            }
        }
        else
        {
            // ── Leaf ノード ──────────────────────────────────
            AttachLayerG(go, node, ctx);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Layer-G 配置
    // ────────────────────────────────────────────────────────────

    private static void AttachLayerG(GameObject layerL, UxmlNode node, ConvertContext ctx)
    {
        string tag = node.TagName.ToLower();

        switch (tag)
        {
            case "label":
                BuildLabel(layerL, node, ctx);
                break;

            case "button":
                BuildButton(layerL, node, ctx);
                break;

            case "textfield":
            case "integerfield":
            case "floatfield":
            case "longfield":
                BuildInputField(layerL, node, ctx);
                break;

            case "toggle":
                BuildToggle(layerL, node, ctx);
                break;

            case "image":
                BuildImageLeaf(layerL, node, ctx);
                break;

            case "visualelement":
                // 子なし VisualElement → 半透明プレースホルダー
                BuildPlaceholder(layerL, node);
                break;

            default:
                ctx.WarnCount++;
                Debug.LogWarning(
                    $"[UxmlConverter] 未知のタグ <{node.TagName}> → プレースホルダーで代替します");
                BuildPlaceholder(layerL, node);
                break;
        }
    }

    // ─── Button ──────────────────────────────────────────────────

    private static void BuildButton(GameObject layerL, UxmlNode node, ConvertContext ctx)
    {
        var btnGo = new GameObject("Button", typeof(RectTransform));
        btnGo.transform.SetParent(layerL.transform, false);
        StretchFill(btnGo);

        var img = UxmlToUguiRegistry.AddComponent<Image>(btnGo);
        img.color = node.Style.BackgroundColor ?? new Color(1f, 1f, 1f, 0.1f);

        var btn = UxmlToUguiRegistry.AddComponent<Button>(btnGo);
        btn.targetGraphic = img;

        var textGo = new GameObject("Text (TMP)", typeof(RectTransform));
        textGo.transform.SetParent(btnGo.transform, false);
        StretchFill(textGo);

        var tmp = UxmlToUguiRegistry.AddComponent<TextMeshProUGUI>(textGo);
        tmp.text      = node.TextContent ?? "Button";
        tmp.fontSize  = (node.Style.FontSize ?? 14f) * ctx.FontScale;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = node.Style.Color ?? Color.white;
    }

    // ─── Label ───────────────────────────────────────────────────

    private static void BuildLabel(GameObject layerL, UxmlNode node, ConvertContext ctx)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(layerL.transform, false);
        StretchFill(go);

        var tmp = UxmlToUguiRegistry.AddComponent<TextMeshProUGUI>(go);
        tmp.text      = node.TextContent ?? node.Name;
        tmp.fontSize  = (node.Style.FontSize ?? 14f) * ctx.FontScale;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color     = node.Style.Color ?? Color.white;
    }

    // ─── InputField ──────────────────────────────────────────────

    private static void BuildInputField(GameObject layerL, UxmlNode node, ConvertContext ctx)
    {
        var fieldGo = new GameObject("InputField", typeof(RectTransform));
        fieldGo.transform.SetParent(layerL.transform, false);
        StretchFill(fieldGo);

        var bgImg = UxmlToUguiRegistry.AddComponent<Image>(fieldGo);
        bgImg.color = node.Style.BackgroundColor ?? new Color(0.15f, 0.15f, 0.15f, 1f);

        // TextArea (RectMask2D)
        var areaGo = new GameObject("Text Area", typeof(RectTransform));
        areaGo.transform.SetParent(fieldGo.transform, false);
        StretchFill(areaGo);
        UxmlToUguiRegistry.AddComponent<RectMask2D>(areaGo);

        // Placeholder
        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(areaGo.transform, false);
        StretchFill(phGo);
        var ph = UxmlToUguiRegistry.AddComponent<TextMeshProUGUI>(phGo);
        ph.text      = "Enter text...";
        ph.fontSize  = (node.Style.FontSize ?? 14f) * ctx.FontScale;
        ph.fontStyle = FontStyles.Italic;
        ph.color     = new Color(1f, 1f, 1f, 0.4f);

        // Input Text
        var inputTextGo = new GameObject("Text", typeof(RectTransform));
        inputTextGo.transform.SetParent(areaGo.transform, false);
        StretchFill(inputTextGo);
        var inputTmp = UxmlToUguiRegistry.AddComponent<TextMeshProUGUI>(inputTextGo);
        inputTmp.fontSize = (node.Style.FontSize ?? 14f) * ctx.FontScale;
        inputTmp.color    = node.Style.Color ?? Color.white;

        // TMP_InputField
        var inputField = UxmlToUguiRegistry.AddComponent<TMP_InputField>(fieldGo);
        inputField.targetGraphic  = bgImg;
        inputField.textViewport   = areaGo.GetComponent<RectTransform>();
        inputField.textComponent  = inputTmp;
        inputField.placeholder     = ph;
        inputField.text            = node.TextContent ?? "";
    }

    // ─── Toggle ──────────────────────────────────────────────────

    private static void BuildToggle(GameObject layerL, UxmlNode node, ConvertContext ctx)
    {
        var toggleGo = new GameObject("Toggle", typeof(RectTransform));
        toggleGo.transform.SetParent(layerL.transform, false);
        StretchFill(toggleGo);

        // Background
        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(toggleGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin        = new Vector2(0f, 0.5f);
        bgRt.anchorMax        = new Vector2(0f, 0.5f);
        bgRt.pivot            = new Vector2(0f, 0.5f);
        bgRt.anchoredPosition = new Vector2(5f, 0f);
        bgRt.sizeDelta        = new Vector2(20f, 20f);
        var bgImg = UxmlToUguiRegistry.AddComponent<Image>(bgGo);
        bgImg.color = node.Style.BackgroundColor ?? new Color(0.3f, 0.3f, 0.3f, 1f);

        // Checkmark
        var ckGo = new GameObject("Checkmark", typeof(RectTransform));
        ckGo.transform.SetParent(bgGo.transform, false);
        StretchFill(ckGo);
        var ckImg = UxmlToUguiRegistry.AddComponent<Image>(ckGo);
        ckImg.color = node.Style.Color ?? Color.white;

        // Label
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(toggleGo.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin        = new Vector2(0f, 0f);
        labelRt.anchorMax        = new Vector2(1f, 1f);
        labelRt.offsetMin        = new Vector2(30f, 0f);
        labelRt.offsetMax        = Vector2.zero;
        var labelTmp = UxmlToUguiRegistry.AddComponent<TextMeshProUGUI>(labelGo);
        labelTmp.text      = node.TextContent ?? "Toggle";
        labelTmp.fontSize  = (node.Style.FontSize ?? 14f) * ctx.FontScale;
        labelTmp.color     = node.Style.Color ?? Color.white;

        // Toggle コンポーネント
        var toggle = UxmlToUguiRegistry.AddComponent<Toggle>(toggleGo);
        toggle.targetGraphic = bgImg;
        toggle.graphic       = ckImg;
        toggle.isOn          = false;
    }

    // ─── ScrollView ──────────────────────────────────────────────
    // ScrollView は「コンテナ型Layer-G」として扱う。
    // LayerL(go) の直下に ScrollRect 固有の構造を構築し、
    // UXMLの子ノードは Content 配下の Layer-L ツリーとして再帰する。

    private static void BuildScrollView(
        GameObject layerL, UxmlNode node,
        float parentWidth, float parentHeight,
        ConvertContext ctx, int depth)
    {
        // ── Scroll View GO（ScrollRect本体） ─────────────────
        // LayerL に直接 ScrollRect を乗せるのではなく、
        // 慣例通り "Scroll View" という名の子 GO に乗せる。
        // これにより LayerL が LayoutGroup に支配される層として機能し続ける。
        var svGo = new GameObject("Scroll View", typeof(RectTransform));
        svGo.transform.SetParent(layerL.transform, false);
        StretchFill(svGo);

        var bgImg = UxmlToUguiRegistry.AddComponent<Image>(svGo);
        bgImg.color = new Color(0f, 0f, 0f, 0f); // 透明（手調整前提）

        // ── Viewport ─────────────────────────────────────────
        var vpGo = new GameObject("Viewport", typeof(RectTransform));
        vpGo.transform.SetParent(svGo.transform, false);
        StretchFill(vpGo);
        UxmlToUguiRegistry.AddComponent<RectMask2D>(vpGo);

        // ── Content ──────────────────────────────────────────
        // UXML の flex-direction に合わせて LayoutGroup を決定する。
        // ContentSizeFitter で Content がスクロール方向に伸びるようにする。
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(vpGo.transform, false);
        var contentRt = contentGo.GetComponent<RectTransform>();

        bool scrollIsRow = node.Style.FlexDirection == FlexDir.Row;
        if (scrollIsRow)
        {
            // 横スクロール: Content を左上基準、横方向に伸ばす
            contentRt.anchorMin        = new Vector2(0f, 0f);
            contentRt.anchorMax        = new Vector2(0f, 1f);
            contentRt.pivot            = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta        = Vector2.zero;

            var hlg = UxmlToUguiRegistry.AddComponent<HorizontalLayoutGroup>(contentGo);
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.padding                = ConvertPadding(node.Style.Padding);

            var csf = UxmlToUguiRegistry.AddComponent<ContentSizeFitter>(contentGo);
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        else
        {
            // 縦スクロール（デフォルト）: Content を左上基準、縦方向に伸ばす
            contentRt.anchorMin        = new Vector2(0f, 1f);
            contentRt.anchorMax        = new Vector2(1f, 1f);
            contentRt.pivot            = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta        = Vector2.zero;

            var vlg = UxmlToUguiRegistry.AddComponent<VerticalLayoutGroup>(contentGo);
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.padding                = ConvertPadding(node.Style.Padding);

            var csf = UxmlToUguiRegistry.AddComponent<ContentSizeFitter>(contentGo);
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ── ScrollRect 設定 ───────────────────────────────────
        var sr = UxmlToUguiRegistry.AddComponent<ScrollRect>(svGo);
        sr.viewport   = vpGo.GetComponent<RectTransform>();
        sr.content    = contentRt;
        sr.horizontal = scrollIsRow;
        sr.vertical   = !scrollIsRow;

        // ── UXML子ノードを Content 配下に再帰 ────────────────
        // Content が持つ LayoutGroup（HLG or VLG）が子を支配する。
        // 子ノードはここから再び通常の Layer-L ツリーとして展開される。
        // Content 自体のサイズは ContentSizeFitter で動的に決まるため、
        // 子への parentSize は ScrollView の親サイズをそのまま引き継ぐ。
        var childSizes = CalcChildSizes(node.Children, parentWidth, parentHeight, scrollIsRow);
        foreach (var child in node.Children)
        {
            var sz = childSizes.ContainsKey(child)
                ? childSizes[child]
                : new Vector2(parentWidth, parentHeight);
            BuildLayerL(contentGo, child, scrollIsRow, sz.x, sz.y, ctx, depth + 1);
        }
    }

    // ─── Image Leaf ──────────────────────────────────────────────

    private static void BuildImageLeaf(GameObject layerL, UxmlNode node, ConvertContext ctx)
    {
        var go = new GameObject("Image", typeof(RectTransform));
        go.transform.SetParent(layerL.transform, false);
        StretchFill(go);
        var img = UxmlToUguiRegistry.AddComponent<Image>(go);
        img.color = node.Style.BackgroundColor ?? node.Style.Color ?? Color.white;
    }

    // ─── Placeholder ─────────────────────────────────────────────

    private static void BuildPlaceholder(GameObject layerL, UxmlNode node)
    {
        if (node.Style.BackgroundColor.HasValue) return;

        var go = new GameObject("Placeholder", typeof(RectTransform));
        go.transform.SetParent(layerL.transform, false);
        StretchFill(go);
        var img = UxmlToUguiRegistry.AddComponent<Image>(go);
        img.color = new Color(0.8f, 0.8f, 0.8f, 0.05f);
    }

    // ────────────────────────────────────────────────────────────
    //  LayoutElement 付与
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// LayoutGroup 配下のノードに必ず付与される LayoutElement を設定する。
    /// preferred 値は UXML の固定指定を優先し、なければ親サイズと flex-grow から
    /// 計算済みの parentWidth/parentHeight（呼び出し元で CalcChildSizes 済み）を使う。
    /// </summary>
    private static void ApplyLayoutElement(
        LayoutElement le,
        ResolvedStyle s,
        bool parentIsRow,
        float parentWidth,
        float parentHeight,
        string nodeName,
        ConvertContext ctx)
    {
        // ── preferredWidth ────────────────────────────────────
        // UXML に width 固定値があればそれを優先。なければ計算済み parentWidth を使う。
        le.preferredWidth  = s.Width  ?? parentWidth;
        le.preferredHeight = s.Height ?? parentHeight;

        // ── minWidth / minHeight ──────────────────────────────
        if (s.MinWidth.HasValue)  le.minWidth  = s.MinWidth.Value;
        if (s.MinHeight.HasValue) le.minHeight = s.MinHeight.Value;

        // ── maxWidth / maxHeight（UGUI未対応）────────────────
        if (s.MaxWidth.HasValue)
        {
            ctx.WarnCount++;
            Debug.LogWarning(
                $"[UxmlConverter] {nodeName}: max-width={s.MaxWidth} はUGUI未対応のため無視します");
        }
        if (s.MaxHeight.HasValue)
        {
            ctx.WarnCount++;
            Debug.LogWarning(
                $"[UxmlConverter] {nodeName}: max-height={s.MaxHeight} はUGUI未対応のため無視します");
        }

        // ── flexibleWidth / flexibleHeight ────────────────────
        // flex-grow が指定されているノードは flexible も設定する。
        // flexible は重みとして機能し、LayoutGroup が余白を比率配分する。
        if (s.FlexGrow > 0f)
        {
            if (parentIsRow) le.flexibleWidth  = s.FlexGrow;
            else             le.flexibleHeight = s.FlexGrow;
        }
    }

    // ────────────────────────────────────────────────────────────
    //  LayoutGroup alignment・Expand 設定
    // ────────────────────────────────────────────────────────────

    private static void ApplyAlignment(
        HorizontalOrVerticalLayoutGroup group,
        ResolvedStyle s, bool isRow,
        string nodeName, ConvertContext ctx,
        GameObject go)
    {
        // JustifyContent → childAlignment の主軸方向
        bool spacingUnsupported =
            s.Justify == JustifyVal.SpaceBetween || s.Justify == JustifyVal.SpaceAround;

        if (spacingUnsupported)
        {
            ctx.WarnCount++;
            ctx.TodoCount++;
            Debug.LogWarning(
                $"[UxmlConverter] {nodeName}: justify-content: {s.Justify} は未対応です。" +
                $"FlexStart で近似し [TODO] オブジェクトを挿入します。");
            var todo = new GameObject(
                $"[TODO] justify-content:{s.Justify} 未対応 (手動調整が必要)", typeof(RectTransform));
            todo.transform.SetParent(go.transform, false);
            s.Justify = JustifyVal.FlexStart;
        }

        // 主軸 (main) × 交差軸 (cross) → TextAnchor
        group.childAlignment = ResolveTextAnchor(s.Justify, s.AlignItems, isRow);
    }

    private static void ApplyAlignItemsExpand(
        HorizontalOrVerticalLayoutGroup group, ResolvedStyle s, bool isRow)
    {
        // 交差軸方向: AlignItems=Stretch のとき forceExpand=true
        // 主軸方向:   常に false（LayoutElement の preferred/flexible で制御する）
        if (isRow)
        {
            group.childForceExpandWidth  = false;  // 主軸=横 → LEで制御
            group.childForceExpandHeight = s.AlignItems == AlignVal.Stretch
                                        || s.AlignItems == AlignVal.Auto; // デフォルトは Stretch 扱い
        }
        else
        {
            group.childForceExpandWidth  = s.AlignItems == AlignVal.Stretch
                                        || s.AlignItems == AlignVal.Auto; // デフォルトは Stretch 扱い
            group.childForceExpandHeight = false;  // 主軸=縦 → LEで制御
        }
    }

    private static TextAnchor ResolveTextAnchor(
        JustifyVal justify, AlignVal align, bool isRow)
    {
        // main軸 (Row=横, Column=縦) × cross軸 の組み合わせ
        int main  = JustifyToInt(justify);   // 0=start, 1=center, 2=end
        int cross = AlignToInt(align);       // 0=start, 1=center, 2=end

        if (isRow)
        {
            // main=横, cross=縦
            return (TextAnchor)(cross * 3 + main);
            // UpperLeft=0, UpperCenter=1, UpperRight=2
            // MiddleLeft=3, MiddleCenter=4, MiddleRight=5
            // LowerLeft=6,  LowerCenter=7,  LowerRight=8
        }
        else
        {
            // main=縦, cross=横
            return (TextAnchor)(main * 3 + cross);
        }
    }

    private static int JustifyToInt(JustifyVal j) =>
        j == JustifyVal.Center  ? 1 :
        j == JustifyVal.FlexEnd ? 2 : 0;

    private static int AlignToInt(AlignVal a) =>
        a == AlignVal.Center  ? 1 :
        a == AlignVal.FlexEnd ? 2 : 0;

    // ────────────────────────────────────────────────────────────
    //  Absolute 座標変換
    // ────────────────────────────────────────────────────────────

    private static void ApplyAbsoluteRect(GameObject go, ResolvedStyle s)
    {
        var rt = go.GetComponent<RectTransform>();
        bool hasLeft  = s.Left.HasValue;
        bool hasRight = s.Right.HasValue;
        bool hasTop   = s.Top.HasValue;
        bool hasBot   = s.Bottom.HasValue;

        if (hasLeft && hasRight && hasTop && hasBot)
        {
            // 全ストレッチ
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = new Vector2(s.Left.Value, s.Bottom.Value);
            rt.offsetMax  = new Vector2(-s.Right.Value, -s.Top.Value);
        }
        else if (hasLeft && hasRight)
        {
            // 横ストレッチ
            rt.anchorMin  = new Vector2(0f, 1f);
            rt.anchorMax  = new Vector2(1f, 1f);
            rt.pivot      = new Vector2(0.5f, 1f);
            rt.offsetMin  = new Vector2(s.Left.Value, -(s.Top ?? 0f) - (s.Height ?? 100f));
            rt.offsetMax  = new Vector2(-s.Right.Value, -(s.Top ?? 0f));
        }
        else if (hasTop && hasBot)
        {
            // 縦ストレッチ
            rt.anchorMin  = new Vector2(0f, 0f);
            rt.anchorMax  = new Vector2(0f, 1f);
            rt.pivot      = new Vector2(0f, 0.5f);
            rt.offsetMin  = new Vector2(s.Left ?? 0f, s.Bottom.Value);
            rt.offsetMax  = new Vector2((s.Left ?? 0f) + (s.Width ?? 100f), -s.Top.Value);
        }
        else if (hasRight)
        {
            // 右基準
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-s.Right.Value, -(s.Top ?? 0f));
            rt.sizeDelta        = new Vector2(s.Width ?? 100f, s.Height ?? 100f);
        }
        else
        {
            // 左基準（デフォルト）
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(0f, 1f);
            rt.pivot            = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(s.Left ?? 0f, -(s.Top ?? 0f));
            rt.sizeDelta        = new Vector2(s.Width ?? 100f, s.Height ?? 100f);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  ヘルパー
    // ────────────────────────────────────────────────────────────

    private static void StretchFill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }

    private static RectOffset ConvertPadding(EdgeValues e)
        => new RectOffset(
            Mathf.RoundToInt(e.Left),
            Mathf.RoundToInt(e.Right),
            Mathf.RoundToInt(e.Top),
            Mathf.RoundToInt(e.Bottom));

    private static float? GetSize(Dictionary<string, string> props, string key)
    {
        if (!props.TryGetValue(key, out string val)) return null;
        return ParseSize(val);
    }

    private static float? ParseSize(string css)
    {
        if (string.IsNullOrEmpty(css) || css == "auto" || css.EndsWith("%")) return null;
        string num = css.Replace("px", "").Trim();
        return float.TryParse(num, out float f) ? f : (float?)null;
    }

    private static AlignVal ParseAlign(string v)
    {
        if (v.Contains("center"))     return AlignVal.Center;
        if (v.Contains("flex-end"))   return AlignVal.FlexEnd;
        if (v.Contains("flex-start")) return AlignVal.FlexStart;
        if (v.Contains("stretch"))    return AlignVal.Stretch;
        return AlignVal.Auto;
    }

    private static JustifyVal ParseJustify(string v)
    {
        if (v.Contains("center"))       return JustifyVal.Center;
        if (v.Contains("flex-end"))     return JustifyVal.FlexEnd;
        if (v.Contains("space-between")) return JustifyVal.SpaceBetween;
        if (v.Contains("space-around")) return JustifyVal.SpaceAround;
        return JustifyVal.FlexStart;
    }

    private static EdgeValues ParseEdge(Dictionary<string, string> props, string prefix)
    {
        var e = new EdgeValues();
        // 4値ショートハンド
        if (props.TryGetValue(prefix, out string shorthand))
        {
            var parts = shorthand.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                float v = ParseSize(parts[0]) ?? 0f;
                e.Top = e.Right = e.Bottom = e.Left = v;
            }
            else if (parts.Length == 2)
            {
                float v = ParseSize(parts[0]) ?? 0f;
                float h = ParseSize(parts[1]) ?? 0f;
                e.Top = e.Bottom = v; e.Left = e.Right = h;
            }
            else if (parts.Length == 4)
            {
                e.Top    = ParseSize(parts[0]) ?? 0f;
                e.Right  = ParseSize(parts[1]) ?? 0f;
                e.Bottom = ParseSize(parts[2]) ?? 0f;
                e.Left   = ParseSize(parts[3]) ?? 0f;
            }
        }
        // 個別指定で上書き
        if (props.TryGetValue(prefix + "-top",    out string t)) e.Top    = ParseSize(t) ?? e.Top;
        if (props.TryGetValue(prefix + "-right",  out string r)) e.Right  = ParseSize(r) ?? e.Right;
        if (props.TryGetValue(prefix + "-bottom", out string b)) e.Bottom = ParseSize(b) ?? e.Bottom;
        if (props.TryGetValue(prefix + "-left",   out string l)) e.Left   = ParseSize(l) ?? e.Left;
        return e;
    }

    private static string GetUniqueName(string parentPath, string name, ConvertContext ctx)
    {
        string key = parentPath;
        if (!ctx.NameCounters.ContainsKey(key))
            ctx.NameCounters[key] = new Dictionary<string, int>();

        var counter = ctx.NameCounters[key];
        if (!counter.ContainsKey(name))
        {
            counter[name] = 0;
            return name;
        }
        counter[name]++;
        return $"{name}_{counter[name]}";
    }

    private static XmlElement GetFirstChildElement(XmlElement el)
    {
        foreach (XmlNode child in el.ChildNodes)
            if (child is XmlElement e) return e;
        return null;
    }
}
#endif