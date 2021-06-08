using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR	
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Object = UnityEngine.Object;
using UnityEngine.Networking;
using System.Net;
using Newtonsoft.Json;
using System.Diagnostics;

public enum BundleNameEnum
{
    ui,
    effect,
    sound,
    configtable,
    font,
    scene,
    atlas,
    obj,
}
// Loaded assetBundle contains the references count which can be used to unload dependent assetBundles automatically.
public class LoadedAssetBundle
{
    public AssetBundle m_AssetBundle;
    public int m_ReferencedCount;

    public LoadedAssetBundle(AssetBundle assetBundle)
    {
        m_AssetBundle = assetBundle;
        m_ReferencedCount = 1;
    }
}

// Class takes care of loading assetBundle and its dependencies automatically, loading variants automatically.

public class AssetBundleManager : MonoBehaviour
{
    static string[] m_Variants = { };
    static public AssetBundleManifest m_RemoteBundleManifest = null;
    static public AssetBundleManifest m_LocalBundleManifest = null;

    static public AssetBundleManifest m_AssetBundleManifest
    {
        get
        {
            //AssetBundleManifest manifest = m_RemoteBundleManifest != null ? m_RemoteBundleManifest : m_LocalBundleManifest;
            AssetBundleManifest manifest = m_LocalBundleManifest;

            if (m_RemoteBundleManifest != null && RemoteVersion > Version)
            {
                manifest = m_RemoteBundleManifest;
            }
            return manifest;
        }

    }  
    public static readonly float TimeOut = 3.0f;
    public static readonly int Version = 01010050;
    public static int RemoteVersion { get; private set; }
    public const string kAssetBundlesPath = "/AssetBundles/";
    public const string versionName = "version.bundle";
    public static string serverURL = "http://127.0.0.1:80";
    public const string tempVersionName = "tempVersion.bundle";
#if UNITY_EDITOR
    static int m_SimulateAssetBundleInEditor = -1;
    const string kSimulateAssetBundles = "SimulateAssetBundles";
#endif

    static Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
    static Dictionary<string, AssetBundleCreateRequest> m_DownloadingWWWs = new Dictionary<string, AssetBundleCreateRequest>();
    /// <summary>
    /// 对下载中的bundle引用计数
    /// </summary>
    static Dictionary<string, int> m_DownloadingCount = new Dictionary<string, int>();
    static Dictionary<string, string> m_DownloadingErrors = new Dictionary<string, string>();
    static List<AssetBundleLoadOperation> m_InProgressOperations = new List<AssetBundleLoadOperation>();
    static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();
   // static Dictionary<string, List<Action<UIAtlas>>> m_LoadedCallBack = new Dictionary<string, List<Action<UIAtlas>>>();

    /// <summary>
    /// 图集缓存池
    /// </summary>
  //  private Dictionary<string, UIAtlas> atlasPools = new Dictionary<string, UIAtlas>();


    private static AssetBundleManager _Instance;
    public static GameObject LoadingObject;
    public static AssetBundleManager Instance
    {
 
        get
        {
            if (_Instance == null)
            {
                var go = new GameObject("AssetBundleManager", typeof(AssetBundleManager));
                _Instance = go.GetComponent<AssetBundleManager>();
                DontDestroyOnLoad(go);
            }
            return _Instance;
        }

        private set
        {
            _Instance = value;
        }
    }

    // Variants which is used to define the active variants.
    public static string[] Variants
    {
        get { return m_Variants; }
        set { m_Variants = value; }
    }


#if UNITY_EDITOR
    // Flag to indicate if we want to simulate assetBundles in Editor without building them actually.
    public static bool SimulateAssetBundleInEditor
    {
        get
        {
            if (m_SimulateAssetBundleInEditor == -1)
                m_SimulateAssetBundleInEditor = EditorPrefs.GetBool(kSimulateAssetBundles, true) ? 1 : 0;

            return m_SimulateAssetBundleInEditor != 0;
        }
        set
        {
            int newValue = value ? 1 : 0;
            if (newValue != m_SimulateAssetBundleInEditor)
            {
                m_SimulateAssetBundleInEditor = newValue;
                EditorPrefs.SetBool(kSimulateAssetBundles, value);
            }
        }
    }
#endif


    public static string GetStreamingAssetsPath(bool hasProtocol = true)
    {
        if (Application.isEditor)
            return (hasProtocol ? "file://" : "") + Environment.CurrentDirectory.Replace("\\", "/") + "/Assets/StreamingAssets"; // Use the build output folder directly.
        else if (Application.platform == RuntimePlatform.IPhonePlayer)
            return (hasProtocol ? "file://" : "") + Application.streamingAssetsPath;

        else if (Application.platform == RuntimePlatform.Android)
            return "jar:file://" + Application.dataPath + "!/assets";

        else // For standalone player.
            return (hasProtocol ? "file://" : "") + Application.streamingAssetsPath;

    }

    private static string GetPersistentDataPath()
    {
        if (Application.isEditor)
            return Environment.CurrentDirectory.Replace("\\", "/") + "/PersistentDataPath"; // Use the build output folder directly.

        if (Application.platform == RuntimePlatform.IPhonePlayer)
            return Application.persistentDataPath;

        else if (Application.platform == RuntimePlatform.Android)
            return Application.persistentDataPath;

        else // For standalone player.
            return Application.persistentDataPath;
    }

    /// <summary>
    /// 这个地址使用 --- www.load 路径中带有：file://
    /// </summary>
    public static string StreamingassetsURL
    {
        get
        {
            return GetStreamingAssetsPath() + kAssetBundlesPath + GetPlatformName() + "/";
        }
    }


    /// <summary>
    /// 这里个地址使用 --- AssetBundle.LoadFromeFile();路径中没有file://
    /// </summary>
    public static string StreamingassetsURLnOProtocol
    {
        get
        {
            return GetStreamingAssetsPath(false) + kAssetBundlesPath + GetPlatformName() + "/";
        }
    }

    public static string PersistentURL
    {
        get
        {
            return GetPersistentDataPath() + kAssetBundlesPath + GetPlatformName() + "/";
        }
    }

