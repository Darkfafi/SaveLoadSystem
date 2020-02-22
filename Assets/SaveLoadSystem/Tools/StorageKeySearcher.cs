using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RDP.SaveLoadSystem.Internal
{
	public static class StorageKeySearcher
	{
		public static Dictionary<Type, Dictionary<string, StorageKeyEntry>> GetSaveablesToKeyEntries()
		{
			Dictionary<Type, Dictionary<string, StorageKeyEntry>> entries = new Dictionary<Type, Dictionary<string, StorageKeyEntry>>();
			Type[] saveableTypes = Assembly.GetAssembly(typeof(ISaveable)).GetTypes().Where(x => x.GetInterfaces().Any(y => typeof(ISaveable).IsAssignableFrom(y))).ToArray();
			for(int i = 0; i < saveableTypes.Length; i++)
			{
				Type saveableType = saveableTypes[i];
				entries.Add(saveableType, GetKeyEntries(saveableType));
			}
			return entries;
		}

		public static Dictionary<string, StorageKeyEntry> GetKeyEntries(Type saveableType)
		{
			if (saveableType == null)
				return new Dictionary<string, StorageKeyEntry>();

			FieldInfo[] fields = saveableType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
			Dictionary<string, StorageKeyEntry> keyEntries = new Dictionary<string, StorageKeyEntry>();
			keyEntries.Add(Storage.STORAGE_REFERENCE_TYPE_STRING_KEY, new StorageKeyEntry(Storage.STORAGE_REFERENCE_TYPE_STRING_KEY, typeof(ISaveable), false));
			foreach (FieldInfo fInfo in fields)
			{
				StorageKeyAttribute keyAttribute = fInfo.GetCustomAttribute<StorageKeyAttribute>(true);
				if (keyAttribute != null)
				{
					string key = fInfo.GetValue(null) as string;
					keyEntries.Add(key, new StorageKeyEntry(key, keyAttribute.ExpectedType, keyAttribute.IsOptional));
				}
			}
			return keyEntries;
		}

		public struct StorageKeyEntry
		{
			public string StorageKey;
			public Type ExpectedType;
			public bool IsOptional;

			public bool IsValid
			{
				get; private set;
			}

			public StorageKeyEntry(string storageKey, Type expectedType, bool isOptional)
			{
				StorageKey = storageKey;
				ExpectedType = expectedType;
				IsOptional = isOptional;
				IsValid = true;
			}

			public bool IsOfExpectedType(string targetTypeString)
			{
				Type safeType;

				try
				{
					safeType = Type.GetType(targetTypeString);
				}
				catch
				{
					safeType = null;
				}

				return IsOfExpectedType(safeType);
			}

			public bool IsOfExpectedType(Type targetType)
			{
				if(targetType == null)
					return false;

				return ExpectedType.IsAssignableFrom(targetType);
			}

			public bool TryGetExpectedDictTypes(out Type keyType, out Type valueType)
			{
				if (!ExpectedType.IsInterface && ExpectedType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
				{
					Type[] arguments = ExpectedType.GetGenericArguments();
					keyType = arguments[0];
					valueType = arguments[1];
					return true;
				}

				keyType = null;
				valueType = null;
				return false;
			}
		}
	}
}