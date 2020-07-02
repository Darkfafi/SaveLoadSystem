using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RDP.SaveLoadSystem.Internal.Utils
{
	[InitializeOnLoad]
	public static class StorageObjectFactoryGenerator
	{
		public const string STORAGE_OBJECT_FACTORY_FILE_NAME = Storage.STORAGE_OBJECT_FACTORY_TYPE_NAME + ".cs";

		private const string STORAGE_OBJECT_FACTORY_TEMPLATE_FILE_NAME = "StorageObjectFactoryTemplate.txt";

		// Progress Bar
		private const string PB_TITLE = "Generating " + STORAGE_OBJECT_FACTORY_FILE_NAME;

		static StorageObjectFactoryGenerator()
		{
			if(Storage.GetStorageFactoryType() == null)
			{
				GenerateStorageFactory();
			}
		}

		public static string GetPathBase()
		{
			return Path.Combine("Assets", "Storage", "Generated");
		}

		public static string GetPathFull()
		{
			return Path.Combine(GetPathBase(), STORAGE_OBJECT_FACTORY_FILE_NAME);
		}

		[MenuItem(EditorMenu.BASE_ROUTE + "Generate " + STORAGE_OBJECT_FACTORY_FILE_NAME, priority = 100)]
		public static void GenerateStorageFactory()
		{
			EditorUtility.DisplayProgressBar(PB_TITLE, "Setup", 0.1f);

			Dictionary<Type, (ulong id, Assembly assembly)> typeToId = new Dictionary<Type, (ulong id, Assembly assembly)>();
			ulong idMilestone = 0;

			if(File.Exists(GetPathFull()))
			{
				#region Identifying already registered types / ids

				ulong? holdingID = null;
				Assembly currentTypeAssembly = null;

				EditorUtility.DisplayProgressBar(PB_TITLE, $"Reading existing {STORAGE_OBJECT_FACTORY_FILE_NAME}", 0.3f);

				foreach(string line in File.ReadLines(GetPathFull()))
				{

					string trimmedLine = line.Trim();
					string[] varToAssignmentSplitItems = trimmedLine.Split(Consts.ASSIGN_CHAR);

					// Assignment of variable identified
					if(varToAssignmentSplitItems.Length == 2)
					{
						string varName = varToAssignmentSplitItems[0].Trim();
						string[] varNameSections = varName.Split(' ');
						varName = varNameSections[varNameSections.Length - 1];

						string varValue = varToAssignmentSplitItems[1].Trim(';');

						if(varName == Consts.CONST_NAME_ID_MILESTONE)
						{
							idMilestone = ulong.Parse(varValue);
						}
					}

					if(!holdingID.HasValue)
					{
						// Identify case ID
						if(trimmedLine.StartsWith(Consts.CASE_START) && trimmedLine.EndsWith(Consts.CASE_END))
						{
							string idString = trimmedLine.Remove(0, Consts.CASE_START.Length);
							idString = idString.Remove(idString.IndexOf(Consts.CASE_END), 1);
							holdingID = ulong.Parse(idString);
							continue;
						}
					}
					else
					{
						// Identify Assembly (before identifying type)
						if(currentTypeAssembly == null)
						{
							if(trimmedLine.StartsWith(Consts.COMMENT_STRING))
							{
								string assemblyStringValue = trimmedLine.Remove(0, Consts.COMMENT_STRING.Length).Trim();
								if(assemblyStringValue.StartsWith(Consts.ASSEMBLY_PREFIX))
								{
									assemblyStringValue = assemblyStringValue.Remove(0, Consts.ASSEMBLY_PREFIX.Length).Trim();
									try
									{
										currentTypeAssembly = Assembly.Load(assemblyStringValue);
									}
									catch
									{
										Debug.LogError($"Could not load Assembly {assemblyStringValue} for ID {holdingID.Value}");
										holdingID = null;
									}
								}
							}
						}
						else
						{
							// Identify class type (after assembly identification)
							if(trimmedLine.Contains(Consts.ASSIGN_NEW))
							{
								string[] classAssignItems = trimmedLine.Split(new string[] { Consts.ASSIGN_NEW }, StringSplitOptions.None);
								string className = classAssignItems[1];
								int endClassNameIndex = className.IndexOf('(');
								className = className.Remove(endClassNameIndex, className.Length - endClassNameIndex).Trim();

								try
								{
									Type classType = currentTypeAssembly.GetType(className, true);
									typeToId.Add(classType, (holdingID.Value, currentTypeAssembly));
								}
								catch
								{
									Debug.LogError($"Could not create type from ClassName {className} inside assembly {currentTypeAssembly.FullName}");
								}
							}

							// Refresh Data for case
							currentTypeAssembly = null;
							holdingID = null;
						}
					}
				}
				#endregion
			}
			else if(!Directory.Exists(GetPathBase()))
			{
				EditorUtility.DisplayProgressBar(PB_TITLE, "Creating directory for new Factory file", 0.3f);

				Directory.CreateDirectory(GetPathBase());
			}

			#region Get New Storage Type -> Ids for factory

			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			ulong newCount = 0;

			EditorUtility.DisplayProgressBar(PB_TITLE, $"Finding new {nameof(ISaveable)} classes for {STORAGE_OBJECT_FACTORY_FILE_NAME}", 0.5f);

			for(int i = 0; i < assemblies.Length; i++)
			{
				Assembly assembly = assemblies[i];
				Type[] newSaveableTypes = assembly.GetTypes().Where(x => typeof(ISaveable).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract).Where(x => !typeToId.ContainsKey(x)).ToArray();

				for(int j = 0; j < newSaveableTypes.Length; j++)
				{
					Type newSaveableType = newSaveableTypes[j];

					if(HasLoaderConstructor(newSaveableType) || typeof(ISaveableLoad).IsAssignableFrom(newSaveableType))
					{
						typeToId.Add(newSaveableType, (idMilestone, newSaveableType.Assembly));
						idMilestone++;
						newCount++;
					}
				}
			}

			#endregion


			EditorUtility.DisplayProgressBar(PB_TITLE, $"Creating new {STORAGE_OBJECT_FACTORY_FILE_NAME} content", 0.8f);

			StringBuilder classBuilder = new StringBuilder();

			foreach(string line in File.ReadLines(GetTemplatePathFull()))
			{
				string trimmedLine = line.Trim();
				string[] varToAssignmentSplitItems = trimmedLine.Split(Consts.ASSIGN_CHAR);
				StringBuilder lineStringBuilder = new StringBuilder();

				if(line.Contains(Consts.TEMPLATE_ID_MILESTONE_VALUE_INSERT_FIELD_ID))
				{
					lineStringBuilder.AppendLine(line.Replace(Consts.TEMPLATE_ID_MILESTONE_VALUE_INSERT_FIELD_ID, idMilestone.ToString(CultureInfo.InvariantCulture)));
				}
				else if(line.Contains(Consts.TEMPLATE_TYPE_TO_ID_MAP_ITEMS_ID))
				{
					foreach(var switchItemPair in typeToId)
					{
						lineStringBuilder.Append(Consts.Tabs(3));
						lineStringBuilder.AppendLine(Consts.ToDictionaryItem(Consts.TypeToTypeOfString(switchItemPair.Key), Consts.ULongToString(switchItemPair.Value.id)));
					}
				}
				else if(trimmedLine == Consts.TEMPLATE_SWITCH_CASE_INSERT_FIELD_ID)
				{
					foreach(var switchItemPair in typeToId)
					{
						string variableName = $"value{switchItemPair.Value.id}";

						lineStringBuilder.AppendLine($"{Consts.Tabs(4)} case {switchItemPair.Value.id}:");
						lineStringBuilder.AppendLine($"{Consts.Tabs(5)} {Consts.COMMENT_ASSEMBLY_PREFIX} {switchItemPair.Value.assembly.FullName}:");

						bool isConstructorLoader = HasLoaderConstructor(switchItemPair.Key);

						lineStringBuilder.AppendLine($"{Consts.Tabs(5)} {(isConstructorLoader ? Consts.SAVEABLE_TYPE : Consts.SAVEABLE_WITHOUT_PARAM_TYPE)} {variableName} {Consts.ASSIGN_CHAR} {Consts.NEW} {switchItemPair.Key.FullName}({(isConstructorLoader ? Consts.PARAM_LOADER : string.Empty)});");

						if(!isConstructorLoader)
						{
							lineStringBuilder.AppendLine($"{Consts.Tabs(5)} {variableName}.{nameof(ISaveableLoad.Load)}({Consts.PARAM_LOADER});");
						}

						lineStringBuilder.AppendLine($"{Consts.Tabs(5)} {Consts.RETURN} {variableName};");
					}
				}
				else
				{
					lineStringBuilder.AppendLine(line);
				}

				classBuilder.Append(lineStringBuilder.ToString());
			}

			EditorUtility.DisplayProgressBar(PB_TITLE, $"Creating new {STORAGE_OBJECT_FACTORY_FILE_NAME} file", 0.8f);

			File.WriteAllText(GetPathFull(), classBuilder.ToString());
			AssetDatabase.Refresh();
			EditorUtility.ClearProgressBar();

			// Highlight Asset
			TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(GetPathFull());
			Selection.activeObject = asset;
			EditorGUIUtility.PingObject(asset);
		}

		private static bool HasLoaderConstructor(Type type)
		{
			return type.GetConstructors().Any(constructor =>
			{
				ParameterInfo[] paramInfos = constructor.GetParameters();
				return paramInfos.Length == 1 && paramInfos[0].ParameterType == typeof(IStorageLoader);
			});
		}

		// Must remain private
		private static string GetTemplatePathBase([CallerFilePath] string sourceFilePath = "")
		{
			return Path.GetDirectoryName(sourceFilePath);
		}

		private static string GetTemplatePathFull()
		{
			return Path.Combine(GetTemplatePathBase(), STORAGE_OBJECT_FACTORY_TEMPLATE_FILE_NAME);
		}

		public static class Consts
		{
			public const string CONST_NAME_ID_MILESTONE = "ID_MILESTONE";

			public const string TEMPLATE_ID_MILESTONE_VALUE_INSERT_FIELD_ID = "[ID_MILESTONE_VALUE]";
			public const string TEMPLATE_SWITCH_CASE_INSERT_FIELD_ID = "[SWITCH_CASE]";
			public const string TEMPLATE_TYPE_TO_ID_MAP_ITEMS_ID = "[TYPE_TO_ID_MAP_ITEMS]";

			public const string COMMENT_STRING = "//";
			public const string ASSEMBLY_PREFIX = "Assembly:";
			public const string COMMENT_ASSEMBLY_PREFIX = COMMENT_STRING + ASSEMBLY_PREFIX;

			public const string NEW = "new";
			public const string RETURN = "return";
			public const string RETURN_NEW = RETURN + " " + NEW;
			public const string ASSIGN_NEW = ASSIGN_STRING + " " + NEW;

			public const string CASE_START = "case";
			public const string CASE_END = ":";

			public const string SAVEABLE_TYPE = nameof(ISaveable);
			public const string SAVEABLE_WITHOUT_PARAM_TYPE = nameof(ISaveableLoad);

			public const string PARAM_LOADER = "loader";

			public const string ASSIGN_STRING = "=";
			public const char ASSIGN_CHAR = '=';

			public static string TypeToTypeOfString(Type type)
			{
				return string.Format("typeof({0})", type.FullName);
			}

			public static string ULongToString(ulong value)
			{
				return $"{value}UL";
			}

			public static string ToDictionaryItem(string key, string value)
			{
				return $"{{ {key}, {value} }},";
			}

			public static string Tabs(int amount)
			{
				string v = "";
				for(int i = 0; i < amount; i++)
				{
					v += "	";
				}
				return v;
			}
		}
	}
}