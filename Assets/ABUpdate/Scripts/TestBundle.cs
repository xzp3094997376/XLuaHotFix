using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestBundle : MonoBehaviour
{
    public GameObject c1;
    public GameObject c2;
    public GameObject sp1;
    public GameObject sp2;
    string inputFieldStr = "http://10.1.23.61:80";
    // Start is called before the first frame update
    void Start()
    {
        //http://localhost/

        AssetBundleManager.serverURL = inputFieldStr;
        CheckData();
    }
    void CheckData()
    {
        StartCoroutine(AssetBundleManager.Instance.CheckUpgrade(ret =>
        {
            if (ret)
            {
                AssetBundleManager.Instance.Init(() =>
                {
                    Debug.Log("资源检查完毕");
                });
            }
        }
        ));
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            AssetBundleManager.Instance.LoadInstance<Transform>("cube", "Cube", trans =>
            {
                if (trans != null)
                {
                    c1 = trans.gameObject;
                    trans.SetParent(transform);
                }
            });
            AssetBundleManager.Instance.LoadInstance<Transform>("cube", "cube1", trans =>
            {
                if (trans != null)
                {
                    c2 = trans.gameObject;
                    trans.SetParent(transform);
                }
            });
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            AssetBundleManager.Instance.LoadInstance<Transform>("sp", "sp1", trans =>
            {
                if (trans != null)
                {
                    trans.SetParent(transform);
                    sp1 = trans.gameObject;
                }
            });
            AssetBundleManager.Instance.LoadInstance<Transform>("sp", "sp2", trans =>
            {
                if (trans != null)
                {
                    trans.SetParent(transform);
                    sp2 = trans.gameObject;
                }
            });
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (c1 != null)
            {
                DestoryAB(c1, "cb");
            }
            if (c2 != null)
            {
                DestoryAB(c2, "cb");
            }
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (sp1 != null)
            {
                DestoryAB(sp1, "sp");
            }
            if (sp2 != null)
            {
                DestoryAB(sp2, "sp");

            }
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            //该函数的主要作用是查找并卸载不再使用的资源。游戏场景越复杂、资源越多，该函数的开销越大 
            Resources.UnloadUnusedAssets();

        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            AssetBundleManager.Instance.LoadResource<TextAsset>("luatest", "LuaTest", (textAsset) =>
            {
                Debug.LogError(textAsset.text);
                TestLua(textAsset);
            });
        }
    }
    private void DestoryAB(GameObject go, string assetName)
    {
        GameObject.Destroy(go);
        AssetBundleManager.UnloadAssetBundle(assetName, true);
        Resources.UnloadUnusedAssets();
    }



    void TestLua(TextAsset textAsset)
    {
        //GameObject cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject lightObj = GameObject.Find("Directional Light");

        AssetBundleManager.Instance.LoadInstance<Transform>("cube", "Cube", trans =>
        {
            if (trans != null)
            {
                c1 = trans.gameObject;
                trans.SetParent(transform);

                XLuaTest.LuaBehaviour lb = c1.AddComponent<XLuaTest.LuaBehaviour>();
                lb.Init(textAsset, new XLuaTest.Injection[] {
                      new XLuaTest.Injection
                      {
                          name="lightObject",
                         value=lightObj
                      }
                });
            }
        });



    }

    private void OnGUI()
    {
        //GUIStyle style= new GUIStyle();
        //style.fontSize = 20;            
        inputFieldStr = GUI.TextField(new Rect(new Vector2(Screen.width * 0.1f, Screen.height / 2), new Vector2(Screen.width / 2, 100)), inputFieldStr);
        if (GUI.Button(new Rect(100, 100, 100, 100), "开始更新"))
        {

            Debug.Log(inputFieldStr);
            AssetBundleManager.Instance.LoadResource<TextAsset>("luatest", "LuaTest", (textAsset) =>
            {
                Debug.LogError(textAsset.text);
                TestLua(textAsset);
            });
        }
    }
}
