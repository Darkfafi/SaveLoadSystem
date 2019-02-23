using System;

namespace RDP.SaveLoadSystem.Internal.Utils
{
	public static class PrimitiveToValueParserUtility
	{
		public static object Parse(string valueString, Type valueType)
		{
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
	}
}