    public static string ServerassetsURL
    {
        get
        {
            return serverURL + kAssetBundlesPath + GetPlatformName() + "/";
        }
    }

    // Get loaded AssetBundle, only return vaild object when all the dependencies are downloaded successfully.
    static public LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
    {
        if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
            return null;

        LoadedAssetBundle bundle = null;
        m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
        if (bundle == null)
            return null;

        // No dependencies are recorded, only the bundle itself is required.
        string[] dependencies = null;
        if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
            return bundle;

        // Make sure all dependencies are loaded
        foreach (var dependency in dependencies)
        {
            if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
                return bundle;

            // Wait all the dependent assetBundles being loaded.
            LoadedAssetBundle dependentBundle;
            m_LoadedAssetBundles.TryGetValue(dependency, out dependentBundle);
            if (dependentBundle == null)
                continue;
                //return null;
        }

        return bundle;
    }

    // Load AssetBundle and its dependencies.
    static protected void LoadAssetBundle(string assetBundleName)
    {
#if UNITY_EDITOR
        // If we're in Editor simulation mode, we don't have to really load the assetBundle and its dependencies.
        if (SimulateAssetBundleInEditor)
            return;
#endif


        if (m_AssetBundleManifest == null)
        {
            //DebugLogHelper.Instance.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
            return;
        }

        // Check if the assetBundle has already been processed.
        bool isAlreadyProcessed = LoadAssetBundleInternal(assetBundleName);
        //Load dependencies.
        if (!isAlreadyProcessed)
            LoadDependencies(assetBundleName);
    }

    // Remaps the asset bundle name to the best fitting asset bundle variant.
    static protected string RemapVariantName(string assetBundleName)
    {
        string[] bundlesWithVariant = m_AssetBundleManifest.GetAllAssetBundlesWithVariant();

        // If the asset bundle doesn't have variant, simply return.
        if (Array.IndexOf(bundlesWithVariant, assetBundleName) < 0)
            return assetBundleName;

        string[] split = assetBundleName.Split('.');

        int bestFit = Int32.MaxValue;
        int bestFitIndex = -1;
        // Loop all the assetBundles with variant to find the best fit variant assetBundle.
        for (int i = 0; i < bundlesWithVariant.Length; i++)
        {
            string[] curSplit = bundlesWithVariant[i].Split('.');
            if (curSplit[0] != split[0])
                continue;

            int found = Array.IndexOf(m_Variants, curSplit[1]);

            // If there is no active variant found. We still want to use the first 
            if (found == -1)
                found = Int32.MaxValue - 1;

            if (found < bestFit)
            {
                bestFit = found;
                bestFitIndex = i;
            }
        }

        if (bestFit == Int32.MaxValue - 1)
        {
            UnityEngine.Debug.LogWarning("Ambigious asset bundle variant chosen because there was no matching active variant: " + bundlesWithVariant[bestFitIndex]);
        }

        if (bestFitIndex != -1)
            return bundlesWithVariant[bestFitIndex];
        else
            return assetBundleName;
    }


    static protected bool CanLoadAssetBundleInternal(string assetBundleName)
    {
        LoadedAssetBundle bundle = null;
        m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
        if (bundle != null)
        {
            bundle.m_ReferencedCount++;
            return true;
        }
        string url = StreamingassetsURLnOProtocol + assetBundleName;
        if (m_RemoteBundleManifest != null && !m_RemoteBundleManifest.GetAssetBundleHash(assetBundleName).Equals(m_LocalBundleManifest.GetAssetBundleHash(assetBundleName)))
        {
            string tempUrl = PersistentURL + assetBundleName;
            if (File.Exists(tempUrl))
            {
                url = "file://" + tempUrl;
               // DebugLogHelper.Instance.Log("Use persistent source:" + assetBundleName);
            }
        }
        return false;
    }


    // Where we actuall call WWW to download the assetBundle.
    static protected bool LoadAssetBundleInternal(string assetBundleName)
    {
        // Already loaded.
        LoadedAssetBundle bundle = null;
        m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
        if (bundle != null)
        {         
            bundle.m_ReferencedCount++;
           // Debug.Log(assetBundleName + "_已经下载 引用计数加一：" + bundle.m_ReferencedCount);
            return true;
        }
        // @TODO: Do we need to consider the referenced count of WWWs?
        // In the demo, we never have duplicate WWWs as we wait LoadAssetAsync()/LoadLevelAsync() to be finished before calling another LoadAssetAsync()/LoadLevelAsync().
        // But in the real case, users can call LoadAssetAsync()/LoadLevelAsync() several times then wait them to be finished which might have duplicate WWWs.
        if (m_DownloadingWWWs.ContainsKey(assetBundleName))
        {
            //引用加一
            m_DownloadingCount[assetBundleName]++;
           // Debug.Log(assetBundleName + "_下载中 引用计数加一：" + m_DownloadingCount[assetBundleName]);
            return true;
        }

        //WWW download = null;
        //  string url = StreamingassetsURL + assetBundleName;
        string url = StreamingassetsURLnOProtocol + assetBundleName;       
        //去数据持久化路径取数据 不在本地StreamimgAssets 取
        //  if (m_RemoteBundleManifest != null && !m_RemoteBundleManifest.GetAssetBundleHash(assetBundleName).Equals(m_LocalBundleManifest.GetAssetBundleHash(assetBundleName)))
        //  {

        string  tempUrl = PersistentURL + assetBundleName;
        //DebugLogHelper.Instance.Log("StreamingassetsURLnOProtocol:" + tempUrl);
        if (File.Exists(tempUrl))
        {       
            url = tempUrl;
            //DebugLogHelper.Instance.Log("Use persistent source:" + assetBundleName);
        }
       // Debug.Log("StreamingassetsURLnOProtocol:" + url);
        //  }
        // download = WWW.LoadFromCacheOrDownload(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), 0); 
        // download = new WWW(url);
        //UnityWebRequest request = UnityWebRequest.GetAssetBundle(url);
       // UnityWebRequestAsyncOperation asyncOperation=  request.SendWebRequest();
       // DebugLogHelper.Instance.Log("UnityWebRequestAsyncOperation:" + url);
        AssetBundleCreateRequest requset = AssetBundle.LoadFromFileAsync(url);
        m_DownloadingWWWs.Add(assetBundleName, requset);
        m_DownloadingCount.Add(assetBundleName,1);
        //Debug.Log(assetBundleName + "_加入下载队列 引用计数加一：");
        return false;
    }


