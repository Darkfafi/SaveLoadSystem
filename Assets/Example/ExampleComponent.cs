using RDP.SaveLoadSystem;
using UnityEngine;

namespace RPD.SaveLoadSystem.Example
{
	public class ExampleComponent : MonoBehaviour, IStorageCapsule
	{
		public const string EXAMPLE_CAPSULE = "ExampleSaveFile";

		public bool ClearSavesAtOnDestroy = false;
		public bool RemoveFileOnClear = false;

		private ReferenceBoy _referenceBoyA;
		private ReferenceBoy _referenceBoyB;

		private Storage _storage;

		public string ID
		{
			get
			{
				return EXAMPLE_CAPSULE;
			}
		}

		public void Awake()
		{
			_storage = new Storage("Saves/", Storage.EncodingType.Base64, this);
			_storage.Load(EXAMPLE_CAPSULE); // Can be left empty to load all given capsules.
		}

		protected void Update()
		{
			if(Input.GetKeyDown(KeyCode.Space))
			{
				_referenceBoyA.Count();
				_referenceBoyB.Count();
			}
		}

		public void OnDestroy()
		{
			if(!ClearSavesAtOnDestroy)
				_storage.Save(true, EXAMPLE_CAPSULE); // Can be left empty to save all given capsules.
			else
				_storage.Clear(RemoveFileOnClear, EXAMPLE_CAPSULE);
		}

		public void Save(IStorageSaver saver)
		{
			saver.SaveRef("RefA", _referenceBoyA);
			saver.SaveRef("RefB", _referenceBoyB);
		}

		public void Load(IStorageLoader loader)
		{
			loader.LoadRef<ReferenceBoy>("RefA", (instance) => _referenceBoyA = instance);
			loader.LoadRef<ReferenceBoy>("RefB", (instance) => _referenceBoyB = instance);
		}

		public void LoadingCompleted()
		{
			if(_referenceBoyA == null)
			{
				_referenceBoyA = new ReferenceBoy("Ref Boy A");
			}

			if(_referenceBoyB == null)
			{
				_referenceBoyB = new ReferenceBoy("Ref Boy B");
				_referenceBoyB.CreateInnerReferenceBoy();
			}
		}
	}
}