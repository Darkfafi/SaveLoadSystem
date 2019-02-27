using RDP.SaveLoadSystem.Internal;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
	public class StorageDictionary : ValueStorageDictionary, IStorageSaver, IStorageLoader, IStorageDictionaryEditor
	{
		private Dictionary<string, object> _keyToReferenceID;
		private IEditableStorageAccess _storageAccess;

		public StorageDictionary(string parentStorageCapsuleID, IEditableStorageAccess storageAccess) : base(parentStorageCapsuleID)
		{
			_storageAccess = storageAccess;
			_keyToReferenceID = new Dictionary<string, object>();
		}

		public StorageDictionary(string parentStorageCapsuleID, IEditableStorageAccess storageAccess, Dictionary<string, object> loadedValues, Dictionary<string, object> loadedRefs) : base(parentStorageCapsuleID, loadedValues)
		{
			_storageAccess = storageAccess;
			_keyToReferenceID = loadedRefs;
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
			object refIDsObject;

			if(!_keyToReferenceID.TryGetValue(key, out refIDsObject))
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
			object refIDObject;

			if(_keyToReferenceID.TryGetValue(key, out refIDObject))
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
			return _storageAccess.GetEditableRefValue(ParentStorageCapsuleID, key);
		}

		public void RemoveValueRef(string key)
		{
			_keyToReferenceID.Remove(key);
		}

		public void SetValueRef(string key, EditableRefValue refValue)
		{
			if(_keyToReferenceID.ContainsKey(key))
			{
				_keyToReferenceID[key] = refValue.ReferenceID;
			}
			else
			{
				_keyToReferenceID.Add(key, refValue.ReferenceID);
			}
		}

		public void RelocateValueRef(string currentKey, string newKey)
		{
			object value;
			if(_keyToReferenceID.TryGetValue(currentKey, out value))
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
	}
}