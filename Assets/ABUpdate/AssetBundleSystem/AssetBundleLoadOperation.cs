using UnityEngine;
using System.Collections;
using System;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEditor;
//using UnityEditor.SceneManagement;
//using UnityEditor.SceneManagement;

public abstract class AssetBundleLoadOperation : IEnumerator
{
    public object Current
    {
        get
        {
            return null;
        }
    }
    public bool MoveNext()
    {
        return !IsDone();
    }

    public void Reset()
    {
    }

    abstract public bool Update();

    abstract public bool IsDone();
}

#if UNITY_EDITOR
public class AssetBundleLoadLevelSimulationOperation : AssetBundleLoadOperation
{
    AsyncOperation m_Operation = null;


    public AssetBundleLoadLevelSimulationOperation(string assetBundleName, string levelName, bool isAdditive)//
    {
        string[] levelPaths = UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, levelName);
        if (levelPaths.Length == 0)
        {
            ///@TODO: The error needs to differentiate that an asset bundle name doesn't exist
            //        from that there right scene does not exist in the asset bundle...

            Debug.LogError("There is no scene with name \"" + levelName + "\" in " + assetBundleName);
            return;
        }

        if (isAdditive)
            m_Operation = EditorApplication.LoadLevelAdditiveAsyncInPlayMode(levelPaths[0]);
        else
            m_Operation = EditorApplication.LoadLevelAsyncInPlayMode(levelPaths[0]);
    }

    public override bool Update()
    {
        return false;
    }

    public override bool IsDone()
    {
        return m_Operation == null || m_Operation.isDone;
    }
}

#endif

public class AssetBundleLoadLevelOperation : AssetBundleLoadOperation
{
	protected string 				m_AssetBundleName;
	protected string 				m_LevelName;
	protected bool 					m_IsAdditive;
	protected string 				m_DownloadingError;
	protected AsyncOperation		m_Request;

	public AssetBundleLoadLevelOperation (string assetbundleName, string levelName, bool isAdditive)
	{
		m_AssetBundleName = assetbundleName;
		m_LevelName = levelName;
		m_IsAdditive = isAdditive;
	}

	public override bool Update ()
	{
		if (m_Request != null)
			return false;
		
		LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle (m_AssetBundleName, out m_DownloadingError);
		if (bundle != null)
		{


            //m_Request = SceneManager.LoadSceneAsync(m_LevelName, m_IsAdditive?LoadSceneMode.Additive:LoadSceneMode.Single);

			if(m_IsAdditive)
			{

                m_Request= SceneManager.LoadSceneAsync(m_LevelName,LoadSceneMode.Additive);
			}

			else
			{
                m_Request = SceneManager.LoadSceneAsync(m_LevelName, LoadSceneMode.Single);
            }

			return false;
		}
		else
			return true;
	}
	
	public override bool IsDone ()
	{
		// Return if meeting downloading error.
		// m_DownloadingError might come from the dependency downloading.
		if (m_Request == null && m_DownloadingError != null)
		{
			Debug.LogError(m_DownloadingError);
			return true;
		}
		
		return m_Request != null && m_Request.isDone;
	}
}

public abstract class AssetBundleLoadAssetOperation : AssetBundleLoadOperation
{
	public abstract T GetAsset<T>() where T : UnityEngine.Object;
}

public class AssetBundleLoadAssetOperationSimulation : AssetBundleLoadAssetOperation
{
	UnityEngine.Object	m_SimulatedObject;
	
	public AssetBundleLoadAssetOperationSimulation (UnityEngine.Object simulatedObject)
	{
		m_SimulatedObject = simulatedObject;
	}
	
	public override T GetAsset<T>()
	{
		return m_SimulatedObject as T;
	}
	
	public override bool Update ()
	{
		return false;
	}
	
	public override bool IsDone ()
	{
		return true;
	}
}

public class AssetBundleLoadAssetOperationFull : AssetBundleLoadAssetOperation
{
	protected string 				m_AssetBundleName;
	protected string 				m_AssetName;
	protected string 				m_DownloadingError;
	protected System.Type 			m_Type;
	protected AssetBundleRequest	m_Request = null;

	public AssetBundleLoadAssetOperationFull (string bundleName, string assetName, System.Type type)
	{
		m_AssetBundleName = bundleName;
		m_AssetName = assetName;
		m_Type = type;
	}
	
