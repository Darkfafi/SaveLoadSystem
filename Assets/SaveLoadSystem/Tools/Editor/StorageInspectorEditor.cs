using Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static RDP.SaveLoadSystem.Internal.StorageKeySearcher;

namespace RDP.SaveLoadSystem.Internal
{
	public class StorageInspectorEditor : EditorWindow
	{
		private const string TYPE_NOT_FOUND_INFO_MESSAGE = "Type not found in project";
		private const string EXPECTED_TYPE_INFO_MESSAGE_F = "Expected type {0} but found type {1}";

		private Storage _currentlyViewingStorage = null;
		private List<CapsuleItem> _capsuleUIItems = new List<CapsuleItem>();

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

			if (GUILayout.Button("Load Storage"))
			{
				LoadStorage(_pathInputValue, _encodingTypeInputValue);
			}

			if (_currentlyViewingStorage != null)
			{
				if (GUILayout.Button("Refresh"))
				{
					LoadStorage(_currentlyViewingStorage.StorageLocationPath, _currentlyViewingStorage.EncodingOption);
				}
			}

			if (_capsuleUIItems != null)
			{
				_scroll = EditorGUILayout.BeginScrollView(_scroll);
				for (int i = 0; i < _capsuleUIItems.Count; i++)
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
			ReadStorageResult[] results = _currentlyViewingStorage.Read(_capsuleIDs.ToArray()).ToArray();

			for (int i = 0; i < results.Length; i++)
			{
				ReadStorageResult result = results[i];

				IStorageCapsule storageCapsuleInstance = _iStorageCapsuleInstances.Find(x => x.ID == result.CapsuleID);
				Dictionary<string, StorageKeyEntry> keyEntries = new Dictionary<string, StorageKeyEntry>();


				if (storageCapsuleInstance != null)
				{
					keyEntries = GetKeyEntries(storageCapsuleInstance.GetType());
				}

				_capsuleUIItems.Add(new CapsuleItem(result.CapsuleID, result.CapsuleStorage, keyEntries));
			}
		}

		private void RefreshStorageCapsuleInstances()
		{
			_iStorageCapsuleInstances.Clear();
			_capsuleIDs.Clear();
			_capsuleUIItems.Clear();
			Type[] storageCapsuleTypes = Assembly.GetAssembly(typeof(IStorageCapsule)).GetTypes().Where(x => x.GetInterfaces().Contains(typeof(IStorageCapsule))).ToArray();
			for (int i = 0; i < storageCapsuleTypes.Length; i++)
			{
				IStorageCapsule instance = Activator.CreateInstance(storageCapsuleTypes[i]) as IStorageCapsule;
				if (instance != null)
				{
					_iStorageCapsuleInstances.Add(instance);
					_capsuleIDs.Add(instance.ID);
				}
			}
		}

		#region UI New

		// Capsule == ID & Storage
		// Ref == ID, Type & Storage
		// Value == Type and Value String
		// Storage == Keys > Values & Keys > Refs
		// A Key can hold 1 or more Refs or Values

		#region Items

		private class StorageItem : BaseItem
		{
			public ValKeyItem[] ValKeys
			{
				get; private set;
			}

			public RefsKeyItem[] RefsKeys
			{
				get; private set;
			}

			public Dictionary<string, StorageKeyEntry> KeyEntries
			{
				get; private set;
			}

			public override State CorruptionState
			{
				get
				{
					List<BaseKeyItem> keys = new List<BaseKeyItem>(ValKeys);
					keys.AddRange(RefsKeys);
					State worstKeysState = GetWorstState(keys.ToArray());
					return GetWorstState(worstKeysState);
				}
			}

			public StorageItem(string parentKey, IStorageDictionaryEditor storageDictionaryEditor, Dictionary<string, StorageKeyEntry> keyEntries) : base(parentKey)
			{
				KeyEntries = keyEntries;
				if (storageDictionaryEditor != null)
				{
					string[] valKeys = storageDictionaryEditor.GetValueStorageKeys();
					ValKeys = new ValKeyItem[valKeys.Length];
					for (int i = 0; i < valKeys.Length; i++)
					{
						if(!keyEntries.TryGetValue(valKeys[i], out StorageKeyEntry valueEntry))
						{
							valueEntry = new StorageKeyEntry(valKeys[i], null, false);
						}
						ValKeys[i] = new ValKeyItem(valueEntry, storageDictionaryEditor.GetValue(valKeys[i]));
					}

					string[] refKeys = storageDictionaryEditor.GetRefStorageKeys();
					RefsKeys = new RefsKeyItem[refKeys.Length];
					for (int i = 0; i < refKeys.Length; i++)
					{
						if (!keyEntries.TryGetValue(refKeys[i], out StorageKeyEntry refEntry))
						{
							refEntry = new StorageKeyEntry(refKeys[i], null, false);
						}
						RefsKeys[i] = new RefsKeyItem(refEntry, storageDictionaryEditor.GetValueRefs(refKeys[i]));
					}
				}
				else
				{
					ValKeys = new ValKeyItem[] { };
					RefsKeys = new RefsKeyItem[] { };
				}
			}

