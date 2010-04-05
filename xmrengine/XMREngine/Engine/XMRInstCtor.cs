//////////////////////////////////////////////////////////////
//
// Copyright (c) 2009 Careminster Limited and Melanie Thielker
// Copyright (c) 2010 Mike Rieker, Beverly, MA, USA
//
// All rights reserved
//

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

namespace OpenSim.Region.ScriptEngine.XMREngine
{
    public partial class XMRInstance
    {
        /****************************************************************************\
         *  The only method of interest to outside this module is the constructor.  *
         *                                                                          *
         *  The rest of this module contains support routines for the constructor.  *
        \****************************************************************************/

        /**
         * @brief Constructor, loads script in memory and all ready for running.
         * @param localID = ?
         * @param itemID = UUID of this script instance
         * @param script = script source code all in one string
         * @param startParam = as passed to llRez() ???
         * @param postOnRez = post an "on_rez" event to script
         * @param stateSource = ?
         * @param engine = XMREngine instance this is part of
         * @param part = the object the script is attached to ?
         * @param item = ?
         * @param scriptBasePath = directory name where files are
         * @param stackSize = number of bytes to allocate for stacks
         * @param errors = return compiler errors in this array
         * Throws exception if any error, so it was successful if it returns.
         */
        public XMRInstance(uint localID, UUID itemID, string script,
                           int startParam, bool postOnRez, int stateSource,
                           XMREngine engine, SceneObjectPart part, 
                           TaskInventoryItem item, string scriptBasePath,
                           UIntPtr stackSize, ArrayList errors)
        {
            if (stackSize.ToUInt64() < 16384) stackSize = (UIntPtr)16384;

            /*
             * Save all call parameters in instance vars for easy access.
             */
            m_LocalID        = localID;
            m_ItemID         = itemID;
            m_SourceCode     = script;
            m_StartParam     = startParam;
            m_PostOnRez      = postOnRez;
            m_StateSource    = (StateSource)stateSource;
            m_Engine         = engine;
            m_Part           = part;
            m_Item           = item;
            m_AssetID        = item.AssetID;
            m_ScriptBasePath = scriptBasePath;
            m_StackSize      = stackSize;
            m_CompilerErrors = errors;
            m_StateFileName  = GetStateFileName(scriptBasePath, itemID);

            /*
             * Set up a descriptive name string for debug messages.
             */
            m_DescName  = MMRCont.HexString(MMRCont.ObjAddr(this)).PadLeft(8, '0') + " ";
            m_DescName += part.Name + ":" + item.Name;

            /*
             * Not in any XMRInstQueue, and it is being constructed so don't
             * try to run it yet.
             */
            m_NextInst = this;
            m_PrevInst = this;
            m_IState   = XMRInstState.CONSTRUCT;

            /*
             * Get object loaded, compiling script and reading .state file as
             * necessary.
             */
            InstantiateScript();
            m_SourceCode = null;
            if (objCode == null) throw new ArgumentNullException ("objCode");
            if (objCode.scriptEventHandlerTable == null) {
                throw new ArgumentNullException ("objCode.scriptEventHandlerTable");
            }
            suspendOnCheckRunHold = false;
            suspendOnCheckRunTemp = false;

            /*
             * Set up list of API calls it has available.
             */
            beAPI = new ScriptBaseClass();
            ApiManager am = new ApiManager();
            foreach (string api in am.GetApis())
            {
                IScriptApi scriptApi;

                if (api != "LSL")
                    scriptApi = am.CreateApi(api);
                else
                    scriptApi = new XMRLSL_Api();

                m_Apis[api] = scriptApi;
                scriptApi.Initialize(m_Engine, m_Part, m_LocalID, m_ItemID);
                beAPI.InitApi(api, scriptApi);
            }

            /*
             * Declare which events the script's current state can handle.
             */
            m_Part.SetScriptEvents(m_ItemID, GetStateEventFlags(stateCode));
        }

