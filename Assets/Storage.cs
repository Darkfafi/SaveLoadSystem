using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public delegate void StorageLoadHandler(bool wasInStorage, IRefereceSaveable instance);
public delegate void StorageLoadHandler<T>(bool wasInStorage, T instance) where T : IRefereceSaveable;

public class Storage
{
	public const string ROOT_SAVE_DATA_CAPSULE_ID = "ID_CAPSULE_SAVE_DATA";
	public const string KEY_REFERENCE_TYPE_STRING = "RESERVED_REFERENCE_TYPE_FULL_NAME_STRING_RESERVED";
	public const string SAVE_FILE_EXTENSION = "rdpsf";

	private Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>> _cachedStorageCapsules = new Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>>();

	public static string GetPathToStorageCapsule(IStorageCapsule capsule, bool addFileType)
	{
		return Path.Combine(Application.persistentDataPath, capsule.ID + (addFileType ? "." + SAVE_FILE_EXTENSION : ""));
	}

	public Storage(string storageLocation, params IStorageCapsule[] allStorageCapsules)
	{
		for(int i = 0, c = allStorageCapsules.Length; i < c; i++)
		{
			_cachedStorageCapsules.Add(allStorageCapsules[i], null);
			RefreshCachedData(allStorageCapsules[i]);
		}
	}

	public void Load(params string[] storageCapsuleIDs)
	{
		using(SaveableReferenceIDManager refHandler = new SaveableReferenceIDManager())
		{
			foreach(var capsuleToStorage in _cachedStorageCapsules)
			{
				if(storageCapsuleIDs == null || storageCapsuleIDs.Length == 0 || Array.IndexOf(storageCapsuleIDs, capsuleToStorage.Key.ID) >= 0)
				{
					if(capsuleToStorage.Value == null)
					{
						RefreshCachedData(capsuleToStorage.Key);
					}

					List<IRefereceSaveable> _allLoadedReferences = new List<IRefereceSaveable>();

					Action<string> referenceRequestedEventAction = (id) =>
					{
						string classTypeFullName;
						StorageDictionary storage = new StorageDictionary();

						if(capsuleToStorage.Value.ContainsKey(id))
						{
							storage = capsuleToStorage.Value[id];
						}


						storage.Using(refHandler);

						if(id == ROOT_SAVE_DATA_CAPSULE_ID)
						{
							capsuleToStorage.Key.Load(storage);
						}
						else if(storage.LoadValue(KEY_REFERENCE_TYPE_STRING, out classTypeFullName))
						{
							IRefereceSaveable referenceInstance = Activator.CreateInstance(Type.GetType(classTypeFullName)) as IRefereceSaveable;
							refHandler.SetReferenceReady(referenceInstance, id);
							referenceInstance.Load(storage);
							_allLoadedReferences.Add(referenceInstance);
						}
						else
						{
							Debug.LogErrorFormat("UNABLE TO LOAD REFERENCE ID {0}'s CLASS TYPE NAME", id);
						}

						storage.StopUsing();
					};

					refHandler.ReferenceRequestedEvent += referenceRequestedEventAction;

					referenceRequestedEventAction(ROOT_SAVE_DATA_CAPSULE_ID);

					refHandler.LoadRemainingAsNull();

					for(int i = _allLoadedReferences.Count - 1; i >= 0; i--)
					{
						_allLoadedReferences[i].LoadingCompleted();
					}

					_allLoadedReferences = null;
				}
			}
		}
	}

