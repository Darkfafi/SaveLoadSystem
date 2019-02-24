using System;
using System.Collections.Generic;
using UnityEngine;

namespace RDP.SaveLoadSystem.Internal.Utils
{
	[Serializable]
	public struct SaveableDict<T, U>
	{
		public DictItem[] Items;

		public SaveableDict(DictItem[] items)
		{
			Items = items;
		}

		public static SaveableDict<T, U> From(Dictionary<T, U> dict)
		{
			DictItem[] items = new DictItem[dict.Count];
			int i = 0;

			foreach(var pair in dict)
			{
				items[i] = new DictItem(pair.Key, pair.Value);
				i++;
			}

			return new SaveableDict<T, U>(items);
		}

		public static Dictionary<T, U> To(SaveableDict<T, U> saveableDict)
		{
			Dictionary<T, U> dict = new Dictionary<T, U>();

			for(int i = 0; i < saveableDict.Items.Length; i++)
			{
				dict.Add((T)saveableDict.Items[i].GetKey(), (U)saveableDict.Items[i].GetValue());
			}

			return dict;
		}
	}

	[Serializable]
	public struct DictItem
	{
		public string SectionKeyString;
		public string SectionValueString;
		public string KeyType;
		public string ValueType;

		public DictItem(object key, object value)
		{
			SectionKeyString = PrimitiveToValueParserUtility.ToJSON(key);
			SectionValueString = PrimitiveToValueParserUtility.ToJSON(value);
			KeyType = key.GetType().AssemblyQualifiedName;
			ValueType = value.GetType().AssemblyQualifiedName;
		}

		public object GetValue()
		{
			return PrimitiveToValueParserUtility.FromJSON(SectionValueString, Type.GetType(ValueType));
		}

		public object GetKey()
		{
			return PrimitiveToValueParserUtility.FromJSON(SectionKeyString, Type.GetType(KeyType));
		}
	}
}