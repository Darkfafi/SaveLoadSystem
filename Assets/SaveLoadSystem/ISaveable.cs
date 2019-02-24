using System;

namespace RDP.SaveLoadSystem
{
	public delegate void StorageLoadHandler<T>(T instance) where T : IRefereceSaveable;

	public interface IRefereceSaveable
	{
		void Save(IReferenceSaver saver);
		void Load(IReferenceLoader loader);
		void LoadingCompleted();
	}

	public interface IReferenceSaver
	{
		void SaveValue<T>(string key, T value) where T : IConvertible, IComparable;
		void SaveStruct<T>(string key, T value) where T : struct;
		void SaveRef<T>(string key, T value, bool allowNull = false) where T : class, IRefereceSaveable;
	}

	public interface IReferenceLoader
	{
		bool LoadValue<T>(string key, out T value) where T : IConvertible, IComparable;
		bool LoadStruct<T>(string key, out T value) where T : struct;
		bool LoadRef<T>(string key, StorageLoadHandler<T> refAvailableCallback) where T : class, IRefereceSaveable;
	}
}