	public void Save(params string[] storageCapsuleIDs)
	{
		Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>> buffer = new Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>>();
		using(SaveableReferenceIDManager refHandler = new SaveableReferenceIDManager())
		{
			foreach(var pair in _cachedStorageCapsules)
			{
				if(storageCapsuleIDs == null || storageCapsuleIDs.Length == 0 || Array.IndexOf(storageCapsuleIDs, pair.Key.ID) >= 0)
				{

					Dictionary<string, StorageDictionary> referencesSaved = new Dictionary<string, StorageDictionary>();

					Action<string, IRefereceSaveable> refDetectedAction = (refID, referenceInstance) =>
					{
						if(!referencesSaved.ContainsKey(refID))
						{
							StorageDictionary storageDictForRef = new StorageDictionary();
							storageDictForRef.Using(refHandler);
							referencesSaved.Add(refID, storageDictForRef);
							storageDictForRef.SaveValue(KEY_REFERENCE_TYPE_STRING, referenceInstance.GetType().FullName);
							referenceInstance.Save(storageDictForRef);
							storageDictForRef.StopUsing();
						}
					};

					refHandler.IdForReferenceCreatedEvent += refDetectedAction;

					StorageDictionary entryStorage = new StorageDictionary();
					entryStorage.Using(refHandler);
					referencesSaved.Add(ROOT_SAVE_DATA_CAPSULE_ID, entryStorage);
					pair.Key.Save(entryStorage);
					entryStorage.StopUsing();

					refHandler.IdForReferenceCreatedEvent -= refDetectedAction;

					buffer.Add(pair.Key, referencesSaved);
				}
			}
		}

		foreach(var pair in buffer)
		{
			_cachedStorageCapsules[pair.Key] = pair.Value;
		}
	}

	public void Clear(params string[] storageCapsuleIDs)
	{
		// if(storageCapsules == null)
		// clear by remolving folders of ids
		// else
		// clear specific storageCapsules
	}

	public void Flush(params string[] storageCapsuleIDs)
	{
		// Write cached save data to disk.
		foreach(var capsuleMapItem in _cachedStorageCapsules)
		{
			if(storageCapsuleIDs != null && storageCapsuleIDs.Length > 0 && Array.IndexOf(storageCapsuleIDs, capsuleMapItem.Key) >= 0)
				continue;

			if(capsuleMapItem.Value != null)
			{
				List<SaveData.SaveDataForReference> sectionsForReferences = new List<SaveData.SaveDataForReference>();

				foreach(var pair in capsuleMapItem.Value)
				{
					sectionsForReferences.Add(new SaveData.SaveDataForReference()
					{
						ReferenceID = pair.Key,
						ValueDataItems = pair.Value.GetValueDataItems(),
						ReferenceDataItems = pair.Value.GetReferenceDataItems(),
					});
				}

				string jsonString = JsonUtility.ToJson(new SaveData()
				{
					CapsuleID = capsuleMapItem.Key.ID,
					ReferencesSaveData = sectionsForReferences.ToArray(),
				});

				using(StreamWriter writer = new StreamWriter(GetPathToStorageCapsule(capsuleMapItem.Key, true)))
				{
					writer.Write(jsonString);
				}
			}
		}
	}

	private void RefreshCachedData(IStorageCapsule capsuleToLoad)
	{
		SaveData saveDataForCapsule = LoadFromDisk(capsuleToLoad);

		Dictionary<string, StorageDictionary> referencesSaveData = new Dictionary<string, StorageDictionary>();

		if(saveDataForCapsule.ReferencesSaveData != null)
		{
			for(int i = 0, c = saveDataForCapsule.ReferencesSaveData.Length; i < c; i++)
			{
				SaveData.SaveDataForReference refData = saveDataForCapsule.ReferencesSaveData[i];
				referencesSaveData.Add(refData.ReferenceID, new StorageDictionary(SaveData.SaveDataItem.ToDictionary(refData.ValueDataItems), SaveData.SaveDataItem.ToDictionary(refData.ReferenceDataItems)));
			}
		}

		_cachedStorageCapsules[capsuleToLoad] = referencesSaveData;
	}

	private SaveData LoadFromDisk(IStorageCapsule capsuleToLoad)
	{
		string path = GetPathToStorageCapsule(capsuleToLoad, true);
		if(File.Exists(path))
		{
			using(StreamReader reader = File.OpenText(path))
			{
				string jsonString = reader.ReadToEnd();
				return JsonUtility.FromJson<SaveData>(jsonString);
			}
		}

		return new SaveData()
		{
			CapsuleID = capsuleToLoad.ID,
		};
	}
}

[Serializable]
public struct SaveData
{
	public string CapsuleID;
	public SaveDataForReference[] ReferencesSaveData;

	[Serializable]
	public struct SaveDataForReference
	{
		public string ReferenceID;
		public SaveDataItem[] ValueDataItems;
		public SaveDataItem[] ReferenceDataItems;
	}