    static protected bool LoadAssetBundleInternalSyn(string assetBundleName)
    {
        LoadedAssetBundle bundle = null;
        m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
      
        if (bundle != null)
        {
           
            bundle.m_ReferencedCount++;
            return true;
        }
        string url = StreamingassetsURLnOProtocol + assetBundleName;
        if (m_RemoteBundleManifest != null && !m_RemoteBundleManifest.GetAssetBundleHash(assetBundleName).Equals(m_LocalBundleManifest.GetAssetBundleHash(assetBundleName)))
        {
            string tempUrl = PersistentURL + assetBundleName;
            if (File.Exists(tempUrl))
            {
                url = "file://" + tempUrl;
              //  DebugLogHelper.Instance.Log("Use persistent source:" + assetBundleName);
            }
        }
        AssetBundle ab = AssetBundle.LoadFromFile(url);
        m_LoadedAssetBundles.Add(assetBundleName, new LoadedAssetBundle(ab));
        return true;
    }

    /// <summary>
    /// 同步加载依赖关系
    /// </summary>
    /// <param name="assetBundleName"></param>
    static protected void LoadDependeciesSyn(string assetBundleName,Action callBack)
    {
        if (m_AssetBundleManifest == null)
        {
            //DebugLogHelper.Instance.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
            return;
        }

        string[] dependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
        if (dependencies.Length == 0)
            return;

        for (int i = 0; i < dependencies.Length; i++)
            dependencies[i] = RemapVariantName(dependencies[i]);

        m_Dependencies.Add(assetBundleName, dependencies);
        for (int i = 0; i < dependencies.Length; i++)
        {
            LoadAssetBundleInternalSyn(dependencies[i]);
        }
        if (callBack != null)
            callBack();
    }

    // Where we get all the dependencies and load them all.
    static protected void LoadDependencies(string assetBundleName)
    {
        if (m_AssetBundleManifest == null)
        {
           // DebugLogHelper.Instance.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
            return;
        }
        // Get dependecies from the AssetBundleManifest object..
        string[] dependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
        if (dependencies.Length == 0)
            return;

        for (int i = 0; i < dependencies.Length; i++)
            dependencies[i] = RemapVariantName(dependencies[i]);

        // Record and load all dependencies.
        m_Dependencies.Add(assetBundleName, dependencies);
        //Debug.Log("---------------bundle："+ assetBundleName);
        for (int i = 0; i < dependencies.Length; i++)
        {
            //Debug.Log("bundle依赖："+ dependencies[i]);
            LoadAssetBundleInternal(dependencies[i]);
            // DebugLogHelper.Instance.Log("load dependency:" + dependencies[i]);
        }
    }

    // Unload assetbundle and its dependencies.
    static public void UnloadAssetBundle(string assetBundleName, bool forceUnload = false)
    {
#if UNITY_EDITOR
        // If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
        if (SimulateAssetBundleInEditor)
            return;
#endif

        UnityEngine.Debug.Log(m_LoadedAssetBundles.Count + " assetbundle(s) in memory before unloading " + assetBundleName);

        UnloadAssetBundleInternal(assetBundleName, forceUnload);
        UnloadDependencies(assetBundleName,forceUnload);

        UnityEngine.Debug.Log(m_LoadedAssetBundles.Count + " assetbundle(s) in memory after unloading " + assetBundleName);
    }

    static protected void UnloadDependencies(string assetBundleName)
    {
        string[] dependencies = null;
        if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
            return;

        // Loop dependencies.
        foreach (var dependency in dependencies)
        {
            UnloadAssetBundleInternal(dependency);
        }

        m_Dependencies.Remove(assetBundleName);
    }
    static protected void UnloadDependencies(string assetBundleName,bool forceUnload=false)
    {
        string[] dependencies = null;
        if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
            return;

        // Loop dependencies.
        foreach (var dependency in dependencies)
        {
            UnloadAssetBundleInternal(dependency,forceUnload);
        }

        m_Dependencies.Remove(assetBundleName);
    }
    static protected void UnloadAssetBundleInternal(string assetBundleName, bool forceUnload = false)
    {
        string error;
        LoadedAssetBundle bundle = GetLoadedAssetBundle(assetBundleName, out error);
        if (bundle == null)
            return;

        //if (forceUnload)
        //{
        //    bundle.m_ReferencedCount = 1;
        //}

        if (--bundle.m_ReferencedCount == 0)
        {
            bundle.m_AssetBundle.Unload(forceUnload);
            m_LoadedAssetBundles.Remove(assetBundleName);
           UnityEngine. Debug.Log("AssetBundle " + assetBundleName + " has been unloaded successfully");
        }
    }

