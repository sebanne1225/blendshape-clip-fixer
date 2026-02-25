using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BlendshapeClipFixer.Editor
{
    public static class BlendshapeClipFixerGenerator
    {
        public sealed class Settings
        {
            public AnimatorController SourceController;
            public string OutputFolder;
            public string ClipsFolder;

            public Mesh TargetMesh;
            public string TargetRendererPath;
            public bool ForceAllBlendshapePaths;
            public AnimationClip FocusOnlyClip;

            public Dictionary<string, List<string>> BlendshapeRenameMap = new(StringComparer.Ordinal);
            public bool VerboseLog;
        }

        public sealed class Result
        {
            public string FixedControllerPath;
            public int CreatedClips;
            public int ReusedClips;
            public int UnresolvedBindings;
            public int TotalBindingsRewritten;
        }

        public static void CleanFolder(string folderAssetPath)
        {
            if (string.IsNullOrEmpty(folderAssetPath) || !folderAssetPath.StartsWith("Assets/"))
                return;

            if (!AssetDatabase.IsValidFolder(folderAssetPath))
                return;

            var all = AssetDatabase.FindAssets("", new[] { folderAssetPath });
            foreach (var guid in all)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p == folderAssetPath) continue;
                if (AssetDatabase.IsValidFolder(p)) continue;
                AssetDatabase.DeleteAsset(p);
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var absFolder = Path.Combine(projectRoot, folderAssetPath).Replace("\\", "/");

            if (Directory.Exists(absFolder))
            {
                var dirs = Directory.GetDirectories(absFolder, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length)
                    .ToArray();

                foreach (var d in dirs)
                {
                    try
                    {
                        FileUtil.DeleteFileOrDirectory(d);
                        FileUtil.DeleteFileOrDirectory(d + ".meta");
                    }
                    catch { }
                }
            }

            AssetDatabase.Refresh();
        }

        public static Result Generate(Settings s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.SourceController == null) throw new ArgumentNullException(nameof(s.SourceController));

            var res = new Result();

            var controllerPath = AssetDatabase.GetAssetPath(s.SourceController);
            var controllerName = Path.GetFileNameWithoutExtension(controllerPath);

            var fixedControllerPath = AssetPathUtil.Combine(s.OutputFolder, controllerName + "_Fixed.controller");
            var clipsUsed = AnimatorControllerUtil.CollectAnimationClips(s.SourceController);

            Log($"Generate: outFolder={s.OutputFolder}");
            Log($"Source: controller={controllerPath} clips={clipsUsed.Count}");

            var clipMap = new Dictionary<AnimationClip, AnimationClip>();
            int created = 0, reused = 0, unresolved = 0, rewritten = 0;

            foreach (var srcClip in clipsUsed)
            {
                var dstPath = MakeDeterministicClipPath(s.ClipsFolder, srcClip);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(dstPath);

                AnimationClip dstClip;
                if (existing == null)
                {
                    dstClip = UnityEngine.Object.Instantiate(srcClip);
                    dstClip.name = srcClip.name;
                    AssetDatabase.CreateAsset(dstClip, dstPath);
                    created++;
                }
                else
                {
                    dstClip = existing;
                    reused++;
                }

                var fixStats = FixClipBlendshapeBindings(
                    dstClip,
                    s.TargetMesh,
                    s.TargetRendererPath,
                    s.ForceAllBlendshapePaths,
                    s.BlendshapeRenameMap,
                    s.FocusOnlyClip == null || srcClip == s.FocusOnlyClip,
                    s.VerboseLog);

                unresolved += fixStats.unresolved;
                rewritten += fixStats.rewritten;

                clipMap[srcClip] = dstClip;
            }

            // controller is always re-copied to ensure clean rewiring
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(fixedControllerPath) != null)
                AssetDatabase.DeleteAsset(fixedControllerPath);

            if (!AssetDatabase.CopyAsset(controllerPath, fixedControllerPath))
                throw new Exception("Failed to copy controller: " + fixedControllerPath);

            AssetDatabase.ImportAsset(fixedControllerPath, ImportAssetOptions.ForceSynchronousImport);

            var fixedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(fixedControllerPath);
            if (fixedController == null)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                fixedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(fixedControllerPath);
            }
            if (fixedController == null)
                throw new Exception("Failed to load fixed controller after copy: " + fixedControllerPath);

            int rewired = AnimatorControllerUtil.ReplaceClipsInController(fixedController, clipMap);

            EditorUtility.SetDirty(fixedController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            res.FixedControllerPath = fixedControllerPath;
            res.CreatedClips = created;
            res.ReusedClips = reused;
            res.UnresolvedBindings = unresolved;
            res.TotalBindingsRewritten = rewritten;

            Log($"Clips: created={created} reused={reused} unresolvedBindings={unresolved} rewrittenBindings={rewritten}");
            Log($"Controller: created={Path.GetFileName(fixedControllerPath)} motionsRewired={rewired}");

            return res;
        }

        private static (int unresolved, int rewritten) FixClipBlendshapeBindings(
            AnimationClip clip,
            Mesh targetMesh,
            string targetRendererPath,
            bool forcePath,
            Dictionary<string, List<string>> renameMap,
            bool applyFixesToThisClip,
            bool verbose)
        {
            int unresolved = 0;
            int rewritten = 0;
            if (!applyFixesToThisClip) return (unresolved, rewritten);

            HashSet<string> targetShapes = null;
            if (targetMesh != null)
                targetShapes = new HashSet<string>(BlendshapeNameUtil.GetBlendshapeNames(targetMesh), StringComparer.Ordinal);

            var bindings = AnimationUtility.GetCurveBindings(clip);

            foreach (var b in bindings)
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!BlendshapeNameUtil.TryParseBlendshapeProperty(b.propertyName, out var oldShape)) continue;

                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;

                string newPath = b.path;
                if (forcePath && !string.IsNullOrEmpty(targetRendererPath))
                    newPath = targetRendererPath;

                List<string> targetNames = null;
                bool shapeExists = targetShapes == null || targetShapes.Contains(oldShape);

                if (renameMap != null && renameMap.TryGetValue(oldShape, out var mappedList))
                {
                    targetNames = mappedList?
                        .Where(m => !string.IsNullOrEmpty(m))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                }

                if (targetNames == null || targetNames.Count == 0)
                {
                    if (shapeExists)
                        targetNames = new List<string> { oldShape };
                    else
                    {
                        unresolved++;
                        if (verbose)
                            Debug.Log($"[BlendshapeFixer] Unresolved shape in clip='{clip.name}': '{oldShape}' (path='{b.path}')");
                        continue;
                    }
                }

                if (targetShapes != null)
                    targetNames = targetNames.Where(targetShapes.Contains).ToList();

                if (targetNames.Count == 0)
                {
                    unresolved++;
                    if (verbose)
                        Debug.Log($"[BlendshapeFixer] Mapped-to-missing shape in clip='{clip.name}': '{oldShape}'");
                    continue;
                }

                bool isSingleUnchanged =
                    targetNames.Count == 1 &&
                    string.Equals(targetNames[0], oldShape, StringComparison.Ordinal) &&
                    string.Equals(newPath, b.path, StringComparison.Ordinal);

                if (isSingleUnchanged) continue;

                AnimationUtility.SetEditorCurve(clip, b, null);
                foreach (var targetName in targetNames)
                {
                    var newBinding = b;
                    newBinding.path = newPath;
                    newBinding.propertyName = "blendShape." + targetName;
                    AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                    rewritten++;
                }
            }

            EditorUtility.SetDirty(clip);
            return (unresolved, rewritten);
        }

        private static string MakeDeterministicClipPath(string clipsFolder, AnimationClip clip)
        {
            string gid = "";
#if UNITY_2020_2_OR_NEWER
            var goid = GlobalObjectId.GetGlobalObjectIdSlow(clip);
            gid = goid.ToString();
#endif
            var safe = MakeFileSafe(gid);
            if (string.IsNullOrEmpty(safe)) safe = "NoGID";

            var file = $"{clip.name}__{safe}.anim";
            return AssetPathUtil.Combine(clipsFolder, file);
        }

        private static string MakeFileSafe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid) s = s.Replace(c, '_');
            s = s.Replace(' ', '_').Replace(':', '_').Replace('-', '_').Replace('.', '_');
            if (s.Length > 48) s = s.Substring(0, 48);
            return s;
        }

        private static void Log(string msg) => Debug.Log($"[BlendshapeFixer] {msg}");
    }
}
