/***************************************************\
 *  COPYRIGHT 2009, Mike Rieker, Beverly, MA, USA  *
 *  All rights reserved.                           *
\***************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OpenSim.Region.ScriptEngine.XMREngine {

	public class InternalFuncDict : VarDict {

		/**
		 * @brief build dictionary of internal functions from an interface.
		 * @param iface = interface with function definitions
		 * @param inclSig = true: catalog by name with arg sig, eg, llSay(integer,string)
		 *                 false: catalog by simple name only, eg, state_entry
		 * @returns dictionary of function definition tokens
		 */
		public InternalFuncDict (Type iface, bool inclSig)
			: base (false)
		{
			/*
			 * Loop through list of all methods declared in the interface.
			 */
			System.Reflection.MethodInfo[] ifaceMethods = iface.GetMethods ();
			foreach (System.Reflection.MethodInfo ifaceMethod in ifaceMethods) {
				string key = ifaceMethod.Name;

				/*
				 * Only do ones that begin with lower-case letters...
				 * as any others can't be referenced by scripts
				 */
				if ((key[0] < 'a') || (key[0] > 'z')) continue;

				try {

					/*
					 * Create a corresponding TokenDeclVar struct.
					 */
					System.Reflection.ParameterInfo[] parameters = ifaceMethod.GetParameters ();
					TokenArgDecl argDecl = new TokenArgDecl (null);
					for (int i = 0; i < parameters.Length; i++) {
						System.Reflection.ParameterInfo param = parameters[i];
						TokenType type = TokenType.FromSysType (null, param.ParameterType);
						TokenName name = new TokenName (null, param.Name);
						argDecl.AddArg (type, name);
					}
					TokenDeclVar declFunc = new TokenDeclVar (null, null, null);
					declFunc.name         = new TokenName (null, key);
					declFunc.retType      = TokenType.FromSysType (null, ifaceMethod.ReturnType);
					declFunc.argDecl      = argDecl;

					/*
					 * Add the TokenDeclVar struct to the dictionary.
					 */
					this.AddEntry (declFunc);
				} catch (Exception except) {

					string msg = except.ToString ();
					int i = msg.IndexOf ("\n");
					if (i > 0) msg = msg.Substring (0, i);
					Console.WriteLine ("InternalFuncDict*: {0}:     {1}", key, msg);

					///??? IGNORE ANY THAT FAIL - LIKE UNRECOGNIZED TYPE ???///
				}
			}
		}
	}
}