        // Get script DLL loaded in memory and all ready to run,
        // ready to resume it from where the .state file says it was last
        private void InstantiateScript()
        {
            lock (m_CompileLock)
            {
                bool compileFailed = false;
                bool compiledIt = false;
                ScriptObjCode objCode;

                /*
                 * There may already be an ScriptObjCode struct in memory that
                 * we can use.  If not, try to compile it.
                 */
                if (!m_CompiledScriptObjCode.TryGetValue (m_AssetID, 
                                                          out objCode))
                {
                    try {
                        objCode = TryToCompile(false);
                        compiledIt = true;
                    } catch (Exception) {
                        compileFailed = true;
                    }
                }

                /*
                 * Get a new instance of that script object code loaded in
                 * memory and try to fill in its initial state from the saved
                 * state file.
                 */
                if (!compileFailed && TryToLoad(objCode))
                {
                    m_log.DebugFormat("[XMREngine]: load successful {0}",
                            m_DescName);
                } else if (compiledIt) {
                    throw new Exception("script load failed");
                } else {

                    /*
                     * If it didn't load, maybe it's because of a version
                     * mismatch somewhere.  So try recompiling and reload.
                     */
                    m_log.DebugFormat("[XMREngine]: attempting recompile {0}",
                            m_DescName);
                    objCode = TryToCompile(true);
                    compiledIt = true;
                    m_log.DebugFormat("[XMREngine]: attempting reload {0}",
                            m_DescName);
                    if (!TryToLoad(objCode))
                    {
                        throw new Exception("script reload failed");
                    }
                    m_log.DebugFormat("[XMREngine]: reload successful {0}",
                            m_DescName);
                }

                /*
                 * (Re)loaded successfully, increment reference count.
                 *
                 * If we just compiled it though, reset count to 0 first as
                 * this is the one-and-only existance of this objCode struct,
                 * and we want any old ones for this assetID to be garbage
                 * collected.
                 */
                if (compiledIt) {
                    m_CompiledScriptObjCode[m_AssetID]  = objCode;
                    m_CompiledScriptRefCount[m_AssetID] = 0;
                }
                m_ObjCode = objCode;
                m_CompiledScriptRefCount[m_AssetID] ++;
            }
        }

        // Try to create object code from source code
        // If error, just throw exception
        private ScriptObjCode TryToCompile(bool forceCompile)
        {
            string objName = ScriptCompile.GetObjFileName(m_AssetID.ToString(),
                                                          m_ScriptBasePath);

            m_CompilerErrors.Clear();

            /*
             * If told to force compilation (presumably because object file 
             * is old version or corrupt), delete the object file which will 
             * make ScriptCompile.Compile() create a new one from the source.
             */
            if (forceCompile) {
                File.Delete(objName);
            }

            /*
             * If we have neither the source nor the object file, not much we
             * can do to create the ScriptObjCode object.
             */
            if ((m_SourceCode == String.Empty) && !File.Exists(objName))
            {
                throw new Exception("Compile of asset " +
                                    m_AssetID.ToString() +
                                    " was requested but source text is not " +
                                    "present and no assembly was found");
            }

            /*
             * If object file exists, create ScriptObjCode directly from that.
             * Otherwise, compile the source to create object file then create
             * ScriptObjCode from that.
             */
            ScriptObjCode objCode = ScriptCompile.Compile(m_SourceCode, 
                                                          m_DescName,
                                                          m_AssetID.ToString(), 
                                                          m_ScriptBasePath, 
                                                          ErrorHandler);
            if (m_CompilerErrors.Count != 0)
            {
                throw new Exception ("compilation errors");
            }
            if (objCode == null)
            {
                throw new Exception ("compilation failed");
            }

            return objCode;
        }

