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

			ValueString = PrimitiveToValueParserUtility.ToJSON(value, specifiedType);
			ValueType = specifiedType.AssemblyQualifiedName;
		}

		public bool IsValid
		{
			get
			{
				return !string.IsNullOrEmpty(ValueType);
			}
		}

		public object GetValue(Type specifiedType)
		{
			return PrimitiveToValueParserUtility.FromJSON(ValueString, specifiedType);
		}

		public object GetValue()
		{
			return GetValue(GetValueType());
		}

		public Type GetValueType()
		{
			return Type.GetType(ValueType);
		}

		public Type GetSafeValueType()
		{
			if (string.IsNullOrEmpty(ValueType))
				return null;

			try
			{
				return Type.GetType(ValueType);
			}
			catch
			{
				return null;
			}
		}
	}
}