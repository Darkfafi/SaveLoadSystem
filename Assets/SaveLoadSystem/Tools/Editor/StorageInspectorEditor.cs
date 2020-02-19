using Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
	public class StorageInspectorEditor : EditorWindow
	{
		private Storage _currentlyViewingStorage = null;
		private List<StoringUIItem> _capsuleUIItems = new List<StoringUIItem>();

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

			if (_capsuleUIItems != null)
			{
				_scroll = EditorGUILayout.BeginScrollView(_scroll);
				for(int i = 0; i < _capsuleUIItems.Count; i++)
				{
					_capsuleUIItems[i].RenderGUI(0);
				}
				EditorGUILayout.EndScrollView();
			}
		}

		protected void OnDestroy()
		{
			_iStorageCapsuleInstances.Clear();
			_capsuleIDs.Clear();
			_capsuleUIItems = null;
			_currentlyViewingStorage = null;
		}

		private void LoadStorage(string path, Storage.EncodingType encodingType)
		{
			RefreshStorageCapsuleInstances();
			_currentlyViewingStorage = new Storage(path, encodingType, _iStorageCapsuleInstances.ToArray());
			ReadStorageResult[]  results = _currentlyViewingStorage.Read(_capsuleIDs.ToArray()).ToArray();

			for (int i = 0; i < results.Length; i++)
			{
				ReadStorageResult result = results[i];
				_capsuleUIItems.Add(new StoringUIItem(result.CapsuleID, result.CapsuleStorage));
			}
		}

		private void RefreshStorageCapsuleInstances()
		{
			_iStorageCapsuleInstances.Clear();
			_capsuleIDs.Clear();
			_capsuleUIItems.Clear();
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

		private class RefUIItem : StoringUIItem
		{
			private EditableRefValue _ref;

			public RefUIItem(string refKey, EditableRefValue refValue) : base("- " + refKey, refValue.Storage)
			{
				_ref = refValue;
			}

			protected override void OnRenderGUI(int layer)
			{
				EditorGUILayout.LabelField(string.Concat("- ID: ", _ref.ReferenceID));
				EditorGUILayout.LabelField(string.Concat("- Type: ", _ref.ReferenceType));
				EditorGUILayout.LabelField(string.Concat("- Storage: ", _ref.Storage == null ? "Empty" : ""));
				base.OnRenderGUI(layer);
			}
		}

		private class StoringUIItem : UIItem
		{
			private List<RefUIItem> _nestedRefs = new List<RefUIItem>();
			private List<UIItem> _nestedValues = new List<UIItem>();

			private IStorageDictionaryEditor _storage;

			public StoringUIItem(string id, IStorageDictionaryEditor storage) : base(id, false)
			{
				_storage = storage;
				if (_storage != null)
				{
					string[] valKeys = _storage.GetValueStorageKeys();
					for (int i = 0; i < valKeys.Length; i++)
					{
						string valKey = valKeys[i];
						object val = _storage.GetValue(valKey);

						if (val.GetType() == typeof(SaveableDict))
						{
							_nestedValues.Add(new DictElementItem(valKey, (SaveableDict)val));
						}
						else
						{
							_nestedValues.Add(new ElementItem(valKey, val));
						}
					}

					string[] refKeys = _storage.GetRefStorageKeys();
					for (int i = 0; i < refKeys.Length; i++)
					{
						string tRefKey = refKeys[i];
						EditableRefValue[] refsVals = _storage.GetValueRefs(tRefKey);
						for (int j = 0; j < refsVals.Length; j++)
						{
							EditableRefValue refVal = refsVals[j];
							_nestedRefs.Add(new RefUIItem(tRefKey, refVal));
						}
					}
				}
			}

			protected override void OnRenderGUI(int layer)
			{
				for (int i = 0; i < _nestedValues.Count; i++)
				{
					_nestedValues[i].RenderGUI(layer + 1);
				}

				for (int i = 0; i < _nestedRefs.Count; i++)
				{
					_nestedRefs[i].RenderGUI(layer + 1);
				}
			}
		}

		private class ElementItem : UIItem
		{
			private object _value;

			public ElementItem(string key, object value) : base("- " + key, true)
			{
				_value = value;
			}

			protected override void OnRenderGUI(int layer)
			{
				EditorGUILayout.LabelField(string.Concat(_value.ToString()));
			}
		}

		private class DictElementItem : UIItem
		{
			private SaveableDict _dict;

			public DictElementItem(string key, SaveableDict dict) : base("- "  + key, false)
			{
				_dict = dict;
			}

			protected override void OnRenderGUI(int layer)
			{
				for (int j = 0; j < _dict.Items.Length; j++)
				{
					DictItem item = _dict.Items[j];
					GUILayout.BeginVertical(GUI.skin.box);
					EditorGUILayout.LabelField(string.Concat("- ", item.KeySection.ValueType, ": ", item.ValueSection.ValueType));
					EditorGUILayout.LabelField(string.Concat("  ", item.KeySection.ValueString, ": ", item.ValueSection.ValueString));
					GUILayout.EndVertical();
				}
			}
		}

		private abstract class UIItem
		{
			public bool IsOpen
			{
				get; private set;
			}

			public string Title
			{
				get; private set;
			}

			public UIItem(string title, bool defaultIsOpenValue)
			{
				Title = title;
				IsOpen = defaultIsOpenValue;
			}

			public void RenderGUI(int layer)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Space(layer * 10);
				IsOpen = EditorGUILayout.Foldout(IsOpen, Title);
				GUILayout.EndHorizontal();

				if (IsOpen)
				{
					GUILayout.BeginHorizontal();
					GUILayout.Space(layer * 20);
					GUILayout.BeginVertical(GUI.skin.box);
					OnRenderGUI(layer);
					GUILayout.EndVertical();
					GUILayout.EndHorizontal();
				}
			}

			protected abstract void OnRenderGUI(int layer);
		}
	}
}