        //  TryToLoad()
        //      create script instance
        //      if no state XML file exists for the asset,
        //          post initial default state events
        //      else
        //          try to restore from .state file
        //          if unable, delete .state file and retry
        //
        private bool TryToLoad(ScriptObjCode objCode)
        {
            // Set up script state in a "never-ever-has-run-before" state.
            LoadObjCode(objCode);

            // If no .state file exists, start from default state
            string envar = Environment.GetEnvironmentVariable("XMREngineIgnoreState");
            if ((envar != null) && ((envar[0] & 1) != 0)) {
                File.Delete(m_StateFileName);
            }
            if (!File.Exists(m_StateFileName))
            {
                m_Running = true;  // event processing is enabled

                // default state_entry() must initialize global variables
                doGblInit = true;
                stateCode = 0;
                PostEvent(new EventParams("state_entry", 
                                          zeroObjectArray,
                                          zeroDetectParams));

                if (m_PostOnRez)
                {
                    PostEvent(new EventParams("on_rez",
                            new Object[] { m_StartParam }, 
                            zeroDetectParams));
                }

                if (m_StateSource == StateSource.AttachedRez)
                {
                    PostEvent(new EventParams("attach",
                            new object[] { m_Part.AttachedAvatar.ToString() }, 
                            zeroDetectParams));
                }
                else if (m_StateSource == StateSource.NewRez)
                {
                    PostEvent(new EventParams("changed",
                            new Object[] { 256 }, 
                            zeroDetectParams));
                }
                else if (m_StateSource == StateSource.PrimCrossing)
                {
                    PostEvent(new EventParams("changed",
                            new Object[] { 512 }, 
                            zeroDetectParams));
                }

                return true;
            }

            // Got a .state file, try to read .state file into script instance
            try
            {
                FileStream fs = File.Open(m_StateFileName, 
                                          FileMode.Open, 
                                          FileAccess.Read);
                StreamReader ss = new StreamReader(fs);
                string xml = ss.ReadToEnd();
                ss.Close();
                fs.Close();

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                LoadScriptState(doc);
                return true;
            }
            catch (Exception e)
            {
                m_log.Error("[XMREngine]: error restoring " +
                        m_DescName + ": " + e.Message);

                // Failed to load state, delete bad .state file and reload
                // instance so we get a script at default state.
                m_log.Info("[XMREngine]: attempting reset " + m_DescName);
                File.Delete(m_StateFileName);
                return TryToLoad(objCode);
            }
        }

        /*
         * Fill in script object initial contents.
         * Set the initial state to "default".
         *
         * Caller should call StartEventHandler() or MigrateInEventHandler() next.
         * If calling StartEventHandler(), use ScriptEventCode.state_entry with no args.
         */
        private void LoadObjCode (ScriptObjCode objCode)
        {
            if (objCode  == null) throw new ArgumentNullException ("objCode");

            this.objCode      = objCode;

            this.gblArrays    = new XMR_Array[objCode.numGblArrays];
            this.gblFloats    = new float[objCode.numGblFloats];
            this.gblIntegers  = new int[objCode.numGblIntegers];
            this.gblLists     = new LSL_List[objCode.numGblLists];
            this.gblRotations = new LSL_Rotation[objCode.numGblRotations];
            this.gblStrings   = new string[objCode.numGblStrings];
            this.gblVectors   = new LSL_Vector[objCode.numGblVectors];

            /*
             * Script can handle these event codes.
             */
            m_HaveEventHandlers = new bool[objCode.scriptEventHandlerTable.GetLength(1)];
            for (int i = objCode.scriptEventHandlerTable.GetLength(0); -- i >= 0;) {
                for (int j = objCode.scriptEventHandlerTable.GetLength(1); -- j >= 0;) {
                    if (objCode.scriptEventHandlerTable[i,j] != null) {
                        m_HaveEventHandlers[j] = true;
                    }
                }
            }

            /*
             * Script must leave this much stack remaining on calls to CheckRun().
             */
            this.stackLimit = (uint)m_StackSize / 2;

            /*
             * This is how many total heap bytes script is allowed to use.
             * Start with some fixed amount then subtract off static global sizes.
             */
            this.heapLimit  = (int)(uint)m_StackSize / 2;
            this.heapLimit -= 16 * objCode.numGblArrays;
            this.heapLimit -=  4 * objCode.numGblFloats;
            this.heapLimit -=  4 * objCode.numGblIntegers;
            this.heapLimit -= 16 * objCode.numGblLists;
            this.heapLimit -= 16 * objCode.numGblRotations;
            this.heapLimit -= 16 * objCode.numGblStrings;
            this.heapLimit -= 12 * objCode.numGblVectors;

            /*
             * Set up sub-objects and cross-polinate so everything can access everything.
             */
            this.microthread  = new ScriptUThread (m_StackSize, m_DescName);
            this.continuation = new ScriptContinuation ();
            this.microthread.instance  = this;
            this.continuation.instance = this;

            /*
             * We do our own object serialization.
             * It avoids null pointer refs and is much more efficient because we
             * have a limited number of types to deal with.
             */
            this.continuation.sendObj = this.SendObjValue;
            this.continuation.recvObj = this.RecvObjValue;

            /*
             * Constant subsArray values...
             */
            this.subsArray[(int)SubsArray.SCRIPT] = this;

            /*
             * All the DLL filenames should be known at this point,
             * so fill in the entries needed.
             *
             * These have to be the exact string returned by mono_image_get_filename().
             * To find out which DLLs are needed, set envar MMRCONTSAVEDEBUG=1 and observe
             * debug output to see which DLLs are referenced.
             */
            this.dllsArray = new string[3];
            this.dllsArray[0] = MMRCont.GetDLLName (typeof (XMRInstance));  // ...XMREngine.dll
            this.dllsArray[1] = MMRCont.GetDLLName (typeof (MMRCont));      // ...Mono.Tasklets.dll
            this.dllsArray[2] = MMRCont.GetDLLName (typeof (LSL_Vector));   // ...ScriptEngine.Shared.dll
        }

