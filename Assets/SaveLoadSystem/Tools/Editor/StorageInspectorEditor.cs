using Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using static RDP.SaveLoadSystem.Internal.StorageKeySearcher;

namespace RDP.SaveLoadSystem.Internal
{
	public class StorageInspectorEditor : EditorWindow
	{
		[Flags]
		public enum CorruptionState
		{
			None	= 1,
			Warning = 2,
			Error	= 4
		}

		// Current Display Variables
		private Storage _currentlyViewingStorage = null;
		private List<CapsuleItem> _capsuleUIItems = new List<CapsuleItem>();

		// Editor Variables
		private Vector2 _scroll = Vector2.zero;
		private string _pathInputValue = string.Empty;
		private Storage.EncodingType _encodingTypeInputValue = Storage.EncodingType.Base64;

		private static StorageInspectorEditor _currentlyOpenStorageInspector = null;

		public static CorruptionState ValidateStorage(string storagePath, Storage.EncodingType encodingType, CorruptionState openOnState = CorruptionState.Warning | CorruptionState.Error)
		{
			CorruptionState worstCorruptionState = CorruptionState.None;
			IStorageCapsule[] capsuleInstances = GetStorageCapsuleInstances();
			CapsuleItem[] capsuleItems = LoadCapsuleItems(storagePath, encodingType, capsuleInstances);
			for(int i = 0; i < capsuleItems.Length; i++)
			{
				CapsuleItem capsuleItem = capsuleItems[i];

				if(worstCorruptionState < capsuleItem.CorruptionState)
				{
					worstCorruptionState = capsuleItem.CorruptionState;
				}

				if(openOnState.HasFlag(worstCorruptionState))
				{
					OpenWindow().LoadStorage(storagePath, encodingType);
				}
			}

			return worstCorruptionState;
		}

		private static CapsuleItem[] LoadCapsuleItems(string storagePath, Storage.EncodingType encodingType, IStorageCapsule[] storageCapsules)
		{
			List<CapsuleItem> loadedItems = new List<CapsuleItem>();
			Storage storage = new Storage(storagePath, encodingType, storageCapsules);
			List<ReadStorageResult> results = storage.Read(storageCapsules.Select(x => x.ID).ToArray());

			for (int i = 0; i < results.Count; i++)
			{
				ReadStorageResult result = results[i];
				IStorageCapsule storageCapsuleInstance = storageCapsules.FirstOrDefault(x => x.ID == result.CapsuleID);
				if (storageCapsuleInstance != null)
				{
					Dictionary<string, StorageKeyEntry> keyEntries = new Dictionary<string, StorageKeyEntry>();
					if (storageCapsuleInstance != null)
					{
						keyEntries = GetKeyEntries(storageCapsuleInstance.GetType());
					}
					loadedItems.Add(new CapsuleItem(result.CapsuleID, result.CapsuleStorage, keyEntries));
				}
			}

			return loadedItems.ToArray();
		}

		private static IStorageCapsule[] GetStorageCapsuleInstances()
		{
			List<IStorageCapsule> storageCapsules = new List<IStorageCapsule>();
			Type[] storageCapsuleTypes = Assembly.GetAssembly(typeof(IStorageCapsule)).GetTypes().Where(x => x.GetInterfaces().Contains(typeof(IStorageCapsule))).ToArray();
			for (int i = 0; i < storageCapsuleTypes.Length; i++)
			{
				IStorageCapsule instance = Activator.CreateInstance(storageCapsuleTypes[i]) as IStorageCapsule;
				if (instance != null)
				{
					storageCapsules.Add(instance);
				}
			}
			return storageCapsules.ToArray();
		}

		[MenuItem(InternalConsts.MENU_ITEM_PREFIX + "Storage/Storage Inspector")]
		private static void Init()
		{
			OpenWindow();
		}

		private static StorageInspectorEditor OpenWindow()
		{
			if(_currentlyOpenStorageInspector != null)
			{
				_currentlyOpenStorageInspector.Focus();
				return _currentlyOpenStorageInspector;
			}

			StorageInspectorEditor window = GetWindow<StorageInspectorEditor>();
			window.titleContent = new GUIContent("Storage Inspector");
			window.Show();
			window.Focus();
			_currentlyOpenStorageInspector = window;
			return window;
		}

		protected void Awake()
		{
			AssemblyReloadEvents.beforeAssemblyReload += Close;
		}

