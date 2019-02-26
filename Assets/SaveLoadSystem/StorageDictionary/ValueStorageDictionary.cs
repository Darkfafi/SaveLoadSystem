using RDP.SaveLoadSystem.Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
	public class ValueStorageDictionary : IStorageValueSaver, IStorageValueLoader
	{
		private Dictionary<string, object> _keyToNormalValue;

		public ValueStorageDictionary()
		{
			_keyToNormalValue = new Dictionary<string, object>();
		}

		public ValueStorageDictionary(Dictionary<string, object> loadedValues)
		{
			_keyToNormalValue = loadedValues;
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

		public T LoadValue<T>(string key) where T : IConvertible, IComparable
		{
			T value;
			LoadValue(key, out value);
			return value;
		}

		public T[] LoadValues<T>(string key) where T : IConvertible, IComparable
		{
			T[] values;
			LoadValues(key, out values);
			return values;
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

		public T LoadStruct<T>(string key) where T : struct
		{
			T value;
			LoadStruct(key, out value);
			return value;
		}

		public T[] LoadStructs<T>(string key) where T : struct
		{
			T[] values;
			LoadStructs(key, out values);
			return values;
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

		public void RemoveKey(string key)
		{
			if(HasKey(key))
			{
				_keyToNormalValue.Remove(key);
			}
		}

		public bool HasKey(string key)
		{
			return _keyToNormalValue.ContainsKey(key);
		}

		public void ReplaceKeyValue(string key, object newValue)
		{
			if(HasKey(key))
			{
				_keyToNormalValue[key] = newValue;
			}
		}

		public void RelocateKeyValue(string currentKey, string newKey)
		{
			object value;
			if(_keyToNormalValue.TryGetValue(currentKey, out value))
			{
				_keyToNormalValue.Remove(currentKey);
				if(HasKey(newKey))
				{
					_keyToNormalValue[newKey] = value;
				}
				else
				{
					_keyToNormalValue.Add(newKey, value);
				}
			}
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