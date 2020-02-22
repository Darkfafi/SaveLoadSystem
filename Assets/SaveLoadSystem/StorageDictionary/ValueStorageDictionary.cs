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

		public string[] GetValueStorageKeys()
		{
			if (_keyToNormalValue != null)
			{
				string[] keys = new string[_keyToNormalValue.Keys.Count];
				_keyToNormalValue.Keys.CopyTo(keys, 0);
				return keys;
			}
			return new string[] { };
		}

		public ValueStorageDictionary(string parentStorageCapsuleID)
		{
			ParentStorageCapsuleID = parentStorageCapsuleID;
			_keyToNormalValue = new Dictionary<string, SaveableValueSection>();
		}

		public ValueStorageDictionary(string parentStorageCapsuleID, Dictionary<string, SaveableValueSection> loadedValues)
		{
			ParentStorageCapsuleID = parentStorageCapsuleID;
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
			if(t.IsClass && !t.IsPrimitive && t != typeof(string))
			{
				throw new Exception(string.Format("Can't save list of values under key `{1}` for they are not of a value or primitive type!", values, key));
			}
			SaveStruct(key, SaveableArray.From(values));
		}

		public bool LoadValue<T>(string key, out T value) where T : IConvertible, IComparable
		{
			ThrowExceptionWhenISaveable("It is forbidden use this method to load an `ISaveable`! Use `LoadRef` instead!", typeof(T));
			return Load(key, out value);
		}

		public bool LoadValues<T>(string key, out T[] values) where T : IConvertible, IComparable
		{
			ThrowExceptionWhenISaveable("It is forbidden use this method to load an `ISaveable`! Use `LoadRefs` instead!", typeof(T));
			SaveableArray<T> oldSaveableArray;
			SaveableArray newSaveableArray;

			if (LoadStruct(key, out oldSaveableArray))
			{
				values = SaveableArray<T>.To(oldSaveableArray);
				return true;
			}
			else if (LoadStruct(key, out newSaveableArray))
			{
				values = SaveableArray.To<T>(newSaveableArray);
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
			SaveStruct(key, SaveableArray.From(values));
		}

		public bool LoadStruct<T>(string key, out T value) where T : struct
		{
			return Load(key, out value);
		}

		public bool LoadStructs<T>(string key, out T[] values) where T : struct
		{
			SaveableArray<T> oldSaveableArray;
			SaveableArray newSaveableArray;

			if (LoadStruct(key, out oldSaveableArray))
			{
				values = SaveableArray<T>.To(oldSaveableArray);
				return true;
			}
			else if (LoadStruct(key, out newSaveableArray))
			{
				values = SaveableArray.To<T>(newSaveableArray);
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
			SaveStruct(key, SaveableDict.From(value));
		}

		public bool LoadDict<T, U>(string key, out Dictionary<T, U> value)
		{
			ThrowExceptionWhenISaveable("It is forbidden to load a dictionary containing an `ISaveable`!", typeof(T), typeof(U));

			SaveableDict<T, U> sdOld;
			SaveableDict sdNew;

			if (LoadStruct(key, out sdOld))
			{
				value = SaveableDict<T, U>.To(sdOld);
				return true;
			}
			else if (LoadStruct(key, out sdNew))
			{
				value = SaveableDict.To<T, U>(sdNew);
				return true;
			}

			value = null;
			return false;
		}

		public Dictionary<T, U> LoadDict<T, U>(string key)
		{
			Dictionary<T, U> v;
			LoadDict(key, out v);
			return v;
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

		public SaveableValueSection GetValue(string key)
		{
			SaveableValueSection readValue;
			if(_keyToNormalValue.TryGetValue(key, out readValue))
			{
				return readValue;
			}

			return default;
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

			if (v.GetValueType() == null)
			{
				UnityEngine.Debug.LogError($"No Type found for {key}'s value {value.GetType().Name}. This means the type was removed or renamed. Please migrate this change to the correct type!");
				return false;
			}

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