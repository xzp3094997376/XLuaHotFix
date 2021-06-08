AssetBundleManager
教程：
    读取本地StreamingAssets 文件夹下的bundle包
	编辑器下 需要菜单栏  AssetBundles/SimulateAssetBundles   取消选中 
	发布exe 则不用做勾选 菜单栏项
	代码初始化部分 需要将serverURL赋值为空
	 AssetBundleManager.serverURL = "";
	 //初始化
            AssetBundleManager.Instance.Init(() =>
            {
                AssetBundleManager.Instance.LoadInstance<Transform>("pbasset", "PB", tran =>
                {
                    Debug.Log(tran.name);
                });
            });
   读取服务器做资源增量更新则
   在编辑器下 需要菜单栏  AssetBundles/SimulateAssetBundles   取消选中 
   代码初始化部分serverURL需要赋值服务器路径  
            AssetBundleManager.serverURL = "http://127.0.0.1:8008";
			必须要加上CheckUpgrade函数
            StartCoroutine(AssetBundleManager.Instance.CheckUpgrade(bol =>
            {
                if (bol)
                {
                    AssetBundleManager.Instance.Init(() =>
                    {
                        AssetBundleManager.Instance.LoadInstance<Transform>("pbasset", "PB", tran =>
                        {
                            Debug.Log(tran.name);
                        });
                    });
                }
            }));
AssetBundleManager.Instance.LoadInstance 该函数是异步读取资源生成模型