        /**
         * @brief Save compilation error messages for later retrieval
         *        via GetScriptErrors().
         */
        private void ErrorHandler(Token token, string message)
        {
            if (token != null)
            {
                m_CompilerErrors.Add(
                        String.Format("({0},{1}) Error: {2}", token.line,
                                token.posn, message));
            }
            else if (message != null)
            {
                m_CompilerErrors.Add(
                        String.Format("(0,0) Error: {0}", message));
            }
            else
            {
                m_CompilerErrors.Add("Error compiling, see exception in log");
            }
        }

        /**
         * @brief Load script state from the given XML doc into the script memory
         *  <ScriptState Asset=...>
         *      <Running>...</Running>
         *      <DoGblInit>...</DoGblInit>
         *      <Permissions granted=... mask=... />
         *      RestoreDetectParams()
         *      <Plugins>
         *          ExtractXMLObjectArray("plugin")
         *      </Plugins>
         *      <Snapshot>
         *          MigrateInEventHandler()
         *      </Snapshot>
         *  </ScriptState>
         */
        private void LoadScriptState(XmlDocument doc)
        {
            DetectParams[] detParams;
            Queue<EventParams> eventQueue;

            // Everything we know is enclosed in <ScriptState>...</ScriptState>
            XmlElement scriptStateN = (XmlElement)doc.SelectSingleNode("ScriptState");
            if (scriptStateN == null)
            {
                throw new Exception("no <ScriptState> tag");
            }

            // AssetID is unique for the script source text so make sure the
            // state file was written for that source file
            string assetID = scriptStateN.GetAttribute("Asset");
            if (assetID != m_Item.AssetID.ToString())
            {
                throw new Exception("assetID mismatch");
            }

            // Get various attributes
            XmlElement runningN = (XmlElement)scriptStateN.SelectSingleNode("Running");
            m_Running = bool.Parse(runningN.InnerText);

            XmlElement doGblInitN = (XmlElement)scriptStateN.SelectSingleNode("DoGblInit");
            doGblInit = bool.Parse(doGblInitN.InnerText);

            XmlElement permissionsN = (XmlElement)scriptStateN.SelectSingleNode("Permissions");
            m_Item.PermsGranter = new UUID(permissionsN.GetAttribute("granter"));
            m_Item.PermsMask = Convert.ToInt32(permissionsN.GetAttribute("mask"));
            m_Part.Inventory.UpdateInventoryItem(m_Item);

            // get values used by stuff like llDetectedGrab, etc.
            detParams = RestoreDetectParams(scriptStateN.SelectSingleNode("DetectArray"));

            // Restore queued events
            eventQueue = RestoreEventQueue(scriptStateN.SelectSingleNode("EventQueue"));

            // Restore timers and listeners
            XmlElement pluginN = (XmlElement)scriptStateN.SelectSingleNode("Plugins");
            Object[] pluginData = ExtractXMLObjectArray(pluginN, "plugin");

            // Script's global variables and stack contents
            XmlElement snapshotN = 
                    (XmlElement)scriptStateN.SelectSingleNode("Snapshot");

            Byte[] data = Convert.FromBase64String(snapshotN.InnerText);
            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
            MigrateInEventHandler(ms);
            ms.Close();

            // Now that we can't throw an exception, do final updates
            AsyncCommandManager.CreateFromData(m_Engine,
                    m_LocalID, m_ItemID, m_Part.UUID,
                    pluginData);
            m_DetectParams = detParams;
            m_EventQueue   = eventQueue;
            for (int i = m_EventCounts.Length; -- i >= 0;) m_EventCounts[i] = 0;
            foreach (EventParams evt in m_EventQueue)
            {
                ScriptEventCode eventCode = (ScriptEventCode)Enum.Parse (typeof (ScriptEventCode),
                                                                         evt.EventName);
                m_EventCounts[(int)eventCode] ++;
            }

            // See if we are supposed to send an 'on_rez' event
            if (m_PostOnRez)
            {
                PostEvent(new EventParams("on_rez",
                        new Object[] { m_StartParam }, zeroDetectParams));
            }

            // Maybe an 'attach' event too
            if (m_StateSource == StateSource.AttachedRez)
            {
                PostEvent(new EventParams("attach",
                        new object[] { m_Part.AttachedAvatar.ToString() }, 
                        zeroDetectParams));
            }
        }

