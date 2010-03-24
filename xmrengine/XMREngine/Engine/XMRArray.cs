/***************************************************\
 *  COPYRIGHT 2010, Mike Rieker, Beverly, MA, USA  *
 *  All rights reserved.                           *
\***************************************************/

using System;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Security.Policy;
using System.IO;
using System.Xml;
using System.Text;
using Mono.Tasklets;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.XMREngine;
using OpenSim.Region.Framework.Scenes;
using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using log4net;

// This class exists in the main app domain
//
namespace OpenSim.Region.ScriptEngine.XMREngine
{

	/**
	 * @brief Array objects.
	 */
	public class XMR_Array {
		private bool enumrValid;                              // true: enumr set to return array[arrayValid]
		                                                      // false: array[0..arrayValid-1] is all there is
		private Dictionary<object, object> dnary = new Dictionary<object, object> ();
		private Dictionary<object, object>.Enumerator enumr;  // enumerator used to fill 'array' past arrayValid to end of dictionary
		private int arrayValid;                               // number of elements in 'array' that have been filled in
		private KeyValuePair<object, object>[] array;         // list of kvp's that have been returned by ForEach() since last modification

		/**
		 * @brief Handle 'array[index]' syntax to get or set an element of the dictionary.
		 * Get returns null if element not defined, script sees type 'undef'.
		 * Setting an element to null removes it.
		 */
		public object GetByKey(object key)
		{
			object val;
			if (!dnary.TryGetValue (key, out val)) val = null;
			return val;
		}

		public void SetByKey(object key, object value)
		{
			/*
			 * Save new value in array, replacing one of same key if there.
			 * null means remove the value, ie, script did array[key] = undef.
			 */
			if (value != null) {
				dnary[key] = value;
			} else {
				dnary.Remove (key);

				/*
				 * Shrink the enumeration array, but always leave at least one element.
				 */
				if ((array != null) && (dnary.Count < array.Length / 2)) {
					Array.Resize<KeyValuePair<object, object>> (ref array, array.Length / 2);
				}
			}

			/*
			 * The enumeration array is invalid because the dictionary has been modified.
			 * Next time a ForEach() call happens, it will repopulate 'array' as elements are retrieved.
			 */
			arrayValid = 0;
		}

		/**
		 * @brief Converts an 'object' type to array, key, list, string, but disallows null,
		 *        as our language doesn't allow types other than 'object' to be null.
		 *        Value types (float, rotation, etc) don't need explicit check for null as
		 *        the C# runtime can't convert a null to a value type, and throws an exception.
		 *        But for any reference type (array, key, etc) we must manually check for null.
		 */
		public static XMR_Array Obj2Array (object obj)
		{
			if (obj == null) throw new NullReferenceException ();
			return (XMR_Array)obj;
		}
		public static LSL_Key Obj2Key (object obj)
		{
			if (obj == null) throw new NullReferenceException ();
			return (LSL_Key)obj;
		}
		public static LSL_List Obj2List (object obj)
		{
			if (obj == null) throw new NullReferenceException ();
			return (LSL_List)obj;
		}
		public static LSL_String Obj2String (object obj)
		{
			if (obj == null) throw new NullReferenceException ();
			return obj.ToString ();
		}

		/**
		 * @brief return number of elements in the array.
		 */
		public int __pub_count {
			get { return dnary.Count; }
		}

		/**
		 * @brief Retrieve index (key) of an arbitrary element.
		 * @param number = number of the element (0 based)
		 * @returns null: array doesn't have that many elements
		 *          else: index (key) for that element
		 */
		public object __pub_index (int number)
		{
			object key = null;
			object val = null;
			ForEach (number, ref key, ref val);
			return key;
		}


		/**
		 * @brief Retrieve value of an arbitrary element.
		 * @param number = number of the element (0 based)
		 * @returns null: array doesn't have that many elements
		 *          else: value for that element
		 */
		public object __pub_value (int number)
		{
			object key = null;
			object val = null;
			ForEach (number, ref key, ref val);
			return val;
		}

