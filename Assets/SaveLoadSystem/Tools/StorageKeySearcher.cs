using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RDP.SaveLoadSystem.Internal
{
	public static class StorageKeySearcher
	{
		public static Dictionary<Type, StorageKeyEntry[]> GetSaveablesToKeyEntries()
		{
			Dictionary<Type, StorageKeyEntry[]> entries = new Dictionary<Type, StorageKeyEntry[]>();
			Type[] saveableTypes = Assembly.GetAssembly(typeof(ISaveable)).GetTypes().Where(x => x.GetInterfaces().Any(y => typeof(ISaveable).IsAssignableFrom(y))).ToArray();
			for(int i = 0; i < saveableTypes.Length; i++)
			{
				Type saveableType = saveableTypes[i];
				entries.Add(saveableType, GetKeyEntries(saveableType));
			}
			return entries;
		}

		public static StorageKeyEntry[] GetKeyEntries(Type saveableType)
		{
			if (saveableType == null)
				return new StorageKeyEntry[] { };

			FieldInfo[] fields = saveableType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
			List<StorageKeyEntry> keyEntries = new List<StorageKeyEntry>();
			foreach (FieldInfo fInfo in fields)
			{
				StorageKeyAttribute keyAttribute = fInfo.GetCustomAttribute<StorageKeyAttribute>(true);
				if (keyAttribute != null)
				{
					keyEntries.Add(new StorageKeyEntry(fInfo.GetValue(null) as string, keyAttribute.ExpectedType, keyAttribute.IsOptional));
				}
			}
			return keyEntries.ToArray();
		}

		public struct StorageKeyEntry
		{
			public string StorageKey
			{
				get; private set;
			}

			public Type ExpectedType
			{
				get; private set;
			}

			public bool IsOptional
			{
				get; private set;
			}

			public StorageKeyEntry(string storageKey, Type expectedType, bool isOptional)
			{
				StorageKey = storageKey;
				ExpectedType = expectedType;
				IsOptional = isOptional;
			}
		}
	}
}