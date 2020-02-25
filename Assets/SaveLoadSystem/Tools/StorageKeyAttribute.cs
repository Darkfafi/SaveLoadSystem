using System;

namespace RDP.SaveLoadSystem
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
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

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class StorageKeysHolderAttribute : Attribute
	{
		public Type ContainerForType
		{
			get; private set;
		}

		public StorageKeysHolderAttribute(Type containerForType)
		{
			ContainerForType = containerForType;
		}
	}
}