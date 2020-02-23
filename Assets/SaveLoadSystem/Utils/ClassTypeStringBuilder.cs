using System.Collections.Generic;
using System.Text;

namespace RDP.SaveLoadSystem.Utils
{
	public class ClassTypeStringBuilder
	{
		private readonly string _mainClass;

		private string _cachedResult;
		private List<string> _namespaces = new List<string>();
		private List<string> _containingClasses = new List<string>();

		public static ClassTypeStringBuilder Create(string className)
		{
			return new ClassTypeStringBuilder(className);
		}

		private ClassTypeStringBuilder(string className)
		{
			_mainClass = className;
			_cachedResult = null;
		}

		public ClassTypeStringBuilder AddNamespace(string namespaceName)
		{
			_namespaces.Add(namespaceName);
			_cachedResult = null;
			return this;
		}

		public ClassTypeStringBuilder AddClass(string className)
		{
			_containingClasses.Add(className);
			_cachedResult = null;
			return this;
		}

		public override string ToString()
		{
			if (!string.IsNullOrEmpty(_cachedResult))
			{
				return _cachedResult;
			}

			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < _namespaces.Count; i++)
			{
				stringBuilder.Append(_namespaces[i]);
				stringBuilder.Append(".");
			}
			for (int i = 0; i < _containingClasses.Count; i++)
			{
				stringBuilder.Append(_containingClasses[i]);
				stringBuilder.Append("+");
			}
			stringBuilder.Append(_mainClass);
			_cachedResult = stringBuilder.ToString();
			return _cachedResult;
		}
	}
}