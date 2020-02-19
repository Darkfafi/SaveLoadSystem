using Internal;
using UnityEditor;
using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using RDP.SaveLoadSystem.Internal.Utils;

namespace RDP.SaveLoadSystem
{
	public class StorageInspectorEditor : EditorWindow
	{
		private Storage _currentlyViewingStorage = null;
		private ReadStorageResult[] _results = null;

		private List<IStorageCapsule> _iStorageCapsuleInstances = new List<IStorageCapsule>();
		private List<string> _capsuleIDs = new List<string>();

		private Vector2 _scroll = Vector2.zero;
		private string _pathInputValue = string.Empty;
		private Storage.EncodingType _encodingTypeInputValue = Storage.EncodingType.Base64;

		[MenuItem(InternalConsts.MENU_ITEM_PREFIX + "Storage/Storage Inspector")]
		private static void Init()
		{
			StorageInspectorEditor window = GetWindow<StorageInspectorEditor>();
			window.Show();
		}

		protected void Awake()
		{
			RefreshStorageCapsuleInstances();
		}

		protected void OnGUI()
		{
			EditorGUILayout.LabelField("Save Files Directory Path:");
			_pathInputValue = EditorGUILayout.TextField(_pathInputValue);

			EditorGUILayout.LabelField("Encoding Type:");
			_encodingTypeInputValue = (Storage.EncodingType)EditorGUILayout.EnumPopup(_encodingTypeInputValue);

			if(GUILayout.Button("Load Storage"))
			{
				LoadStorage(_pathInputValue, _encodingTypeInputValue);
			}

			if(_currentlyViewingStorage != null)
			{
				if (GUILayout.Button("Refresh"))
				{
					LoadStorage(_currentlyViewingStorage.StorageLocationPath, _currentlyViewingStorage.EncodingOption);
				}
			}

			if (_results != null)
			{
				_scroll = EditorGUILayout.BeginScrollView(_scroll);
				for(int i = 0; i < _results.Length; i++)
				{
					ReadStorageResult result = _results[i];
					EditorGUILayout.LabelField("---- " + result.CapsuleID + " ----");
					RenderStorageGUI(result.CapsuleStorage, i);
				}
				EditorGUILayout.EndScrollView();
			}
		}

		protected void OnDestroy()
		{
			_iStorageCapsuleInstances.Clear();
			_capsuleIDs.Clear();
			_results = null;
			_currentlyViewingStorage = null;
		}

		private void LoadStorage(string path, Storage.EncodingType encodingType)
		{
			RefreshStorageCapsuleInstances();
			_currentlyViewingStorage = new Storage(path, encodingType, _iStorageCapsuleInstances.ToArray());
			_results = _currentlyViewingStorage.Read(_capsuleIDs.ToArray()).ToArray();
		}

		private void RefreshStorageCapsuleInstances()
		{
			_iStorageCapsuleInstances.Clear();
			_capsuleIDs.Clear();
			Type[] storageCapsuleTypes = Assembly.GetAssembly(typeof(IStorageCapsule)).GetTypes().Where(x => x.GetInterfaces().Contains(typeof(IStorageCapsule))).ToArray();
			for(int i = 0; i < storageCapsuleTypes.Length; i++)
			{
				IStorageCapsule instance = Activator.CreateInstance(storageCapsuleTypes[i]) as IStorageCapsule;
				if(instance != null)
				{
					_iStorageCapsuleInstances.Add(instance);
					_capsuleIDs.Add(instance.ID);
				}
			}
		}

		private bool RenderStorageGUI(IStorageDictionaryEditor storageDictionary, int index, int padding = 0)
		{
			if (storageDictionary == null)
			{
				return false;
			}

			GUIStyle masterStyle = new GUIStyle(GUI.skin.label);
			masterStyle.padding.left = padding + 10;

			GUIStyle childrenStyle = new GUIStyle(GUI.skin.label);
			childrenStyle.padding.left = masterStyle.padding.left + 10;

			string[] valKeys = storageDictionary.GetValueStorageKeys();

			for (int i = 0; i < valKeys.Length; i++)
			{
				string valKey = valKeys[i];
				object val = storageDictionary.GetValue(valKey);

				if (val.GetType() == typeof(SaveableDict))
				{
					SaveableDict dict = (SaveableDict)val;
					for(int j = 0; j < dict.Items.Length; j++)
					{
						DictItem item = dict.Items[j];
						EditorGUILayout.LabelField(string.Concat("- ", item.KeySection.ValueType, ": ", item.ValueSection.ValueType), masterStyle);
						EditorGUILayout.LabelField(string.Concat("  ", item.KeySection.ValueString, ": ", item.ValueSection.ValueString), childrenStyle);
					}
				}
				else
				{
					EditorGUILayout.LabelField(string.Concat("- ", valKey, ": ", val.ToString()), masterStyle);
				}
			}

			string[] refKeys = storageDictionary.GetRefStorageKeys();

			for (int i = 0; i < refKeys.Length; i++)
			{
				string refKey = refKeys[i];
				EditorGUILayout.LabelField(string.Concat(refKey, ": "), masterStyle);
				List<EditableRefValue> refsVals = new List<EditableRefValue>();
				refsVals.Add(storageDictionary.GetValueRef(refKey));
				refsVals.AddRange(storageDictionary.GetValueRefs(refKey));
				for (int j = 0; j < refsVals.Count; j++)
				{
					EditableRefValue refVal = refsVals[j];
					if (!string.IsNullOrEmpty(refVal.ReferenceID))
					{
						EditorGUILayout.LabelField(string.Concat("- ID: ", refVal.ReferenceID), childrenStyle);
						EditorGUILayout.LabelField(string.Concat("- Type: ", refVal.ReferenceType), childrenStyle);
						EditorGUILayout.LabelField(string.Concat("- Storage: ", refVal.Storage == null ? "Empty" : ""), childrenStyle);
						RenderStorageGUI(refVal.Storage, index, childrenStyle.padding.left);
					}
				}
			}

			return true;
		}
	}
}