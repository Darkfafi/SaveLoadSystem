using Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RDP.SaveLoadSystem.Internal
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
			StorageKeySearcher.GetSaveablesToKeyEntries();
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

				IStorageCapsule storageCapsuleInstance = _iStorageCapsuleInstances.Find(x => x.ID == result.CapsuleID);
				StorageKeySearcher.StorageKeyEntry[] keyEntries = new StorageKeySearcher.StorageKeyEntry[] { };


				if (storageCapsuleInstance != null)
				{
					keyEntries = StorageKeySearcher.GetKeyEntries(storageCapsuleInstance.GetType());
				}

				_capsuleUIItems.Add(new StoringUIItem(result.CapsuleID, result.CapsuleStorage, keyEntries));
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

		#region UI Elements

		private class RefUIItem : StoringUIItem
		{
			public EditableRefValue Reference
			{
				get; private set;
			}

			public StorageKeySearcher.StorageKeyEntry OwnKeyEntry
			{
				get; private set;
			}

			public override bool IsExpectedTypeMatch()
			{
				if(Reference.ReferenceType == null)
				{
					return base.IsExpectedTypeMatch();
				}

				return OwnKeyEntry.ExpectedType != null && OwnKeyEntry.ExpectedType.IsAssignableFrom(Reference.ReferenceType);
			}

			public override State CorruptionState
			{
				get
				{
					if(base.CorruptionState == State.Error || Reference.ReferenceType == null)
					{
						return State.Error;
					}

					if(!IsExpectedTypeMatch())
					{
						return State.Warning;
					}

					return base.CorruptionState;
				}
			}

			public RefUIItem(string refKey, EditableRefValue refValue, StorageKeySearcher.StorageKeyEntry ownKey, StorageKeySearcher.StorageKeyEntry[] keyEntries) : base(refKey, refValue.Storage, keyEntries)
			{
				OwnKeyEntry = ownKey;
				Reference = refValue;
			}

			protected override void OnRenderGUI(int layer)
			{
				GUIStyle typeStyle = new GUIStyle(GUI.skin.label);
				string typeDisplay = string.Empty;
				string infoText = string.Empty;
				State typeCorruptionState = State.Normal;

				if (Reference.ReferenceType == null)
				{
					typeCorruptionState = State.Error;
					typeDisplay = Reference.ReferenceTypeString;
					infoText = $"Reference type not found in project!";
				}
				else
				{
					if(!IsExpectedTypeMatch())
					{
						typeCorruptionState = State.Warning;
						infoText = $"Expected type {OwnKeyEntry.ExpectedType.Name} but found value of type {Reference.ReferenceType.Name}";
					}

					typeDisplay = Reference.ReferenceType.ToString();
				}

				Color? typeColor = GetCorruptionStateColor(typeCorruptionState);

				if(typeColor.HasValue)
				{
					typeStyle.normal.textColor = typeColor.Value;
				}

				EditorGUILayout.LabelField(string.Concat("- ID: ", Reference.ReferenceID));
				EditorGUILayout.LabelField(new GUIContent(string.Concat("- Type: ", typeDisplay), infoText), typeStyle);
				EditorGUILayout.LabelField(string.Concat("- Storage: ", Reference.Storage == null ? "Empty" : ""));
				base.OnRenderGUI(layer);
			}
		}

		private class StoringUIItem : UIItem
		{
			private List<RefUIItem> _nestedRefs = new List<RefUIItem>();
			private List<UIItem> _nestedValues = new List<UIItem>();

			private IStorageDictionaryEditor _storage;

			public override bool IsExpectedTypeMatch()
			{
				return true;
			}

			public override State CorruptionState
			{
				get
				{
					if(_nestedValues.Any(x => x.CorruptionState == State.Error) || _nestedRefs.Any(x => x.CorruptionState == State.Error))
					{
						return State.Error;
					}

					if (_nestedValues.Any(x => x.CorruptionState == State.Warning) || _nestedRefs.Any(x => x.CorruptionState == State.Warning))
					{
						return State.Warning;
					}

					return State.Normal;
				}
			}

			public StorageKeySearcher.StorageKeyEntry[] KeyEntries
			{
				get; private set;
			}

			public StoringUIItem(string id, IStorageDictionaryEditor storage, StorageKeySearcher.StorageKeyEntry[] keyEntries) : base(id, false)
			{
				_storage = storage;
				KeyEntries = keyEntries;
				if (_storage != null)
				{
					string[] valKeys = _storage.GetValueStorageKeys();
					for (int i = 0; i < valKeys.Length; i++)
					{
						string valKey = valKeys[i];
						object val = _storage.GetValue(valKey);
						StorageKeySearcher.StorageKeyEntry entry = KeyEntries.FirstOrDefault(x => x.StorageKey == valKey);
						if (val.GetType() == typeof(SaveableDict))
						{
							_nestedValues.Add(new DictElementItem(valKey, (SaveableDict)val, entry));
						}
						else
						{
							_nestedValues.Add(new ElementItem(valKey, val, entry));
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
							StorageKeySearcher.StorageKeyEntry entry = KeyEntries.FirstOrDefault(x => x.StorageKey == tRefKey);
							_nestedRefs.Add(new RefUIItem(tRefKey, refVal, entry, StorageKeySearcher.GetKeyEntries(refVal.ReferenceType)));
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

			public override State CorruptionState
			{
				get
				{
					if (!IsExpectedTypeMatch())
						return State.Warning;

					return State.Normal;
				}
			}

			public StorageKeySearcher.StorageKeyEntry KeyEntry
			{
				get; private set;
			}

			public ElementItem(string key, object value, StorageKeySearcher.StorageKeyEntry keyEntry) : base(key, true)
			{
				KeyEntry = keyEntry;
				_value = value;
			}

			protected override void OnRenderGUI(int layer)
			{
				GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
				State labelState = State.Normal;
				string infoText = string.Empty;

				if(!IsExpectedTypeMatch())
				{
					labelState = State.Warning;
					infoText = $"Expected type {KeyEntry.ExpectedType.Name} but found value of type {_value.GetType().Name}";
				}

				Color? labelColor = GetCorruptionStateColor(labelState);

				if (labelColor.HasValue)
				{
					labelStyle.normal.textColor = labelColor.Value;
				}

				EditorGUILayout.LabelField(new GUIContent(string.Concat(_value.ToString()), infoText), labelStyle);
			}

			public override bool IsExpectedTypeMatch()
			{
				if(_value == null || KeyEntry.ExpectedType == null)
				{
					return true;
				}

				return KeyEntry.ExpectedType.IsAssignableFrom(_value.GetType());
			}
		}

		private class DictElementItem : ElementItem
		{
			private SaveableDict _dict;

			public DictElementItem(string key, SaveableDict dict, StorageKeySearcher.StorageKeyEntry keyEntry) : base(key, dict, keyEntry)
			{
				_dict = dict;
			}

			public override State CorruptionState
			{
				get
				{
					if(_dict.Items.Any(x => !IsTypeValid(x.KeySection.ValueType) || !IsTypeValid(x.ValueSection.ValueType)))
					{
						return State.Error;
					}

					if(!IsExpectedTypeMatch())
					{
						return State.Warning;
					}

					return State.Normal;
				}
			}

			public override bool IsExpectedTypeMatch()
			{
				if(KeyEntry.ExpectedType == null)
					return false;

				GetKeyValueTypesValid(out bool keyTypeValid, out bool valueTypeValid);

				return keyTypeValid && valueTypeValid;
			}

			protected override void OnRenderGUI(int layer)
			{
				for (int j = 0; j < _dict.Items.Length; j++)
				{
					DictItem item = _dict.Items[j];

					GUIStyle keyLabelStyle = new GUIStyle(GUI.skin.label);
					GUIStyle valueLabelStyle = new GUIStyle(GUI.skin.label);

					State keyState = State.Normal;
					State valueState = State.Normal;

					string keyType = item.KeySection.ValueType;
					string valueType = item.ValueSection.ValueType;

					string keyInfoText = string.Empty;
					string valueInfoText = string.Empty;

					GetKeyValueTypesValid(out bool keyTypeValid, out bool valueTypeValid);
					TryGetExpectedTypes(out Type expectedKeyType, out Type expectedValueType);

					if (IsTypeValid(item.KeySection.ValueType))
					{
						keyType = item.KeySection.GetValueType().Name;

						if (!keyTypeValid)
						{
							keyState = State.Warning;
							keyInfoText = $"Expected type {expectedKeyType.Name} but found value of type {keyType}";
						}
					}
					else
					{
						keyState = State.Error;
					}

					if(IsTypeValid(item.ValueSection.ValueType))
					{
						valueType = item.ValueSection.GetValueType().Name;

						if(!valueTypeValid)
						{
							valueState = State.Warning;
							valueInfoText = $"Expected type {expectedValueType.Name} but found value of type {valueType}";
						}
					}
					else
					{
						valueState = State.Error;
					}

					Color? keyColor = GetCorruptionStateColor(keyState);
					Color? valueColor = GetCorruptionStateColor(valueState);

					if(keyColor.HasValue)
					{
						keyLabelStyle.normal.textColor = keyColor.Value;
					}

					if(valueColor.HasValue)
					{
						valueLabelStyle.normal.textColor = valueColor.Value;
					}

					GUILayout.BeginVertical(GUI.skin.box);

					EditorGUILayout.LabelField(new GUIContent(string.Concat("Key: ", item.KeySection.ValueString, " << ", keyType), keyInfoText), keyLabelStyle);
					EditorGUILayout.LabelField(new GUIContent(string.Concat("Value: ", item.ValueSection.ValueString, " << ", valueType), valueInfoText), valueLabelStyle);
					GUILayout.EndVertical();
				}
			}

			private void GetKeyValueTypesValid(out bool keyTypeValid, out bool valueTypeValid)
			{
				keyTypeValid = false;
				valueTypeValid = false;

				if (KeyEntry.ExpectedType == null)
				{
					return;
				}

				if (TryGetExpectedTypes(out Type keyType, out Type valueType))
				{
					if (_dict.Items.Length == 0)
					{
						keyTypeValid = true;
						valueTypeValid = true;
						return;
					}

					DictItem item = _dict.Items[0];
					Type tKey = GetTypeSafe(item.KeySection.ValueType);
					Type tValue = GetTypeSafe(item.ValueSection.ValueType);

					keyTypeValid = tKey != null && keyType.IsAssignableFrom(tKey);
					valueTypeValid = tValue != null && valueType.IsAssignableFrom(tValue);
				}
			}

			private bool TryGetExpectedTypes(out Type keyType, out Type valueType)
			{
				if (KeyEntry.ExpectedType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
				{
					Type[] arguments = KeyEntry.ExpectedType.GetGenericArguments();
					keyType = arguments[0];
					valueType = arguments[1];
					return true;
				}

				keyType = null;
				valueType = null;
				return false;
			}
		}

		private abstract class UIItem
		{
			public enum State
			{
				Normal,
				Warning,
				Error
			}

			public bool IsOpen
			{
				get; private set;
			}

			public string Title
			{
				get; private set;
			}

			public string Key
			{
				get; private set;
			}

			public abstract State CorruptionState
			{
				get;
			}

			public UIItem(string key, bool defaultIsOpenValue)
			{
				Key = key;
				Title = Key;
				IsOpen = defaultIsOpenValue;
			}

			public void RenderGUI(int layer)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Space(layer * 10);

				GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);

				Color? color = GetCorruptionStateColor(CorruptionState);

				if(color.HasValue)
				{
					foldoutStyle.normal.textColor = color.Value;
				}

				IsOpen = EditorGUILayout.Foldout(IsOpen, Title + " " + GetCorruptionStateIcon(CorruptionState), foldoutStyle);
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

			public abstract bool IsExpectedTypeMatch();

			protected abstract void OnRenderGUI(int layer);

			protected string GetCorruptionStateIcon(State state)
			{
				switch(state)
				{
					case State.Error:
						return "[!]";
					case State.Warning:
						return "[?]";
					default:
						return string.Empty;
				}
			}

			protected Color? GetCorruptionStateColor(State state)
			{
				switch (state)
				{
					case State.Error:
						return Color.red;
					case State.Warning:
						return new Color(1f, 0.65f, 0f);
					default:
						return null;
				}
			}

			protected bool IsTypeValid(string typeString)
			{
				return GetTypeSafe(typeString) != null;
			}

			protected Type GetTypeSafe(string typeString)
			{
				if (string.IsNullOrEmpty(typeString))
					return null;

				try
				{
					return Type.GetType(typeString);
				}
				catch
				{
					return null;
				}
			}
		}

		#endregion
	}
}