        /**
         * @brief Read llDetectedGrab, etc, values from XML
         *  <EventQueue>
         *      <DetectParams>...</DetectParams>
         *          .
         *          .
         *          .
         *  </EventQueue>
         */
        private Queue<EventParams> RestoreEventQueue(XmlNode eventsN)
        {
            Queue<EventParams> eventQueue = new Queue<EventParams>();
            if (eventsN != null) {
                XmlNodeList eventL = eventsN.SelectNodes("Event");
                foreach (XmlNode evnt in eventL)
                {
                    string name            = ((XmlElement)evnt).GetAttribute("Name");
                    object[] parms         = ExtractXMLObjectArray(evnt, "param");
                    DetectParams[] detects = RestoreDetectParams(evnt);

                    if (parms   == null) parms   = zeroObjectArray;
                    if (detects == null) detects = zeroDetectParams;

                    EventParams evt = new EventParams(name, parms, detects);
                    eventQueue.Enqueue(evt);
                }
            }
            return eventQueue;
        }

        /**
         * @brief Read llDetectedGrab, etc, values from XML
         *  <DetectArray>
         *      <DetectParams>...</DetectParams>
         *          .
         *          .
         *          .
         *  </DetectArray>
         */
        private DetectParams[] RestoreDetectParams(XmlNode detectedN)
        {
            if (detectedN == null)
            {
                return null;
            }

            List<DetectParams> detected = new List<DetectParams>();

            XmlNodeList detectL = detectedN.SelectNodes("DetectParams");
            foreach (XmlNode det in detectL)
            {
                string vect =
                        det.Attributes.GetNamedItem(
                        "pos").Value;
                LSL_Types.Vector3 v =
                        new LSL_Types.Vector3(vect);

                int d_linkNum=0;
                UUID d_group = UUID.Zero;
                string d_name = String.Empty;
                UUID d_owner = UUID.Zero;
                LSL_Types.Vector3 d_position =
                    new LSL_Types.Vector3();
                LSL_Types.Quaternion d_rotation =
                    new LSL_Types.Quaternion();
                int d_type = 0;
                LSL_Types.Vector3 d_velocity =
                    new LSL_Types.Vector3();

                string tmp;

                tmp = det.Attributes.GetNamedItem("linkNum").Value;
                int.TryParse(tmp, out d_linkNum);

                tmp = det.Attributes.GetNamedItem("group").Value;
                UUID.TryParse(tmp, out d_group);

                d_name = det.Attributes.GetNamedItem("name").Value;

                tmp = det.Attributes.GetNamedItem("owner").Value;
                UUID.TryParse(tmp, out d_owner);

                tmp = det.Attributes.GetNamedItem("position").Value;
                d_position = new LSL_Types.Vector3(tmp);

                tmp = det.Attributes.GetNamedItem("rotation").Value;
                d_rotation = new LSL_Types.Quaternion(tmp);

                tmp = det.Attributes.GetNamedItem("type").Value;
                int.TryParse(tmp, out d_type);

                tmp = det.Attributes.GetNamedItem("velocity").Value;
                d_velocity = new LSL_Types.Vector3(tmp);

                UUID uuid = new UUID();
                UUID.TryParse(det.InnerText, out uuid);

                DetectParams d = new DetectParams();
                d.Key = uuid;
                d.OffsetPos = v;
                d.LinkNum = d_linkNum;
                d.Group = d_group;
                d.Name = d_name;
                d.Owner = d_owner;
                d.Position = d_position;
                d.Rotation = d_rotation;
                d.Type = d_type;
                d.Velocity = d_velocity;

                detected.Add(d);
            }
            return detected.ToArray();
        }