			protected override void OnRenderGUI(int layer)
			{
				for(int i = 0; i < ValKeys.Length; i++)
				{
					ValKeys[i].RenderGUI(layer + 1);
				}

				for (int i = 0; i < RefsKeys.Length; i++)
				{
					RefsKeys[i].RenderGUI(layer + 1);
				}
			}
		}

		private class CapsuleItem : BaseFoldoutItem
		{
			public string ID
			{
				get; private set;
			}

			public StorageItem StorageItem
			{
				get; private set;
			}

			public override State CorruptionState
			{
				get
				{
					return StorageItem.CorruptionState;
				}
			}

			public CapsuleItem(string id, IStorageDictionaryEditor storage, Dictionary<string, StorageKeyEntry> keyEntries) : base(id, false)
			{
				ID = id;
				StorageItem = new StorageItem(id, storage, keyEntries);
			}

			protected override void OnRenderGUI(int layer)
			{
				StorageItem.RenderGUI(layer + 1);
			}
		}

		private class RefItem : BaseItem
		{
			public string ID
			{
				get
				{
					return _editableRefValue.ReferenceID;
				}
			}

			public StorageItem StorageItem
			{
				get; private set;
			}

			public override State CorruptionState
			{
				get
				{
					return GetWorstState(GetTypeCurruptionState(), StorageItem.CorruptionState);
				}
			}

			private EditableRefValue _editableRefValue;
			private StorageKeyEntry _keyEntry;

			public RefItem(StorageKeyEntry keyEntry, EditableRefValue editableRefValue) : base(keyEntry.StorageKey)
			{
				_keyEntry = keyEntry;
				_editableRefValue = editableRefValue;
				StorageItem = new StorageItem(keyEntry.StorageKey, _editableRefValue.Storage, GetKeyEntries(_editableRefValue.ReferenceType));
			}

			protected override void OnRenderGUI(int layer)
			{
				DrawNormalItemLabel(string.Concat("- ID: ", ID));
				DrawItemLabel(string.Concat("- Type: ", GetTypeString(_editableRefValue.ReferenceType, _editableRefValue.ReferenceTypeString)), GetTypeInfoText(), GetTypeCurruptionState());
				DrawItemLabel("- Storage: ", string.Empty, StorageItem.CorruptionState);
				StorageItem.RenderGUI(layer + 1);
			}

			private string GetTypeInfoText()
			{
				if (_editableRefValue.ReferenceType == null)
				{
					return TYPE_NOT_FOUND_INFO_MESSAGE;
				}
				else
				{
					return _keyEntry.IsOfExpectedType(_editableRefValue.ReferenceType) ? string.Empty : string.Format(EXPECTED_TYPE_INFO_MESSAGE_F, _keyEntry.ExpectedType.Name, _editableRefValue.ReferenceType.Name);
				}
			}

			private State GetTypeCurruptionState()
			{
				if (_editableRefValue.ReferenceType == null)
				{
					return State.Error;
				}
				else
				{
					return _keyEntry.IsOfExpectedType(_editableRefValue.ReferenceType) ? State.Normal : State.Error;
				}
			}
		}

		private class ValItem : BaseItem
		{
			public bool IsDict
			{
				get
				{
					return _dictValue.HasValue;
				}
			}

			private SaveableValueSection _valueSection;
			private SaveableDict? _dictValue = null;
			private StorageKeyEntry _keyEntry;

			public ValItem(StorageKeyEntry keyEntry, SaveableValueSection valueSection) : base(keyEntry.StorageKey)
			{
				_keyEntry = keyEntry;
				_valueSection = valueSection;
				if(_valueSection.GetSafeValueType() == typeof(SaveableDict))
				{
					_dictValue = (SaveableDict)_valueSection.GetValue();
				}
			}

			public override State CorruptionState
			{
				get
				{
					GetCorruptStateWithInfo(out State state, out _);
					return state;
				}
			}

