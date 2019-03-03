using RDP.SaveLoadSystem.Internal;
using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;

namespace RDP.SaveLoadSystem
{
	public class ValueStorageDictionary : IStorageValueSaver, IStorageValueLoader, IValueStorageDictionaryEditor
	{
		public string ParentStorageCapsuleID
		{
			get; private set;
		}

		private Dictionary<string, SaveableValueSection> _keyToNormalValue;

		public ValueStorageDictionary(string parentStorageCapsuleID)
		{
			_keyToNormalValue = new Dictionary<string, SaveableValueSection>();
		}

		public ValueStorageDictionary(string parentStorageCapsuleID, Dictionary<string, SaveableValueSection> loadedValues)
		{
			_keyToNormalValue = loadedValues;
		}

		public void SaveValue<T>(string key, T value) where T : IConvertible, IComparable
		{
			Type t = typeof(T);
			ThrowExceptionWhenISaveable("It is forbidden use this method to save an `ISaveable`! Use `SaveRef` instead!", t);
			if(t.IsClass && !t.IsPrimitive && t != typeof(string))
			{
				throw new Exception(string.Format("Can't save value `{0}` under key `{1}` for it is not of a value or primitive type!", value, key));
			}
			Save(key, value, t);
		}

		public void SaveValues<T>(string key, T[] values) where T : IConvertible, IComparable
		{
			Type t = typeof(T);
			ThrowExceptionWhenISaveable("It is forbidden use this method to save an `ISaveable`! Use `SaveRefs` instead!", t);
			if(t.IsClass && !t.IsPrimitive)
			{
				throw new Exception(string.Format("Can't save list of values under key `{1}` for they are not of a value or primitive type!", values, key));
			}
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
			Save(key, value, typeof(T));
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

		public void SetValue(string key, object value)
		{
			if(_keyToNormalValue.ContainsKey(key))
			{
				_keyToNormalValue[key] = new SaveableValueSection(value, value.GetType());
			}
			else
			{
				_keyToNormalValue.Add(key, new SaveableValueSection(value, value.GetType()));
			}
		}

		public object GetValue(string key)
		{
			SaveableValueSection readValue;
			if(_keyToNormalValue.TryGetValue(key, out readValue))
			{
				return readValue.GetValue();
			}

			return null;
		}

		public void RemoveValue(string key)
		{
			_keyToNormalValue.Remove(key);
		}

		public void RelocateValue(string currentKey, string newKey)
		{
			SaveableValueSection value;
			if(_keyToNormalValue.TryGetValue(currentKey, out value))
			{
				_keyToNormalValue.Remove(currentKey);
				SetValue(newKey, value.GetValue());
			}
		}

		public SaveDataItem[] GetValueDataItems()
		{
			List<SaveDataItem> items = new List<SaveDataItem>();
			foreach(var pair in _keyToNormalValue)
			{
				items.Add(new SaveDataItem(pair.Key, pair.Value.GetValue()));
			}

			return items.ToArray();
		}

		private void Save(string key, object value, Type specifiedType)
		{
			_keyToNormalValue.Add(key, new SaveableValueSection(value, specifiedType));
		}

		private bool Load<T>(string key, out T value)
		{
			SaveableValueSection v;
			value = default(T);

			if(!_keyToNormalValue.TryGetValue(key, out v))
				return false;

			if(v.GetValueType().IsAssignableFrom(typeof(T)))
			{
				value = (T)v.GetValue();
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