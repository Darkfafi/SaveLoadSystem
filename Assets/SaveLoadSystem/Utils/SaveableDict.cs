using System;
using System.Collections.Generic;

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

			foreach (var pair in dict)
			{
				items[i] = new DictItem(pair.Key, pair.Value);
				i++;
			}

			return new SaveableDict<T, U>(items);
		}

		public static Dictionary<T, U> To(SaveableDict<T, U> saveableDict)
		{
			Dictionary<T, U> dict = new Dictionary<T, U>();

			for (int i = 0; i < saveableDict.Items.Length; i++)
			{
				dict.Add((T)saveableDict.Items[i].KeySection.GetValue(), (U)saveableDict.Items[i].ValueSection.GetValue());
			}

			return dict;
		}
	}

	[Serializable]
	public struct SaveableDict
	{
		public DictItem[] Items;

		public SaveableDict(DictItem[] items)
		{
			Items = items;
		}

		public static SaveableDict From<T, U>(Dictionary<T, U> dict)
		{
			DictItem[] items = new DictItem[dict.Count];
			int i = 0;

			foreach(var pair in dict)
			{
				items[i] = new DictItem(pair.Key, pair.Value);
				i++;
			}

			return new SaveableDict(items);
		}

		public static Dictionary<T, U> To<T, U>(SaveableDict saveableDict)
		{
			Dictionary<T, U> dict = new Dictionary<T, U>();

			for(int i = 0; i < saveableDict.Items.Length; i++)
			{
				dict.Add((T)saveableDict.Items[i].KeySection.GetValue(), (U)saveableDict.Items[i].ValueSection.GetValue());
			}

			return dict;
		}
	}

	[Serializable]
	public struct DictItem
	{
		public SaveableValueSection KeySection;
		public SaveableValueSection ValueSection;

		public DictItem(object key, object value)
		{
			KeySection = new SaveableValueSection(key);
			ValueSection = new SaveableValueSection(value);
		}
	}
}