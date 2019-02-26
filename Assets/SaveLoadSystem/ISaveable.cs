namespace RDP.SaveLoadSystem
{
	public interface ISaveableLoad : ISaveable
	{
		void Load(IStorageLoader loader);
	}

	public interface ISaveable
	{
		void Save(IStorageSaver saver);
		void LoadingCompleted();
	}
}