using RDP.SaveLoadSystem.Internal.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RDP.SaveLoadSystem.Internal
{
	[Serializable]
	public struct SaveData
	{
		public string CapsuleID;
		public SaveDataForReference[] ReferencesSaveData;
	}

	[Serializable]
	public struct SaveDataForReference
	{
		public string ReferenceID;
		public SaveDataItem[] ValueDataItems;
		public SaveDataItem[] ReferenceDataItems;
	}

	[Serializable]
	public struct SaveDataItem
	{
		public string SectionKey;
		public string SectionValueString;
		public string ValueType;

		public SaveDataItem(string key, object value)
		{
			SectionKey = key;
			ValueType = value.GetType().FullName;
			SectionValueString = (value.GetType().IsValueType && !value.GetType().IsPrimitive) ? JsonUtility.ToJson(value) : value.ToString();
		}

		public object GetValue()
		{
			return PrimitiveToValueParserUtility.Parse(SectionValueString, Type.GetType(ValueType));
		}

		public static Dictionary<string, object> ToDictionary(SaveDataItem[] itemsCollection)
		{
			Dictionary<string, object> returnValue = new Dictionary<string, object>();

			for(int i = 0, c = itemsCollection.Length; i < c; i++)
			{
				returnValue.Add(itemsCollection[i].SectionKey, itemsCollection[i].GetValue());
			}

			return returnValue;
		}
	}
}