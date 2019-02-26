using RDP.SaveLoadSystem.Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
	public class StorageDictionary : ValueStorageDictionary, IStorageSaver, IStorageLoader
	{
		private Dictionary<string, object> _keyToReferenceID;
		private SaveableReferenceIdHandler _refHandler;

		public StorageDictionary() : base()
		{
			_keyToReferenceID = new Dictionary<string, object>();
		}

		public StorageDictionary(Dictionary<string, object> loadedValues, Dictionary<string, object> loadedRefs) : base(loadedValues)
		{
			_keyToReferenceID = loadedRefs;
		}

		public void HandlingRefs(SaveableReferenceIdHandler refHandler)
		{
			_refHandler = refHandler;
		}

		public void StopHandlingRefs()
		{
			_refHandler = null;
		}

		void IStorageReferenceSaver.SaveRef<T>(string key, T value, bool allowNull)
		{
			if(value == null)
			{
				if(!allowNull)
					Debug.LogErrorFormat("Cannot add {0} due to the value being `null`", key);
				return;
			}

			_keyToReferenceID.Add(key, _refHandler.GetIdForReference(value));
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
				idsCollection += _refHandler.GetIdForReference(values[i]);
				if(i < c - 1)
				{
					idsCollection += ",";
				}
			}

			_keyToReferenceID.Add(key, idsCollection);
		}

		bool IStorageReferenceLoader.LoadRef<T>(string key, StorageLoadHandler<T> refLoadedCallback)
		{
			object refIDObject;

			if(!_keyToReferenceID.TryGetValue(key, out refIDObject))
			{
				refLoadedCallback(null);
				return false;
			}

			string refId = refIDObject.ToString();

			_refHandler.GetReferenceFromID(refId, (trueReferenceLoad, reference) =>
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

			_refHandler.GetReferencesFromID(key, refIds, (references) =>
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

		public SaveDataItem[] GetReferenceDataItems()
		{
			List<SaveDataItem> items = new List<SaveDataItem>();
			foreach(var pair in _keyToReferenceID)
			{
				items.Add(new SaveDataItem(pair.Key, pair.Value));
			}

			return items.ToArray();
		}
	}
}