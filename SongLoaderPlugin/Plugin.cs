using System;
using IllusionPlugin;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SongLoaderPlugin
{
	public class Plugin : IPlugin
	{
		public string Name
		{
			get { return "Song Loader Plugin"; }
		}

		public string Version
		{
			get { return "v3.1"; }
		}
		
		public void OnApplicationStart()
		{

		}

		public void OnApplicationQuit()
		{
            SongLoader.OnClose();
			PlayerPrefs.DeleteKey("lbPatched");
		}

		public void OnLevelWasLoaded(int level)
		{
            if (level != SongLoader.MenuIndex) return;
        }

		public void OnLevelWasInitialized(int level)
		{
			if (level != SongLoader.MenuIndex) return;
			SongLoader.OnLoad();
            GameObject diff_display = new GameObject("DifficultyDisplay");
            diff_display.AddComponent<DifficultyDisplay>();
        }

		public void OnUpdate()
		{
			
		}

		public void OnFixedUpdate()
		{
			
		}
	}
}