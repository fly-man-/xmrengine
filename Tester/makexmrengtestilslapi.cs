/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Reflection;
using System.Text;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.XMREngine
{
    public class XMREngTest
    {

        /*
         * These functions have actual implementations in xmrengtest.cx's ScriptBaseClass.
         */
        public static readonly string[] skips = new string[] {
            "llAbs",
            "llAcos",
            "llAngleBetween",
            "llAsin",
            "llAtan2",
            "llAxes2Rot",
            "llAxisAngle2Rot",
            "llBase64ToInteger",
            "llBase64ToString",
            "llCeil",
            "llCos",
            "llCSV2List",
            "llDeleteSubList",
            "llDeleteSubString",
            "llDie",
            "llDumpList2String",
            "llEscapeURL",
            "llEuler2Rot",
            "llFabs",
            "llFloor",
            "llGetKey",
            "llGetLinkNumber",
            "llGetListEntryType",
            "llGetListLength",
            "llGetObjectName",
            "llGetSubString",
            "llInsertString",
            "llIntegerToBase64",
            "llList2CSV",
            "llList2Float",
            "llList2Integer",
            "llList2Key",
            "llList2List",
            "llList2ListStrided",
            "llList2Rot",
            "llList2String",
            "llList2Vector",
            "llListen",
            "llListenControl",
            "llListenRemove",
            "llListFindList",
            "llListInsertList",
            "llListRandomize",
            "llListReplaceList",
            "llListSort",
            "llListStatistics",
            "llLog",
            "llLog10",
            "llMD5String",
            "llMessageLinked",
            "llModPow",
            "llParseString2List",
            "llParseStringKeepNulls",
            "llPow",
            "llRegionSay",
            "llRegionSayTo",
            "llResetScript",
            "llRot2Angle",
            "llRot2Axis",
            "llRot2Euler",
            "llRot2Fwd",
            "llRot2Left",
            "llRot2Up",
            "llRotBetween",
            "llRound",
            "llSay",
            "llSHA1String",
            "llSin",
            "llSqrt",
            "llStringLength",
            "llStringToBase64",
            "llStringTrim",
            "llSubStringIndex",
            "llTan",
            "llToLower",
            "llToUpper",
            "llUnescapeURL",
            "llVecDist",
            "llVecMag",
            "llVecNorm",
            "llXorBase64StringsCorrect",
            "osDrawEllipse",
            "osDrawFilledPolygon",
            "osDrawFilledRectangle",
            "osDrawImage",
            "osDrawLine",
            "osDrawPolygon",
            "osDrawRectangle",
            "osDrawText",
            "osFormatString",
            "osList2Double",
            "osMatchString",
            "osMovePen",
            "osParseJSON",
            "osSetFontName",
            "osSetFontSize",
            "osSetPenCap",
            "osSetPenColor",
            "osSetPenColour",
            "osSetPenSize",
            "osUnixTimeToTimestamp",
            "state"
        };

        public static void Main (string[] args)
        {
            DoMethods (typeof (OpenSim.Region.ScriptEngine.Shared.Api.Interfaces.ILSL_Api));
            DoMethods (typeof (OpenSim.Region.ScriptEngine.Shared.Api.Interfaces.IOSSL_Api));

            FieldInfo[] fields = typeof (OpenSim.Region.ScriptEngine.Shared.ScriptBase.ScriptBaseClass).GetFields ();
            foreach (FieldInfo field in fields) {
                string name = field.Name;
                if (!field.IsPublic) continue;
                if (field.IsLiteral) goto doit;
                if (!field.IsStatic) continue;
                if (!field.IsInitOnly) continue;
            doit:
                Console.Write ("        public");
                if (field.IsLiteral) Console.Write (" const");
                                else Console.Write (" static readonly");
                Console.Write (" " + TypeStr (field.FieldType) + " " + name + " = ");
                Console.Write (ValuStr (field.GetValue (null)));
                Console.WriteLine (";");
            }
        }

        public static void DoMethods (Type api)
        {
            MethodInfo[] methods = api.GetMethods ();
            foreach (MethodInfo method in methods) {
                string name = method.Name;

                int i;
                for (i = skips.Length; -- i >= 0;) {
                    if (skips[i] == name) break;
                }
                if (i >= 0) continue;

                string stubname = null;
                Type retType = method.ReturnType;
                if (retType == typeof (void)) {
                    stubname = "StubVoid";
                }
                if (retType == typeof (LSL_Float)) {
                    stubname = "return StubLSLFloat";
                }
                if (retType == typeof (LSL_Integer)) {
                    stubname = "return StubLSLInteger";
                }
                if (retType == typeof (LSL_List)) {
                    stubname = "return StubLSLList";
                }
                if (retType == typeof (LSL_Rotation)) {
                    stubname = "return StubLSLRotation";
                }
                if (retType == typeof (LSL_String)) {
                    stubname = "return StubLSLString";
                }
                if (retType == typeof (LSL_Vector)) {
                    stubname = "return StubLSLVector";
                }
                if (retType == typeof (bool)) {
                    stubname = "return StubSysBoolean";
                }
                if (retType == typeof (double)) {
                    stubname = "return StubSysDouble";
                }
                if (retType == typeof (int)) {
                    stubname = "return StubSysInteger";
                }
                if (retType == typeof (object)) {
                    stubname = "return StubSysObject";
                }
                if (retType == typeof (string)) {
                    stubname = "return StubSysString";
                }
                if (stubname == null) {
                    throw new Exception ("unsupported return type " + retType.ToString () + " for " + name + "()");
                }

                Console.Write ("        [xmrMethodIsNoisyAttribute]");
                Console.Write ("        public " + TypeStr (retType) + " " + name + " (");
                ParameterInfo[] parms = method.GetParameters ();
                for (i = 0; i < parms.Length; i ++) {
                    if (i > 0) Console.Write (", ");
                    Console.Write (TypeStr (parms[i].ParameterType) + " " + parms[i].Name);
                }
                Console.WriteLine (")");
                Console.WriteLine ("        {");
                Console.Write ("            " + stubname + " (\"" + name + "\"");
                for (i = 0; i < parms.Length; i ++) {
                    Console.Write (", " + parms[i].Name);
                }
                Console.WriteLine (");");
                Console.WriteLine ("        }");
            }
        }

        public static string TypeStr (Type t)
        {
            if (t == typeof (double))       return "double";
            if (t == typeof (float))        return "float";
            if (t == typeof (int))          return "int";
            if (t == typeof (string))       return "string";
            if (t == typeof (void))         return "void";

            if (t == typeof (LSL_Float))    return "LSL_Float";
            if (t == typeof (LSL_Integer))  return "LSL_Integer";
            if (t == typeof (LSL_List))     return "LSL_List";
            if (t == typeof (LSL_Rotation)) return "LSL_Rotation";
            if (t == typeof (LSL_String))   return "LSL_String";
            if (t == typeof (LSL_Vector))   return "LSL_Vector";

            return t.ToString ();
        }

        public static string ValuStr (object o)
        {
            if (o is double)       return ((double)o).ToString ();
            if (o is float)        return ((float)o).ToString ();
            if (o is int)          return ((int)o).ToString ();
            if (o is string)       return StrStr ((string)o);

            if (o is LSL_Float)    return "new LSL_Float(" + ((double)(LSL_Float)o).ToString () + ")";
            if (o is LSL_Integer)  return "new LSL_Integer(" + ((int)(LSL_Integer)o).ToString () + ")";
            if (o is LSL_Rotation) return RotStr ((LSL_Rotation)o);
            if (o is LSL_String)   return "new LSL_String(" + StrStr ((string)o) + ")";
            if (o is LSL_Vector)   return VecStr ((LSL_Vector)o);

            throw new Exception ("unknown type " + o.GetType ().ToString ());
        }

        public static string RotStr (LSL_Rotation r)
        {
            return "new LSL_Rotation(" + r.x + "," + r.y + "," + r.z + "," + r.s + ")";
        }
        public static string StrStr (string s)
        {
            StringBuilder sb = new StringBuilder ();
            sb.Append ('"');
            foreach (char c in s) {
                if (c == '\n') {
                    sb.Append ("\\n");
                    continue;
                }
                if (c == '"') {
                    sb.Append ("\\\"");
                    continue;
                }
                if (c == '\\') {
                    sb.Append ("\\\\");
                    continue;
                }
                sb.Append (c);
            }
            sb.Append ('"');
            return sb.ToString ();
        }
        public static string VecStr (LSL_Vector v)
        {
            return "new LSL_Vector(" + v.x + "," + v.y + "," + v.z + ")";
        }
    }
}
