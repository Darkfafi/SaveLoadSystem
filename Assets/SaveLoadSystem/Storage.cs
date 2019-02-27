using RDP.SaveLoadSystem.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RDP.SaveLoadSystem
{
	public class Storage : IStorageAccess
	{
		public enum EncodingType
		{
			None,
			Base64,
		}

		public const string ROOT_SAVE_DATA_CAPSULE_ID = "ID_CAPSULE_SAVE_DATA";
		public const string KEY_REFERENCE_TYPE_STRING = "RESERVED_REFERENCE_TYPE_FULL_NAME_STRING_RESERVED";
		public const string SAVE_FILE_EXTENSION = "rdpsf";

		public SaveableReferenceIdHandler ActiveRefHandler
		{
			get; private set;
		}

		private Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>> _cachedStorageCapsules = new Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>>();
		private EncodingType _encodingOption;
		private string _storageLocationPath;

		public static string GetPathToStorageCapsule(string locationPath, IStorageCapsule capsule, bool addFileType)
		{
			return Path.Combine(Application.persistentDataPath, string.Concat(locationPath, capsule.ID + (addFileType ? "." + SAVE_FILE_EXTENSION : "")));
		}

		public static string GetPathToStorage(string locationPath)
		{
			return Path.Combine(Application.persistentDataPath, locationPath);
		}

		public Storage(string storageLocationPath, EncodingType encodingType, params IStorageCapsule[] allStorageCapsules)
		{
			_storageLocationPath = storageLocationPath;
			_encodingOption = encodingType;
			for(int i = 0, c = allStorageCapsules.Length; i < c; i++)
			{
				_cachedStorageCapsules.Add(allStorageCapsules[i], null);
				RefreshCachedData(allStorageCapsules[i]);
			}
		}

		public void Load(params string[] storageCapsuleIDs)
		{
			using(ActiveRefHandler = new SaveableReferenceIdHandler())
			{
				foreach(var capsuleToStorage in _cachedStorageCapsules)
				{
					if(storageCapsuleIDs == null || storageCapsuleIDs.Length == 0 || Array.IndexOf(storageCapsuleIDs, capsuleToStorage.Key.ID) >= 0)
					{
						if(capsuleToStorage.Value == null)
						{
							RefreshCachedData(capsuleToStorage.Key);
						}

						List<ISaveable> _allLoadedReferences = new List<ISaveable>();
						List<string> _allLoadedReferenceIds = new List<string>();

						Action<string> referenceRequestedEventAction = (id) =>
						{
							if(_allLoadedReferenceIds.Contains(id))
								return;

							_allLoadedReferenceIds.Add(id);

							string classTypeFullName;
							StorageDictionary storage;

							if(!capsuleToStorage.Value.TryGetValue(id, out storage))
							{
								storage = new StorageDictionary(capsuleToStorage.Key.ID, this);
							}

							if(id == ROOT_SAVE_DATA_CAPSULE_ID)
							{
								capsuleToStorage.Key.Load(storage);
								_allLoadedReferences.Add(capsuleToStorage.Key);
							}
							else if(storage.LoadValue(KEY_REFERENCE_TYPE_STRING, out classTypeFullName))
							{
								IStorageLoader loader = storage;
								Type referenceType = Type.GetType(classTypeFullName);
								bool methodLoadInterface = typeof(ISaveableLoad).IsAssignableFrom(referenceType);
								ISaveable referenceInstance = (methodLoadInterface ? Activator.CreateInstance(referenceType) : Activator.CreateInstance(referenceType, loader)) as ISaveable;
								ActiveRefHandler.SetReferenceReady(referenceInstance, id);

								if(methodLoadInterface)
									((ISaveableLoad)referenceInstance).Load(loader);

								_allLoadedReferences.Add(referenceInstance);
							}
							else
							{
								Debug.LogErrorFormat("UNABLE TO LOAD REFERENCE ID {0}'s CLASS TYPE NAME", id);
							}
						};

						ActiveRefHandler.ReferenceRequestedEvent += referenceRequestedEventAction;
						referenceRequestedEventAction(ROOT_SAVE_DATA_CAPSULE_ID);
						ActiveRefHandler.LoadRemainingAsNull();
						ActiveRefHandler.ReferenceRequestedEvent -= referenceRequestedEventAction;

						for(int i = _allLoadedReferences.Count - 1; i >= 0; i--)
						{
							_allLoadedReferences[i].LoadingCompleted();
						}

						_allLoadedReferences = null;
						_allLoadedReferenceIds = null;
					}
				}
			}
		}

		public void Save(bool flushAfterSave, params string[] storageCapsuleIDs)
		{
			Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>> buffer = new Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>>();
			using(ActiveRefHandler = new SaveableReferenceIdHandler())
			{
				foreach(var pair in _cachedStorageCapsules)
				{
					if(storageCapsuleIDs == null || storageCapsuleIDs.Length == 0 || Array.IndexOf(storageCapsuleIDs, pair.Key.ID) >= 0)
					{
						Dictionary<string, StorageDictionary> referencesSaved = new Dictionary<string, StorageDictionary>();

						Action<string, ISaveable> refDetectedAction = (refID, referenceInstance) =>
						{
							if(!referencesSaved.ContainsKey(refID))
							{
								StorageDictionary storageDictForRef = new StorageDictionary(pair.Key.ID, this);
								referencesSaved.Add(refID, storageDictForRef);
								storageDictForRef.SaveValue(KEY_REFERENCE_TYPE_STRING, referenceInstance.GetType().AssemblyQualifiedName);
								referenceInstance.Save(storageDictForRef);
							}
						};

						ActiveRefHandler.IdForReferenceCreatedEvent += refDetectedAction;
						refDetectedAction(ROOT_SAVE_DATA_CAPSULE_ID, pair.Key);
						ActiveRefHandler.IdForReferenceCreatedEvent -= refDetectedAction;

						buffer.Add(pair.Key, referencesSaved);
					}
				}
			}

			foreach(var pair in buffer)
			{
				_cachedStorageCapsules[pair.Key] = pair.Value;
			}

			if(flushAfterSave)
				Flush(storageCapsuleIDs);
		}

		public bool TryRead(string storageCapsuleID, out ReadStorageResult readStorageResult)
		{
			List<ReadStorageResult> storages = Read(new string[] { storageCapsuleID });

			if(storages.Count > 0)
			{
				readStorageResult = storages[0];
				return true;
			}

			readStorageResult = default(ReadStorageResult);
			return false;
		}

		public List<ReadStorageResult> Read(params string[] storageCapsuleIDs)
		{
			List<ReadStorageResult> storageDicts = new List<ReadStorageResult>();

			foreach(var capsuleToStorage in _cachedStorageCapsules)
			{
				if(storageCapsuleIDs == null || storageCapsuleIDs.Length == 0 || Array.IndexOf(storageCapsuleIDs, capsuleToStorage.Key.ID) >= 0)
				{
					if(capsuleToStorage.Value == null)
					{
						RefreshCachedData(capsuleToStorage.Key);
					}

					StorageDictionary capsuleStorage = null;
					List<KeyValuePair<Type, IStorageDictionaryEditor>> refStorages = new List<KeyValuePair<Type, IStorageDictionaryEditor>>();
					if(capsuleToStorage.Value.TryGetValue(ROOT_SAVE_DATA_CAPSULE_ID, out capsuleStorage))
					{
						foreach(var storageItem in capsuleToStorage.Value)
						{
							string referenceTypeString;
							if(storageItem.Key != ROOT_SAVE_DATA_CAPSULE_ID && storageItem.Value.LoadValue(KEY_REFERENCE_TYPE_STRING, out referenceTypeString))
							{
								Type referenceType = Type.GetType(referenceTypeString);
								refStorages.Add(new KeyValuePair<Type, IStorageDictionaryEditor>(referenceType, storageItem.Value));
							}
						}

						storageDicts.Add(new ReadStorageResult(capsuleToStorage.Key.ID, capsuleStorage, refStorages));
					}
				}
			}

			return storageDicts;
		}

		public void Clear(bool removeSaveFiles, params string[] storageCapsuleIDs)
		{
			Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>> buffer = new Dictionary<IStorageCapsule, Dictionary<string, StorageDictionary>>();
			foreach(var pair in _cachedStorageCapsules)
			{
				if(storageCapsuleIDs == null || storageCapsuleIDs.Length == 0 || Array.IndexOf(storageCapsuleIDs, pair.Key.ID) >= 0)
				{
					buffer.Add(pair.Key, new Dictionary<string, StorageDictionary>());
				}
			}

			if(!Directory.Exists(GetPathToStorage(_storageLocationPath)))
				return;

			foreach(var pair in buffer)
			{
				if(removeSaveFiles)
				{
					string pathToFile = GetPathToStorageCapsule(_storageLocationPath, pair.Key, true);
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

			if(Directory.GetFiles(GetPathToStorage(_storageLocationPath)).Length == 0)
				Directory.Delete(GetPathToStorage(_storageLocationPath));

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


					try
					{
						string pathToStorage = GetPathToStorage(_storageLocationPath);
						if(!Directory.Exists(pathToStorage))
						{
							Directory.CreateDirectory(pathToStorage);
						}

						using(StreamWriter writer = new StreamWriter(GetPathToStorageCapsule(_storageLocationPath, capsuleMapItem.Key, true)))
						{
							writer.Write(Encode(JsonUtility.ToJson(new SaveFileWrapper()
							{
								SafeFileText = jsonString,
								SaveFilePassword = GetEncryptionPassword(jsonString)
							})));
						}
					}
					catch(Exception e)
					{
						throw new Exception(string.Format("Could not save file {0}. Error: {1}", capsuleMapItem.Key,  e.Message));
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
					referencesSaveData.Add(refData.ReferenceID, new StorageDictionary(capsuleToLoad.ID, this, SaveDataItem.ToDictionary(refData.ValueDataItems), SaveDataItem.ToDictionary(refData.ReferenceDataItems)));
				}
			}

			_cachedStorageCapsules[capsuleToLoad] = referencesSaveData;
		}

		private SaveData LoadFromDisk(IStorageCapsule capsuleToLoad)
		{
			string path = GetPathToStorageCapsule(_storageLocationPath, capsuleToLoad, true);
			if(File.Exists(path))
			{
				using(StreamReader reader = File.OpenText(path))
				{
					string jsonString = reader.ReadToEnd();
					SaveFileWrapper saveFileWrapper = JsonUtility.FromJson<SaveFileWrapper>(Decode(jsonString));
					if(ValidateEncryptionPassword(saveFileWrapper.SaveFilePassword, saveFileWrapper.SafeFileText))
					{
						return JsonUtility.FromJson<SaveData>(saveFileWrapper.SafeFileText);
					}
					else
					{
						Debug.Log("SAVE FILE IS CORRUPT, NEW SAVE FILE CREATED!");
					}
				}
			}

			return new SaveData()
			{
				CapsuleID = capsuleToLoad.ID,
			};
		}

		private string Encode(string text)
		{
			switch(_encodingOption)
			{
				case EncodingType.None:
					return text;
				case EncodingType.Base64:
					return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
				default:
					Debug.LogErrorFormat ("Encryption type {0} not supported!", _encodingOption);
					return text;
			}
		}

		private string Decode(string text)
		{
			switch(_encodingOption)
			{
				case EncodingType.None:
					return text;
				case EncodingType.Base64:
					return Encoding.UTF8.GetString(Convert.FromBase64String(text));
				default:
					Debug.LogErrorFormat("Decryption type {0} not supported!", _encodingOption);
					return text;
			}
		}

		private string GetEncryptionPassword(string fileText)
		{
			HashAlgorithm algorithm = MD5.Create();
			List<byte> bytes = new List<byte>(Encoding.UTF8.GetBytes(Encode(fileText)));
			bytes.AddRange(Encoding.UTF8.GetBytes(fileText));

			StringBuilder sb = new StringBuilder();
			foreach(byte b in algorithm.ComputeHash(bytes.ToArray()))
				sb.Append(b.ToString("X2"));

			return Encode(sb.ToString());
		}

		private bool ValidateEncryptionPassword(string password, string fileText)
		{
			return password == GetEncryptionPassword(fileText);
		}

		public EditableRefValue GetEditableRefValue(string storageCapsuleID, string refID)
		{
			foreach(var item in _cachedStorageCapsules)
			{
				if(item.Key.ID == storageCapsuleID)
				{
					StorageDictionary storageForRef;
					if(item.Value.TryGetValue(refID, out storageForRef))
					{
						string referenceTypeString;
						if(storageForRef.LoadValue(KEY_REFERENCE_TYPE_STRING, out referenceTypeString))
						{
							Type refType = Type.GetType(referenceTypeString);
							return new EditableRefValue(refID, refType, storageForRef);
						}
					}
					break;
				}
			}

			return default(EditableRefValue);
		}

		public EditableRefValue RegisterNewRefInCapsule(string storageCapsuleID, Type referenceType)
		{
			IStorageCapsule capsuleToEdit = null;
			EditableRefValue editableRefValue = default(EditableRefValue);

			foreach(var item in _cachedStorageCapsules)
			{
				if(item.Key.ID == storageCapsuleID)
				{
					StorageDictionary storageForRef = new StorageDictionary(storageCapsuleID, this);
					storageForRef.SaveValue(KEY_REFERENCE_TYPE_STRING, referenceType.AssemblyQualifiedName);
					string randomOnFlyID = Guid.NewGuid().ToString("N");
					editableRefValue = new EditableRefValue(randomOnFlyID, referenceType, storageForRef);
					capsuleToEdit = item.Key;
					break;
				}
			}

			if(editableRefValue.IsValidRefValue && capsuleToEdit != null)
			{
				_cachedStorageCapsules[capsuleToEdit].Add(editableRefValue.ReferenceID, editableRefValue.Storage as StorageDictionary);
			}

			return editableRefValue;
		}
	}

	public interface IStorageCapsule : ISaveableLoad
	{
		string ID
		{
			get;
		}
	}

	public interface IStorageAccess : IEditableStorage
	{
		SaveableReferenceIdHandler ActiveRefHandler
		{
			get;
		}
	}
}