    void Update()
    {
        // Collect all the finished WWWs.
        var keysToRemove = new List<string>();
        foreach (var keyValue in m_DownloadingWWWs)
        {
            AssetBundleCreateRequest download = keyValue.Value;

            // If downloading fails.
    //        if (download.error != null)
    //        {
				//DebugLogHelper.Instance.LogError (string.Format("Failed downloading bundle {0} from {1}: {2}", keyValue.Key, download.url, download.error));
    //            m_DownloadingErrors.Add(keyValue.Key, string.Format("Failed downloading bundle {0} from {1}: {2}", keyValue.Key, download.url, download.error));
    //            keysToRemove.Add(keyValue.Key);
    //            continue;
    //        }

            // If downloading succeeds.
            if (download.isDone)
            {
               // DebugLogHelper.Instance.Log("download.assetBundle："+ keyValue.Key);
                AssetBundle bundle = download.assetBundle;//DownloadHandlerAssetBundle.GetContent(download.assetBundle);
                //DebugLogHelper.Instance.Log("download：" + bundle.name+"_url:"+ download.assetBundle);
                if (bundle == null)
                {
                   // Main.str = string.Format("{0} is not a valid asset bundle.", keyValue.Key);
                    //DebugLogHelper.Instance.LogError (string.Format("{0} is not a valid asset bundle.", keyValue.Key));
                    m_DownloadingErrors.Add(keyValue.Key, string.Format("{0} is not a valid asset bundle.", keyValue.Key));
                    keysToRemove.Add(keyValue.Key);
                    continue;
                }

                //Debug.Log("Downloading " + keyValue.Key + " is done at frame " + Time.frameCount);
                m_LoadedAssetBundles.Add(keyValue.Key, new LoadedAssetBundle(bundle));
                m_LoadedAssetBundles[keyValue.Key].m_ReferencedCount = m_DownloadingCount[keyValue.Key];
                keysToRemove.Add(keyValue.Key);
            }
        }

        // Remove the finished WWWs.
        foreach (var key in keysToRemove)
        {
            // AssetBundleCreateRequest download = m_DownloadingWWWs[key];
            AssetBundleCreateRequest download = m_DownloadingWWWs[key];
            m_DownloadingCount.Remove(key);
            m_DownloadingWWWs.Remove(key);
            download = null;
        }

        // Update all in progress operations
        for (int i = 0; i < m_InProgressOperations.Count;)
        {
            if (!m_InProgressOperations[i].Update())
            {
                m_InProgressOperations.RemoveAt(i);
            }
            else
                i++;
        }
    }

    // Load asset from the given assetBundle.
    static public AssetBundleLoadAssetOperation LoadAssetAsync(string assetBundleName, string assetName, Type type)
    {
        AssetBundleLoadAssetOperation operation = null;
#if UNITY_EDITOR
        if (SimulateAssetBundleInEditor)
        {
            string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, assetName);
            if (assetPaths.Length == 0)
            {
                UnityEngine.Debug.LogError("There is no asset with name \"" + assetName + "\" in " + assetBundleName);
                return null;
            }
            UnityEngine.Debug.Log(" UNITY_EDITOR LoadAssetAsync:"+ assetPaths[0]);
            // @TODO: Now we only get the main object from the first asset. Should consider type also.
            Object target = AssetDatabase.LoadMainAssetAtPath(assetPaths[0]);
            operation = new AssetBundleLoadAssetOperationSimulation(target);
        }
        else
#endif
        {
            assetBundleName = RemapVariantName(assetBundleName);
            LoadAssetBundle(assetBundleName);
            operation = new AssetBundleLoadAssetOperationFull(assetBundleName, assetName, type);

            m_InProgressOperations.Add(operation);
        }

