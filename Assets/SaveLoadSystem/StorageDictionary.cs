using RDP.SaveLoadSystem.Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
	public class StorageDictionary : IReferenceSaver, IReferenceLoader
	{
		private Dictionary<string, object> _keyToNormalValue;
		private Dictionary<string, object> _keyToReferenceID;

		private SaveableReferenceIdHandler _refHandler;

		public StorageDictionary()
		{
			_keyToNormalValue = new Dictionary<string, object>();
			_keyToReferenceID = new Dictionary<string, object>();
		}

		public StorageDictionary(Dictionary<string, object> loadedValues, Dictionary<string, object> loadedRefs)
		{
			_keyToNormalValue = loadedValues;
			_keyToReferenceID = loadedRefs;
		}

		public void Using(SaveableReferenceIdHandler refHandler)
		{
			_refHandler = refHandler;
		}

		public void StopUsing()
		{
			_refHandler = null;
		}

		public void SaveValue<T>(string key, T value) where T : IConvertible, IComparable
		{
			ThrowExceptionWhenISaveable("It is forbidden use this method to save an `ISaveable`! Use `SaveRef` instead!", typeof(T));
			Save(key, value);
		}

		public void SaveValues<T>(string key, T[] values) where T : IConvertible, IComparable
		{
			ThrowExceptionWhenISaveable("It is forbidden use this method to save an `ISaveable`! Use `SaveRefs` instead!", typeof(T));
			SaveStruct(key, SaveableArray<T>.From(values));
		}

		public bool LoadValue<T>(string key, out T value) where T : IConvertible, IComparable
		{
			ThrowExceptionWhenISaveable("It is forbidden use this method to load an `ISaveable`! Use `LoadRef` instead!", typeof(T));
			return Load(key, out value);
		}

		public bool LoadValues<T>(string key, out T[] values) where T : IConvertible, IComparable
		{
			ThrowExceptionWhenISaveable("It is forbidden use this method to load an `ISaveable`! Use `LoadRefs` instead!", typeof(T));
			SaveableArray<T> saveableArray;
			if(LoadStruct(key, out saveableArray))
			{
				values = SaveableArray<T>.To(saveableArray);
				return true;
			}

			values = null;
			return false;
		}

		public void SaveStruct<T>(string key, T value) where T : struct
		{
			Save(key, value);
		}

		public void SaveStructs<T>(string key, T[] values) where T : struct
		{
			SaveStruct(key, SaveableArray<T>.From(values));
		}

		public bool LoadStruct<T>(string key, out T value) where T : struct
		{
			return Load(key, out value);
		}

		public bool LoadStructs<T>(string key, out T[] values) where T : struct
		{
			SaveableArray<T> saveableArray;
			if(LoadStruct(key, out saveableArray))
			{
				values = SaveableArray<T>.To(saveableArray);
				return true;
			}

			values = null;
			return false;
		}

		public void SaveDict<T, U>(string key, Dictionary<T, U> value)
		{
			ThrowExceptionWhenISaveable("It is forbidden to save a dictionary containing an `ISaveable`!", typeof(T), typeof(U));
			SaveStruct(key, SaveableDict<T, U>.From(value));
		}

		public bool LoadDict<T, U>(string key, out Dictionary<T, U> value)
		{
			ThrowExceptionWhenISaveable("It is forbidden to load a dictionary containing an `ISaveable`!", typeof(T), typeof(U));
			SaveableDict<T, U> saveableDict;
			if(LoadStruct(key, out saveableDict))
			{
				value = SaveableDict<T, U>.To(saveableDict);
				return true;
			}

			value = null;
			return false;
		}

		void IReferenceSaver.SaveRef<T>(string key, T value, bool allowNull)
		{
			if(value == null)
			{
				if(!allowNull)
					Debug.LogErrorFormat("Cannot add {0} due to the value being `null`", key);
				return;
			}

			_keyToReferenceID.Add(key, _refHandler.GetIdForReference(value));
		}

		void IReferenceSaver.SaveRefs<T>(string key, T[] values, bool allowNull)
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

		bool IReferenceLoader.LoadRef<T>(string key, StorageLoadHandler<T> refLoadedCallback)
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

		bool IReferenceLoader.LoadRefs<T>(string key, StorageLoadMultipleHandler<T> refLoadedCallback)
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

		public SaveDataItem[] GetValueDataItems()
		{
			List<SaveDataItem> items = new List<SaveDataItem>();
			foreach(var pair in _keyToNormalValue)
			{
				items.Add(new SaveDataItem(pair.Key, pair.Value));
			}

			return items.ToArray();
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

		private void Save(string key, object value)
		{
			_keyToNormalValue.Add(key, value);
		}

		private bool Load<T>(string key, out T value)
		{
			object v;
			value = default(T);

			if(!_keyToNormalValue.TryGetValue(key, out v))
				return false;

			if(v.GetType().IsAssignableFrom(typeof(T)))
			{
				value = (T)v;
				return true;
			}

			return false;
		}

		private void ThrowExceptionWhenISaveable(string message, params Type[] typesToCheck)
		{
			Type iSaveableType = typeof(ISaveable);

			for(int i = 0; i < typesToCheck.Length; i++)
			{
				if(iSaveableType.IsAssignableFrom(typesToCheck[i]))
				{
					throw new Exception(message);
				}
			}
		}
	}
}