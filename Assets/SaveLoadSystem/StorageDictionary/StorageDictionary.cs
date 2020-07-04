using RDP.SaveLoadSystem.Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
	[StorageKeysHolder(typeof(ISaveable))]
	public class StorageDictionary : ValueStorageDictionary, IStorageSaver, IStorageLoader, IStorageDictionaryEditor
	{
		[StorageKey(typeof(string[]), true)]
		public const string REF_KEYS_TO_KEEP_KEY = "RESERVED_REF_KEYS_TO_KEEP_KEY_RESERVED";

		private Dictionary<string, object> _keyToReferenceID;
		private IStorageAccess _storageAccess;

		private List<string> _keysToKeep = new List<string>();

		public bool HasRefKey(string key)
		{
			return _keyToReferenceID.ContainsKey(key);
		}

		public string[] GetRefStorageKeys()
		{
			if(_keyToReferenceID != null)
			{
				string[] keys = new string[_keyToReferenceID.Keys.Count];
				_keyToReferenceID.Keys.CopyTo(keys, 0);
				return keys;
			}
			return new string[] { };
		}

		public bool ShouldKeepRefKey(string key)
		{
			return _keysToKeep.Contains(key);
		}

		public StorageDictionary(string parentStorageCapsuleID, IStorageAccess storageAccess) : base(parentStorageCapsuleID)
		{
			_storageAccess = storageAccess;
			_keyToReferenceID = new Dictionary<string, object>();
		}

		public StorageDictionary(string parentStorageCapsuleID, IStorageAccess storageAccess, Dictionary<string, SaveableValueSection> loadedValues, Dictionary<string, object> loadedRefs) : base(parentStorageCapsuleID, loadedValues)
		{
			_storageAccess = storageAccess;
			_keyToReferenceID = loadedRefs;

			SaveableValueSection keysToKeepSection = GetValueSection(REF_KEYS_TO_KEEP_KEY);
			if(keysToKeepSection.IsValid)
			{
				_keysToKeep = new List<string>(SaveableArray.To<string>((SaveableArray)keysToKeepSection.GetValue(typeof(SaveableArray))));
			}
		}

		void IStorageReferenceSaver.SaveRef<T>(string key, T value, bool allowNull)
		{
			if(value == null)
			{
				if(!allowNull)
					Debug.LogErrorFormat("Cannot add {0} due to the value being `null`", key);
				return;
			}

			_keyToReferenceID.Add(key, _storageAccess.ActiveRefHandler.GetIdForReference(value));
			_keysToKeep.Remove(key);
			SetKeysToKeep();
		}

		void IStorageReferenceSaver.SaveRefs<T>(string key, T[] values, bool allowNull)
		{
			List<T> valuesList = new List<T>(values);
			valuesList.RemoveAll((v) => v == null);
			values = valuesList.ToArray();

			if(values == null)
			{
				if(!allowNull)
					Debug.LogErrorFormat("Cannot add {0} due to the value being `null`", key);
				return;
			}

			string idsCollection = "";
			for(int i = 0, c = values.Length; i < c; i++)
			{
				idsCollection += _storageAccess.ActiveRefHandler.GetIdForReference(values[i]);
				if(i < c - 1)
				{
					idsCollection += ",";
				}
			}

			_keyToReferenceID.Add(key, idsCollection);
			_keysToKeep.Remove(key);
			SetKeysToKeep();
		}

		bool IStorageReferenceLoader.LoadRef<T>(string key, StorageLoadHandler<T> refLoadedCallback)
		{
			string refId = GetRefID(key);

			if(string.IsNullOrEmpty(refId))
			{
				refLoadedCallback(null);
				return false;
			}

			_storageAccess.ActiveRefHandler.GetReferenceFromID(refId, (trueReferenceLoad, reference) =>
			{
				if(trueReferenceLoad)
					trueReferenceLoad = reference == null || reference.GetType().IsAssignableFrom(typeof(T)) && _keyToReferenceID.ContainsKey(key);

				if(trueReferenceLoad)
					refLoadedCallback((T)reference);
				else
					refLoadedCallback(default(T));
			});

			return true;
		}

		bool IStorageReferenceLoader.LoadRefs<T>(string key, StorageLoadMultipleHandler<T> refLoadedCallback)
		{
			if(!_keyToReferenceID.TryGetValue(key, out object refIDsObject))
			{
				refLoadedCallback(new T[] { });
				return false;
			}

			string[] refIds = refIDsObject.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			_storageAccess.ActiveRefHandler.GetReferencesFromID(key, refIds, (references) =>
			{
				if(references != null)
				{
					Array castedReferencesArray = Array.CreateInstance(typeof(T), references.Length);
					Array.Copy(references, castedReferencesArray, references.Length);
					refLoadedCallback((T[])castedReferencesArray);
				}
				else
					refLoadedCallback(new T[] { });
			});

			return true;
		}

		public string GetRefID(string key)
		{
			if(_keyToReferenceID.TryGetValue(key, out object refIDObject))
			{
				return refIDObject.ToString();
			}

			return null;
		}

		public SaveDataItem[] GetReferenceDataItems()
		{
			List<SaveDataItem> items = new List<SaveDataItem>();
			foreach(var pair in _keyToReferenceID)
			{
				items.Add(new SaveDataItem(pair.Key, pair.Value));
			}

			return items.ToArray();
		}

		public EditableRefValue GetValueRef(string key)
		{
			return _storageAccess.GetEditableRefValue(ParentStorageCapsuleID, GetRefID(key));
		}

		public EditableRefValue[] GetValueRefs(string key)
		{
			List<EditableRefValue> returnValues = new List<EditableRefValue>();
			if(!_keyToReferenceID.TryGetValue(key, out object refIDsObject))
			{
				return returnValues.ToArray();
			}
			string[] refIds = refIDsObject.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			for(int i = 0; i < refIds.Length; i++)
			{
				returnValues.Add(_storageAccess.GetEditableRefValue(ParentStorageCapsuleID, refIds[i]));
			}
			return returnValues.ToArray();
		}

		public void RemoveValueRef(string key)
		{
			_keyToReferenceID.Remove(key);

			if(_keysToKeep.Contains(key))
				_keysToKeep.Remove(key);

			SetKeysToKeep();
		}

		public void SetValueRef(string key, EditableRefValue refValue)
		{
			_keyToReferenceID[key] = refValue.ReferenceID;

			if(!_keysToKeep.Contains(key))
				_keysToKeep.Add(key);

			SetKeysToKeep();
		}

		public void SetValueRefs(string key, EditableRefValue[] refsValues)
		{
			string idsCollection = "";
			for(int i = 0, c = refsValues.Length; i < c; i++)
			{
				idsCollection += refsValues[i].ReferenceID;
				if(i < c - 1)
				{
					idsCollection += ",";
				}
			}
			_keyToReferenceID.Add(key, idsCollection);

			if(!_keysToKeep.Contains(key))
				_keysToKeep.Add(key);

			SetKeysToKeep();
		}

		public void RelocateValueRef(string currentKey, string newKey)
		{
			if(_keyToReferenceID.TryGetValue(currentKey, out object value))
			{
				string refID = value.ToString();
				RemoveValueRef(currentKey);
				SetValueRef(newKey, GetValueRef(refID));
			}
		}

		public EditableRefValue RegisterNewRefInCapsule(Type referenceType)
		{
			return _storageAccess.RegisterNewRefInCapsule(ParentStorageCapsuleID, referenceType);
		}

		private void SetKeysToKeep()
		{
			SetValue(REF_KEYS_TO_KEEP_KEY, SaveableArray.From(_keysToKeep.ToArray()));
		}
	}
}