			protected override void OnRenderGUI(int layer)
			{
				if (GetDictState(out State keyState, out string infoKey, out State valueState, out string infoValue))
				{
					foreach (DictItem item in _dictValue.Value.Items)
					{
						GUILayout.BeginVertical(GUI.skin.box);
						DrawTypeItemLabel(string.Concat("Key: ", item.KeySection.ValueString), GetTypeString(item.KeySection.GetSafeValueType(), item.KeySection.ValueType), infoKey, keyState);
						DrawTypeItemLabel(string.Concat("Value: ", item.ValueSection.ValueString), GetTypeString(item.ValueSection.GetSafeValueType(), item.ValueSection.ValueType), infoValue, valueState);
						GUILayout.EndVertical();
					}
				}
				else
				{
					GetCorruptStateWithInfo(out State state, out string info);
					DrawTypeItemLabel(_valueSection.ValueString, GetTypeString(_valueSection.GetSafeValueType(), _valueSection.ValueType), info, state);
				}
			}

			private void GetCorruptStateWithInfo(out State state, out string info)
			{
				if (Storage.STORAGE_REFERENCE_TYPE_STRING_KEY == _keyEntry.StorageKey)
				{
					state = _keyEntry.IsOfExpectedType(_valueSection.ValueString) ? State.Normal : State.Error;
					info = state == State.Normal ? string.Empty : $"Type is not of interface `{nameof(ISaveable)}`";
					return;
				}

				if (_valueSection.GetSafeValueType() == null || _keyEntry.ExpectedType == null)
				{
					state = State.Error;
					info = state == State.Normal ? string.Empty : TYPE_NOT_FOUND_INFO_MESSAGE;
					return;
				}

				if (GetDictState(out State keyState, out string a, out State valueState, out string b))
				{
					state = GetWorstState(keyState, valueState);
					info = state == State.Normal ? string.Empty : (a.Length > b.Length ? a : b);
					return;
				}

				state = _keyEntry.IsOfExpectedType(_valueSection.GetSafeValueType()) ? State.Normal : State.Error;
				info = state == State.Normal ? string.Empty : string.Format(EXPECTED_TYPE_INFO_MESSAGE_F, _keyEntry.ExpectedType.Name, _valueSection.GetSafeValueType().Name);
			}

			private bool GetDictState(out State keyState, out string infoKey, out State valueState, out string infoValue)
			{
				infoKey = string.Empty;
				infoValue = string.Empty;
				keyState = State.Normal;
				valueState = State.Normal;

				if (IsDict)
				{
					if (_dictValue.Value.Items.Length == 0)
					{
						keyState = State.Normal;
						valueState = State.Normal;
					}
					else
					{
						if (_keyEntry.TryGetExpectedDictTypes(out Type expectedKeyType, out Type expectedValueType))
						{
							DictItem item = _dictValue.Value.Items[0];
							Type tKey = item.KeySection.GetSafeValueType();
							Type tValue = item.ValueSection.GetSafeValueType();

							keyState = tKey != null && expectedKeyType.IsAssignableFrom(tKey) ? State.Normal : State.Error;
							valueState = tValue != null && expectedValueType.IsAssignableFrom(tValue) ? State.Normal : State.Error;

							if(keyState == State.Error)
							{
								if(tKey == null)
								{
									infoKey = TYPE_NOT_FOUND_INFO_MESSAGE;
								}
								else
								{
									infoKey = string.Format(EXPECTED_TYPE_INFO_MESSAGE_F, expectedKeyType.Name, tKey.Name);
								}
							}

							if (valueState == State.Error)
							{
								if (tValue == null)
								{
									infoValue = TYPE_NOT_FOUND_INFO_MESSAGE;
								}
								else
								{
									infoValue = string.Format(EXPECTED_TYPE_INFO_MESSAGE_F, expectedValueType.Name, tValue.Name);
								}
							}
						}
					}

					return true;
				}

				return false;
			}
		}

		#endregion

		#region Key Items

		private class RefsKeyItem : BaseKeyItem
		{
			public RefItem[] RefItems
			{
				get; private set;
			}

			public override State CorruptionState
			{
				get
				{
					return GetWorstState(RefItems);
				}
			}

			public RefsKeyItem(StorageKeyEntry keyEntry, EditableRefValue[] refs) : base(keyEntry.StorageKey)
			{
				RefItems = new RefItem[refs.Length];
				for (int i = 0; i < refs.Length; i++)
				{
					RefItems[i] = new RefItem(keyEntry, refs[i]);
				}
			}

			protected override void OnRenderGUI(int layer)
			{
				if (RefItems.Length != 1)
				{
					for (int i = 0; i < RefItems.Length; i++)
					{
						EditorGUILayout.BeginVertical(GUI.skin.box);
						RefItems[i].RenderGUI(layer);
						EditorGUILayout.EndVertical();
					}
				}
				else
				{
					RefItems[0].RenderGUI(layer);
				}
			}
		}