        return operation;
    }

    // Load level from the given assetBundle.
    static public AssetBundleLoadOperation LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive)
    {
        AssetBundleLoadOperation operation = null;
#if UNITY_EDITOR
        if (SimulateAssetBundleInEditor)
        {
            operation = new AssetBundleLoadLevelSimulationOperation(assetBundleName, levelName, isAdditive);
        }
        else
#endif
        {
            assetBundleName = RemapVariantName(assetBundleName);
            LoadAssetBundle(assetBundleName);
            operation = new AssetBundleLoadLevelOperation(assetBundleName, levelName, isAdditive);

            m_InProgressOperations.Add(operation);
        }

        return operation;
    }



    #region Handle Bundles

    private bool initedFlag = false;
    // Use this for initialization.
    public void Init(Action callback)
    {
        if (initedFlag)
        {
            if (callback != null)
            {
                callback();
            }

            return;
        }

 

        StartCoroutine(Initialize(() =>
        {
            initedFlag = true;
            if (callback != null)
            {
                callback();
            }
        }));
    }

    // Initialize the downloading url and AssetBundleManifest object.
    protected IEnumerator Initialize(Action callback)
    {
        // Wait for the Caching system to be ready
        //        while (!Caching.ready)
        //            yield return null;

#if UNITY_EDITOR
        // If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
        if (SimulateAssetBundleInEditor || !Application.isPlaying)
        {
            if (callback != null)
            {
                callback();
            }
            yield break;
        }
#endif
        yield return StartCoroutine(new AssetBundleLoadManifestOperation());


        if (m_LocalBundleManifest != null)
        {
            UnityEngine.Debug.Log("localhash:" + m_LocalBundleManifest.GetAssetBundleHash("configtable"));
        }
        else
        {
            UnityEngine.Debug.Log("m_LocalBundleManifest==nu    ll");
        }


        if (callback != null)
        {
            callback();
        }
    }


    #region Upgrade    
    public IEnumerator CheckUpgrade(Action<bool> callback)
    {
        if(string.IsNullOrEmpty(serverURL))
        {
            if (callback != null)
            {               
                callback(true);
            }
            yield break;
        }
#if UNITY_EDITOR
        //If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
        if (SimulateAssetBundleInEditor || !Application.isPlaying)
        {
            UnityEngine.Debug.Log("editor 模拟");
            if (callback != null)
            {
                callback(true);
            }
            yield break;
        }
#endif


        bool ret = true;
        Action handleAction = () =>
        {
            if (callback != null)
            {
                callback(ret);
            }
        };

        Action failAction = () =>
        {
            //UIDialog.Show("Tip", "Connect to asset server error!", "Retry", "Skip", () =>
            //{
            //    ret = false;
            //    handleAction();
            //},
            //     () =>//cancel,直接跳过不进行版本对比,发布的时候要删除
            //     {
            //         handleAction();
            //     }
            // );

            UnityEngine.Debug.LogError("获取远程manifest失败");

        };

        yield return StartCoroutine(new RemoteBundleLoadManifestOperation());
        if (m_RemoteBundleManifest != null)
        {
            //DebugLogHelper.Instance.Log("remotehash:" + m_RemoteBundleManifest.GetAssetBundleHash("configtable"));
        }
        else//或获取远程manifest失败
        {
            failAction();
            yield break;
        }

        BundleInfo streamingInfo = null;
        BundleInfo persistentInfo = null;
        BundleInfo remoteInfo = null;
        UnityEngine.Debug.Log("local:"+ StreamingassetsURL + versionName);
        UnityEngine.Debug.Log("R:" + ServerassetsURL + versionName);
        if(File.Exists(StreamingassetsURL + versionName))
        {
            yield return StartCoroutine(GetBundleInfo(StreamingassetsURL + versionName, (info) => { streamingInfo = info; }));
        }     
        yield return StartCoroutine(GetBundleInfo(ServerassetsURL + versionName, (info) => {
            remoteInfo = info;
            UnityEngine.Debug.Log(" 服务器 获取版本号"+ info.Version);
        }));
        string persitentVersionPath = PersistentURL + versionName;
        if (streamingInfo == null)
        {
            UnityEngine.Debug.Log("streamingInfo==null");
            if (!File.Exists(persitentVersionPath))
            {
                UnityEngine.Debug.Log("数据持久化路径不存在存在：" + persitentVersionPath);
                yield return StartCoroutine(DownLoadAssetBundle(versionName, null, isOver =>
            {
                UnityEngine.Debug.Log(isOver ? versionName + "下载成功" : versionName + "下载失败");
            }));
            }
            else
            {
                UnityEngine.Debug.Log("数据持久化路径存在："+persitentVersionPath);
            }
        }
        else
        {
            UnityEngine.Debug.Log("streamingInfo 不为空 存在 ："+ streamingInfo.Version);
        }
        string streamingInfoVerson = streamingInfo == null ? "Streamingassets 不存在资源包" : streamingInfo.Version.ToString();
        UnityEngine.Debug.Log("streamingInfo.Version:" + streamingInfoVerson +"remoteInfo.Version:"+ remoteInfo.Version);
        if (!File.Exists(persitentVersionPath))
        {
            persistentInfo = streamingInfo;
            UnityEngine.Debug.Log("创建 persitentVersionPath："+persistentInfo.Version);
            yield return CopyFile(StreamingassetsURL + versionName, PersistentURL + versionName);
        }

        else
        {
            UnityEngine.Debug.Log("数据持久化路径不在资源："+ persitentVersionPath);
            yield return StartCoroutine(GetBundleInfo(persitentVersionPath, (info) => { persistentInfo = info; }));
            if (streamingInfo!=null&&streamingInfo.Version > persistentInfo.Version) //安装了新包,必须删除旧资源
            {
                persistentInfo = streamingInfo;
                Directory.Delete(PersistentURL, true);
                UnityEngine.Debug.Log("Delete old assets!");
                yield return CopyFile(StreamingassetsURL + versionName, PersistentURL + versionName); ;
            }
        }

        if (remoteInfo == null)//获取远程版本信息失败
        {
            failAction();
            yield break;
        }
        //版本号相等要检查文件是否完成，不完整要下载
        if (persistentInfo.Version >remoteInfo.Version)//不需要更新
        {
            handleAction();
            //DebugLogHelper.Instance.Log(persistentInfo.Url);
           // DebugLogHelper.Instance.Log("persistentInfo.Version:"+ persistentInfo.Version+ "---- remoteInfo.Version: "+ remoteInfo.Version);
            //DebugLogHelper.Instance.Log("No need to upgrade");
            yield break;
        }

        if (persistentInfo != null)
        {
            //DebugLogHelper.Instance.Log("PersistentInfo:" + persistentInfo.Version);
        }

        if (remoteInfo != null)
        {
            //DebugLogHelper.Instance.Log("RemoteInfo:" + remoteInfo.Version);
        }
        #region 
        if(persistentInfo.Version == remoteInfo.Version&&!File.Exists(PersistentURL+GetPlatformName()))
        {
            yield return StartCoroutine(DownLoadAssetBundle(GetPlatformName(), null, isOver =>
            {
               // DebugLogHelper.Instance.Log(isOver ? "AssetBundleManifest更新成功" : "AssetBundleManifest更新失败");
            }));
        }
              
        #endregion
        int localA = 0, localB = 0, localC = 0;
        int remoteA = 0, remoteB = 0, remoteC = 0;
        SplitVersion(persistentInfo.Version, out localA, out localB, out localC);
        SplitVersion(remoteInfo.Version, out remoteA, out remoteB, out remoteC);
        RemoteVersion = remoteInfo.Version;
        if (remoteA > localA)
        {
            //UIDialog.Show("", "Your game version is older than offical version, please go to app store for newest version.", "GO", "",
            //    () =>
            //    {
            //        Application.OpenURL(remoteInfo.Url);
            //        Application.Quit();
            //    });
            yield break;
        }

        if (remoteB >= localB)
        {

            Dictionary<string, double> diffList = CollectDiffList(persistentInfo, remoteInfo);
            int count = diffList.Count;
            List<string> bundls = new List<string>(diffList.Keys);

            double totleSize = 0;

            foreach (var item in diffList)
            {
                totleSize += item.Value;
            }

            totleSize = totleSize / (1024 * 1024);

          yield return  StartCoroutine(DownloadDiffList(persistentInfo, remoteInfo, callback));

            //UIDialog.Show("", string.Format("You need to download resource package for better gameplay experience.\n[00ffFF]Pacage Size:" + totleSize + "MB.[-]\n[Wifi recommended]", remoteA, remoteB, remoteC), "DOWN", "CANCEL",
            //    () =>
            //    {
            //        StartCoroutine(DownloadDiffList(persistentInfo, remoteInfo, callback));
            //    }, () =>
            //    {
            //        handleAction();
            //    });
            yield break;
        }

        if (remoteC >= localC)//小版本直接更新不提示
        {
            StartCoroutine(DownloadDiffList(persistentInfo, remoteInfo, callback));
            yield break;
        }
    }

    static void SplitVersion(int version, out int a, out int b, out int c)
    {
        a = version / 1000000;
        b = (version / 10000) % 100;
        c = version % 10000;
    }

    public static string GetVersion()
    {
        int localA, localB, localC;
        int version = Mathf.Max(RemoteVersion, Version);
        SplitVersion(version, out localA, out localB, out localC);

        return string.Format("Game version:{0}.{1}.{2}", localA, localB, localC);
    }

    IEnumerator CopyFile(string src, string dest)
    {
        WWW readWWW = new WWW(src);
        yield return readWWW;
        byte[] data = readWWW.bytes;

        string dir = Path.GetDirectoryName(dest);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        FileStream fs = new FileStream(dest, FileMode.Create);
        fs.Write(data, 0, data.Length);//开始写入     
        fs.Flush(); //清空缓冲区、关闭流
        fs.Close();
        readWWW.Dispose();
    }

    //float 下载进度, bool 下载成功与否
    IEnumerator DownLoadAssetBundle(string bundleName, Action<float> progressCallback, Action<bool> complteCallback)
    {
        WWW downloadWww = new WWW(ServerassetsURL + bundleName);

        bool flag = true;
        while (!downloadWww.isDone)
        {
            if (progressCallback != null)
            {
                progressCallback(downloadWww.progress);
            }

            if (!string.IsNullOrEmpty(downloadWww.error))
            {
               // DebugLogHelper.Instance.LogError("Download bundle error:" + bundleName);
                flag = false;
                break;
            }

            yield return new WaitForSeconds(0.2f);
        }

        if (downloadWww.progress < 1 || !string.IsNullOrEmpty(downloadWww.error))//出错
        {
            flag = false;
        }

        if (!flag)//下载出错
        {
            if (complteCallback != null)
            {
                complteCallback(false);
            }
        }

        else
        {
            if (progressCallback != null)
            {
                progressCallback(1.0f);
            }

            byte[] data = downloadWww.bytes;
            string dest = PersistentURL + bundleName;
            string dir = Path.GetDirectoryName(dest);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            FileStream fs = new FileStream(dest, FileMode.Create);
            fs.Write(data, 0, data.Length);//开始写入     
            fs.Flush(); //清空缓冲区、关闭流
            fs.Close();
            downloadWww.Dispose();


            if (complteCallback != null)
            {
                complteCallback(true);
            }
        }
    }

    void UpdateBundleInfo(BundleInfo info)
    {
        string dir = PersistentURL;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string versionString = JsonConvert.SerializeObject(info);
        FileStream fs = new FileStream(dir + versionName, FileMode.Create);
        byte[] data = new UTF8Encoding().GetBytes(versionString); //获得字节数组
        fs.Write(data, 0, data.Length);//开始写入     
        fs.Flush(); //清空缓冲区、关闭流
        fs.Close();
    }

    Dictionary<string, double> CollectDiffList(BundleInfo localInfo, BundleInfo remoteInfo)
    {
       // List<string> ret = new List<string>();
        Dictionary<string, double> temp = new Dictionary<string, double>();
        foreach (BundleInfoData remoteData in remoteInfo.dataList)
        {
            string remoteHash = remoteData.Hash;
           
            BundleInfoData localData = localInfo.GetData(remoteData.BundleName);
            if (localData == null)
            {
              //  ret.Add(remoteData.BundleName);
                temp.Add(remoteData.BundleName, remoteData.Size);
                continue;
            }

            string localHash = localData.Hash;
            #region 新增 再做更新时如果数据持久化路径不存改bundle需要下载

            string dest = PersistentURL + remoteData.BundleName;
            string dir = Path.GetFullPath(dest);          
             bool isHave=File.Exists(dir);
           // DebugLogHelper.Instance.Log("PersistentURL :" + dir + "____isHave:" + isHave);
            #endregion 
            if (!remoteHash.Equals(localHash, StringComparison.CurrentCultureIgnoreCase)||!isHave)
            {
                //ret.Add(remoteData.BundleName);
                if (temp.ContainsKey(remoteData.BundleName) == false)
                 temp.Add(remoteData.BundleName, remoteData.Size);
            }
            
        }
        return temp;
    }


    IEnumerator DownloadDiffList(BundleInfo localInfo, BundleInfo remoteInfo, Action<bool> callback)
    {
        Dictionary<string,double> diffList = CollectDiffList(localInfo, remoteInfo);
        int count = diffList.Count;
        
        int a, b, c;
        SplitVersion(localInfo.Version, out a, out b, out c);
        string version = string.Format("Version {0}.{1}.{2}", a, b, c);

        List<string> bundls = new List<string>(diffList.Keys);

        double totleSize = 0;

        double currentSize = 0;
        foreach (var item in diffList)
        {
            totleSize += item.Value;
        }

        totleSize = totleSize / (1024*1024);

        string downPrompt = "Downloading Resource Pack";

       // string sizeValue = "";

       // string progressTxt = "";

        float pregressValue = 0;

        List<string> bundlePath = new List<string>();
        for (int i = 0; i < count;)
        {
            float eachProgress = (float)1 / count;
            string bundleName = bundls[i];
            double size = diffList[bundleName];
            yield return StartCoroutine(DownLoadAssetBundle(bundleName, (percent) =>
            {

                float currentProgress = pregressValue + eachProgress * percent;

                //string totalProgress = string.Format("{0}/{1}", (i + 1), count);
                //UIMgr.ShowLoading(currentProgress, string.Format("Downloading......{0}", totalProgress), version);

                double temp = Math.Round(( size * currentProgress+currentSize) / (1024 * 1024), 1);//TODO:计算方式有问题

               // sizeValue = "(" + temp + "/" + Math.Round(totleSize, 1) + "M)";

                currentProgress = (float)(temp / Math.Round(totleSize, 1));
                // UIAssetLoading.Ins.Loading(currentProgress);
                // progressTxt = String.Format("{0:F}", currentProgress*100) + "%";

                UnityEngine.Debug.Log(currentProgress + "========" + temp + "==========" + bundleName + "========" + i);
                // UIAssetLoading.ins.Loading(currentProgress);
                //UIMgr.Instance.ShowLoading(currentProgress, downPrompt, (float)totleSize);

            }, (downloadRet) =>
            {
                if (downloadRet)
                {
                    i++;
                    currentSize +=size;
                    pregressValue = (float) i / count;
                }

            }));
        }

      //  UIMgr.Instance.ShowLoading(1, "Downloading Resource Pack ");
        UpdateBundleInfo(remoteInfo);


        if (callback != null)
        {
            callback(true);
        }
    }

    #endregion


    IEnumerator GetBundleInfo(string url, Action<BundleInfo> callback)
    {
        BundleInfo ret = null;
        Action handleCb = () =>
        {
            if (callback != null)
            {
                callback(ret);
            }

        };
        string text;

        if (url.Contains("://"))
        {
            float startTime = Time.realtimeSinceStartup;
            WWW download = new WWW(url);
            while (!download.isDone)
            {
                if (Time.realtimeSinceStartup - startTime >= startTime)//下载超时
                {
                    handleCb();
                    yield break;
                }

                yield return new WaitForSeconds(0.2f);
            }

            if (download.error != null || download.progress < 1.0f)
            {
                handleCb();
                yield break;
            }
            text = download.text;
            download.Dispose();
        }
        else
        {
            text = File.ReadAllText(url);
        }

        ret = JsonConvert.DeserializeObject<BundleInfo>(text);
        handleCb();
    }

