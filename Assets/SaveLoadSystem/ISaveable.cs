using System;
using System.Collections.Generic;

namespace RDP.SaveLoadSystem
{
	public delegate void StorageLoadHandler<T>(T instance) where T : ISaveable;
	public delegate void StorageLoadMultipleHandler<T>(T[] instance) where T : ISaveable;

	public interface ISaveableLoad : ISaveable
	{
		void Load(IReferenceLoader loader);
	}

	public interface ISaveable
	{
		void Save(IReferenceSaver saver);
		void LoadingCompleted();
	}

	public interface IReferenceSaver
	{
		void SaveValue<T>(string key, T value) where T : IConvertible, IComparable;
		void SaveStruct<T>(string key, T value) where T : struct;
		void SaveDict<T, U>(string key, Dictionary<T, U> value);
		void SaveRef<T>(string key, T value, bool allowNull = false) where T : class, ISaveable;
		void SaveRefs<T>(string key, T[] values, bool allowNull = false) where T : class, ISaveable;
	}

	public interface IReferenceLoader
	{
		bool LoadValue<T>(string key, out T value) where T : IConvertible, IComparable;
		bool LoadStruct<T>(string key, out T value) where T : struct;
		bool LoadDict<T, U>(string key, out Dictionary<T, U> value);
		bool LoadRef<T>(string key, StorageLoadHandler<T> refAvailableCallback) where T : class, ISaveable;
		bool LoadRefs<T>(string key, StorageLoadMultipleHandler<T> refsAvailableCallback) where T : class, ISaveable;
	}
}