	public override T GetAsset<T>()
	{
	    if (m_Request != null && m_Request.isDone)
	    {
            if (string.IsNullOrEmpty(m_AssetName))
	        {
	            return null;
	        }	            
	        else
	        {
               
				try
				{
					return m_Request.asset as T; 

				}catch(Exception e)
				{
					// DebugLogHelper.Instance.LogError(m_AssetName+ "----------" + typeof(T));
					return null;
				}
	        }	        

	    }
			
		else
			return null;
	}
	
	// Returns true if more Update calls are required.
	public override bool Update ()
	{
		if (m_Request != null)
			return false;

		LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle (m_AssetBundleName, out m_DownloadingError);
		if (bundle != null)
		{
		    if (string.IsNullOrEmpty(m_AssetName))
		    {
		        string[] allAssetNames = bundle.m_AssetBundle.GetAllAssetNames();
		        if (allAssetNames != null && allAssetNames.Length > 0)
		        {
		            m_Request = bundle.m_AssetBundle.LoadAssetWithSubAssetsAsync(allAssetNames[0]);
		        }
		       // m_Request = bundle.m_AssetBundle.LoadAllAssetsAsync();
		    }
		    else
		    {
		        m_Request = bundle.m_AssetBundle.LoadAssetWithSubAssetsAsync(m_AssetName, m_Type);
		    }
			
			return false;
		}
		else
		{
			return true;
		}
	}
	
	public override bool IsDone ()
	{
		// Return if meeting downloading error.
		// m_DownloadingError might come from the dependency downloading.
		if (m_Request == null && m_DownloadingError != null)
		{
			Debug.LogError(m_DownloadingError);
			return true;
		}

		return m_Request != null && m_Request.isDone;
	}
}

public class AssetBundleLoadManifestOperation : IEnumerator
{
    protected float startTime;
    public enum LoadingState
    {
        None,
        LoadingBundle,
        LoadingRequest,
    }
    protected WWW downloadWWW;
    protected string url;
	public AssetBundleLoadManifestOperation ()
	{
	    startTime = Time.realtimeSinceStartup;
        if (string.IsNullOrEmpty(AssetBundleManager.serverURL))
            // If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
            url = AssetBundleManager.StreamingassetsURL + AssetBundleManager.GetPlatformName();
        else
        {
            url = "file://" + AssetBundleManager.PersistentURL + AssetBundleManager.GetPlatformName();
        }
        Debug.Log("AssetBundleLoadManifestOperation:" + url);
        downloadWWW = new WWW(url);
    }

    public bool MoveNext()
    {
        if (!downloadWWW.isDone)
        {
            if (Time.realtimeSinceStartup - startTime >= AssetBundleManager.TimeOut)//超时
            {
                return false;
            }

            return true;
        }

        else
        {
            if(downloadWWW.error!=null)
            {
                //DebugLogHelper.Instance.Log("downloadWWW.error:"+ downloadWWW.error);
                if (downloadWWW.error.Contains("404"))
                {
                    //UIAssetLoading.ins.Logining("资源包下载失败："+ downloadWWW.error +"\n"+downloadWWW.url);
                }
                return false;
            }

           
            AssetBundle bundle = downloadWWW.assetBundle;
           
            if (bundle == null)
            {
                 Debug.Log("bundle is null");
                isDone = true;
                return false;
            }
            AssetBundleManifest manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            OnLoadedManifest(manifest);
            downloadWWW.assetBundle.Unload(false);
            downloadWWW.Dispose();

            isDone = true;
            return false; 
           
        }    
    }

    protected virtual void OnLoadedManifest(AssetBundleManifest manifest)
    { 
        AssetBundleManager.m_LocalBundleManifest = manifest;
        Debug.Log("OnLoadedManifest:" + AssetBundleManager.m_LocalBundleManifest.name);
    }

    public void Reset()
    {
        
    }

    private bool isDone = false;
    public bool IsDone()
    {
        return isDone;
    }

    public object Current { get; private set; }
}

public class RemoteBundleLoadManifestOperation : AssetBundleLoadManifestOperation
{
    public RemoteBundleLoadManifestOperation():base()
    {
        url = AssetBundleManager.ServerassetsURL + AssetBundleManager.GetPlatformName();
        downloadWWW = new WWW(url);

        Debug.LogError(url);
    }
        
    protected override void OnLoadedManifest(AssetBundleManifest manifest)
    {
        AssetBundleManager.m_RemoteBundleManifest = manifest;
    }
}


