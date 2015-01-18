
using UnityEngine;

namespace UbioWeldingLtd
{
	public static class Debugger
	{
		/// <summary>
		/// provides additional log information in case it is wanted
		/// </summary>
		/// <param name="debugText"></param>
		/// <param name="advancedDebuging"></param>
		public static void AdvDebug(string debugText, bool advancedDebuging)
		{
			if (advancedDebuging)
			{
				Debug.Log(string.Format("{0} {1}", Constants.logPrefix, debugText));
			}
		}

	}
}