        /**
         * @brief Extract elements of an array of objects from an XML parent.
         *        Each element is of form <tag ...>...</tag>
         * @param parent = XML parent to extract them from
         * @param tag = what the value's tag is
         * @returns object array of the values
         */
        private static object[] ExtractXMLObjectArray(XmlNode parent, string tag)
        {
            List<Object> olist = new List<Object>();

            XmlNodeList itemL = parent.SelectNodes(tag);
            foreach (XmlNode item in itemL)
            {
                olist.Add(ExtractXMLObjectValue(item));
            }

            return olist.ToArray();
        }

        private static object ExtractXMLObjectValue(XmlNode item)
        {
            string itemType = item.Attributes.GetNamedItem("type").Value;

            if (itemType == "list")
            {
                return new LSL_List(ExtractXMLObjectArray(item, "item"));
            }

            if (itemType == "OpenMetaverse.UUID")
            {
                UUID val = new UUID();
                UUID.TryParse(item.InnerText, out val);
                return val;
            }

            Type itemT = Type.GetType(itemType);
            if (itemT == null)
            {
                Object[] args = new Object[] { item.InnerText };

                string assembly = itemType + ", OpenSim.Region.ScriptEngine.Shared";
                itemT = Type.GetType(assembly);
                if (itemT == null)
                {
                    return null;
                }
                return Activator.CreateInstance(itemT, args);
            }

            return Convert.ChangeType(item.InnerText, itemT);
        }

        /*
         * Migrate an event handler in from a stream.
         *
         * Input:
         *  stream = as generated by MigrateOutEventHandler()
         *  this.beAPI = 'this' pointer passed to things like llSay()
         */
        private void MigrateInEventHandler (Stream stream)
        {
            /*
             * Set up to migrate state in from the network stream.
             */
            this.migrateInReader = new BinaryReader (stream);
            this.migrateInStream = stream;

            /*
             * Read current state code and event code from stream.
             * And it also marks us busy (by setting this.eventCode) so we can't be
             * started again and this event lost.
             */
            int mv = stream.ReadByte ();
            if (mv != migrationVersion) {
                throw new Exception ("incoming migration version " + mv + " but accept only " + migrationVersion);
            }
            this.stateCode = (int)RecvObjValue (stream);
            this.eventCode = (ScriptEventCode)RecvObjValue (stream);
            this.heapLeft  = this.heapLimit - (int)RecvObjValue (stream);
            this.ehArgs    = (object[])RecvObjValue (stream);

            /*
             * Read script globals in.
             */
            this.MigrateGlobalsIn (stream);

            /*
             * If eventCode is None, it means the script was idle when migrated.
             * So we don't have to read script's stack in.
             */
            if (this.eventCode != ScriptEventCode.None) {

                /*
                 * We have to be running on the microthread stack to restore to it.
                 * So microthread.Start() calls XMRScriptUThread.Main() which reads
                 * the stack from the stream and returns immediately because it
                 * suspends the microthread as soon as the restore is complete.
                 */
                this.migrateComplete = false;
                microthread.Start ();
                if (!this.migrateComplete) throw new Exception ("migrate in did not complete");
            }

            /*
             * Clear out migration state.
             */
            this.migrateInReader = null;
            this.migrateInStream = null;
        }