		protected void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Save Files Directory Path:");
			_pathInputValue = EditorGUILayout.TextField(_pathInputValue);
			if (GUILayout.Button("Try Open Location"))
			{
				string path = Storage.GetPathToStorage(_pathInputValue);
				if (System.IO.Directory.Exists(path))
				{
					EditorUtility.RevealInFinder(path);
				}
				else
				{
					Debug.LogWarning($"Path {path} does not exist!");
				}
			}
			EditorGUILayout.EndHorizontal();

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
			AssemblyReloadEvents.beforeAssemblyReload -= Close;
			_currentlyOpenStorageInspector = null;
			_capsuleUIItems = null;
			_currentlyViewingStorage = null;
		}

		public void LoadStorage(string path, Storage.EncodingType encodingType)
		{
			_capsuleUIItems.Clear();
			IStorageCapsule[] storageCapsuleInstances = GetStorageCapsuleInstances();
			_currentlyViewingStorage = new Storage(path, encodingType, storageCapsuleInstances);
			_capsuleUIItems.AddRange(LoadCapsuleItems(path, encodingType, storageCapsuleInstances));
		}

		#region UI Items

		private const string TYPE_NOT_FOUND_INFO_MESSAGE = "Type not found in project";
		private const string EXPECTED_TYPE_INFO_MESSAGE_F = "Expected type {0} but found type {1}";
		private const string KEY_VALIDATION_CORRUPT_INFO_MESSAGE = "Key Validation is corrupt";
		private const string KEY_HAS_DUPLICATE_F = "Key {0} has a duplicate!";

		// Capsule == ID & Storage
		// Ref == ID, Type & Storage
		// Value == Type and Value String
		// Storage == Keys > Values & Keys > Refs
		// A Key can hold 1 or more Refs or Values

		#region Value Items

		private class StorageItem : BaseFoldoutItem
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

			public override string TitleInfo
			{
				get
				{
					
					CorruptionState worstState = CorruptionState.None;
					string info = string.Empty;
					List<BaseKeyItem> keyItems = new List<BaseKeyItem>(ValKeys);
					keyItems.AddRange(RefsKeys);

					for (int i = 0; i < keyItems.Count; i++)
					{
						BaseKeyItem item = keyItems[i];
						if (worstState < item.CorruptionState && !string.IsNullOrEmpty(item.TitleInfo))
						{
							worstState = item.CorruptionState;
							info = item.TitleInfo;
						}
					}

					StorageKeyEntry[] missingKeys = GetMissingKeyEntries();
					if (missingKeys.Length > 0 && worstState != CorruptionState.Error)
					{
						StringBuilder messageBuilder = new StringBuilder();
						messageBuilder.AppendLine("The following keys are expected but not found:");
						for(int i = 0; i < missingKeys.Length; i++)
						{
							messageBuilder.AppendLine(string.Concat("* ", missingKeys[i].StorageKey));
						}
						return messageBuilder.ToString();
					}

					if (!string.IsNullOrEmpty(info))
					{
						return info;
					}

					return base.TitleInfo;
				}
			}

			public override CorruptionState CorruptionState
			{
				get
				{
					List<BaseKeyItem> keys = new List<BaseKeyItem>(ValKeys);
					keys.AddRange(RefsKeys);
					CorruptionState worstKeysState = GetWorstState(keys.ToArray());
					CorruptionState missingEntriesState = GetMissingKeyEntries().Length > 0 ? CorruptionState.Warning : CorruptionState.None;
					return GetWorstState(worstKeysState, missingEntriesState);
				}
			}

