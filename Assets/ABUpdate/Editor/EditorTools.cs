using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EditorTools 
{
    [MenuItem("AssetDatabase/Display assets in an AssetBundle")]
    static void ExampleScriptCode1()
    {
        string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName("cube", "cube");
        foreach (var assetPath in assetPaths)
            Debug.Log(assetPath);
    }

    [MenuItem("AssetDatabase/AssetBundle creation")]
    static void ExampleScriptCode2()
    {
        BuildPipeline.BuildAssetBundles("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
    }
}
