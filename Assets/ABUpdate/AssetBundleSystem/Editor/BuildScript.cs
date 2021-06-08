using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using DotNet.Utilities;

public class BuildScript
{
    const string kAssetBundlesOutputPath = "AssetBundles"; 
    public static void BuildAssetBundles()
    {
        // Choose the output path according to the build target.
        // string outputPath = Path.Combine(kAssetBundlesOutputPath, AssetBundleManager.GetPlatformFolderForAssetBundles(EditorUserBuildSettings.activeBuildTarget));
        
        string outPath = Path.Combine(Application.streamingAssetsPath, kAssetBundlesOutputPath);
        string outputFolder = AssetBundleManager.GetPlatformFolderForAssetBundles(EditorUserBuildSettings.activeBuildTarget);
        var outputPath = Path.Combine(outPath, outputFolder);
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        AssetBundleManifest bundleManifest = BuildPipeline.BuildAssetBundles(outputPath,
        BuildAssetBundleOptions.ChunkBasedCompression/* | BuildAssetBundleOptions.DisableWriteTypeTree| BuildAssetBundleOptions.DeterministicAssetBundle*/,
        EditorUserBuildSettings.activeBuildTarget);
        BundleInfo bundleInfo = ScriptableObject.CreateInstance<BundleInfo>();
        bundleInfo.Version = AssetBundleManager.Version;
        if (bundleManifest != null)
        {
            foreach (var bundleName in bundleManifest.GetAllAssetBundles())
            {
                //Debug.Log(bundleName);
                FileInfo info = new FileInfo(AssetBundleManager.StreamingassetsURL.Replace("file://", "") + bundleName);
                double size = info.Length;
                bundleInfo.dataList.Add(new BundleInfoData(bundleName,
                bundleManifest.GetAssetBundleHash(bundleName).ToString(), size));
            }
        }

        string path = "Assets/StreamingAssets/" + kAssetBundlesOutputPath + "/" + outputFolder + "/";
        string bundelPath = path + AssetBundleManager.versionName;

        string versionString =JsonConvert.SerializeObject(bundleInfo);
        FileOperate.WriteFile(bundelPath, versionString);

        //AssetBundle ab;
        //ab.Unload()
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void BuildPlayer(bool run = false, string outputPath = "")
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            outputPath = EditorUtility.SaveFolderPanel("Choose Location of the Built Game", "", "");
            if (outputPath.Length == 0)
                return;

#if UNITY_EDITOR_OSX
			outputPath += "/Package";
#endif
        }


        string[] levels = GetLevelsFromBuildSettings();
        if (levels.Length == 0)
        {
            Debug.Log("Nothing to build.");
            return;
        }

        string targetName = GetBuildTargetName(EditorUserBuildSettings.activeBuildTarget);
        if (targetName == null)
            return;


        // Build and copy AssetBundles.
        BuildAssetBundles();

        //string outPath = Path.Combine(Application.streamingAssetsPath, kAssetBundlesOutputPath);
        //CopyAssetBundlesTo(outPath);

        BuildOptions option = BuildOptions.None;

        if (EditorUserBuildSettings.development)
        {
            option |= BuildOptions.Development;
        }

        if (EditorUserBuildSettings.allowDebugging)
        {
            option |= BuildOptions.AllowDebugging;
        }

        if (EditorUserBuildSettings.connectProfiler)
        {
            option |= BuildOptions.ConnectWithProfiler;
        }

        if (EditorUserBuildSettings.connectProfiler)
        {
            option |= BuildOptions.ConnectWithProfiler;
        }

        if (run)
        {
            option |= BuildOptions.AutoRunPlayer;
        }
        BuildPipeline.BuildPlayer(levels, outputPath + targetName, EditorUserBuildSettings.activeBuildTarget, option);
    }


    public static string GetBuildTargetName(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.Android:
                return "/7G.apk";
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return "/7G.exe";
            case BuildTarget.StandaloneOSXIntel:
            case BuildTarget.StandaloneOSXIntel64:
            case BuildTarget.StandaloneOSX:
                return "/7G.app";
            case BuildTarget.iOS:
                return "";
            // Add more build targets for your own.
            default:
                Debug.Log("Target not implemented.");
                return null;
        }
    }

    static void CopyAssetBundlesTo(string outputPath)
    {
        // Clear streaming assets folder.
        FileUtil.DeleteFileOrDirectory(Application.streamingAssetsPath);
        Directory.CreateDirectory(outputPath);

        string outputFolder =
        AssetBundleManager.GetPlatformFolderForAssetBundles(EditorUserBuildSettings.activeBuildTarget);

        // Setup the source folder for assetbundles.
        var source = Path.Combine(Path.Combine(Environment.CurrentDirectory, kAssetBundlesOutputPath), outputFolder);
        if (!Directory.Exists(source))
            Debug.Log("No assetBundle output folder, try to build the assetBundles first.");

        // Setup the destination folder for assetbundles.
        var destination = Path.Combine(outputPath, outputFolder);
        if (Directory.Exists(destination))
            FileUtil.DeleteFileOrDirectory(destination);

        FileUtil.CopyFileOrDirectory(source, destination);
    }

    static string[] GetLevelsFromBuildSettings()
    {
        List<string> levels = new List<string>();
        for (int i = 0; i < EditorBuildSettings.scenes.Length; ++i)
        {
            if (EditorBuildSettings.scenes[i].enabled)
                levels.Add(EditorBuildSettings.scenes[i].path);
        }

        return levels.ToArray();
    }


    [MenuItem("Assets/Get AssetBundle names")]
    static void GetNames()
    {
        var names = AssetDatabase.GetAllAssetBundleNames();
        foreach (var name in names)
            Debug.Log("AssetBundle: " + name);
    }
}
