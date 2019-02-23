using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IRefereceSaveable
{
	void Save(IReferenceSaver saver);
	void Load(IReferenceLoader loader); // load should be the constructor
	void LoadingCompleted();
}
