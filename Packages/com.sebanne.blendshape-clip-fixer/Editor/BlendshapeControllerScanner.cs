using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BlendshapeClipFixer.Editor
{
    public static class BlendshapeControllerScanner
    {
        public sealed class ScanResult
        {
            public string ControllerName;
            public List<AnimationClip> Clips = new();
            public int TotalBlendshapeBindings;
            public HashSet<string> UniqueBlendshapeNames = new(StringComparer.Ordinal);
            public HashSet<string> MissingBlendshapeNames = new(StringComparer.Ordinal);
            public int PathMismatchCount;
            public string TargetRendererPath;

            public List<Issue> Issues = new();

            public sealed class Issue
            {
                public AnimationClip Clip;
                public string ClipName;
                public string BindingPath;
                public string ShapeName;
                public string Reason;
            }
        }

        public static ScanResult Scan(AnimatorController controller, Mesh targetMesh, string targetRendererPath)
        {
            var res = new ScanResult
            {
                ControllerName = controller != null ? controller.name : "(null)",
                TargetRendererPath = targetRendererPath ?? "",
            };
            if (controller == null) return res;

            var clips = AnimatorControllerUtil.CollectAnimationClips(controller);
            res.Clips = clips.OrderBy(c => c.name).ToList();

            var targetShapeSet = new HashSet<string>(StringComparer.Ordinal);
            if (targetMesh != null)
                foreach (var n in BlendshapeNameUtil.GetBlendshapeNames(targetMesh))
                    targetShapeSet.Add(n);

            foreach (var clip in res.Clips)
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var b in bindings)
                {
                    if (b.type != typeof(SkinnedMeshRenderer)) continue;
                    if (!BlendshapeNameUtil.TryParseBlendshapeProperty(b.propertyName, out var shapeName)) continue;

                    res.TotalBlendshapeBindings++;
                    res.UniqueBlendshapeNames.Add(shapeName);

                    bool shapeExists = targetMesh == null || targetShapeSet.Contains(shapeName);
                    bool pathMatches = string.IsNullOrEmpty(targetRendererPath) || string.Equals(b.path, targetRendererPath, StringComparison.Ordinal);

                    string reason = null;
                    if (!shapeExists) reason = "MissingShape";
                    else if (!pathMatches) reason = "PathMismatch";

                    if (reason != null)
                    {
                        if (!shapeExists) res.MissingBlendshapeNames.Add(shapeName);
                        if (!pathMatches) res.PathMismatchCount++;

                        res.Issues.Add(new ScanResult.Issue
                        {
                            Clip = clip,
                            ClipName = clip.name,
                            BindingPath = b.path,
                            ShapeName = shapeName,
                            Reason = reason
                        });
                    }
                }
            }

            return res;
        }
    }
}