#if UNITY_EDITOR
    public static string GetPlatformFolderForAssetBundles(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.Android:
                return "Android";
            case BuildTarget.iOS:
                return "iOS";
            case BuildTarget.WebGL:
                return "WebPlayer";
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return "Windows";
            case BuildTarget.StandaloneOSXIntel:
            case BuildTarget.StandaloneOSXIntel64:
            case BuildTarget.StandaloneOSX:
                return "OSX";
            // Add more build targets for your own.
            // If you add more targets, don't forget to add the same platforms to GetPlatformFolderForAssetBundles(RuntimePlatform) function.
            default:
                return null;
        }
    }
#endif

    static string GetPlatformFolderForAssetBundles(RuntimePlatform platform)
    {
        switch (platform)
        {
            case RuntimePlatform.Android:
                return "Android";
            case RuntimePlatform.IPhonePlayer:
                return "iOS";
            case RuntimePlatform.WebGLPlayer:
                return "WebPlayer";
            case RuntimePlatform.WindowsPlayer:
                return "Windows";
            case RuntimePlatform.OSXPlayer:
                return "OSX";
            // Add more build platform for your own.
            // If you add more platforms, don't forget to add the same targets to GetPlatformFolderForAssetBundles(BuildTarget) function.
            default:
                return null;
        }
    }

    public static string GetPlatformName()
    {
#if UNITY_EDITOR
        return GetPlatformFolderForAssetBundles(EditorUserBuildSettings.activeBuildTarget);
#else
		return GetPlatformFolderForAssetBundles(Application.platform);
#endif     
    }

    protected IEnumerator Load<T>(string assetBundleName, string assetName, Action<T> callback) where T : Object
    {
        // Load asset from assetBundle.
        AssetBundleLoadAssetOperation request = LoadAssetAsync(assetBundleName, assetName, typeof(T));
        if (request == null)
        {
            yield break;
        }

        yield return StartCoroutine(request);

        // Get the asset.
        T prefab = request.GetAsset<T>();
        if (callback != null)
        {
            callback(prefab);
        }

    }


    protected IEnumerator _LoadLevel(string assetBundleName, string levelName, bool isAdditive, Action callback)
    {
        // Load level from assetBundle.
        AssetBundleLoadOperation request = LoadLevelAsync(assetBundleName, levelName, isAdditive);
        if (request == null)
            yield break;
        yield return StartCoroutine(request);
      //  DebugLogHelper.Instance.Log("-----------------_LoadLevel2:" + levelName);
        if (callback != null)
        {
            callback();
        }
    }

     IEnumerator LoadAsyncCoroutine(string path, Action<AssetBundle> callback)
    {
        AssetBundleCreateRequest abcr = AssetBundle.LoadFromFileAsync(path);
        yield return abcr;
        callback(abcr.assetBundle);
    }


    IEnumerator HandleLoadBundle(AssetBundleLoadAssetOperation operation, Action callback)
    {
        yield return StartCoroutine(operation);
        if (callback != null)
        {
            callback();
        }
    }

    //返回bundle的所有文件名    
    public void GetBundleAssets(string assetBundleName, Action<string[]> callback)
    {
        AssetBundleLoadAssetOperation operation = null;
        string[] assetPaths = null;
#if UNITY_EDITOR
        if (SimulateAssetBundleInEditor)
        {
            assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
            if (callback != null)
            {
                callback(assetPaths);
            }
            return;
        }
        else
#endif
        {
            LoadAssetBundle(assetBundleName);
            operation = new AssetBundleLoadAssetOperationFull(assetBundleName, null, typeof(Object));

            m_InProgressOperations.Add(operation);
        }


        StartCoroutine(HandleLoadBundle(operation, () =>
        {
            string errorString;
            LoadedAssetBundle bundle = GetLoadedAssetBundle(assetBundleName, out errorString);
            if (bundle != null)
            {
                assetPaths = bundle.m_AssetBundle.GetAllAssetNames();

            }

            if (callback != null)
            {
                callback(assetPaths);
            }

        }));

    }

    public void LoadLevel(string assetBundleName, string levelName, bool isAdditive, Action callback)
    {
        StartCoroutine(_LoadLevel(assetBundleName, levelName, isAdditive, () =>
        {
            //DebugLogHelper.Instance.Log("_LoadLevel:"+ levelName);
            if (callback != null)
            {
                callback();
            }

        }));
    }

