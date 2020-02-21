using System;

namespace RDP.SaveLoadSystem
{
	public class StorageKeyAttribute : Attribute
	{
		public Type ExpectedType
		{
			get; private set;
		}

		public bool IsOptional
		{
			get; private set;
		}

		public StorageKeyAttribute(Type expectedType, bool isOptional = false)
		{
			ExpectedType = expectedType;
			IsOptional = isOptional;
		}
	}
}