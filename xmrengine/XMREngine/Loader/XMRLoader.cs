//////////////////////////////////////////////////////////////
//
// Copyright (c) 2009 Careminster Limited and Melanie Thielker
//
// All rights reserved
//

using System;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using OpenSim.Region.ScriptEngine.Shared.Api.Runtime;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using MMR;

namespace OpenSim.Region.ScriptEngine.XMREngine.Loader
{
    [Flags]
    public enum scriptEvents : int
    {
        None = 0,
        attach = 1,
        collision = 16,
        collision_end = 32,
        collision_start = 64,
        control = 128,
        dataserver = 256,
        email = 512,
        http_response = 1024,
        land_collision = 2048,
        land_collision_end = 4096,
        land_collision_start = 8192,
        at_target = 16384,
        listen = 32768,
        money = 65536,
        moving_end = 131072,
        moving_start = 262144,
        not_at_rot_target = 524288,
        not_at_target = 1048576,
        remote_data = 8388608,
        run_time_permissions = 268435456,
        state_entry = 1073741824,
        state_exit = 2,
        timer = 4,
        touch = 8,
        touch_end = 536870912,
        touch_start = 2097152,
        object_rez = 4194304
    }

    public class XMRLoader : MarshalByRefObject, IDisposable, ISponsor
    {
        private ScriptBaseClass m_ScriptBase;
        private int m_Renew = 10;
        private ScriptWrapper m_Wrapper = null;
        private string m_DllName;

        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromSeconds(10);
                lease.RenewOnCallTime = TimeSpan.FromSeconds(10);
                lease.SponsorshipTimeout = TimeSpan.FromSeconds(20);

                lease.Register(this);
            }

            return lease;
        }

        public XMRLoader()
        {
            m_ScriptBase = new ScriptBaseClass();
        }

        public void Dispose()
        {
            if (m_Wrapper != null)
                m_Wrapper.Dispose();
            m_Wrapper = null;

            ILease lease = (ILease)GetLifetimeService();

            lease.Unregister(this);
            m_Renew = 0;
        }

        public TimeSpan Renewal(ILease lease)
        {
            if (m_Renew == 0)
                lease.Unregister(this);
            return TimeSpan.FromSeconds(m_Renew);
        }

        public void InitApi(string name, IScriptApi api)
        {
            m_ScriptBase.InitApi(name, api);
        }

        public bool Load(string dllName)
        {
            m_DllName = dllName;

            try
            {
                m_Wrapper = ScriptWrapper.CreateScriptInstance(dllName);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("[XMREngine]: Error loading script\n" + e.ToString());
                return false;
            }

            if (m_Wrapper == null)
                return false;

            m_Wrapper.beAPI = m_ScriptBase;
            m_Wrapper.alwaysSuspend = true;

            return true;
        }

        public void PostEvent(string eventName, Object[] args)
        {
            m_Wrapper.StartEventHandler(eventName, args);
        }

        public bool RunOne()
        {
            return m_Wrapper.ResumeEventHandler();
        }

        public void Reset()
        {
            m_Wrapper.Dispose();
            m_Wrapper = null;

            Load(m_DllName);
        }

        public int StateCode
        {
            get { return m_Wrapper.stateCode; }
        }

        public int GetStateEventFlags(int stateCode)
        {
            if (stateCode < 0 || stateCode >= m_Wrapper.scriptEventHandlerTable.GetLength(0))
                return 0;

            int code = 0;

            ScriptEventHandler h;

            for (int i = 0 ; i < m_Wrapper.scriptEventHandlerTable.GetLength(1) ; i++ )
            {
                h = m_Wrapper.scriptEventHandlerTable[stateCode, i];

                if (h != null)
                {
                    ScriptEventCode c = (ScriptEventCode)i;
                    string eventName = c.ToString();

                    try
                    {
                        scriptEvents scriptEventFlag = (scriptEvents)Enum.Parse(typeof(scriptEvents), eventName);
                        code |= (int)scriptEventFlag;
                    }
                    catch
                    {
                    }
                }
            }

            return code;
        }

        public Byte[] GetSnapshot()
        {
            MemoryStream ms = new MemoryStream();

            bool suspended = m_Wrapper.MigrateOutEventHandler(ms);

            Byte[] data = ms.ToArray();

            ms.Close();

            Array.Resize(ref data, data.Length+1);
            if (suspended)
                data[data.Length-1] = 1;
            else
                data[data.Length-1] = 0;

            return data;
        }

        public bool RestoreSnapshot(Byte[] data)
        {
            bool suspended = data[data.Length-1] == 1 ? true : false;

            MemoryStream ms = new MemoryStream();

            ms.Write(data, 0, data.Length-1);

            m_Wrapper.MigrateInEventHandler(ms);

            ms.Close();

            return suspended;
        }
    }
}