	[Serializable]
	public struct SaveDataItem
	{
		public string SectionKey;
		public string SectionValueString;
		public string ValueType;

		public SaveDataItem(string key, object value)
		{
			SectionKey = key;
			ValueType = value.GetType().FullName;
			SectionValueString = (value.GetType().IsValueType && !value.GetType().IsPrimitive) ? JsonUtility.ToJson(value) : value.ToString();
		}

		public object GetValue()
		{
			return PrimitiveToValueParserUtility.Parse(SectionValueString, Type.GetType(ValueType));
		}

		public static Dictionary<string, object> ToDictionary(SaveDataItem[] itemsCollection)
		{
			Dictionary<string, object> returnValue = new Dictionary<string, object>();

			for(int i = 0, c = itemsCollection.Length; i < c; i++)
			{
				returnValue.Add(itemsCollection[i].SectionKey, itemsCollection[i].GetValue());
			}

			return returnValue;
		}
	}
}

public class SaveableReferenceIDManager : IDisposable
{
	public event Action<string, IRefereceSaveable> IdForReferenceCreatedEvent;
	public event Action<string> ReferenceRequestedEvent;

	private Dictionary<IRefereceSaveable, string> _refToIdMap = new Dictionary<IRefereceSaveable, string>();
	private Dictionary<string, IRefereceSaveable> _idToRefMap = new Dictionary<string, IRefereceSaveable>();
	private Dictionary<string, StorageLoadHandler> _refReadyActions = new Dictionary<string, StorageLoadHandler>();
	private long _refCounter = 0L;

	public string GetIdForReference(IRefereceSaveable reference)
	{
		string refID;
		if(!_refToIdMap.TryGetValue(reference, out refID))
		{
			refID = _refCounter.ToString();
			_refToIdMap.Add(reference, refID);
			_refCounter++;

			if(IdForReferenceCreatedEvent != null)
				IdForReferenceCreatedEvent(refID, reference);
		}

		return refID;
	}

	public void GetReferenceFromID(string refID, StorageLoadHandler callback)
	{
		if(callback == null)
			return;

		IRefereceSaveable reference;

		if(_idToRefMap.TryGetValue(refID, out reference))
		{
			callback(true, reference);
		}
		else
		{
			if(!_refReadyActions.ContainsKey(refID))
			{
				_refReadyActions.Add(refID, callback);
			}
			else
			{
				_refReadyActions[refID] += callback;
			}
		}

		if(ReferenceRequestedEvent != null)
		{
			ReferenceRequestedEvent(refID);
		}
	}

	public void SetReferenceReady(IRefereceSaveable refToSetReady, string refID = null)
	{
		if(string.IsNullOrEmpty(refID))
			refID = GetIdForReference(refToSetReady);

		if(!_idToRefMap.ContainsKey(refID))
			_idToRefMap.Add(refID, refToSetReady);

		if(_refReadyActions.ContainsKey(refID))
		{
			_refReadyActions[refID](true, _idToRefMap[refID]);
			_refReadyActions.Remove(refID);
		}
	}

	public void LoadRemainingAsNull()
	{
		foreach(var pair in _refReadyActions)
		{
			pair.Value(false, null);
		}
	}

	public void Dispose()
	{
		_refToIdMap.Clear();
		_idToRefMap.Clear();
		_refReadyActions.Clear();

		_refToIdMap = null;
		_idToRefMap = null;
		_refReadyActions = null;

		IdForReferenceCreatedEvent = null;

		_refCounter = 0L;
	}
}

public interface IStorageCapsule
{
	string ID
	{
		get;
	}

	void Save(IReferenceSaver saver);
	void Load(IReferenceLoader loader);
}

/// <summary>
/// Every ISaveable gets its own StorageDictionary assigned to it which it can use to get and set values by unique keys for that specific class. 
/// </summary>
public class StorageDictionary : IReferenceSaver, IReferenceLoader
{

	private Dictionary<string, object> _keyToNormalValue = new Dictionary<string, object>();
	private Dictionary<string, object> _keyToReferenceID = new Dictionary<string, object>();

	private SaveableReferenceIDManager _refHandler;

	public StorageDictionary()
	{

	}

