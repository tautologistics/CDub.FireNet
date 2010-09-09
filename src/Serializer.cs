/******************************************************************************
 * FirePHP ASP.Net Server Library | CDub.FireNet.Serializer
 * Copyright (C) 2008 Chris Winberry / Tautologistics
 * Email: chris(at)winberry(dot)net
 * Postal: 112 Pawnee Ave. | Oakland, NJ 07436
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Reflection;
using System.Xml;

namespace CDub.FireNet {

	/// <summary>
	/// Serializes objects to a FirePHP-specific format of JSON
	/// </summary>
	public class Serializer {

		/// <summary>
		/// JSON null value
		/// </summary>
		protected const string NULLVALUE = "null";
		/// <summary>
		/// JSON property that represent an object's class/type
		/// </summary>
		protected const string CLASSPROPNAME = "Class";
		/// <summary>
		/// JSON token used to start a hash/object
		/// </summary>
		protected const string HASHOPEN = "{";
		/// <summary>
		/// JSON token used to end a hash/object
		/// </summary>
		protected const string HASHCLOSE = "}";
		/// <summary>
		/// JSON token used to start an array
		/// </summary>
		protected const string ARRAYOPEN = "[";
		/// <summary>
		/// JSON token used to end an array
		/// </summary>
		protected const string ARRAYCLOSE = "]";

		/// <summary>
		/// Cached IDictionary type
		/// </summary>
		protected static Type _idictType;
		/// <summary>
		/// Cached ICollection type
		/// </summary>
		protected static Type _icollType;
		/// <summary>
		/// Cached IEnumerable type
		/// </summary>
		protected static Type _ienumType;

		static Serializer () {
			_idictType = typeof(IDictionary);
			_icollType = typeof(ICollection);
			_ienumType = typeof(IEnumerable);
		}

		/// <summary>
		/// Escapes and quotes a string
		/// </summary>
		/// <param name="data">String to be escaped</param>
		/// <returns>Escaped and quoted (double) string</returns>
		public static string QuoteData (string data) {
			return (String.Format("\"{0}\"",
				data.Replace("\\", "\\\\")
					.Replace("\"", "\\\"")
					.Replace("\r", "\\r")
					.Replace("\n", "\\n")
				));
		}

		/// <summary>
		/// Serializes an object to FirePHP-specific format of JSON
		/// </summary>
		/// <param name="data">Object to serialize</param>
		/// <returns>JSON formatted string</returns>
		public static string Serialize (Object data) {
			StringBuilder sb = new StringBuilder();
			Serialize(data, sb);
			return (sb.ToString());
		}

		/// <summary>
		/// Serializes an object to FirePHP-specific format of JSON
		/// </summary>
		/// <param name="data">Object to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		public static void Serialize (Object data, StringBuilder sb) {
			if (null == data) {
				sb.Append(NULLVALUE);
				return;
			}

			switch (Type.GetTypeCode(data.GetType())) {
				case TypeCode.Empty:
				case TypeCode.DBNull:
					sb.Append(NULLVALUE);
					break;

				case TypeCode.Boolean:
				case TypeCode.Byte:
				case TypeCode.Char:
				case TypeCode.Decimal:
				case TypeCode.Double:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
				case TypeCode.SByte:
				case TypeCode.Single:
				case TypeCode.String:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
					sb.Append(QuoteData(String.Format("{0}", data)));
					break;

				case TypeCode.DateTime:
					sb.Append(XmlConvert.ToString((DateTime)data, XmlDateTimeSerializationMode.Utc));
					break;

				case TypeCode.Object:
					Type dataType = data.GetType();
					if (dataType.IsArray) {
						HandleArray((Array)data, sb);
					}
					else if (data is NameValueCollection) {
						HandleNVCollection((NameValueCollection)data, sb);
					}
					else if (dataType.GetInterface(_idictType.FullName) != null) {
						HandleIDictionary((IDictionary)data, sb);
					}
					else if (dataType.GetInterface(_ienumType.FullName) != null) {
						HandleIEnumerable((IEnumerable)data, sb);
					}
					else if (dataType.GetInterface(_icollType.FullName) != null) {
						HandleICollection((ICollection)data, sb);
					}
					else {
						HandleObject(data, sb);
					}
					break;
			}

		}

		/// <summary>
		/// Serializes an object to JSON
		/// </summary>
		/// <param name="data">Object to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		protected static void HandleObject (Object data, StringBuilder sb) {
			Type dataType = data.GetType();
			sb.Append(HASHOPEN);
			sb.Append(String.Format("\"{0}\":\"{1}\"", CLASSPROPNAME, data.GetType().FullName));
			HandleObjectProperties(data, sb);
			HandleObjectFields(data, sb);
			sb.Append(HASHCLOSE);
		}

		/// <summary>
		/// Serializes an object's fields to JSON
		/// </summary>
		/// <param name="data">Object to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		protected static void HandleObjectFields (Object data, StringBuilder sb) {
			Type dataType = data.GetType();
			FieldInfo[] fields = dataType.GetFields();
			for (int i = 0; i < fields.Length; i++) {
				Object fieldValue = fields[i].GetValue(data);
				if (
					(null != fieldValue)
					&&
					(fieldValue.GetType().IsNotPublic || !fieldValue.GetType().IsVisible)) {
					continue;
				}

				sb.Append(String.Format(",\"{0}\":", fields[i].Name));
				Serialize(fieldValue, sb);
			}
		}

		/// <summary>
		/// Serializes an object's properties to JSON
		/// </summary>
		/// <param name="data">Object to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		protected static void HandleObjectProperties (Object data, StringBuilder sb) {
			Type dataType = data.GetType();
			PropertyInfo[] props = dataType.GetProperties();
			for (int i = 0; i < props.Length; i++) {
				Object propValue = props[i].GetValue(data, null);
				if (
					(null != propValue)
					&&
					(propValue.GetType().IsNotPublic || !propValue.GetType().IsVisible)) {
					continue;
				}

				sb.Append(String.Format(",\"{0}\":", props[i].Name));
				Serialize(propValue, sb);
			}
		}

		/// <summary>
		/// Serializes an ICollection object to JSON
		/// </summary>
		/// <param name="collection">ICollection to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		protected static void HandleICollection (ICollection collection, StringBuilder sb) {
			Boolean isFirst = true;
			sb.Append(ARRAYOPEN);
			foreach (object obj in collection) {
				if (isFirst)
					isFirst = false;
				else
					sb.Append(",");
				Serialize(obj, sb);
			}
			sb.Append(ARRAYCLOSE);
		}

		/// <summary>
		/// Serializes an IEnumerable object to JSON
		/// </summary>
		/// <param name="enumerable">IEnumerable to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		protected static void HandleIEnumerable (IEnumerable enumerable, StringBuilder sb) {
			Boolean isFirst = true;
			sb.Append(ARRAYOPEN);
			foreach (object obj in enumerable) {
				if (isFirst)
					isFirst = false;
				else
					sb.Append(",");
				Serialize(obj, sb);
			}
			sb.Append(ARRAYCLOSE);
		}

		/// <summary>
		/// Serializes an IDictionary object to JSON
		/// </summary>
		/// <param name="dictionary">IDictionary to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		protected static void HandleIDictionary (IDictionary dictionary, StringBuilder sb) {
			Boolean isFirst = true;
			sb.Append(HASHOPEN);
			foreach (DictionaryEntry entry in dictionary) {
				if (isFirst)
					isFirst = false;
				else
					sb.Append(",");
				Serialize(entry.Key, sb);
				sb.Append(":");
				Serialize(entry.Value, sb);
			}
			sb.Append(HASHCLOSE);
		}

		/// <summary>
		/// Serializes an NameValueCollection to JSON
		/// </summary>
		/// <param name="nvData">NameValueCollection to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		protected static void HandleNVCollection (NameValueCollection nvData, StringBuilder sb) {
			Boolean isFirst = true;
			sb.Append(HASHOPEN);
			foreach (String name in nvData.AllKeys) {
				if (isFirst)
					isFirst = false;
				else
					sb.Append(",");
				Serialize(name, sb);
				sb.Append(":");
				Serialize(nvData[name], sb);
			}
			sb.Append(HASHCLOSE);
		}

		/// <summary>
		/// Serializes an Array to JSON
		/// </summary>
		/// <param name="arrayData">Array to serialize</param>
		/// <param name="sb">StringBuilder to write the JSON to</param>
		protected static void HandleArray (Array arrayData, StringBuilder sb) {
			sb.Append(ARRAYOPEN);
			for (int i = 0; i < arrayData.Length; i++) {
				if (i > 0)
					sb.Append(",");
				Serialize(arrayData.GetValue(i), sb);
			}
			sb.Append(ARRAYCLOSE);
		}

	}

}