			public StorageItem(string parentKey, IStorageDictionaryEditor storageDictionaryEditor, Dictionary<string, StorageKeyEntry> keyEntries) 
				: base(parentKey, string.Concat("Storage: (", parentKey, ")"), false)
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
							valueEntry = new StorageKeyEntry()
							{
								StorageKey = valKeys[i],
							};
						}
						ValKeys[i] = new ValKeyItem(valueEntry, storageDictionaryEditor.GetValueSection(valKeys[i]));
					}

					string[] refKeys = storageDictionaryEditor.GetRefStorageKeys();
					RefsKeys = new RefsKeyItem[refKeys.Length];
					for (int i = 0; i < refKeys.Length; i++)
					{
						if (!keyEntries.TryGetValue(refKeys[i], out StorageKeyEntry refEntry))
						{
							refEntry = new StorageKeyEntry()
							{
								StorageKey = refKeys[i],
							};
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
					ValKeys[i].RenderGUI(layer);
				}

				for (int i = 0; i < RefsKeys.Length; i++)
				{
					RefsKeys[i].RenderGUI(layer);
				}
			}

			private StorageKeyEntry[] GetMissingKeyEntries()
			{
				List<StorageKeyEntry> missingEntries = new List<StorageKeyEntry>(KeyEntries.Select(x => x.Value).Where(x => !x.IsOptional));
				for (int i = 0; i < ValKeys.Length; i++)
				{
					StorageKeyEntry entry = missingEntries.Find(x => x.StorageKey == ValKeys[i].Key);
					if(entry.IsValid)
					{
						missingEntries.Remove(entry);
					}
				}

				for (int i = 0; i < RefsKeys.Length; i++)
				{
					StorageKeyEntry entry = missingEntries.Find(x => x.StorageKey == RefsKeys[i].Key);
					if (entry.IsValid)
					{
						missingEntries.Remove(entry);
					}
				}

				return missingEntries.ToArray();
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

			public override CorruptionState CorruptionState
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

			public override CorruptionState CorruptionState
			{
				get
				{
					return GetWorstState(GetTypeCurruptionState(), StorageItem.CorruptionState, _keyEntry.HasDuplicate ? CorruptionState.Error : CorruptionState.None);
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

			public string GetInfoText()
			{
				if(_keyEntry.HasDuplicate)
				{
					return string.Format(KEY_HAS_DUPLICATE_F, _keyEntry.StorageKey);
				}
				return GetTypeInfoText();
			}

			protected override void OnRenderGUI(int layer)
			{
				DrawNormalItemLabel(string.Concat("- ID: ", ID));
				DrawItemLabel(string.Concat("- Type: ", GetTypeString(_editableRefValue.ReferenceType, _editableRefValue.ReferenceTypeString)), GetTypeInfoText(), GetTypeCurruptionState());
				StorageItem.RenderGUI(layer);
			}

			private string GetTypeInfoText()
			{
				if (_editableRefValue.ReferenceType == null)
				{
					return TYPE_NOT_FOUND_INFO_MESSAGE;
				}
				else if (!_keyEntry.IsValid)
				{
					return KEY_VALIDATION_CORRUPT_INFO_MESSAGE;
				}
				else
				{
					return _keyEntry.IsOfExpectedType(_editableRefValue.ReferenceType) ? string.Empty : string.Format(EXPECTED_TYPE_INFO_MESSAGE_F, _keyEntry.GetExpectedType().Name, _editableRefValue.ReferenceType.Name);
				}
			}

			private CorruptionState GetTypeCurruptionState()
			{
				if (_editableRefValue.ReferenceType == null)
				{
					return CorruptionState.Error;
				}
				else if(!_keyEntry.IsValid)
				{
					return CorruptionState.Warning;
				}
				else
				{
					return _keyEntry.IsOfExpectedType(_editableRefValue.ReferenceType) ? CorruptionState.None : CorruptionState.Error;
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

			public bool IsArray
			{
				get
				{
					return _arrayValue.HasValue;
				}
			}

			private SaveableValueSection _valueSection;
			private SaveableDict? _dictValue = null;
			private SaveableArray? _arrayValue = null;
			private StorageKeyEntry _keyEntry;

			public ValItem(StorageKeyEntry keyEntry, SaveableValueSection valueSection) : base(keyEntry.StorageKey)
			{
				_keyEntry = keyEntry;
				_valueSection = valueSection;
				if(_valueSection.GetSafeValueType() == typeof(SaveableDict))
				{
					_dictValue = (SaveableDict)_valueSection.GetValue();
				}
				else if(_valueSection.GetSafeValueType() == typeof(SaveableArray))
				{
					_arrayValue = (SaveableArray)_valueSection.GetValue();
				}
			}

			public override CorruptionState CorruptionState
			{
				get
				{
					GetCorruptStateWithInfo(out CorruptionState state, out _);
					return state;
				}
			}

			protected override void OnRenderGUI(int layer)
			{
				if (GetDictState(out CorruptionState keyState, out string infoKey, out CorruptionState valueState, out string infoValue))
				{
					foreach (DictItem item in _dictValue.Value.Items)
					{
						GUILayout.BeginVertical(GUI.skin.box);
						DrawTypeItemLabel(string.Concat("Key: ", item.KeySection.ValueString), GetTypeString(item.KeySection.GetSafeValueType(), item.KeySection.ValueType), infoKey, keyState);
						DrawTypeItemLabel(string.Concat("Value: ", item.ValueSection.ValueString), GetTypeString(item.ValueSection.GetSafeValueType(), item.ValueSection.ValueType), infoValue, valueState);
						GUILayout.EndVertical();
					}
				}
				else if(GetArrayState(out CorruptionState arrayState, out string arrayInfo))
				{
					for(int i = 0; i < _arrayValue.Value.Items.Length; i++)
					{
						SaveableValueSection entry = _arrayValue.Value.Items[i];
						GUILayout.BeginVertical(GUI.skin.box);
						DrawTypeItemLabel(string.Concat(i, ": ", entry.ValueString), GetTypeString(entry.GetSafeValueType(), entry.ValueType), arrayInfo, arrayState);
						GUILayout.EndVertical();
					}
				}
				else
				{
					GetCorruptStateWithInfo(out CorruptionState state, out string info);
					DrawTypeItemLabel(_valueSection.ValueString, GetTypeString(_valueSection.GetSafeValueType(), _valueSection.ValueType), info, state);
				}
			}

			public void GetCorruptStateWithInfo(out CorruptionState state, out string info)
			{
				if(!_keyEntry.IsValid)
				{
					state = CorruptionState.Warning;
					info = KEY_VALIDATION_CORRUPT_INFO_MESSAGE;
					return;
				}

				if (Storage.STORAGE_REFERENCE_TYPE_STRING_KEY == _keyEntry.StorageKey)
				{
					state = _keyEntry.IsOfExpectedType(_valueSection.ValueString) ? CorruptionState.None : CorruptionState.Error;
					info = state == CorruptionState.None ? string.Empty : $"Type is not of interface `{nameof(ISaveable)}`";
					return;
				}

				if(_keyEntry.HasDuplicate)
				{
					state = CorruptionState.Error;
					info = string.Format(KEY_HAS_DUPLICATE_F, _keyEntry.StorageKey);
					return;
				}

				if (_valueSection.GetSafeValueType() == null || _keyEntry.GetExpectedType() == null)
				{
					state = CorruptionState.Error;
					info = state == CorruptionState.None ? string.Empty : TYPE_NOT_FOUND_INFO_MESSAGE;
					return;
				}

				if (GetDictState(out CorruptionState keyState, out string a, out CorruptionState valueState, out string b))
				{
					state = GetWorstState(keyState, valueState);
					info = state == CorruptionState.None ? string.Empty : (a.Length > b.Length ? a : b);
					return;
				}

				if(GetArrayState(out CorruptionState arrayState, out string arrayInfo))
				{
					state = arrayState;
					info = arrayInfo;
					return;
				}

				state = _keyEntry.IsOfExpectedType(_valueSection.GetSafeValueType()) ? CorruptionState.None : CorruptionState.Error;
				info = state == CorruptionState.None ? string.Empty : string.Format(EXPECTED_TYPE_INFO_MESSAGE_F, _keyEntry.GetExpectedType().Name, _valueSection.GetSafeValueType().Name);
			}

			private bool GetArrayState(out CorruptionState arrayState, out string info)
			{
				info = string.Empty;
				arrayState = CorruptionState.None;

				if(IsArray)
				{
					if (_arrayValue.Value.Items.Length > 0)
					{
						if (_keyEntry.TryGetExpectedArrayType(out Type expectedArrayType))
						{
							SaveableValueSection item = _arrayValue.Value.Items[0];
							arrayState = item.GetSafeValueType() != null && expectedArrayType.IsAssignableFrom(item.GetSafeValueType()) ? CorruptionState.None : CorruptionState.Error;

							if (arrayState == CorruptionState.Error)
							{
								if (item.GetSafeValueType() == null)
								{
									info = TYPE_NOT_FOUND_INFO_MESSAGE;
								}
								else
								{
									info = string.Format(EXPECTED_TYPE_INFO_MESSAGE_F, expectedArrayType.Name, item.GetSafeValueType().Name);
								}
							}
						}
					}
					return true;
				}

				return false;
			}

			private bool GetDictState(out CorruptionState keyState, out string infoKey, out CorruptionState valueState, out string infoValue)
			{
				infoKey = string.Empty;
				infoValue = string.Empty;
				keyState = CorruptionState.None;
				valueState = CorruptionState.None;

				if (IsDict)
				{
					if (_dictValue.Value.Items.Length > 0)
					{
						if (_keyEntry.TryGetExpectedDictTypes(out Type expectedKeyType, out Type expectedValueType))
						{
							DictItem item = _dictValue.Value.Items[0];
							Type tKey = item.KeySection.GetSafeValueType();
							Type tValue = item.ValueSection.GetSafeValueType();

							keyState = tKey != null && expectedKeyType.IsAssignableFrom(tKey) ? CorruptionState.None : CorruptionState.Error;
							valueState = tValue != null && expectedValueType.IsAssignableFrom(tValue) ? CorruptionState.None : CorruptionState.Error;

							if(keyState == CorruptionState.Error)
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

							if (valueState == CorruptionState.Error)
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

			public override CorruptionState CorruptionState
			{
				get
				{
					return GetWorstState(RefItems);
				}
			}

			public override string TitleInfo
			{
				get
				{
					string infoText = string.Empty;
					if(RefItems.Length > 0)
					{
						infoText = RefItems[0].GetInfoText();
					}

					if (string.IsNullOrEmpty(infoText))
					{
						return base.TitleInfo;
					}

					return infoText;
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

			public override CorruptionState CorruptionState
			{
				get
				{
					return GetWorstState(ValItem.CorruptionState);
				}
			}

			public override string TitleInfo
			{
				get
				{
					ValItem.GetCorruptStateWithInfo(out _, out string info);
					if(string.IsNullOrEmpty(info))
					{
						return base.TitleInfo;
					}
					return info;
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

		#endregion

		#region Base Items

		private abstract class BaseKeyItem : BaseFoldoutItem
		{
			public BaseKeyItem(string key) : base(key, false)
			{

			}
		}

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

			public virtual string TitleInfo
			{
				get
				{
					return string.Empty;
				}
			}

			public BaseFoldoutItem(string key, string title, bool defaultIsOpenValue) : this(key, defaultIsOpenValue)
			{
				Title = title;
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

				GUIContent titleContent;
				string titleValue = string.Concat(Title, " ", GetCorruptionStateIcon(CorruptionState));

				if(string.IsNullOrEmpty(TitleInfo))
				{
					titleContent = new GUIContent(titleValue);
				}
				else
				{
					titleContent = new GUIContent(titleValue, TitleInfo);
				}

				IsOpen = EditorGUILayout.Foldout(IsOpen, titleContent, foldoutStyle);
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
			public abstract CorruptionState CorruptionState
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

			protected string GetCorruptionStateIcon(CorruptionState state)
			{
				switch (state)
				{
					case CorruptionState.Error:
						return "[!]";
					case CorruptionState.Warning:
						return "[?]";
					default:
						return string.Empty;
				}
			}

			protected Color? GetCorruptionStateColor(CorruptionState state)
			{
				switch (state)
				{
					case CorruptionState.Error:
						return Color.red;
					case CorruptionState.Warning:
						return new Color(1f, 0.65f, 0f);
					default:
						return null;
				}
			}

			protected void DrawNormalItemLabel(string labelValue, string infoText = "")
			{
				DrawItemLabel(labelValue, infoText, CorruptionState.None);
			}

			protected void DrawItemLabel(string labelValue, string infoText = "")
			{
				DrawItemLabel(labelValue, infoText, CorruptionState);
			}

			protected void DrawItemLabel(string labelValue, string infoText, CorruptionState curruptionState)
			{
				GUIStyle labelStyle = new GUIStyle(GUI.skin.label);

				string icon = GetCorruptionStateIcon(curruptionState);
				Color? color = GetCorruptionStateColor(curruptionState);

				if (color.HasValue)
				{
					labelStyle.normal.textColor = color.Value;
				}

				GUIContent labelContent;

				if (string.IsNullOrEmpty(infoText))
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

			protected void DrawTypeItemLabel(string labelValue, string typeValue, string infoText, CorruptionState curruptionState)
			{
				DrawItemLabel(string.Concat(labelValue, " << ", typeValue), infoText, curruptionState);
			}

			protected string GetTypeString(Type type, string typeString)
			{
				return type == null ? typeString : type.Name;
			}

			protected CorruptionState GetWorstState(params BaseItem[] items)
			{
				return GetWorstState(items.Select(x => x.CorruptionState).ToArray());
			}

			protected CorruptionState GetWorstState(params CorruptionState[] states)
			{
				CorruptionState state = CorruptionState.None;

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

		#endregion

		#endregion
	}
}