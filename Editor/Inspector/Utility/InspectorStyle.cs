using UnityEditor;
using UnityEngine;

namespace MyInspector.Editor
{
    /// <summary>
    /// Single source of truth for colors, indentation, paddings and sizes used by the Inspector drawers.
    /// Edit values here only: drawers must reference this class and never hard-code style values.
    /// </summary>
    public static class InspectorStyle
    {
        // ---- Colors ----
        private static readonly Color HeaderBackgroundPro = new Color(0.26f, 0.26f, 0.26f);
        private static readonly Color HeaderBackgroundPersonal = new Color(0.72f, 0.72f, 0.72f);
        private static readonly Color ContentBackgroundPro = new Color(0.19f, 0.19f, 0.19f);
        private static readonly Color ContentBackgroundPersonal = new Color(0.78f, 0.78f, 0.78f);
        private static readonly Color TableBorderPro = new Color(0.13f, 0.13f, 0.13f, 1f);
        private static readonly Color TableBorderPersonal = new Color(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color NestedBorderPro = new Color(0.14f, 0.14f, 0.14f);
        private static readonly Color NestedBorderPersonal = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color ColumnHeaderPro = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color ColumnHeaderPersonal = new Color(0.78f, 0.78f, 0.78f);

        /// <summary>Background of box / list / table headers.</summary>
        public static Color HeaderBackground => EditorGUIUtility.isProSkin ? HeaderBackgroundPro : HeaderBackgroundPersonal;

        /// <summary>Background of box / foldout content.</summary>
        public static Color ContentBackground => EditorGUIUtility.isProSkin ? ContentBackgroundPro : ContentBackgroundPersonal;

        /// <summary>Border of tables.</summary>
        public static Color TableBorder => EditorGUIUtility.isProSkin ? TableBorderPro : TableBorderPersonal;

        /// <summary>Border of nested tables.</summary>
        public static Color NestedBorder => EditorGUIUtility.isProSkin ? NestedBorderPro : NestedBorderPersonal;

        /// <summary>Background of table column headers.</summary>
        public static Color ColumnHeaderBackground => EditorGUIUtility.isProSkin ? ColumnHeaderPro : ColumnHeaderPersonal;

        public static readonly Color ListHeaderTint = new Color(0f, 0f, 0f, 0.2f);
        public static readonly Color Border = new Color(0.14f, 0.14f, 0.14f, 1f);
        public static readonly Color EvenRow = new Color(1f, 1f, 1f, 0.04f);
        public static readonly Color OddRow = new Color(0f, 0f, 0f, 0.09f);
        public static readonly Color HorizontalLine = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        public static readonly Color ProgressBarBackground = new Color(0.13f, 0.13f, 0.13f);
        public static readonly Color ProgressBarLabel = Color.white;
        public static readonly Color CountLabel = Color.gray;
        public static readonly Color SearchPlaceholder = Color.gray;
        public static readonly Color DragInsertionLine = Color.white;

        // ---- Indentation ----
        public const float IndentLength = 0f;
        public const float HorizontalSpacing = 2f;
        public const float ListElementIndent = 32f;
        public const float ListContainerIndent = 14f;
        public const float FoldoutBodyIndentLeft = 16f;
        public const float FoldoutBodyIndentRight = 4f;

        // ---- Tab groups (also used by PatternDescriptionDrawer) ----
        public const float TabBoxPaddingLeft = 18f;
        public const float TabBoxPaddingRight = 6f;
        public const float TabBoxPaddingVertical = 6f;
        public const float TabBoxHiddenTopHeight = 3f;

        // ---- Lists, dictionaries, foldouts ----
        public const float ListHeaderHeight = 22f;
        public const float ListButtonWidth = 22f;
        public const float ListSpacing = 2f;
        public const float ListTopPadding = 5f;
        public const float ListInnerPadding = 4f;
        public const float ListHeaderPadding = 5f;
        public const float ListItemPadding = 3f;
        public const float ListBottomPadding = 2f;
        public const float ListBoxPadding = 3f;
        public const float FoldoutTopSpace = ListTopPadding;
        // Horizontal offset of the BoolTitle checkbox from the foldout header's left edge (the arrow sits just before it)
        public const float BoolTitleToggleOffset = 2f;
        // Width reserved for the foldout arrow before the BoolTitle checkbox in list/container headers
        public const float BoolTitleArrowWidth = 5f;
        // Margins around a UnityEvent drawn inside a container, since its drawer paints its box beyond the given rect
        public const float UnityEventTopMargin = 6f;
        public const float UnityEventLeftIndent = 14f;
        public const float FoldoutBottomSpace = ListBottomPadding;
        public const float ListCountWidth = 110f;
        public const float ListSearchWidth = 110f;
        public const float DictionaryMinColumnWidth = 40f;

        // ---- Tables ----
        public const float TableRowButtonWidth = 22f;
        public const float TableRowPadding = 3f;
        public const float TableMinColumnWidth = 60f;
        public const float TableNestedLabelWidth = 70f;
        public const float TableCellSidePadding = 2f;
        public const float TableCellRightPadding = 8f;

        // ---- Other drawers ----
        public const float MinMaxSliderSpacing = 4f;
        public const float MinMaxSliderFieldWidth = 50f;
        public const float BrowseButtonWidth = 60f;
        public const float BoxHeaderHeight = 26f;
        public const float DisplayAsStringHorizontalMargin = 40f;

        // ---- Title drawers ----
        public const float TitleLineHeight = 1f;
        public const float TitlePadding = 4f;
        public const float TitleBottomMargin = 2f;
        public const float TitleTopSpace = 8f;
        public const float TitleBottomSpace = 3f;
        public const float TitleRowHeight = 20f;
        public const float TitleSeparatorThickness = 1f;
        public static readonly Color TitleLine = HorizontalLine;
        public static readonly Color TitleSeparator = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        // ---- Number range drawers ----
        public const float RangeTypeWidth = 50f;
        public const float RangeModeWidth = 60f;
        public const float RangeConditionWidth = 80f;
        public const float RangeFieldWidth = 50f;

        /// <summary>Smallest width of a number field sized from its content.</summary>
        public const float RangeFieldMinWidth = 24f;

        /// <summary>Horizontal padding added to the measured text of a number field.</summary>
        public const float RangeFieldPadding = 8f;
        public const float RangeStepButtonWidth = 20f;
        public const float RangeSpacing = 4f;
        public const float RangePrefixPadding = 2f;
    }
}