//     public void LoadAtlas(string assetBundleName, string assetName, Action<UIAtlas> callback)
//     {
// 
//         if (atlasPools.ContainsKey(assetName))
//         {
//             if(callback!=null)
//             {
//                 callback(atlasPools[assetName]);
//             }
//             return;
//         }
// 
//         if (!m_LoadedCallBack.ContainsKey(assetBundleName))
//         {
//             List<Action<UIAtlas>> list = new List<Action<UIAtlas>>();
//             list.Add(callback);
//             m_LoadedCallBack.Add(assetBundleName, list);
//             StartCoroutine(Load<GameObject>(assetBundleName, assetName, (res) =>
//             {
//                 if (res == null)
//                 {
//                     DebugLogHelper.Instance.LogError("load res is null " + assetName);
//                 }
//                 UIAtlas atlas = res.GetComponent<UIAtlas>();
//                 atlasPools.Add(assetName, atlas);
//                 foreach (Action<UIAtlas> act in m_LoadedCallBack[assetBundleName])
//                 {
//                     if (act != null)
//                     {
//                         if (res != null)
//                             act(atlas);
//                         else
//                             act(null);
//                     }
//                 }
//                 m_LoadedCallBack[assetBundleName].Clear();
//                 m_LoadedCallBack.Remove(assetBundleName);
//             }));
//         }
//         else
//         {
//             if (m_LoadedCallBack[assetBundleName] == null)
//                 m_LoadedCallBack[assetBundleName] = new List<Action<UIAtlas>>();
//             //
//             m_LoadedCallBack[assetBundleName].Add(callback);
//         }
//     }
    //加载资源,仅仅获取bundle中的资源,没有产生gameobject
    public void LoadResource<T>(string assetBundleName, string assetName, Action<T> callback) where T : Object
    {
        StartCoroutine(Load<T>(assetBundleName, assetName, (res) =>
        {
            if (res == null)
            {
                UnityEngine.Debug.LogError("load res is null " + assetName);
            }
            if (callback != null)
            {
                callback(res);
            }
        }));
    }

    //加载资源中的component,没有产生新的gameObject
    public void LoadComponent<T>(string assetBundleName, string assetName, Action<T> callback) where T : Component
    {
        StartCoroutine(Load<GameObject>(assetBundleName, assetName, (go) =>
        {
            T ret = null;
            do
            {
                if (go == null)
                    break;

                ret = go.GetComponent<T>();

            } while (false);

            if (callback != null)
            {
                callback(ret);
            }
        }));
    }
    //产生新的gameobject并返回该gameobject上的component
    public void LoadInstance<T>(string assetBundleName, string assetName, Action<T> callback)
        where T : Component
    {
        StartCoroutine(Load<GameObject>(assetBundleName, assetName, (go) =>
        {
            T ret = null;
            do
            {
                if (go == null)
                    break;

                ret = Instantiate(go).GetComponent<T>();

            } while (false);

            if (callback != null)
            {
                callback(ret);
            }
        }));

    }

    public static string GetRolePackageName(string roleName)
    {
        return "role/" + roleName;
    }

    public void LoadRoleInstance<T>(string roleName, Action<T> callback) where T : Component
    {
        string roleBundleName = GetRolePackageName(roleName).ToLower();
        LoadInstance<T>(roleBundleName, roleName, (role) =>
        {
            // UnloadAssetBundle(roleBundleName);
            //Resources.UnloadUnusedAssets();
            if (callback != null)
            {
                callback(role);
            }
        });
    }


    #endregion



    #region Preload resources

    public void PreloadBundles(BundleNameEnum[] bundleNames, Action<float, string> updateCallback, Action completeCallback, bool loadAll = true)
    {
        StartCoroutine(PreloadResources(bundleNames, updateCallback, completeCallback, loadAll));
    }


    IEnumerator PreloadResources(BundleNameEnum[] bundleNames, Action<float, string> updateCallback, Action completeCallback, bool loadAll = true)
    {
        if (bundleNames == null || bundleNames.Length <= 0)
        {
            if (completeCallback != null)
            {
                completeCallback();
            }
        }

        int index = 0;
        int count = bundleNames.Length;
        float currentPercent = 0;
        string text = "";
        float step = 1.0f / bundleNames.Length;

        Action update = () =>
        {
            if (updateCallback != null)
            {
                updateCallback(currentPercent, text);
            }
        };


        Action callback = () =>
        {
            index++;
            if (index < count)
            {
                text = "Loading [" + bundleNames[index] + "]";
                update();
            }

            else
            {
                if (completeCallback != null)
                {
                    completeCallback();
                }
            }
        };


        text = "Loading [" + bundleNames[index] + "]";
        update();

        while (index < count)
        {
            yield return StartCoroutine(PreloadBundle(bundleNames[index].ToString(), (percent, assetName) =>
            {
                currentPercent = ((float)index) / count + percent * step;
                text = "[" + bundleNames[index] + "]" + Path.GetFileNameWithoutExtension(assetName);

                if (updateCallback != null)
                {
                    updateCallback(currentPercent, text);
                }

            }, callback, bundleNames[index] != BundleNameEnum.ui));


            yield return new WaitForSeconds(0.1f);

        };
        yield return null;
    }


    IEnumerator PreloadBundle(string bundleName, Action<float, string> update, Action callback, bool loadAll = true)
    {
        bool completeFlag = false;

        PreloadInerBundle(bundleName, update, () =>
        {
            completeFlag = true;
        }, loadAll);

        while (!completeFlag)
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (callback != null)
        {
            callback();
        }
    }


    void PreloadInerBundle(string assetBundleName, Action<float, string> update, Action callback, bool loadAll = true)
    {
        GetBundleAssets(assetBundleName, (assetNames) =>
        {

            if (assetNames == null || assetNames.Length <= 0 || !loadAll)
            {
                if (callback != null)
                {
                    callback();
                }
                return;
            }

            int count = assetNames.Length;
            int index = 0;
            Action<Object> cb = (obj) =>
            {
                index++;

                if (update != null)
                {
                    float percent = ((float)index) / count;
                    string currentAssent = assetNames[index - 1];
                    update(percent, currentAssent);
                }


                if (index >= count && callback != null)
                {
                    callback();
                }

            };

            foreach (var assetName in assetNames)
            {
                LoadResource<Object>(assetBundleName, assetName, cb);
            }


        });
    }

    #endregion
} // End of AssetBundleManager.