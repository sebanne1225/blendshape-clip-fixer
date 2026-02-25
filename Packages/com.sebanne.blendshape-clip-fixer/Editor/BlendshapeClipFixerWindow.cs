using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BlendshapeClipFixer.Editor
{
    public sealed class BlendshapeClipFixerWindow : EditorWindow
    {
        private enum UiLanguage
        {
            English,
            Japanese,
        }

        [SerializeField] private UiLanguage uiLanguage = UiLanguage.Japanese;
        [SerializeField] private AnimatorController sourceController;

        [Header("Optional but recommended (for path fix)")]
        [SerializeField] private GameObject targetAvatarRoot;

        [SerializeField] private SkinnedMeshRenderer targetRenderer;

        [Header("Output")]
        [SerializeField] private string outputRootFolder = "Assets/BlendshapeClipFixer_Output";
        [SerializeField] private bool cleanOutputBeforeGenerate = true;

        [Header("Fix options")]
        [SerializeField] private bool forceAllBlendshapePathsToTargetRenderer = true;

        [Header("Logging")]
        [SerializeField] private bool verboseLog = false;
        [SerializeField] private string mappingSearchKeyword = "";
        [SerializeField] private bool clipFocusMode = false;
        [SerializeField] private AnimationClip focusedClip;
        [SerializeField] private bool generateFocusedClipOnly = false;
        [SerializeField] private float mappingPanelHeight = 300f;

        private BlendshapeControllerScanner.ScanResult lastScan;
        private Vector2 scroll;
        private Vector2 windowScroll;
        private bool isResizingMapPanel;
        private string lastAutoLoadSignature = "";
        private readonly Dictionary<string, string[]> bulkAmbiguousCandidates = new(StringComparer.Ordinal);

        [SerializeField] private List<MappingRow> mappingRows = new();
        [Serializable]
        private class MappingRow
        {
            public string oldName;
            public List<string> newNames = new() { "" };
        }

        [Serializable]
        private class PersistedMappingRow
        {
            public string oldName;
            public List<string> newNames = new();
        }

        [Serializable]
        private class PersistedState
        {
            public string selectionSignature;
            public string sourceControllerPath;
            public string targetAvatarRootGlobalId;
            public string targetRendererGlobalId;
            public string focusedClipPath;
            public string outputRootFolder;
            public bool cleanOutputBeforeGenerate;
            public bool forceAllBlendshapePathsToTargetRenderer;
            public bool verboseLog;
            public string mappingSearchKeyword;
            public bool clipFocusMode;
            public bool generateFocusedClipOnly;
            public float mappingPanelHeight;
            public UiLanguage uiLanguage;
            public List<PersistedMappingRow> mappings = new();
        }

        private const string PersistedStateKey = "BlendshapeClipFixer.Editor.State.v1";
        private const float MapPanelMinHeight = 160f;
        private const float MapPanelMaxHeight = 700f;

        [MenuItem("Tools/Blendshape Clip Fixer")]
        public static void Open()
        {
            var w = GetWindow<BlendshapeClipFixerWindow>();
            w.titleContent = new GUIContent("Blendshape Clip Fixer / ブレンドシェイプ修正");
            w.minSize = new Vector2(560, 520);
            w.Show();
        }

        private bool IsJapanese => uiLanguage == UiLanguage.Japanese;
        private string L(string en, string ja) => IsJapanese ? ja : en;

        private void OnGUI()
        {
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            EditorGUILayout.Space(6);
            DrawInput();
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("Scan Controller", "コントローラーをスキャン"), GUILayout.Height(28)))
                    Scan();

                GUI.enabled = lastScan != null;
                if (GUILayout.Button(L("Generate Fixed Assets", "修正アセットを生成"), GUILayout.Height(28)))
                    Generate();
                GUI.enabled = true;
            }

            EditorGUILayout.Space(8);
            DrawScanSummary();
            EditorGUILayout.Space(8);
            DrawMappingUI();
            EditorGUILayout.Space(8);
            DrawIssuesUI();
            EditorGUILayout.EndScrollView();
        }

        private void DrawInput()
        {
            EditorGUILayout.LabelField(L("Language", "言語"), EditorStyles.boldLabel);
            uiLanguage = (UiLanguage)EditorGUILayout.EnumPopup(L("UI Language", "UI言語"), uiLanguage);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(L("Inputs", "入力"), EditorStyles.boldLabel);

            sourceController = (AnimatorController)EditorGUILayout.ObjectField(
                new GUIContent(L("Source Controller", "元コントローラー"), L("AnimatorController asset to scan & fix.", "スキャン・修正対象のAnimatorController。")),
                sourceController, typeof(AnimatorController), false);

            targetAvatarRoot = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(L("Target Avatar Root (optional)", "対象アバタールート（任意）"), L("Used to compute relative path for the target renderer when path-fix is ON.", "パス修正ON時にターゲットRendererの相対パス計算に使用。")),
                targetAvatarRoot, typeof(GameObject), true);

            targetRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                new GUIContent(L("Target SkinnedMeshRenderer", "対象SkinnedMeshRenderer"), L("Renderer that actually has the correct blendshapes (usually Face mesh).", "正しいBlendshapeを持つRenderer（通常は顔メッシュ）。")),
                targetRenderer, typeof(SkinnedMeshRenderer), true);

            TryAutoLoadPersistedState();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(L("Options", "オプション"), EditorStyles.boldLabel);

            forceAllBlendshapePathsToTargetRenderer = EditorGUILayout.ToggleLeft(
                new GUIContent(L("Force all blendshape curves to target renderer path", "すべてのBlendshapeカーブを対象Rendererパスへ強制"),
                    L("If ON, all blendshape curves will be re-bound to the selected target renderer path (relative to Avatar Root). Fixes 'path mismatch' missing.", "ONの場合、すべてのBlendshapeカーブを選択したRendererパス（Avatar Root基準）へ再バインドします。パス不一致を修正します。")),
                forceAllBlendshapePathsToTargetRenderer);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(L("Output Root Folder", "出力ルートフォルダ"), L("Must be under Assets/", "Assets/ 配下である必要があります。")), GUILayout.Width(140));
                outputRootFolder = EditorGUILayout.TextField(outputRootFolder);
                if (GUILayout.Button(L("Pick", "選択"), GUILayout.Width(60)))
                {
                    var abs = EditorUtility.OpenFolderPanel(L("Pick Output Folder (under Assets)", "出力フォルダを選択（Assets配下）"), Application.dataPath, "");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        var rel = ToAssetsRelativePath(abs);
                        if (!string.IsNullOrEmpty(rel)) outputRootFolder = rel;
                        else EditorUtility.DisplayDialog(L("Invalid folder", "無効なフォルダ"), L("Please pick a folder under this project's Assets/.", "このプロジェクトのAssets/配下のフォルダを選択してください。"), "OK");
                    }
                }
            }

            cleanOutputBeforeGenerate = EditorGUILayout.ToggleLeft(
                new GUIContent(L("Clean output before generate (recommended)", "生成前に出力先をクリーン（推奨）"), L("Deletes previously generated files in the output subfolder before generating.", "生成前に出力サブフォルダ内の既存生成物を削除します。")),
                cleanOutputBeforeGenerate);

            verboseLog = EditorGUILayout.ToggleLeft(L("Verbose log", "詳細ログ"), verboseLog);

            if (forceAllBlendshapePathsToTargetRenderer && targetAvatarRoot == null)
                EditorGUILayout.HelpBox(L("Path-fix is ON, but Avatar Root is not set. We can still fix blendshape names, but cannot force the renderer path.", "パス修正はONですがAvatar Rootが未設定です。Blendshape名の修正は可能ですが、Rendererパス強制はできません。"), MessageType.Warning);

            if (targetRenderer == null)
                EditorGUILayout.HelpBox(L("Target SkinnedMeshRenderer is not set. Scan will still work, but Missing detection/mapping dropdown won't be available.", "対象SkinnedMeshRendererが未設定です。スキャンは可能ですが、Missing判定やマッピング候補表示は利用できません。"), MessageType.Info);
        }

        private void DrawScanSummary()
        {
            EditorGUILayout.LabelField(L("Scan Result", "スキャン結果"), EditorStyles.boldLabel);

            if (lastScan == null)
            {
                EditorGUILayout.HelpBox(L("Press 'Scan Controller' to list clips and missing blendshapes.", "「コントローラーをスキャン」を押すと、クリップと不足Blendshapeを一覧表示します。"), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{L("Controller", "コントローラー")}: {lastScan.ControllerName}");
            EditorGUILayout.LabelField($"{L("Clips", "クリップ")}: {lastScan.Clips.Count}   {L("Blendshape Bindings", "Blendshapeバインディング")}: {lastScan.TotalBlendshapeBindings}");
            EditorGUILayout.LabelField($"{L("Unique shapes", "ユニーク形状")}: {lastScan.UniqueBlendshapeNames.Count}   {L("Missing shapes", "不足形状")}: {lastScan.MissingBlendshapeNames.Count}");
            EditorGUILayout.LabelField($"{L("Path mismatches (vs target renderer)", "パス不一致（対象Renderer基準）")}: {lastScan.PathMismatchCount}");
            EditorGUILayout.LabelField($"{L("Target renderer path", "対象Rendererパス")}: {(string.IsNullOrEmpty(lastScan.TargetRendererPath) ? L("(not available)", "（未設定）") : lastScan.TargetRendererPath)}");

            clipFocusMode = EditorGUILayout.ToggleLeft(L("Clip focus mode (show one AnimationClip only)", "クリップ集中モード（1つのAnimationClipのみ表示）"), clipFocusMode);
            if (clipFocusMode)
            {
                focusedClip = DrawFocusedClipSelector(focusedClip);
                generateFocusedClipOnly = EditorGUILayout.ToggleLeft(
                    L("Generate: fix focused clip only (other clips are copied as-is)", "Generate時: 集中対象クリップのみ修正（他クリップはそのままコピー）"),
                    generateFocusedClipOnly);

                if (focusedClip != null && !lastScan.Clips.Contains(focusedClip))
                {
                    EditorGUILayout.HelpBox(L("Selected clip is not part of the scanned controller.", "選択中のクリップは、スキャンしたコントローラーには含まれていません。"), MessageType.Warning);
                }
            }

            if (lastScan.MissingBlendshapeNames.Count > 0 && targetRenderer != null)
                EditorGUILayout.HelpBox(L("Fill the Missing/Replace map below. Unresolved ones will be left as-is and reported in logs.", "下のMissing/置換マップを設定してください。未解決のものはそのまま残し、ログに出力されます。"), MessageType.Warning);
        }

        private void DrawMappingUI()
        {
            EditorGUILayout.LabelField(L("Missing / Replace Map", "Missing / 置換マップ"), EditorStyles.boldLabel);

            if (lastScan == null)
            {
                EditorGUILayout.HelpBox(L("Scan first.", "先にスキャンしてください。"), MessageType.Info);
                return;
            }

            if (targetRenderer == null || targetRenderer.sharedMesh == null)
            {
                EditorGUILayout.HelpBox(L("Set Target SkinnedMeshRenderer with a valid sharedMesh to edit mappings using dropdowns.", "有効なsharedMeshを持つ対象SkinnedMeshRendererを設定すると、ドロップダウンでマッピング編集できます。"), MessageType.Info);
                return;
            }

            var mesh = targetRenderer.sharedMesh;
            var targetShapes = BlendshapeNameUtil.GetBlendshapeNames(mesh);
            var filteredShapes = FilterShapesByKeyword(targetShapes, mappingSearchKeyword);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("Auto map by exact name (ignore case)", "完全一致で自動マップ（大文字小文字無視）"), GUILayout.Height(22)))
                    AutoMapExactIgnoreCase(targetShapes);

                if (GUILayout.Button(L("Auto map (normalized/fuzzy)", "自動マップ（正規化/あいまい）"), GUILayout.Height(22)))
                    AutoMapNormalizedFuzzy(targetShapes);

                if (GUILayout.Button(L("Bulk map by keyword (prefix/contains)", "キーワード一括マップ（prefix/contains）"), GUILayout.Height(22)))
                    AutoMapByKeywordPrefixContains(targetShapes);

                if (GUILayout.Button(L("Clear mappings", "マッピングをクリア"), GUILayout.Height(22), GUILayout.Width(120)))
                {
                    foreach (var r in mappingRows)
                    {
                        r.newNames.Clear();
                        r.newNames.Add("");
                    }
                    bulkAmbiguousCandidates.Clear();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L("Search keyword", "検索キーワード"), GUILayout.Width(110));
                mappingSearchKeyword = EditorGUILayout.TextField(mappingSearchKeyword ?? "");
                if (GUILayout.Button(L("Clear", "クリア"), GUILayout.Width(60)))
                    mappingSearchKeyword = "";
            }

            if (!string.IsNullOrWhiteSpace(mappingSearchKeyword))
                EditorGUILayout.HelpBox(L($"Dropdown candidates are filtered by keyword: '{mappingSearchKeyword}'", $"ドロップダウン候補はキーワード '{mappingSearchKeyword}' で絞り込まれます。"), MessageType.None);

            if (bulkAmbiguousCandidates.Count > 0)
                EditorGUILayout.HelpBox(L($"Bulk map found multiple candidates for {bulkAmbiguousCandidates.Count} rows. Pick one from each row's 'Suggested' dropdown.", $"一括マップで {bulkAmbiguousCandidates.Count} 件が複数候補でした。各行の「候補」ドロップダウンから選択してください。"), MessageType.Warning);

            if (lastScan.MissingBlendshapeNames.Count == 0)
            {
                EditorGUILayout.HelpBox(L("No missing blendshape names detected against the selected target renderer.", "選択した対象Rendererに対する不足Blendshape名は検出されませんでした。"), MessageType.Info);
                return;
            }

            HashSet<string> focusedMissingShapes = null;
            if (TryGetFocusedClip(out var clip))
            {
                focusedMissingShapes = CollectMissingShapesForClip(clip);
                if (focusedMissingShapes.Count == 0)
                {
                    EditorGUILayout.HelpBox(L("Focused clip has no missing blendshape names.", "集中対象クリップには不足Blendshape名がありません。"), MessageType.Info);
                    return;
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(mappingPanelHeight));
            var mappingRowsToShow = mappingRows.Where(r => lastScan.MissingBlendshapeNames.Contains(r.oldName));
            if (focusedMissingShapes != null)
                mappingRowsToShow = mappingRowsToShow.Where(r => focusedMissingShapes.Contains(r.oldName));

            foreach (var row in mappingRowsToShow)
            {
                EnsureRowTargets(row);

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(row.oldName, GUILayout.Width(240));
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("+", GUILayout.Width(28)))
                            row.newNames.Add("");
                    }

                    for (int i = 0; i < row.newNames.Count; i++)
                    {
                        DrawTargetSelectorRow(row, i, filteredShapes);
                    }

                    if (bulkAmbiguousCandidates.TryGetValue(row.oldName, out var suggestions) && suggestions != null && suggestions.Length > 0)
                        DrawSuggestedSelector(row, suggestions);
                }
            }
            EditorGUILayout.EndScrollView();

            DrawMapResizeHandle();
        }

        private void DrawIssuesUI()
        {
            EditorGUILayout.LabelField(L("Bindings (preview)", "バインディング（プレビュー）"), EditorStyles.boldLabel);
            if (lastScan == null) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField(L("Preview list of blendshape bindings found in clips. 'Missing' means: not found on the chosen target renderer mesh, or path mismatch vs target renderer path.", "クリップ内で検出したBlendshapeバインディングのプレビューです。'Missing' は対象Rendererメッシュに存在しないか、対象Rendererパスと不一致であることを示します。"));
                EditorGUILayout.Space(4);

                IEnumerable<BlendshapeControllerScanner.ScanResult.Issue> issues = lastScan.Issues;
                if (TryGetFocusedClip(out var clip))
                    issues = issues.Where(i => i.Clip == clip);

                int shown = 0;
                foreach (var issue in issues)
                {
                    if (shown >= 80) { EditorGUILayout.LabelField(L("... (truncated)", "…（省略）")); break; }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(issue.ClipName, GUILayout.Width(180));
                        EditorGUILayout.LabelField(issue.BindingPath, GUILayout.Width(220));
                        EditorGUILayout.LabelField(issue.ShapeName, GUILayout.Width(180));
                        EditorGUILayout.LabelField(issue.Reason, GUILayout.Width(140));
                    }
                    shown++;
                }
            }
        }

        private void Scan()
        {
            if (sourceController == null)
            {
                EditorUtility.DisplayDialog(L("Missing input", "入力不足"), L("Please set Source Controller.", "Source Controller を設定してください。"), "OK");
                return;
            }

            string targetPath = "";
            Mesh targetMesh = null;

            if (targetRenderer != null)
            {
                targetMesh = targetRenderer.sharedMesh;
                if (targetAvatarRoot != null)
                    targetPath = AnimationUtility.CalculateTransformPath(targetRenderer.transform, targetAvatarRoot.transform);
            }

            lastScan = BlendshapeControllerScanner.Scan(sourceController, targetMesh, targetPath);
            bulkAmbiguousCandidates.Clear();
            if (focusedClip != null && !lastScan.Clips.Contains(focusedClip))
                focusedClip = null;
            if (focusedClip == null && lastScan.Clips.Count > 0)
                focusedClip = lastScan.Clips[0];

            var existing = new HashSet<string>(mappingRows.Select(r => r.oldName));
            foreach (var m in lastScan.MissingBlendshapeNames)
                if (!existing.Contains(m))
                    mappingRows.Add(new MappingRow { oldName = m, newNames = new List<string> { "" } });

            Debug.Log($"[BlendshapeFixer] Scan: clips={lastScan.Clips.Count} blendshapeBindings={lastScan.TotalBlendshapeBindings} uniqueShapes={lastScan.UniqueBlendshapeNames.Count} missingShapes={lastScan.MissingBlendshapeNames.Count} pathMismatches={lastScan.PathMismatchCount}");
            SavePersistedState();
        }

        private void Generate()
        {
            if (sourceController == null)
            {
                EditorUtility.DisplayDialog(L("Missing input", "入力不足"), L("Please set Source Controller.", "Source Controller を設定してください。"), "OK");
                return;
            }

            if (!outputRootFolder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog(L("Invalid output folder", "出力フォルダが無効"), L("Output Root Folder must be under Assets/.", "Output Root Folder は Assets/ 配下である必要があります。"), "OK");
                return;
            }

            string controllerPath = AssetDatabase.GetAssetPath(sourceController);
            if (string.IsNullOrEmpty(controllerPath))
            {
                EditorUtility.DisplayDialog(L("Invalid controller", "コントローラーが無効"), L("Could not find controller asset path.", "コントローラーのアセットパスが見つかりません。"), "OK");
                return;
            }

            Mesh targetMesh = targetRenderer != null ? targetRenderer.sharedMesh : null;

            bool canForcePath = forceAllBlendshapePathsToTargetRenderer && targetAvatarRoot != null && targetRenderer != null;
            string targetRendererPath = canForcePath
                ? AnimationUtility.CalculateTransformPath(targetRenderer.transform, targetAvatarRoot.transform)
                : "";

            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var r in mappingRows)
            {
                if (string.IsNullOrEmpty(r.oldName)) continue;
                EnsureRowTargets(r);
                var targets = r.newNames
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (targets.Count > 0)
                    map[r.oldName] = targets;
            }

            var subFolder = $"{Path.GetFileNameWithoutExtension(controllerPath)}_Fixed";
            var outFolder = AssetPathUtil.Combine(outputRootFolder, subFolder);
            var clipsFolder = AssetPathUtil.Combine(outFolder, "Clips");

            if (cleanOutputBeforeGenerate)
                BlendshapeClipFixerGenerator.CleanFolder(outFolder);

            AssetPathUtil.EnsureFolderExists(outputRootFolder);
            AssetPathUtil.EnsureFolderExists(outFolder);
            AssetPathUtil.EnsureFolderExists(clipsFolder);

            var settings = new BlendshapeClipFixerGenerator.Settings
            {
                SourceController = sourceController,
                OutputFolder = outFolder,
                ClipsFolder = clipsFolder,
                TargetMesh = targetMesh,
                TargetRendererPath = targetRendererPath,
                ForceAllBlendshapePaths = canForcePath,
                BlendshapeRenameMap = map,
                VerboseLog = verboseLog,
                FocusOnlyClip = (clipFocusMode && generateFocusedClipOnly && focusedClip != null) ? focusedClip : null,
            };

            var result = BlendshapeClipFixerGenerator.Generate(settings);

            EditorUtility.DisplayDialog(
                L("Blendshape Clip Fixer", "Blendshape Clip Fixer"),
                L($"Done!\n\nController: {result.FixedControllerPath}\nClips created: {result.CreatedClips}\nClips reused: {result.ReusedClips}\nUnresolved bindings: {result.UnresolvedBindings}\n\nSee Console for details.",
                  $"完了しました。\n\nコントローラー: {result.FixedControllerPath}\n作成クリップ数: {result.CreatedClips}\n再利用クリップ数: {result.ReusedClips}\n未解決バインディング: {result.UnresolvedBindings}\n\n詳細はConsoleを確認してください。"),
                "OK");

            SavePersistedState();
        }

        private void AutoMapExactIgnoreCase(string[] targetShapes)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in targetShapes) dict[s] = s;

            foreach (var row in mappingRows)
            {
                EnsureRowTargets(row);
                if (!string.IsNullOrEmpty(row.oldName) && dict.TryGetValue(row.oldName, out var match))
                    row.newNames[0] = match;
            }
        }

        private void AutoMapNormalizedFuzzy(string[] targetShapes)
        {
            var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var normalized = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var shape in targetShapes)
            {
                exact[shape] = shape;

                var key = NormalizeShapeKey(shape);
                if (string.IsNullOrEmpty(key)) continue;
                if (!normalized.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    normalized[key] = list;
                }
                list.Add(shape);
            }

            foreach (var row in mappingRows)
            {
                EnsureRowTargets(row);
                if (string.IsNullOrEmpty(row.oldName) || !string.IsNullOrEmpty(row.newNames[0])) continue;

                if (exact.TryGetValue(row.oldName, out var exactMatch))
                {
                    row.newNames[0] = exactMatch;
                    continue;
                }

                var oldKey = NormalizeShapeKey(row.oldName);
                if (string.IsNullOrEmpty(oldKey)) continue;

                if (normalized.TryGetValue(oldKey, out var sameKeyMatches) && sameKeyMatches.Count == 1)
                {
                    row.newNames[0] = sameKeyMatches[0];
                    continue;
                }

                string fuzzyMatch = null;
                bool multiple = false;
                foreach (var candidate in targetShapes)
                {
                    var candKey = NormalizeShapeKey(candidate);
                    if (string.IsNullOrEmpty(candKey)) continue;

                    bool related =
                        candKey.IndexOf(oldKey, StringComparison.Ordinal) >= 0 ||
                        oldKey.IndexOf(candKey, StringComparison.Ordinal) >= 0;

                    if (!related) continue;

                    if (fuzzyMatch == null) fuzzyMatch = candidate;
                    else { multiple = true; break; }
                }

                if (!multiple && !string.IsNullOrEmpty(fuzzyMatch))
                    row.newNames[0] = fuzzyMatch;
            }
        }

        private static string NormalizeShapeKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));

            return sb.ToString();
        }

        private void AutoMapByKeywordPrefixContains(string[] targetShapes)
        {
            if (targetShapes == null || targetShapes.Length == 0) return;
            if (string.IsNullOrWhiteSpace(mappingSearchKeyword))
            {
                EditorUtility.DisplayDialog(L("Keyword required", "キーワードが必要です"), L("Set 'Search keyword' first.", "先に「検索キーワード」を入力してください。"), "OK");
                return;
            }

            var keywordCandidates = FilterShapesByKeyword(targetShapes, mappingSearchKeyword);
            if (keywordCandidates.Length == 0) return;

            bulkAmbiguousCandidates.Clear();

            foreach (var row in mappingRows)
            {
                EnsureRowTargets(row);
                if (string.IsNullOrEmpty(row.oldName) || !string.IsNullOrEmpty(row.newNames[0])) continue;
                if (lastScan != null && !lastScan.MissingBlendshapeNames.Contains(row.oldName)) continue;

                var oldKey = NormalizeShapeKey(row.oldName);
                if (string.IsNullOrEmpty(oldKey)) continue;

                var matches = new List<string>();

                foreach (var candidate in keywordCandidates)
                {
                    var candKey = NormalizeShapeKey(candidate);
                    if (string.IsNullOrEmpty(candKey)) continue;

                    bool relatedPrefix =
                        candKey.StartsWith(oldKey, StringComparison.Ordinal) ||
                        oldKey.StartsWith(candKey, StringComparison.Ordinal);

                    bool relatedContains =
                        candKey.IndexOf(oldKey, StringComparison.Ordinal) >= 0 ||
                        oldKey.IndexOf(candKey, StringComparison.Ordinal) >= 0;

                    if (!relatedPrefix && !relatedContains) continue;
                    matches.Add(candidate);
                }

                var unique = matches.Distinct(StringComparer.Ordinal).ToArray();
                if (unique.Length == 1)
                {
                    row.newNames[0] = unique[0];
                }
                else if (unique.Length > 1)
                {
                    bulkAmbiguousCandidates[row.oldName] = unique;
                }
            }
        }

        private void EnsureRowTargets(MappingRow row)
        {
            if (row.newNames == null)
                row.newNames = new List<string>();
            if (row.newNames.Count == 0)
                row.newNames.Add("");
        }

        private string[] BuildOptionsWithCurrent(string[] filteredShapes, string currentValue)
        {
            var rowShapes = filteredShapes ?? Array.Empty<string>();
            if (!string.IsNullOrEmpty(currentValue) &&
                Array.FindIndex(rowShapes, s => string.Equals(s, currentValue, StringComparison.Ordinal)) < 0)
            {
                var withCurrent = new string[rowShapes.Length + 1];
                withCurrent[0] = currentValue;
                Array.Copy(rowShapes, 0, withCurrent, 1, rowShapes.Length);
                rowShapes = withCurrent;
            }

            var options = new string[rowShapes.Length + 1];
            options[0] = L("(unassigned)", "（未割り当て）");
            for (int i = 0; i < rowShapes.Length; i++) options[i + 1] = rowShapes[i];
            return options;
        }

        private void DrawTargetSelectorRow(MappingRow row, int targetIndex, string[] filteredShapes)
        {
            var current = row.newNames[targetIndex] ?? "";
            var options = BuildOptionsWithCurrent(filteredShapes, current);
            int currentIndex = Array.FindIndex(options, s => string.Equals(s, current, StringComparison.Ordinal));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{L("To", "割当先")} {targetIndex + 1}", GUILayout.Width(56));
                int selected = EditorGUILayout.Popup(Mathf.Max(0, currentIndex), options);
                row.newNames[targetIndex] = selected <= 0 ? "" : options[selected];

                if (targetIndex > 0)
                {
                    if (GUILayout.Button("-", GUILayout.Width(28)))
                    {
                        row.newNames.RemoveAt(targetIndex);
                        if (row.newNames.Count == 0) row.newNames.Add("");
                    }
                }
                else
                {
                    GUILayout.Space(32);
                }
            }
        }

        private void DrawSuggestedSelector(MappingRow row, string[] suggestions)
        {
            EnsureRowTargets(row);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L("Suggested", "候補"), GUILayout.Width(56));

                int suggestedIndex = Array.FindIndex(suggestions, s => string.Equals(s, row.newNames[0], StringComparison.Ordinal));
                var suggestedOptions = new string[suggestions.Length + 1];
                suggestedOptions[0] = L("(choose)", "（選択）");
                for (int i = 0; i < suggestions.Length; i++) suggestedOptions[i + 1] = suggestions[i];

                int suggestedSelected = EditorGUILayout.Popup(suggestedIndex + 1, suggestedOptions);
                if (suggestedSelected > 0)
                    row.newNames[0] = suggestedOptions[suggestedSelected];
            }
        }

        private AnimationClip DrawFocusedClipSelector(AnimationClip current)
        {
            if (lastScan == null || lastScan.Clips == null || lastScan.Clips.Count == 0)
                return null;

            var options = new string[lastScan.Clips.Count + 1];
            options[0] = L("(none)", "（なし）");
            int selected = 0;

            for (int i = 0; i < lastScan.Clips.Count; i++)
            {
                var clip = lastScan.Clips[i];
                options[i + 1] = clip != null ? clip.name : L("(missing clip)", "（欠落クリップ）");
                if (clip == current) selected = i + 1;
            }

            int picked = EditorGUILayout.Popup(
                new GUIContent(L("Focus Clip", "集中対象クリップ"), L("Only this clip's rows/issues are shown in Mapping/Preview.", "Mapping/Previewをこのクリップの内容だけに絞ります。")),
                selected,
                options);

            return picked <= 0 ? null : lastScan.Clips[picked - 1];
        }

        private static string[] FilterShapesByKeyword(string[] targetShapes, string keyword)
        {
            if (targetShapes == null || targetShapes.Length == 0) return Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(keyword)) return targetShapes;

            var key = keyword.Trim();
            var keyNorm = NormalizeShapeKey(key);
            var list = new List<string>(targetShapes.Length);

            foreach (var shape in targetShapes)
            {
                if (string.IsNullOrEmpty(shape)) continue;

                bool hitRaw = shape.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
                bool hitNorm = !string.IsNullOrEmpty(keyNorm) &&
                               NormalizeShapeKey(shape).IndexOf(keyNorm, StringComparison.Ordinal) >= 0;

                if (hitRaw || hitNorm)
                    list.Add(shape);
            }

            if (list.Count == 0) return targetShapes;
            return list.ToArray();
        }

        private void DrawMapResizeHandle()
        {
            var handleRect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeVertical);
            EditorGUI.DrawRect(new Rect(handleRect.x, handleRect.center.y, handleRect.width, 1f), new Color(0.45f, 0.45f, 0.45f, 1f));

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && handleRect.Contains(evt.mousePosition))
            {
                isResizingMapPanel = true;
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                isResizingMapPanel = false;
            }
            else if (evt.type == EventType.MouseDrag && isResizingMapPanel)
            {
                mappingPanelHeight = Mathf.Clamp(mappingPanelHeight + evt.delta.y, MapPanelMinHeight, MapPanelMaxHeight);
                evt.Use();
                Repaint();
            }
        }

        private void TryAutoLoadPersistedState()
        {
            if (sourceController == null || targetAvatarRoot == null) return;

            var signature = BuildSelectionSignature(sourceController, targetAvatarRoot);
            if (string.IsNullOrEmpty(signature) || signature == lastAutoLoadSignature) return;
            lastAutoLoadSignature = signature;

            if (!TryLoadPersistedState(out var state) || state == null) return;
            if (!string.Equals(state.selectionSignature, signature, StringComparison.Ordinal)) return;

            ApplyPersistedState(state);
        }

        private void SavePersistedState()
        {
            if (sourceController == null || targetAvatarRoot == null) return;

            var state = new PersistedState
            {
                selectionSignature = BuildSelectionSignature(sourceController, targetAvatarRoot),
                sourceControllerPath = AssetDatabase.GetAssetPath(sourceController),
                targetAvatarRootGlobalId = GetGlobalId(targetAvatarRoot),
                targetRendererGlobalId = GetGlobalId(targetRenderer),
                focusedClipPath = focusedClip != null ? AssetDatabase.GetAssetPath(focusedClip) : "",
                outputRootFolder = outputRootFolder,
                cleanOutputBeforeGenerate = cleanOutputBeforeGenerate,
                forceAllBlendshapePathsToTargetRenderer = forceAllBlendshapePathsToTargetRenderer,
                verboseLog = verboseLog,
                mappingSearchKeyword = mappingSearchKeyword,
                clipFocusMode = clipFocusMode,
                generateFocusedClipOnly = generateFocusedClipOnly,
                mappingPanelHeight = mappingPanelHeight,
                uiLanguage = uiLanguage,
                mappings = mappingRows.Select(r => new PersistedMappingRow
                {
                    oldName = r.oldName,
                    newNames = r.newNames != null ? new List<string>(r.newNames) : new List<string>()
                }).ToList()
            };

            var json = JsonUtility.ToJson(state);
            EditorPrefs.SetString(PersistedStateKey, json);
        }

        private bool TryLoadPersistedState(out PersistedState state)
        {
            state = null;
            if (!EditorPrefs.HasKey(PersistedStateKey)) return false;
            var json = EditorPrefs.GetString(PersistedStateKey, "");
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                state = JsonUtility.FromJson<PersistedState>(json);
                return state != null;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyPersistedState(PersistedState state)
        {
            if (state == null) return;

            var restoredOutput = string.IsNullOrEmpty(state.outputRootFolder) ? outputRootFolder : state.outputRootFolder;
            if (string.Equals(restoredOutput, "Assets/BlendshapeClipFixer/Output", StringComparison.Ordinal))
                restoredOutput = "Assets/BlendshapeClipFixer_Output";
            outputRootFolder = restoredOutput;
            cleanOutputBeforeGenerate = state.cleanOutputBeforeGenerate;
            forceAllBlendshapePathsToTargetRenderer = state.forceAllBlendshapePathsToTargetRenderer;
            verboseLog = state.verboseLog;
            mappingSearchKeyword = state.mappingSearchKeyword ?? "";
            clipFocusMode = state.clipFocusMode;
            generateFocusedClipOnly = state.generateFocusedClipOnly;
            mappingPanelHeight = Mathf.Clamp(state.mappingPanelHeight <= 0 ? mappingPanelHeight : state.mappingPanelHeight, MapPanelMinHeight, MapPanelMaxHeight);
            uiLanguage = state.uiLanguage;

            var restoredRenderer = ResolveObjectByGlobalId<SkinnedMeshRenderer>(state.targetRendererGlobalId);
            if (restoredRenderer != null)
                targetRenderer = restoredRenderer;

            var restoredFocusClip = !string.IsNullOrEmpty(state.focusedClipPath)
                ? AssetDatabase.LoadAssetAtPath<AnimationClip>(state.focusedClipPath)
                : null;
            if (restoredFocusClip != null)
                focusedClip = restoredFocusClip;

            if (state.mappings != null && state.mappings.Count > 0)
            {
                mappingRows = state.mappings.Select(m => new MappingRow
                {
                    oldName = m.oldName,
                    newNames = m.newNames != null && m.newNames.Count > 0 ? new List<string>(m.newNames) : new List<string> { "" }
                }).ToList();
            }
        }

        private static string BuildSelectionSignature(AnimatorController controller, GameObject avatarRoot)
        {
            if (controller == null || avatarRoot == null) return "";
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            var avatarId = GetGlobalId(avatarRoot);
            if (string.IsNullOrEmpty(controllerPath) || string.IsNullOrEmpty(avatarId)) return "";
            return controllerPath + "|" + avatarId;
        }

        private static string GetGlobalId(UnityEngine.Object obj)
        {
            if (obj == null) return "";
            try { return GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString(); }
            catch { return ""; }
        }

        private static T ResolveObjectByGlobalId<T>(string id) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!GlobalObjectId.TryParse(id, out var gid)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as T;
        }

        private bool TryGetFocusedClip(out AnimationClip clip)
        {
            clip = null;
            if (!clipFocusMode || lastScan == null || focusedClip == null) return false;
            if (!lastScan.Clips.Contains(focusedClip)) return false;
            clip = focusedClip;
            return true;
        }

        private HashSet<string> CollectMissingShapesForClip(AnimationClip clip)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (lastScan == null || clip == null) return set;

            foreach (var issue in lastScan.Issues)
                if (issue.Clip == clip && string.Equals(issue.Reason, "MissingShape", StringComparison.Ordinal))
                    set.Add(issue.ShapeName);

            return set;
        }

        private static string ToAssetsRelativePath(string absoluteFolderPath)
        {
            absoluteFolderPath = absoluteFolderPath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!absoluteFolderPath.StartsWith(dataPath, StringComparison.Ordinal)) return null;
            return ("Assets" + absoluteFolderPath.Substring(dataPath.Length)).Replace('\\', '/');
        }

        private void OnDisable()
        {
            SavePersistedState();
        }
    }
}