        /**
         * @brief Read script global variables from the input stream.
         */
        private void MigrateGlobalsIn (System.IO.Stream stream)
        {
            this.gblArrays    = (XMR_Array[])   RecvObjArray (stream, typeof (XMR_Array));
            this.gblFloats    = (float[])       RecvObjArray (stream, typeof (float));
            this.gblIntegers  = (int[])         RecvObjArray (stream, typeof (int));
            this.gblLists     = (LSL_List[])    RecvObjArray (stream, typeof (LSL_List));
            this.gblRotations = (LSL_Rotation[])RecvObjArray (stream, typeof (LSL_Rotation));
            this.gblStrings   = (string[])      RecvObjArray (stream, typeof (string));
            this.gblVectors   = (LSL_Vector[])  RecvObjArray (stream, typeof (LSL_Vector));
        }

        /**
         * @brief Read an array of values from the stream.
         * @param stream = where to read them from
         * @param eleType = type of each element
         * @returns array of the elements
         */
        private Array RecvObjArray (System.IO.Stream stream, Type eleType)
        {
            int length = (int)RecvObjValue (stream);
            Array array = Array.CreateInstance (eleType, length);
            for (int i = 0; i < length; i ++) {
                array.SetValue (RecvObjValue (stream), i);
            }
            return array;
        }

        /**
         * @brief Read a single value from the stream.
         * @param stream = stream to read the value from
         * @returns value (boxed as needed)
         */
        private object RecvObjValue (Stream stream)
        {
            Ser code = (Ser)this.migrateInReader.ReadByte ();
            switch (code) {
                case Ser.NULL: {
                    return null;
                }
                case Ser.EVENTCODE: {
                    return (ScriptEventCode)this.migrateInReader.ReadInt32 ();
                }
                case Ser.LSLFLOAT: {
                    return new LSL_Float (this.migrateInReader.ReadSingle ());
                }
                case Ser.LSLINT: {
                    return new LSL_Integer (this.migrateInReader.ReadInt32 ());
                }
                case Ser.LSLKEY: {
                    return new LSL_Key ((string)RecvObjValue (stream));
                }
                case Ser.LSLLIST: {
                    object[] array = (object[])RecvObjValue (stream);
                    return new LSL_List (array);
                }
                case Ser.LSLROT: {
                    double x = this.migrateInReader.ReadDouble ();
                    double y = this.migrateInReader.ReadDouble ();
                    double z = this.migrateInReader.ReadDouble ();
                    double s = this.migrateInReader.ReadDouble ();
                    return new LSL_Rotation (x, y, z, s);
                }
                case Ser.LSLSTR: {
                    return new LSL_String ((string)RecvObjValue (stream));
                }
                case Ser.LSLVEC: {
                    double x = this.migrateInReader.ReadDouble ();
                    double y = this.migrateInReader.ReadDouble ();
                    double z = this.migrateInReader.ReadDouble ();
                    return new LSL_Vector (x, y, z);
                }
                case Ser.OBJARRAY: {
                    int len = this.migrateInReader.ReadInt32 ();
                    object[] array = new object[len];
                    for (int i = 0; i < len; i ++) {
                        array[i] = RecvObjValue (stream);
                    }
                    return array;
                }
                case Ser.SYSDOUB: {
                    return this.migrateInReader.ReadDouble ();
                }
                case Ser.SYSFLOAT: {
                    return this.migrateInReader.ReadSingle ();
                }
                case Ser.SYSINT: {
                    return this.migrateInReader.ReadInt32 ();
                }
                case Ser.SYSSTR: {
                    return this.migrateInReader.ReadString ();
                }
                case Ser.XMRARRAY: {
                    XMR_Array array = new XMR_Array ();
                    array.RecvArrayObj (this.RecvObjValue, stream);
                    return array;
                }
                default: throw new Exception ("bad stream code " + code.ToString ());
            }
        }
    }
}