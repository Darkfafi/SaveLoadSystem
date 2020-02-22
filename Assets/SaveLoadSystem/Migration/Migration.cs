using RDP.SaveLoadSystem.Internal;

namespace RDP.SaveLoadSystem
{
	public abstract class Migration
	{
		public abstract string CapsuleIDTarget
		{
			get;
		}

		public abstract void Do(ReadStorageResult storage);
		public abstract void Undo(ReadStorageResult storage);
	}
}