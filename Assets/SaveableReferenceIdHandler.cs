using System;
using System.Collections.Generic;

namespace RDP.SaveLoadSystem
{
	public class SaveableReferenceIdHandler : IDisposable
	{
		public delegate void StorageLoadHandler(bool wasInStorage, IRefereceSaveable instance);

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
			ReferenceRequestedEvent = null;

			_refCounter = 0L;
		}
	}
}