	public StorageDictionary(Dictionary<string, object> loadedValues, Dictionary<string, object> loadedRefs)
	{
		_keyToNormalValue = loadedValues;
		_keyToReferenceID = loadedRefs;
	}

	public void Using(SaveableReferenceIDManager refHandler)
	{
		_refHandler = refHandler;
	}

	public void StopUsing()
	{
		_refHandler = null;
	}

	public void SaveValue<T>(string key, T value) where T : IConvertible, IComparable
	{
		Save(key, value);
	}

	public bool LoadValue<T>(string key, out T value) where T : IConvertible, IComparable
	{
		return Load(key, out value);
	}

	public void SaveStruct<T>(string key, T value) where T : struct
	{
		Save(key, value);
	}

	public bool LoadStruct<T>(string key, out T value) where T : struct
	{
		return Load(key, out value);
	}

	void IReferenceSaver.SaveRef<T>(string key, T value)
	{
		if(value == null)
		{
			Debug.LogErrorFormat("Cannot add {0} due to the value being `null`", key);
			return;
		}
		_keyToReferenceID.Add(key, _refHandler.GetIdForReference(value));
	}

	bool IReferenceLoader.LoadRef<T>(string key, StorageLoadHandler<T> refLoadedCallback)
	{
		object refIDObject;

		if(!_keyToReferenceID.TryGetValue(key, out refIDObject))
		{
			refLoadedCallback(false, null);
			return false;
		}

		string refId = refIDObject.ToString();

		_refHandler.GetReferenceFromID(refId, (trueReferenceLoad, reference) => 
		{
			if(trueReferenceLoad)
				trueReferenceLoad = reference == null || reference.GetType().IsAssignableFrom(typeof(T)) && _keyToReferenceID.ContainsKey(key);

			if(trueReferenceLoad)
				refLoadedCallback(true, (T)reference);
			else
				refLoadedCallback(false, default(T));
		});

		return true;
	}

	public SaveData.SaveDataItem[] GetValueDataItems()
	{
		List<SaveData.SaveDataItem> items = new List<SaveData.SaveDataItem>();
		foreach(var pair in _keyToNormalValue)
		{
			items.Add(new SaveData.SaveDataItem(pair.Key, pair.Value));
		}

		return items.ToArray();
	}

	public SaveData.SaveDataItem[] GetReferenceDataItems()
	{
		List<SaveData.SaveDataItem> items = new List<SaveData.SaveDataItem>();
		foreach(var pair in _keyToReferenceID)
		{
			items.Add(new SaveData.SaveDataItem(pair.Key, pair.Value));
		}

		return items.ToArray();
	}

	private void Save(string key, object value)
	{
		_keyToNormalValue.Add(key, value);
	}

	private bool Load<T>(string key, out T value)
	{
		object v;
		value = default(T);

		if(!_keyToNormalValue.TryGetValue(key, out v))
			return false;

		if(v.GetType().IsAssignableFrom(typeof(T)))
		{
			value = (T)v;
			return true;
		}

		return false;
	}
}

public interface IReferenceSaver
{
	void SaveValue<T>(string key, T value) where T : IConvertible, IComparable;
	void SaveStruct<T>(string key, T value) where T : struct;
	void SaveRef<T>(string key, T value) where T : class, IRefereceSaveable;
}

public interface IReferenceLoader
{
	bool LoadValue<T>(string key, out T value) where T : IConvertible, IComparable;
	bool LoadStruct<T>(string key, out T value) where T : struct;
	bool LoadRef<T>(string key, StorageLoadHandler<T> refAvailableCallback) where T : class, IRefereceSaveable;
}


public static class PrimitiveToValueParserUtility
{
	public static object Parse(string valueString, Type valueType)
	{
		if(valueType == typeof(bool))
			return bool.Parse(valueString);
		if(valueType == typeof(short))
			return short.Parse(valueString);
		if(valueType == typeof(int))
			return int.Parse(valueString);
		if(valueType == typeof(long))
			return long.Parse(valueString);
		if(valueType == typeof(float))
			return float.Parse(valueString);
		if(valueType == typeof(double))
			return double.Parse(valueString);
		if(valueType == typeof(decimal))
			return decimal.Parse(valueString);

		return valueString.ToString();
	}
}