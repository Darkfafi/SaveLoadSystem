using System;
using UnityEngine;

namespace RDP.SaveLoadSystem.Internal.Utils
{
	public static class PrimitiveToValueParserUtility
	{
		public static object FromJSON(string valueString, Type valueType)
		{
			if(valueType.IsValueType && !valueType.IsPrimitive)
				return JsonUtility.FromJson(valueString, valueType);

			if(valueType == typeof(bool))
				return bool.Parse(valueString);
			if(valueType == typeof(short))
				return short.Parse(valueString);
			if(valueType == typeof(int))
				return int.Parse(valueString);
			if(valueType == typeof(long))
				return long.Parse(valueString);
			if(valueType == typeof(float))
				return float.Parse(valueString);
			if(valueType == typeof(double))
				return double.Parse(valueString);
			if(valueType == typeof(decimal))
				return decimal.Parse(valueString);
	
			return valueString.ToString();
		}

		public static string ToJSON(object value)
		{
			return (value.GetType().IsValueType && !value.GetType().IsPrimitive) ? JsonUtility.ToJson(value) : value.ToString();
		}
	}
}