		private class ValKeyItem : BaseKeyItem
		{
			public ValItem ValItem
			{
				get; private set;
			}

			public override State CorruptionState
			{
				get
				{
					return ValItem.CorruptionState;
				}
			}

			public ValKeyItem(StorageKeyEntry keyEntry, SaveableValueSection value) : base(keyEntry.StorageKey)
			{
				ValItem = new ValItem(keyEntry, value);
			}

			protected override void OnRenderGUI(int layer)
			{
				ValItem.RenderGUI(layer + 1);
			}
		}

		private abstract class BaseKeyItem : BaseFoldoutItem
		{
			public BaseKeyItem(string key) : base(key, false)
			{

			}
		}

		#endregion

		#endregion

		private abstract class BaseFoldoutItem : BaseItem
		{
			public bool IsOpen
			{
				get; private set;
			}

			public string Title
			{
				get; private set;
			}

			public BaseFoldoutItem(string key, bool defaultIsOpenValue) : base(key)
			{
				Title = key;
				IsOpen = defaultIsOpenValue;
			}

			public override void RenderGUI(int layer)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Space(layer * 5);

				GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);

				Color? color = GetCorruptionStateColor(CorruptionState);

				if (color.HasValue)
				{
					foldoutStyle.normal.textColor = color.Value;
				}

				IsOpen = EditorGUILayout.Foldout(IsOpen, Title + " " + GetCorruptionStateIcon(CorruptionState), foldoutStyle);
				GUILayout.EndHorizontal();

				if (IsOpen)
				{
					GUILayout.BeginHorizontal();
					GUILayout.Space(layer * 10);
					GUILayout.BeginVertical(GUI.skin.box);
					base.RenderGUI(layer);
					GUILayout.EndVertical();
					GUILayout.EndHorizontal();
				}
			}
		}

		private abstract class BaseItem
		{
			public enum State
			{
				Normal = 0,
				Warning = 1,
				Error = 2
			}

			public abstract State CorruptionState
			{
				get;
			}

			public string Key
			{
				get; private set;
			}

			public BaseItem(string key)
			{
				Key = key;
			}

			public virtual void RenderGUI(int layer)
			{
				OnRenderGUI(layer);
			}

			protected abstract void OnRenderGUI(int layer);

			protected string GetCorruptionStateIcon(State state)
			{
				switch (state)
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

			protected void DrawNormalItemLabel(string labelValue, string infoText = "")
			{
				DrawItemLabel(labelValue, infoText, State.Normal);
			}

			protected void DrawItemLabel(string labelValue, string infoText = "")
			{
				DrawItemLabel(labelValue, infoText, CorruptionState);
			}

			protected void DrawItemLabel(string labelValue, string infoText, State curruptionState)
			{
				GUIStyle labelStyle = new GUIStyle(GUI.skin.label);

				string icon = GetCorruptionStateIcon(curruptionState);
				Color? color = GetCorruptionStateColor(curruptionState);

				if(color.HasValue)
				{
					labelStyle.normal.textColor = color.Value;
				}

				GUIContent labelContent;

				if(string.IsNullOrEmpty(infoText))
				{
					labelContent = new GUIContent(string.Concat(labelValue, " ", icon));
				}
				else
				{
					labelContent = new GUIContent(string.Concat(labelValue, " ", icon), string.Concat(infoText, " ", icon));
				}

				EditorGUILayout.LabelField(labelContent, labelStyle);
			}

			protected void DrawTypeItemLabel(string labelValue, string typeValue, string infoText = "")
			{
				DrawTypeItemLabel(labelValue, typeValue, infoText, CorruptionState);
			}

			protected void DrawTypeItemLabel(string labelValue, string typeValue, string infoText, State curruptionState)
			{
				DrawItemLabel(string.Concat(labelValue, " << ", typeValue), infoText, curruptionState);
			}

			protected string GetTypeString(Type type, string typeString)
			{
				return type == null ? typeString : type.Name;
			}

			protected State GetWorstState(params BaseItem[] items)
			{
				return GetWorstState(items.Select(x => x.CorruptionState).ToArray());
			}

			protected State GetWorstState(params State[] states)
			{
				State state = State.Normal;

				if (states == null || states.Length == 0)
					return state;

				for (int i = 0; i < states.Length; i++)
				{
					if (states[i] > state)
					{
						state = states[i];
					}
				}

				return state;
			}
		}
	}
}