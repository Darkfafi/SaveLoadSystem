using System;
using System.Collections.Generic;

namespace RDP.SaveLoadSystem
{
	public interface IStorageDictionaryEditor : IReferenceStorageDictionaryEditor, IValueStorageDictionaryEditor
	{
		
	}

	public interface IReferenceStorageDictionaryEditor
	{
		string[] GetRefStorageKeys();
		EditableRefValue GetValueRef(string key);
		EditableRefValue[] GetValueRefs(string key);
		void RemoveValueRef(string key);
		void SetValueRef(string key, EditableRefValue refValue);
		void SetValueRefs(string key, EditableRefValue[] refsValues);
		void RelocateValueRef(string currentKey, string newKey);
		EditableRefValue RegisterNewRefInCapsule(Type referenceType);
	}

	public interface IValueStorageDictionaryEditor
	{
		string[] GetValueStorageKeys();
		void SetValue(string key, object value);
		object GetValue(string key);
		void RemoveValue(string key);
		void RelocateValue(string currentKey, string newKey);
	}

	public interface IEditableStorage
	{
		EditableRefValue GetEditableRefValue(string storageCapsuleID, string refID);
		EditableRefValue RegisterNewRefInCapsule(string storageCapsuleID, Type referenceType);
		bool TryRead(string storageCapsuleID, out ReadStorageResult readStorageResult);
		List<ReadStorageResult> Read(params string[] storageCapsuleIDs);
	}

	public struct ReadStorageResult
	{
		public string CapsuleID;
		public IStorageDictionaryEditor CapsuleStorage;
		public List<KeyValuePair<Type, IStorageDictionaryEditor>> SavedRefsStorage;

		public ReadStorageResult(string capsuleID, IStorageDictionaryEditor capsuleStorage, List<KeyValuePair<Type, IStorageDictionaryEditor>> savedRefsStorage)
		{
			CapsuleID = capsuleID;
			CapsuleStorage = capsuleStorage;
			SavedRefsStorage = savedRefsStorage;
		}
	}

	public struct EditableRefValue
	{
		public string ReferenceID;
		public string ReferenceTypeString;
		public IStorageDictionaryEditor Storage;

		public bool IsValidRefValue
		{
			get; private set;
		}

		public Type ReferenceType
		{
			get; private set;
		}

		public EditableRefValue(string refID, string refTypeString, IStorageDictionaryEditor storageEditor)
		{
			ReferenceID = refID;
			ReferenceTypeString = refTypeString;
			Storage = storageEditor;
			IsValidRefValue = true;

			try
			{
				ReferenceType = Type.GetType(ReferenceTypeString);
			}
			catch
			{
				ReferenceType = null;
			}
		}
	}
}