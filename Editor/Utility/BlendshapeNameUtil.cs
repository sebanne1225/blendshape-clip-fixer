using System;
using UnityEngine;

namespace BlendshapeClipFixer.Editor
{
    public static class BlendshapeNameUtil
    {
        public static bool TryParseBlendshapeProperty(string propertyName, out string shapeName)
        {
            shapeName = null;
            if (string.IsNullOrEmpty(propertyName)) return false;

            const string prefix = "blendShape.";
            if (!propertyName.StartsWith(prefix, StringComparison.Ordinal)) return false;

            shapeName = propertyName.Substring(prefix.Length);
            return !string.IsNullOrEmpty(shapeName);
        }

        public static string[] GetBlendshapeNames(Mesh mesh)
        {
            if (mesh == null) return Array.Empty<string>();

            int count = mesh.blendShapeCount;
            var arr = new string[count];
            for (int i = 0; i < count; i++)
                arr[i] = mesh.GetBlendShapeName(i);

            return arr;
        }
    }
}