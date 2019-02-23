﻿using RDP.SaveLoadSystem.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
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
			using(SaveableReferenceIdHandler refHandler = new SaveableReferenceIdHandler())
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

						capsuleToStorage.Key.LoadingCompleted();

					   _allLoadedReferences = null;
					}
				}
			}
		}

		public void Save(params string[] storageCapsuleIDs)
		{
			Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>> buffer = new Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>>();
			using(SaveableReferenceIdHandler refHandler = new SaveableReferenceIdHandler())
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

		public void FlushClear(bool removeSaveFiles, params string[] storageCapsuleIDs)
		{
			Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>> buffer = new Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>>();
			foreach(var pair in _cachedStorageCapsules)
			{
				if(storageCapsuleIDs == null || storageCapsuleIDs.Length == 0 || Array.IndexOf(storageCapsuleIDs, pair.Key.ID) >= 0)
				{
					buffer.Add(pair.Key, new Dictionary<string, StorageDictionary>());
				}
			}

			foreach(var pair in buffer)
			{
				if(removeSaveFiles)
				{
					string pathToFile = GetPathToStorageCapsule(pair.Key, true);
					if(File.Exists(pathToFile))
					{
						File.Delete(pathToFile);
					}
				}
				else
				{
					_cachedStorageCapsules[pair.Key] = pair.Value;
				}
			}

			if(!removeSaveFiles)
				Flush(storageCapsuleIDs);
		}

		public void Flush(params string[] storageCapsuleIDs)
		{
			foreach(var capsuleMapItem in _cachedStorageCapsules)
			{
				if(storageCapsuleIDs != null && storageCapsuleIDs.Length > 0 && Array.IndexOf(storageCapsuleIDs, capsuleMapItem.Key) >= 0)
					continue;

				if(capsuleMapItem.Value != null)
				{
					List<SaveDataForReference> sectionsForReferences = new List<SaveDataForReference>();

					foreach(var pair in capsuleMapItem.Value)
					{
						sectionsForReferences.Add(new SaveDataForReference()
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
					SaveDataForReference refData = saveDataForCapsule.ReferencesSaveData[i];
					referencesSaveData.Add(refData.ReferenceID, new StorageDictionary(SaveDataItem.ToDictionary(refData.ValueDataItems), SaveDataItem.ToDictionary(refData.ReferenceDataItems)));
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

	public interface IStorageCapsule : IRefereceSaveable
	{
		string ID
		{
			get;
		}
	}
}