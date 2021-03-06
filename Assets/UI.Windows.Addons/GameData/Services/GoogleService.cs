﻿using ME;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI.Windows.Extensions;
using UnityEngine.UI.Windows.Extensions.Tiny;
using UnityEngine.UI.Windows.Plugins.Flow;
using UnityEngine.UI.Windows.Plugins.Services;
#if UNITY_5_4_OR_NEWER
using UnityEngine.Networking;
#else
using UnityEngine.Experimental.Networking;
#endif

namespace UnityEngine.UI.Windows.Plugins.GameData.Services {

	public class GoogleService : GameDataService {
		
		public override AuthKeyPermissions GetAuthPermissions() {

			return AuthKeyPermissions.None;

		}

		public override bool IsSupported() {

			return true;

		}

		public override string GetServiceName() {

			return "Google Docs Data";

		}

		public override System.Collections.Generic.IEnumerator<byte> Auth(string key, ServiceItem serviceItem) {

			var settings = this.serviceManager.settings as GameDataSettings;
			return this.GetData(settings, (result) => {

				if (result.hasError == false) {

					GameDataSystem.TryToSaveCSV(result.data);

				} else {

					if (UnityEngine.UI.Windows.Constants.LOGS_ENABLED == true) UnityEngine.Debug.LogError(string.Format("[ GameData.GoogleService ] CSV GetData error: {0}", result.errorText));
					GameDataSystem.TryToLoadCache();

				}

			});

		}

		#region Client API Events
		public override System.Collections.Generic.IEnumerator<byte> GetData(GameDataSettings settings, System.Action<GameDataResult> onResult) {

			if (Application.internetReachability == NetworkReachability.NotReachable) {

				onResult.Invoke(new GameDataResult() { hasError = true, errorText = "No Connection", data = string.Empty });
				yield break;

			}

			if (Application.isPlaying == false || this.serviceManager.logEnabled == true) {
				
				if (UnityEngine.UI.Windows.Constants.LOGS_ENABLED == true) UnityEngine.Debug.LogFormat("[ GameData ] Loading: {0}", settings.url);
				
			}

			var eTag = settings.eTag;
			var eTagPrefsKey = "GameDataSystem.GoogleService.ETag";
			if (PlayerPrefs.HasKey(eTagPrefsKey) == true) {

				eTag = PlayerPrefs.GetString(eTagPrefsKey);

			}

#if !UNITY_SWITCH
			var www = UnityWebRequest.Get(settings.url);
			www.SetRequestHeader("ETag", eTag);
			www.SendWebRequest();
#else
			var form = new WWWForm();
			form.AddField("ETag", eTag);
			var www = new WWW(settings.url, form);
#endif

			#if UNITY_EDITOR
			if (Application.isPlaying == false) {

				while (www.isDone == false) {

#if !UNITY_SWITCH
					var progress = www.downloadProgress;
#else
					var progress = www.progress;
#endif
					if (UnityEditor.EditorUtility.DisplayCancelableProgressBar("Wait a while", "...", progress) == true) {

						break;

					}

				}

				UnityEditor.EditorUtility.ClearProgressBar();

#if !UNITY_SWITCH
				eTag = www.GetResponseHeader("ETag");
#else
				www.responseHeaders.TryGetValue("ETag", out eTag);
#endif
				if (eTag != null) {

					settings.eTag = eTag;
					PlayerPrefs.SetString(eTagPrefsKey, eTag);

				}

			} else {
			#endif

				while (www.isDone == false) {

					yield return 0;

				}

#if !UNITY_SWITCH
				eTag = www.GetResponseHeader("ETag");
#else
				www.responseHeaders.TryGetValue("ETag", out eTag);
#endif
				if (eTag != null) {

					PlayerPrefs.SetString(eTagPrefsKey, eTag);

				}

			#if UNITY_EDITOR
			}
			#endif
#if !UNITY_SWITCH
			var data = www.downloadHandler.text;
#else
			var data = www.text;
#endif

			onResult.Invoke(new GameDataResult() { hasError = !string.IsNullOrEmpty(www.error), data = data, errorText = www.error });

			www.Dispose();
			www = null;

		}
		#endregion

		#region Editor API Events
		#if UNITY_EDITOR
		public override void EditorLoad(GameDataSettings settings, GameDataServiceItem item) {

			if (item.processing == false) {

				item.processing = true;

				// Connecting
				this.OnEditorAuth(item.authKey, (result) => {

					//UnityEditor.EditorApplication.delayCall += () => {

					this.StartCoroutine(this.GetData(settings, (res) => {

						if (res.hasError == false) {

							GameDataSystem.TryToSaveCSV(res.data);

						}

						item.processing = false;

					}));

					//};

				});

			}

		}

		protected override void OnInspectorGUI(GameDataSettings settings, GameDataServiceItem item, System.Action onReset, GUISkin skin) {

			if (settings == null) return;

			var data = FlowSystem.GetData();
			if (data == null) return;

			if (settings.url == null) settings.url = string.Empty;

			GUILayout.Label("URL:");
			var newKey = GUILayout.TextArea(settings.url);
			if (newKey != settings.url) {

				settings.url = newKey;
				UnityEditor.EditorUtility.SetDirty(settings);

			}

			GUILayout.Label(string.Format("ETag: {0}", settings.eTag));

			UnityEditor.EditorGUI.BeginDisabledGroup(item.processing);
			if (GUILayout.Button(item.processing == true ? "Loading..." : "Load", skin.button) == true) {

				this.EditorLoad(settings, item);

			}
			UnityEditor.EditorGUI.EndDisabledGroup();

		}
		#endif
		#endregion

	}

}