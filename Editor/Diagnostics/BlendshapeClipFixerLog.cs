using UnityEngine;

namespace BlendshapeClipFixer.Editor
{
    internal static class BlendshapeClipFixerLog
    {
        private const string Prefix = "[BlendShape Clip Fixer] ";

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }
    }
}
