#if XUNIT_NULLABLE
#nullable enable
#endif

using System;
using Xunit.Internal;

namespace Xunit.Sdk
{
	/// <summary>
	/// Exception thrown when a collection unexpectedly does not contain the expected value.
	/// </summary>
#if XUNIT_VISIBILITY_INTERNAL
	internal
#else
	public
#endif
	class DoesNotContainException : XunitException
	{
		DoesNotContainException(string message) :
			base(message)
		{ }

		/// <summary>
		/// Creates a new instance of the <see cref="DoesNotContainException"/> class to be thrown
		/// when the requested filter matches an item in the collection.
		/// </summary>
		/// <param name="indexFailurePoint">The item index for where the item was found</param>
		/// <param name="failurePointerIndent">The number of spaces needed to indent the failure pointer</param>
		/// <param name="collection">The collection</param>
		public static DoesNotContainException ForCollectionFilterMatched(
			int indexFailurePoint,
			int failurePointerIndent,
			string collection) =>
				new DoesNotContainException(
					"Assert.DoesNotContain() Failure: Filter matched in collection" + Environment.NewLine +
					"            " + new string(' ', failurePointerIndent) + "↓ (pos " + indexFailurePoint + ")" + Environment.NewLine +
					"Collection: " + collection
				);

		/// <summary>
		/// Creates a new instance of the <see cref="DoesNotContainException"/> class to be thrown
		/// when the requested item was found in the collection.
		/// </summary>
		/// <param name="item">The item that was found in the collection</param>
		/// <param name="indexFailurePoint">The item index for where the item was found</param>
		/// <param name="failurePointerIndent">The number of spaces needed to indent the failure pointer</param>
		/// <param name="collection">The collection</param>
		public static DoesNotContainException ForCollectionItemFound(
			string item,
			int indexFailurePoint,
			int failurePointerIndent,
			string collection) =>
				new DoesNotContainException(
					"Assert.DoesNotContain() Failure: Item found in collection" + Environment.NewLine +
					"            " + new string(' ', failurePointerIndent) + "↓ (pos " + indexFailurePoint + ")" + Environment.NewLine +
					"Collection: " + collection + Environment.NewLine +
					"Found:      " + item
				);

		/// <summary>
		/// Creates a new instance of the <see cref="DoesNotContainException"/> class to be thrown
		/// when the requested key was found in the dictionary.
		/// </summary>
		/// <param name="expectedKey">The expected key value</param>
		/// <param name="keys">The dictionary keys</param>
		public static DoesNotContainException ForKeyFound(
			string expectedKey,
			string keys) =>
				new DoesNotContainException(
					"Assert.DoesNotContain() Failure: Key found in dictionary" + Environment.NewLine +
					"Keys:  " + keys + Environment.NewLine +
					"Found: " + expectedKey
				);

		/// <summary>
		/// Creates a new instance of the <see cref="DoesNotContainException"/> class to be thrown
		/// when the requested item was found in the set.
		/// </summary>
		/// <param name="item">The item that was found in the collection</param>
		/// <param name="set">The set</param>
		public static DoesNotContainException ForSetItemFound(
			string item,
			string set) =>
				new DoesNotContainException(
					"Assert.DoesNotContain() Failure: Item found in set" + Environment.NewLine +
					"Set:   " + set + Environment.NewLine +
					"Found: " + item
				);

		/// <summary>
		/// Creates a new instance of the <see cref="DoesNotContainException"/> class to be thrown
		/// when the requested sub-memory was found in the memory.
		/// </summary>
		/// <param name="expectedSubMemory">The expected sub-memory</param>
		/// <param name="indexFailurePoint">The item index for where the item was found</param>
		/// <param name="failurePointerIndent">The number of spaces needed to indent the failure pointer</param>
		/// <param name="memory">The memory</param>
		public static Exception ForSubMemoryFound(
			string expectedSubMemory,
			int indexFailurePoint,
			int failurePointerIndent,
			string memory) =>
				new DoesNotContainException(
					"Assert.DoesNotContain() Failure: Sub-memory found" + Environment.NewLine +
					"        " + new string(' ', failurePointerIndent) + "↓ (pos " + indexFailurePoint + ")" + Environment.NewLine +
					"Memory: " + memory + Environment.NewLine +
					"Found:  " + expectedSubMemory
				);

		/// <summary>
		/// Creates a new instance of the <see cref="DoesNotContainException"/> class to be thrown
		/// when the requested sub-span was found in the span.
		/// </summary>
		/// <param name="expectedSubSpan">The expected sub-span</param>
		/// <param name="indexFailurePoint">The item index for where the item was found</param>
		/// <param name="failurePointerIndent">The number of spaces needed to indent the failure pointer</param>
		/// <param name="span">The span</param>
		public static Exception ForSubSpanFound(
			string expectedSubSpan,
			int indexFailurePoint,
			int failurePointerIndent,
			string span) =>
				new DoesNotContainException(
					"Assert.DoesNotContain() Failure: Sub-span found" + Environment.NewLine +
					"       " + new string(' ', failurePointerIndent) + "↓ (pos " + indexFailurePoint + ")" + Environment.NewLine +
					"Span:  " + span + Environment.NewLine +
					"Found: " + expectedSubSpan
				);

		/// <summary>
		/// Creates a new instance of the <see cref="DoesNotContainException"/> class to be thrown
		/// when the requested sub-string was found in the string.
		/// </summary>
		/// <param name="expectedSubString">The expected sub-string</param>
		/// <param name="indexFailurePoint">The item index for where the item was found</param>
		/// <param name="string">The string</param>
		public static Exception ForSubStringFound(
			string expectedSubString,
			int indexFailurePoint,
			string @string)
		{
			int failurePointerIndent;
			var encodedString = AssertHelper.ShortenAndEncodeString(@string, indexFailurePoint, out failurePointerIndent);

			return new DoesNotContainException(
				"Assert.DoesNotContain() Failure: Sub-string found" + Environment.NewLine +
				"        " + new string(' ', failurePointerIndent) + "↓ (pos " + indexFailurePoint + ")" + Environment.NewLine +
				"String: " + encodedString + Environment.NewLine +
				"Found:  " + AssertHelper.ShortenAndEncodeString(expectedSubString)
			);
		}
	}
}
