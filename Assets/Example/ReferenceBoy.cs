using RDP.SaveLoadSystem;
using UnityEngine;

namespace RPD.SaveLoadSystem.Example
{
	public class ReferenceBoy : IRefereceSaveable
	{
		public string BoyName;
		private ReferenceBoy _referenceBoy;
		private int _count = 0;
		private Vector2 _vecCool = new Vector2(1, 1);

		public ReferenceBoy RefKid
		{
			get
			{
				return _referenceBoy;
			}
		}

		public ReferenceBoy()
		{
			BoyName = "My Parents disliked me..";
			_referenceBoy = null;
		}

		public ReferenceBoy(string name)
		{
			BoyName = name;
		}

		public void Count()
		{
			_count++;
			_vecCool = Vector2.one * _count;

			if(RefKid != null)
			{
				RefKid.Count();
				RefKid.Count();
			}

			Debug.Log(ToString());
		}

		public void CreateInnerReferenceBoy()
		{
			_referenceBoy = new ReferenceBoy("Reference Inner");
		}

		public void Save(IReferenceSaver saver)
		{
			saver.SaveValue("name", BoyName);
			saver.SaveValue("count", _count);
			saver.SaveStruct("vec", _vecCool);
			saver.SaveRef("ref", _referenceBoy, true);
		}

		public void Load(IReferenceLoader loader)
		{
			loader.LoadValue("name", out BoyName);
			loader.LoadValue("count", out _count);
			loader.LoadStruct("vec", out _vecCool);
			loader.LoadRef<ReferenceBoy>("ref", (instance) =>
			{
				_referenceBoy = instance;
			});
		}

		public void LoadingCompleted()
		{
			Debug.Log(ToString());
		}

		public override string ToString()
		{
			return "Name: " + BoyName + " | Count: " + _count + " | Vec: " + _vecCool;
		}
	}
}