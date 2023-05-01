﻿#if XUNIT_NULLABLE
#nullable enable
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Xunit.Sdk
{
	interface ICollectionTracker : IEnumerable, IDisposable
	{
		string FormatStart(int depth);
	}

	static class ArgumentFormatter
	{
		public static string Ellipsis = new string((char)0x00B7, 3);

		internal const int MAX_DEPTH = 3;
		internal const int MAX_ENUMERABLE_LENGTH = 5;
		internal const int MAX_ENUMERABLE_LENGTH_HALF = 2;
		internal const int MAX_OBJECT_PARAMETER_COUNT = 5;
		internal const int MAX_STRING_LENGTH = 50;

		static readonly object[] EmptyObjects = new object[0];
		static readonly Type[] EmptyTypes = new Type[0];

#if XUNIT_NULLABLE
		static PropertyInfo? tupleIndexer;
		static Type? tupleInterfaceType;
		static PropertyInfo? tupleLength;
#else
		static PropertyInfo tupleIndexer;
		static Type tupleInterfaceType;
		static PropertyInfo tupleLength;
#endif

		// List of intrinsic types => C# type names
		static readonly Dictionary<TypeInfo, string> TypeMappings = new Dictionary<TypeInfo, string>
		{
			{ typeof(bool).GetTypeInfo(), "bool" },
			{ typeof(byte).GetTypeInfo(), "byte" },
			{ typeof(sbyte).GetTypeInfo(), "sbyte" },
			{ typeof(char).GetTypeInfo(), "char" },
			{ typeof(decimal).GetTypeInfo(), "decimal" },
			{ typeof(double).GetTypeInfo(), "double" },
			{ typeof(float).GetTypeInfo(), "float" },
			{ typeof(int).GetTypeInfo(), "int" },
			{ typeof(uint).GetTypeInfo(), "uint" },
			{ typeof(long).GetTypeInfo(), "long" },
			{ typeof(ulong).GetTypeInfo(), "ulong" },
			{ typeof(object).GetTypeInfo(), "object" },
			{ typeof(short).GetTypeInfo(), "short" },
			{ typeof(ushort).GetTypeInfo(), "ushort" },
			{ typeof(string).GetTypeInfo(), "string" },
		};

		static ArgumentFormatter()
		{
			tupleInterfaceType = Type.GetType("System.Runtime.CompilerServices.ITuple");

			if (tupleInterfaceType != null)
			{
				tupleIndexer = tupleInterfaceType.GetRuntimeProperty("Item");
				tupleLength = tupleInterfaceType.GetRuntimeProperty("Length");
			}

			if (tupleIndexer == null || tupleLength == null)
				tupleInterfaceType = null;
		}

		internal static string EscapeString(string s)
		{
			var builder = new StringBuilder(s.Length);
			for (var i = 0; i < s.Length; i++)
			{
				var ch = s[i];
#if XUNIT_NULLABLE
				string? escapeSequence;
#else
				string escapeSequence;
#endif
				if (TryGetEscapeSequence(ch, out escapeSequence))
					builder.Append(escapeSequence);
				else if (ch < 32) // C0 control char
					builder.AppendFormat(@"\x{0}", (+ch).ToString("x2"));
				else if (char.IsSurrogatePair(s, i)) // should handle the case of ch being the last one
				{
					// For valid surrogates, append like normal
					builder.Append(ch);
					builder.Append(s[++i]);
				}
				// Check for stray surrogates/other invalid chars
				else if (char.IsSurrogate(ch) || ch == '\uFFFE' || ch == '\uFFFF')
				{
					builder.AppendFormat(@"\x{0}", (+ch).ToString("x4"));
				}
				else
					builder.Append(ch); // Append the char like normal
			}
			return builder.ToString();
		}

		public static string Format(
#if XUNIT_NULLABLE
			object? value,
#else
			object value,
#endif
			int depth = 1)
		{
			if (value == null)
				return "null";

			var valueAsType = value as Type;
			if (valueAsType != null)
				return $"typeof({FormatTypeName(valueAsType, fullTypeName: true)})";

			try
			{
				if (value.GetType().GetTypeInfo().IsEnum)
					return FormatEnumValue(value);

				if (value is char)
					return FormatCharValue((char)value);

				if (value is float)
					return FormatFloatValue(value);

				if (value is double)
					return FormatDoubleValue(value);

				if (value is DateTime || value is DateTimeOffset)
					return FormatDateTimeValue(value);

				var stringParameter = value as string;
				if (stringParameter != null)
					return FormatStringValue(stringParameter);

				var tracker = value as ICollectionTracker;
				if (tracker != null)
					return FormatCollectionTrackerValue(tracker, depth);

				var dictionary = value as IDictionary;
				if (dictionary != null)
					return FormatSafeEnumerableValue(dictionary, depth);

				var list = value as IList;
				if (list != null)
					return FormatSafeEnumerableValue(list, depth);

				var enumerable = value as IEnumerable;
				if (enumerable != null)
					return FormatUnsafeEnumerableValue(enumerable);

				var type = value.GetType();
				var typeInfo = type.GetTypeInfo();

				if (tupleInterfaceType != null && type.GetTypeInfo().ImplementedInterfaces.Contains(tupleInterfaceType))
					return FormatTupleValue(value, depth);

				if (typeInfo.IsValueType)
					return FormatValueTypeValue(value, typeInfo);

				var task = value as Task;
				if (task != null)
				{
					var typeParameters = typeInfo.GenericTypeArguments;
					var typeName = typeParameters.Length == 0 ? "Task" : $"Task<{string.Join(",", typeParameters.Select(FormatTypeName))}>";
					return $"{typeName} {{ Status = {task.Status} }}";
				}

				// TODO: ValueTask?

				var isAnonymousType = typeInfo.IsAnonymousType();
				if (!isAnonymousType)
				{
					var toString = type.GetRuntimeMethod("ToString", EmptyTypes);

					if (toString != null && toString.DeclaringType != typeof(object))
#if XUNIT_NULLABLE
						return ((string?)toString.Invoke(value, EmptyObjects)) ?? "null";
#else
						return ((string)toString.Invoke(value, EmptyObjects)) ?? "null";
#endif
				}

				return FormatComplexValue(value, depth, type, isAnonymousType);
			}
			catch (Exception ex)
			{
				// Sometimes an exception is thrown when formatting an argument, such as in ToString.
				// In these cases, we don't want xunit to crash, as tests may have passed despite this.
				return $"{ex.GetType().Name} was thrown formatting an object of type \"{value.GetType()}\"";
			}
		}

		static string FormatCharValue(char value)
		{
			if (value == '\'')
				return @"'\''";

			// Take care of all of the escape sequences
#if XUNIT_NULLABLE
			string? escapeSequence;
#else
			string escapeSequence;
#endif
			if (TryGetEscapeSequence(value, out escapeSequence))
				return $"'{escapeSequence}'";

			if (char.IsLetterOrDigit(value) || char.IsPunctuation(value) || char.IsSymbol(value) || value == ' ')
				return $"'{value}'";

			// Fallback to hex
			return $"0x{(int)value:x4}";
		}

		static string FormatComplexValue(
			object value,
			int depth,
			Type type,
			bool isAnonymousType)
		{
			var typeName = isAnonymousType ? "" : $"{type.Name} ";

			if (depth == MAX_DEPTH)
				return $"{typeName}{{ {Ellipsis} }}";

			var fields =
				type
					.GetRuntimeFields()
					.Where(f => f.IsPublic && !f.IsStatic)
					.Select(f => new { name = f.Name, value = WrapAndGetFormattedValue(() => f.GetValue(value), depth) });

			var properties =
				type
					.GetRuntimeProperties()
					.Where(p => p.GetMethod != null && p.GetMethod.IsPublic && !p.GetMethod.IsStatic)
					.Select(p => new { name = p.Name, value = WrapAndGetFormattedValue(() => p.GetValue(value), depth) });

			var parameters =
				fields
					.Concat(properties)
					.OrderBy(p => p.name)
					.Take(MAX_OBJECT_PARAMETER_COUNT + 1)
					.ToList();

			if (parameters.Count == 0)
				return $"{typeName}{{ }}";

			var formattedParameters = string.Join(", ", parameters.Take(MAX_OBJECT_PARAMETER_COUNT).Select(p => $"{p.name} = {p.value}"));

			if (parameters.Count > MAX_OBJECT_PARAMETER_COUNT)
				formattedParameters += ", " + Ellipsis;

			return $"{typeName}{{ {formattedParameters} }}";
		}

		static string FormatCollectionTrackerValue(
			ICollectionTracker tracker,
			int depth) =>
				tracker.FormatStart(depth);

		static string FormatDateTimeValue(object value) =>
			$"{value:o}";

		static string FormatDoubleValue(object value) =>
			$"{value:G17}";

		static string FormatEnumValue(object value) =>
			value.ToString()?.Replace(", ", " | ") ?? "null";

		static string FormatFloatValue(object value) =>
			$"{value:G9}";

		static string FormatSafeEnumerableValue(
			IEnumerable enumerable,
			int depth)
		{
			if (depth == MAX_DEPTH)
				return "[" + Ellipsis + "]";

			// This should only be used on values that are known to be re-enumerable
			// safely, like collections that implement IDictionary or IList.
			var idx = 0;
			var result = new StringBuilder("[");
			var enumerator = enumerable.GetEnumerator();

			while (enumerator.MoveNext())
			{
				if (idx != 0)
					result.Append(", ");

				if (idx == MAX_ENUMERABLE_LENGTH)
				{
					result.Append(Ellipsis);
					break;
				}

				var current = enumerator.Current;
				var nextDepth = current is IEnumerable ? depth + 1 : depth;

				result.Append(Format(current, nextDepth));

				++idx;
			}

			result.Append(']');
			return result.ToString();
		}

		static string FormatStringValue(string value)
		{
			value = EscapeString(value).Replace(@"""", @"\"""); // escape double quotes

			if (value.Length > MAX_STRING_LENGTH)
			{
				var displayed = value.Substring(0, MAX_STRING_LENGTH);
				return $"\"{displayed}\"" + Ellipsis;
			}

			return $"\"{value}\"";
		}

		static string FormatTupleValue(
			object tupleParameter,
			int depth)
		{
			var result = new StringBuilder("Tuple (");
#if XUNIT_NULLABLE
			var length = (int)tupleLength!.GetValue(tupleParameter)!;
#else
			var length = (int)tupleLength.GetValue(tupleParameter);
#endif

			for (var idx = 0; idx < length; ++idx)
			{
				if (idx != 0)
					result.Append(", ");

#if XUNIT_NULLABLE
				var value = tupleIndexer!.GetValue(tupleParameter, new object[] { idx });
#else
				var value = tupleIndexer.GetValue(tupleParameter, new object[] { idx });
#endif
				result.Append(Format(value, depth + 1));
			}

			result.Append(')');

			return result.ToString();
		}

		public static string FormatTypeName(Type type) =>
			FormatTypeName(type, false);

		public static string FormatTypeName(
			Type type,
			bool fullTypeName)
		{
			var typeInfo = type.GetTypeInfo();
			var arraySuffix = "";

			// Deconstruct and re-construct array
			while (typeInfo.IsArray)
			{
				if (typeInfo.IsSZArrayType())
					arraySuffix += "[]";
				else
				{
					var rank = typeInfo.GetArrayRank();
					if (rank == 1)
						arraySuffix += "[*]";
					else
						arraySuffix += $"[{new string(',', rank - 1)}]";
				}

#if XUNIT_NULLABLE
				typeInfo = typeInfo.GetElementType()!.GetTypeInfo();
#else
				typeInfo = typeInfo.GetElementType().GetTypeInfo();
#endif
			}

			// Map C# built-in type names
#if XUNIT_NULLABLE
			string? result;
#else
			string result;
#endif
			var shortTypeInfo = typeInfo.IsGenericType ? typeInfo.GetGenericTypeDefinition().GetTypeInfo() : typeInfo;
			if (!TypeMappings.TryGetValue(shortTypeInfo, out result))
				result = fullTypeName ? typeInfo.FullName : typeInfo.Name;

			if (result == null)
				return typeInfo.Name;

			var tickIdx = result.IndexOf('`');
			if (tickIdx > 0)
				result = result.Substring(0, tickIdx);

			if (typeInfo.IsGenericTypeDefinition)
				result = $"{result}<{new string(',', typeInfo.GenericTypeParameters.Length - 1)}>";
			else if (typeInfo.IsGenericType)
			{
				if (typeInfo.GetGenericTypeDefinition() == typeof(Nullable<>))
					result = FormatTypeName(typeInfo.GenericTypeArguments[0]) + "?";
				else
					result = $"{result}<{string.Join(", ", typeInfo.GenericTypeArguments.Select(FormatTypeName))}>";
			}

			return result + arraySuffix;
		}

		static string FormatUnsafeEnumerableValue(IEnumerable enumerable)
		{
			// When we don't know if we can safely enumerate (and to prevent double enumeration),
			// we just print out the container type name and the ellipsis.
			return $"{FormatTypeName(enumerable.GetType())} [{Ellipsis}]";
		}

		static string FormatValueTypeValue(
			object value,
			TypeInfo typeInfo)
		{
			if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
			{
				var k = typeInfo.GetDeclaredProperty("Key")?.GetValue(value, null);
				var v = typeInfo.GetDeclaredProperty("Value")?.GetValue(value, null);

				return $"[{Format(k)}] = {Format(v)}";
			}

			return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "null";
		}

		static bool IsAnonymousType(this TypeInfo typeInfo)
		{
			// There isn't a sanctioned way to do this, so we look for compiler-generated types that
			// include "AnonymousType" in their names.
			if (typeInfo.GetCustomAttribute(typeof(CompilerGeneratedAttribute)) == null)
				return false;

			return typeInfo.Name.Contains("AnonymousType");
		}

		static bool IsSZArrayType(this TypeInfo typeInfo)
		{
#if NETCOREAPP2_0_OR_GREATER
			return typeInfo.IsSZArray;
#elif XUNIT_NULLABLE
			return typeInfo == typeInfo.GetElementType()!.MakeArrayType().GetTypeInfo();
#else
			return typeInfo == typeInfo.GetElementType().MakeArrayType().GetTypeInfo();
#endif
		}

		static bool TryGetEscapeSequence(
			char ch,
#if XUNIT_NULLABLE
			out string? value)
#else
			out string value)
#endif
		{
			value = null;

			if (ch == '\t') // tab
				value = @"\t";
			if (ch == '\n') // newline
				value = @"\n";
			if (ch == '\v') // vertical tab
				value = @"\v";
			if (ch == '\a') // alert
				value = @"\a";
			if (ch == '\r') // carriage return
				value = @"\r";
			if (ch == '\f') // formfeed
				value = @"\f";
			if (ch == '\b') // backspace
				value = @"\b";
			if (ch == '\0') // null char
				value = @"\0";
			if (ch == '\\') // backslash
				value = @"\\";

			return value != null;
		}

		static Exception UnwrapException(Exception ex)
		{
			while (true)
			{
				var tiex = ex as TargetInvocationException;
				if (tiex == null || tiex.InnerException == null)
					return ex;

				ex = tiex.InnerException;
			}
		}

		static string WrapAndGetFormattedValue(
#if XUNIT_NULLABLE
			Func<object?> getter,
#else
			Func<object> getter,
#endif
			int depth)
		{
			try
			{
				return Format(getter(), depth + 1);
			}
			catch (Exception ex)
			{
				return $"(throws {UnwrapException(ex)?.GetType().Name})";
			}
		}
	}
}
