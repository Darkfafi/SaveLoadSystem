using System;
using System.Collections.Generic;

namespace RDP.SaveLoadSystem
{
	public interface IStorageDictionaryEditor : IReferenceStorageDictionaryEditor, IValueStorageDictionaryEditor
	{
		
	}

	public interface IReferenceStorageDictionaryEditor
	{
		EditableRefValue GetValueRef(string key);
		void RemoveValueRef(string key);
		void SetValueRef(string key, EditableRefValue refValue);
		void RelocateValueRef(string currentKey, string newKey);
		EditableRefValue RegisterNewRefInCapsule(Type referenceType);
	}

	public interface IValueStorageDictionaryEditor
	{
		void SetValue(string key, object value);
		object GetValue(string key);
		void RemoveValue(string key);
		void RelocateValue(string currentKey, string newKey);
	}

	public interface IEditableStorageAccess
	{
		SaveableReferenceIdHandler ActiveRefHandler
		{
			get;
		}

		EditableRefValue GetEditableRefValue(string storageCapsuleID, string key);
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
		public Type ReferenceType;
		public IStorageDictionaryEditor Storage;

		public bool IsValidRefValue
		{
			get; private set;
		}

		public EditableRefValue(string refID, Type refType, IStorageDictionaryEditor storageEditor)
		{
			ReferenceID = refID;
			ReferenceType = refType;
			Storage = storageEditor;
			IsValidRefValue = true;
		}
	}
}