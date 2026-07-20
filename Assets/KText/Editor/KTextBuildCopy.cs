using UnityEngine;
using UnityEditor;
using System.IO;

namespace KText.Editor
{
    public static class KTextBuildCopy
    {
        private const string DLL_NAME = "KText.dll";
        private const string TARGET_DIR = "AAA";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += CopyDll;
        }

        [MenuItem("KText/Copy DLL to AAA")]
        public static void CopyDll()
        {
            string projectPath = Application.dataPath.Replace("/Assets", "");
            string sourcePath = Path.Combine(projectPath, "Library", "ScriptAssemblies", DLL_NAME);
            
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning("KText DLL not found: " + sourcePath);
                return;
            }

            string targetPath = Path.Combine(projectPath, TARGET_DIR);
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            string targetFilePath = Path.Combine(targetPath, DLL_NAME);
            File.Copy(sourcePath, targetFilePath, true);
            
            Debug.Log("KText DLL copied to: " + targetFilePath);
        }
    }
}