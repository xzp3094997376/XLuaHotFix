using UnityEngine;
using UnityEditor;
using System.Collections;

public class AssetbundlesMenuItems
{
	const string kSimulateAssetBundlesMenu = "AssetBundles/Simulate AssetBundles";
    static bool isChecked = false;

	[MenuItem(kSimulateAssetBundlesMenu)]
	public static void ToggleSimulateAssetBundle ()
	{        
        AssetBundleManager.SimulateAssetBundleInEditor = !AssetBundleManager.SimulateAssetBundleInEditor;
        Menu.SetChecked(kSimulateAssetBundlesMenu, AssetBundleManager.SimulateAssetBundleInEditor);        
    }

	[MenuItem(kSimulateAssetBundlesMenu, true)]
	public static bool ToggleSimulateAssetBundleValidate ()
	{
        //Debug.Log("check menu set value1  "+(AssetBundleManager.SimulateAssetBundleInEditor).ToString());
      
        //Debug.Log("check menu set value2  " + (AssetBundleManager.SimulateAssetBundleInEditor).ToString());
        return true;
	}
	
	[MenuItem ("AssetBundles/Build AssetBundles")]
	static public void BuildAssetBundles ()
	{
		BuildScript.BuildAssetBundles();
	}

	[MenuItem ("AssetBundles/Build Player")]
	static void BuildPlayer ()
	{
		BuildScript.BuildPlayer();
	}

    [MenuItem("AssetBundles/Build Player and Run")]
    static void BuildPlayerandRun()
    {
        BuildScript.BuildPlayer(true);       
    }

    [MenuItem("AssetBundles/Print Asset path")]
    static void BuildPlayerAsset()
    {       
        string[] paths= AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName("cube", "Cube");
        foreach (var item in paths)
        {
            Debug.Log(item);
        }
    }
}
