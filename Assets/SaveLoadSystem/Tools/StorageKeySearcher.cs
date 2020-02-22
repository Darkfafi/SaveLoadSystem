using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RDP.SaveLoadSystem.Internal
{
	public static class StorageKeySearcher
	{
		private static Dictionary<string, StorageKeyEntry> EXCPECTION_KEY_ENTRIES = new Dictionary<string, StorageKeyEntry>()
		{
			{ Storage.STORAGE_REFERENCE_TYPE_STRING_KEY,  new StorageKeyEntry(Storage.STORAGE_REFERENCE_TYPE_STRING_KEY, typeof(ISaveable), false) },
			{ Migrator.MIGRATOR_INDEX_KEY,  new StorageKeyEntry(Migrator.MIGRATOR_INDEX_KEY, typeof(int), true) },
			{ ValueStorageDictionary.VALUE_KEYS_TO_KEEP_KEY,  new StorageKeyEntry(ValueStorageDictionary.VALUE_KEYS_TO_KEEP_KEY, typeof(string[]), true) },
			{ StorageDictionary.REF_KEYS_TO_KEEP_KEY,  new StorageKeyEntry(StorageDictionary.REF_KEYS_TO_KEEP_KEY, typeof(string[]), true) },
		};

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

			foreach(var exceptionPair in EXCPECTION_KEY_ENTRIES)
			{
				keyEntries.Add(exceptionPair.Key, exceptionPair.Value);
			}

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
			public bool IsOptional;

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