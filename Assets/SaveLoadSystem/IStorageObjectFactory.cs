using System;

namespace RDP.SaveLoadSystem.Internal
{
	public interface IStorageObjectFactory
	{
		ISaveable LoadSaveableObject(ulong id, IStorageLoader loader);
		Type GetTypeForId(ulong id);
		ulong GetIdForSaveable<T>() where T : ISaveable;
		ulong GetIdForSaveable(Type type);
	}
}