		/**
		 * @brief Called in each iteration of a 'foreach' statement.
		 * @param number = index of element to retrieve (0 = first one)
		 * @returns false: element does not exist
		 *           true: element exists:
		 *                 key = key of retrieved element
		 *                 val = value of retrieved element
		 */
		public bool ForEach (int number, ref object key, ref object val)
		{

			/*
			 * If we don't have any array, we can't have ever done
			 * any calls here before, so allocate an array big enough
			 * and set everything else to the beginning.
			 */
			if (array == null) {
				array = new KeyValuePair<object, object>[dnary.Count];
				arrayValid = 0;
			}

			/*
			 * If dictionary modified since last enumeration, get a new enumerator.
			 */
			if (arrayValid == 0) {
				enumr = dnary.GetEnumerator ();
				enumrValid = true;
			}

			/*
			 * Make sure we have filled the array up enough for requested element.
			 */
			while ((arrayValid <= number) && enumrValid && enumr.MoveNext ()) {
				if (arrayValid >= array.Length) {
					Array.Resize<KeyValuePair<object, object>> (ref array, dnary.Count);
				}
				array[arrayValid++] = enumr.Current;
			}

			/*
			 * If we don't have that many elements, return end-of-array status.
			 */
			if (arrayValid <= number) return false;

			/*
			 * Return the element values.
			 */
			key = array[number].Key;
			val = array[number].Value;
			return true;
		}

		/**
		 * @brief Transmit array out in such a way that it can be reconstructed,
		 *        including any in-progress ForEach() enumerations.
		 */
		public void SendArrayObj (Mono.Tasklets.MMRContSendObj sendObj, Stream stream)
		{
			int index = arrayValid;
			object key = null;
			object val = null;

			/*
			 * Completely fill the array from where it is already filled to the end.
			 * Any elements before arrayValid remain as the current enumerator has
			 * seen them, and any after arrayValid will be filled by that same
			 * enumerator.  The array will then be used on the receiving end to iterate
			 * in the same exact order, because a new enumerator on the receiving end
			 * probably wouldn't see them in the same order.
			 */
			while (ForEach (index ++, ref key, ref val)) { }

			/*
			 * Set the count then the elements themselves.
			 */
			sendObj (stream, (object)arrayValid);
			for (index = 0; index < arrayValid; index ++) {
				sendObj (stream, array[index].Key);
				sendObj (stream, array[index].Value);
			}
		}

		/**
		 * @brief Receive array in.  Any previous contents are erased.
		 *        Set up such that any enumeration in progress will resume
		 *        at the exact spot and in the exact same order as they
		 *        were in on the sending side.
		 */
		public void RecvArrayObj (Mono.Tasklets.MMRContRecvObj recvObj, Stream stream)
		{
			int index;

			/*
			 * Empty the dictionary.
			 */
			dnary.Clear ();

			/*
			 * Any enumerator in progress is now invalid, and all elements
			 * for enumeration must come from the array, so they will be in
			 * the same order they were in on the sending side.
			 */
			enumrValid = false;

			/*
			 * Get number of elements we will receive and set up an
			 * array to receive them into.  The array will be used
			 * for any enumerations in progress, and will have elements
			 * in order as given by previous calls to those enumerations.
			 */
			arrayValid = (int)recvObj (stream);
			array = new KeyValuePair<object, object>[arrayValid];

			/*
			 * Fill the array and dictionary.
			 * Any enumerations will use the array so they will be in the
			 * same order as on the sending side (until the dictionary is
			 * modified).
			 */
			for (index = 0; index < arrayValid; index ++) {
				object key = recvObj (stream);
				object val = recvObj (stream);
				array[index] = new KeyValuePair<object, object> (key, val);
				dnary.Add (key, val);
			}
		}
	}
}
