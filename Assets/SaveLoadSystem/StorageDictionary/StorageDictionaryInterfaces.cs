using System;
using System.Collections.Generic;

namespace RDP.SaveLoadSystem
{
	public delegate void StorageLoadHandler<T>(T instance) where T : ISaveable;
	public delegate void StorageLoadMultipleHandler<T>(T[] instance) where T : ISaveable;

	public interface IStorageSaver : IStorageValueSaver, IStorageReferenceSaver
	{

	}

	public interface IStorageLoader : IStorageValueLoader, IStorageReferenceLoader
	{

	}

	public interface IStorageReferenceSaver
	{
		void SaveRef<T>(string key, T value, bool allowNull = false) where T : class, ISaveable;
		void SaveRefs<T>(string key, T[] values, bool allowNull = false) where T : class, ISaveable;
	}

	public interface IStorageReferenceLoader
	{
		bool HasRefKey(string key);
		bool LoadRef<T>(string key, StorageLoadHandler<T> refAvailableCallback) where T : class, ISaveable;
		bool LoadRefs<T>(string key, StorageLoadMultipleHandler<T> refsAvailableCallback) where T : class, ISaveable;
	}

	public interface IStorageValueSaver
	{
		void SaveValue<T>(string key, T value) where T : IConvertible, IComparable;
		void SaveValues<T>(string key, T[] values) where T : IConvertible, IComparable;
		void SaveStruct<T>(string key, T value) where T : struct;
		void SaveStructs<T>(string key, T[] values) where T : struct;
		void SaveDict<T, U>(string key, Dictionary<T, U> value);
	}

	public interface IStorageValueLoader
	{
		bool HasValueKey(string key);
		bool LoadValue<T>(string key, out T value) where T : IConvertible, IComparable;
		bool LoadValues<T>(string key, out T[] values) where T : IConvertible, IComparable;
		bool LoadStruct<T>(string key, out T value) where T : struct;
		bool LoadStructs<T>(string key, out T[] values) where T : struct;
		T LoadValue<T>(string key) where T : IConvertible, IComparable;
		T[] LoadValues<T>(string key) where T : IConvertible, IComparable;
		T LoadStruct<T>(string key) where T : struct;
		T[] LoadStructs<T>(string key) where T : struct;
		bool LoadDict<T, U>(string key, out Dictionary<T, U> value);
		Dictionary<T, U> LoadDict<T, U>(string key);
	}
}