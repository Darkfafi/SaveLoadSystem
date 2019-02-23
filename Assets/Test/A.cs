using RDP.SaveLoadSystem;
using UnityEngine;

public class A : MonoBehaviour, IStorageCapsule
{
	private Storage _storage;
	private RefBoy _refBoy;
	private RefBoy _refKid;

	public string ID
	{
		get
		{
			return "Game";
		}
	}

	public void Awake()
	{
		_storage = new Storage("GameStorage", this);
		_storage.Load("Game");
	}

	public void Update()
	{
		if(Input.GetKeyDown(KeyCode.Space))
		{
			_refBoy.Count();
			_refKid.Count();
			_refKid.Count();
		}
	}

	public void OnDestroy()
	{
		_storage.Save("Game");
		_storage.Flush("Game");
	}

	public void Save(IReferenceSaver saver)
	{
		saver.SaveRef("ref", _refBoy);
	}

	public void Load(IReferenceLoader loader)
	{
		loader.LoadRef<RefBoy>("ref", (instance) =>
		{
			if(instance != null)
				_refBoy = instance;
			else
			{
				_refBoy = new RefBoy("Ref Boyyy!");
			}
		});
	}

	public void LoadingCompleted()
	{
		if(_refBoy.RefKid == null)
		{
			_refBoy.SetReference(new RefBoy("Kid!"));
		}

		_refKid = _refBoy.RefKid;
	}
}

public class RefBoy : IRefereceSaveable
{
	public string BoyName;
	private RefBoy _referenceBoy;
	private int _count = 0;
	private Vector2 _vecCool = new Vector2(3, 3);

	public RefBoy RefKid
	{
		get
		{
			return _referenceBoy;
		}
	}

	public RefBoy()
	{
		BoyName = "My Parents disliked me..";
		_referenceBoy = null;
	}

	public RefBoy(string name)
	{
		BoyName = name;
	}

	public void Count()
	{
		_count++;
	}

	public void SetReference(RefBoy reference)
	{
		_referenceBoy = reference;
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
		loader.LoadRef<RefBoy>("ref", (instance) =>
		{
			_referenceBoy = instance;
		});
	}

	public void LoadingCompleted()
	{
		Debug.Log("Name: " + BoyName + " | Count: " + _count);
	}
}
