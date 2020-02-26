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

			// Add keys of the saveable itself
			foreach (FieldInfo fInfo in fields)
			{
				StorageKeyAttribute keyAttribute = fInfo.GetCustomAttribute<StorageKeyAttribute>(true);
				if (keyAttribute != null)
				{
					StorageKeyEntry entry = new StorageKeyEntry(fInfo.GetValue(null) as string, keyAttribute.ExpectedType, keyAttribute.IsOptional);
					AddKeyEntry(keyEntries, entry);
				}
			}

			// Add keys of StorageKeysHolders (holding keys for other types)
			StorageKeysHolderAttribute ska = saveableType.GetCustomAttribute<StorageKeysHolderAttribute>(false);
			if (ska == null || !ska.ContainerForType.IsAssignableFrom(saveableType))
			{
				Type[] storageKeysHolders = Assembly.GetAssembly(typeof(StorageKeysHolderAttribute)).GetTypes().Where(x =>
				{
					StorageKeysHolderAttribute attr = x.GetCustomAttribute<StorageKeysHolderAttribute>(false);
					return attr != null && attr.ContainerForType.IsAssignableFrom(saveableType);
				}).ToArray();

				for (int i = 0; i < storageKeysHolders.Length; i++)
				{
					Dictionary<string, StorageKeyEntry> storageKeysHolderEntries = GetKeyEntries(storageKeysHolders[i]);
					foreach (var newKeyEntryPair in storageKeysHolderEntries)
					{
						AddKeyEntry(keyEntries, newKeyEntryPair.Value);
					}
				}
			}
			return keyEntries;
		}

		private static void AddKeyEntry(Dictionary<string, StorageKeyEntry> entries, StorageKeyEntry entry)
		{
			if (!entries.ContainsKey(entry.StorageKey))
			{
				entries.Add(entry.StorageKey, entry);
			}
			else
			{
				entry.HasDuplicate = true;
				entries[entry.StorageKey] = entry;
			}
		}

		public struct StorageKeyEntry
		{
			public string StorageKey;
			public bool IsOptional;
			public bool HasDuplicate;

			public bool IsValid
			{
				get; private set;
			}

			private Type _expectedType;

			public StorageKeyEntry(string storageKey, Type expectedType, bool isOptional)
			{
				StorageKey = storageKey;
				_expectedType = expectedType;
				IsOptional = isOptional;
				HasDuplicate = false;
				IsValid = true;
			}

			public Type GetExpectedType()
			{
				return _expectedType;
			}

			public Type GetExpectedType(string targetTypeString)
			{
				try
				{
					return Type.GetType(targetTypeString);
				}
				catch
				{
					return null;
				}
			}

			public bool IsOfExpectedType(string targetTypeString)
			{
				return IsOfExpectedType(GetExpectedType(targetTypeString));
			}

			public bool IsOfExpectedType(Type targetType)
			{
				if(targetType == null)
					return false;

				return _expectedType.IsAssignableFrom(targetType);
			}

			public bool TryGetExpectedDictTypes(out Type keyType, out Type valueType)
			{
				if (!_expectedType.IsInterface && _expectedType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
				{
					Type[] arguments = _expectedType.GetGenericArguments();
					keyType = arguments[0];
					valueType = arguments[1];
					return true;
				}

				keyType = null;
				valueType = null;
				return false;
			}

			public bool TryGetExpectedArrayType(out Type arrayType)
			{
				if (_expectedType.IsArray)
				{
					arrayType = _expectedType.GetElementType();
					return true;
				}

				arrayType = null;
				return false;
			}
		}
	}
}