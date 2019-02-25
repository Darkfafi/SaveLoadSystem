using RDP.SaveLoadSystem.Internal.Utils;
using System;

namespace RDP.SaveLoadSystem.Internal
{
	[Serializable]
	public struct SaveableValueSection
	{
		public string ValueString;
		public string ValueType;

		public SaveableValueSection(object value, Type specifiedType = null)
		{
			if(specifiedType == null)
				specifiedType = value.GetType();

			ValueString = PrimitiveToValueParserUtility.ToJSON(value);
			ValueType = specifiedType.AssemblyQualifiedName;
		}

		public object GetValue()
		{
			return PrimitiveToValueParserUtility.FromJSON(ValueString, Type.GetType(ValueType));
		}
	}
}