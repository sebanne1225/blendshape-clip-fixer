using System.IO;
using UnityEditor;

namespace BlendshapeClipFixer.Editor
{
    public static class AssetPathUtil
    {
        public static string Combine(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b;
            if (string.IsNullOrEmpty(b)) return a;
            return (a.TrimEnd('/') + "/" + b.TrimStart('/')).Replace("\\", "/");
        }

        public static void EnsureFolderExists(string assetFolderPath)
        {
            assetFolderPath = assetFolderPath.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            // Create nested folders
            var parts = assetFolderPath.Split('/');
            if (parts.Length == 0) return;

            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }
                cur = next;
            }
        }
    }
}