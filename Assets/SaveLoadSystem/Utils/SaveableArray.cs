using System;

namespace RDP.SaveLoadSystem.Internal.Utils
{
	[Serializable]
	public struct SaveableArray<T>
	{
		public SaveableValueSection[] Items;

		public SaveableArray(SaveableValueSection[] items)
		{
			Items = items;
		}

		public static SaveableArray<T> From(T[] array)
		{
			SaveableValueSection[] items = new SaveableValueSection[array.Length];
			for(int i = 0; i < array.Length; i++)
			{
				items[i] = new SaveableValueSection(array[i]);
			}
			return new SaveableArray<T>(items);
		}

		public static T[] To(SaveableArray<T> saveableArray)
		{
			T[] array = new T[saveableArray.Items.Length];

			for(int i = 0; i < array.Length; i++)
			{
				array[i] = (T)saveableArray.Items[i].GetValue();
			}

			return array;
		}
	}
}