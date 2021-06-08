using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;

class ProjectBuild : Editor{
	

	//得到项目的名称
	public static string projectName
	{
		get
		{ 
			//在这里分析shell传入的参数， 还记得上面我们说的哪个 project-$1 这个参数吗？
			//这里遍历所有参数，找到 project开头的参数， 然后把-符号 后面的字符串返回，
			//这个字符串就是 91 了。。
			foreach(string arg in System.Environment.GetCommandLineArgs()) {
				if(arg.StartsWith("project"))
				{
					return arg.Split("-"[0])[1];
				}
                Debug.Log(arg);
			}
			return "test";
		}
	}
	
	//shell脚本直接调用这个静态方法
	static void BuildForIPhone()
	{ 
		EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS,BuildTarget.iOS);
		BuildScript.BuildPlayer (false,projectName);
	}

	static void BuildForAndroid()
	{ 
		EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android);
		BuildScript.BuildPlayer (false,projectName);
	}
}