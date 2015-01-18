using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UbioWeldingLtd
{
	public class ModuleMerger
	{

		/// <summary>
		/// merges the modules in a almost generic way.
		/// </summary>
		/// <param name="partname"></param>
		/// <param name="configuration"></param>
		public void mergeModules(string partname, UrlDir.UrlConfig configuration, List<ConfigNode> modulelist, bool advancedDebugging)
		{
			ConfigNode[] originalModules = configuration.config.GetNodes(Constants.weldModuleNode);
			string newModuleName = "";
			bool exist = false;

			ConfigNode newModule;

			foreach (ConfigNode originalModule in originalModules)
			{
				newModule = originalModule.CreateCopy();
				newModuleName = newModule.GetValue(newModule.values.DistinctNames()[0]);
				exist = false;

				foreach (ConfigNode existingNewModule in modulelist)
				{
					if (newModuleName.Equals(existingNewModule.GetValue(existingNewModule.values.DistinctNames()[0])))
					{
						if (!WeldingHelpers.isArrayContaing(newModuleName, UbioZurWeldingLtd.instance.config.modulesToMultiply))
						{
							Debugger.AdvDebug(string.Format("| {0} Module already exists!!!", existingNewModule.GetValue(existingNewModule.values.DistinctNames()[0])), advancedDebugging);
							if (newModule.values.DistinctNames().Length < 2)
							{
								// making shure that the MODULE gets not duplicated in case it has no attributes
								exist = true;
								break;
							}
							else
							{
								string[] breakingAttributes = new string[newModule.values.DistinctNames().Count()];
								for (int i = 0; i < newModule.values.DistinctNames().Count(); i++)
								{
									breakingAttributes[i] = string.Concat(newModuleName, Constants.underline, newModule.values.DistinctNames()[i]);
								}

								breakingAttributes = WeldingHelpers.getSharedArrayValues(breakingAttributes, UbioZurWeldingLtd.instance.config.breakingModuleAttributes);
								Debugger.AdvDebug(string.Format("| BreakingAttributes found = {0} ", breakingAttributes.Length), advancedDebugging);

								if (breakingAttributes.Length > 0)
								{
									foreach (string s in breakingAttributes)
									{
										string breakingAttribute = s.Replace(string.Concat(newModuleName, Constants.underline), "");
										var existingValue = existingNewModule.GetValue(breakingAttribute);
										var newValue = newModule.GetValue(breakingAttribute);
										Debugger.AdvDebug(string.Format("| BreakingAttributes found | current one is {0} | ExistingValue = {1} - NewValue = {2}", breakingAttribute, existingValue, newValue), advancedDebugging);
										exist = Equals(existingValue, newValue);
										if (!exist)
										{
											break;
										}
									}
									if (exist)
									{
										mergeModuleAttributes(newModuleName, newModule, existingNewModule, advancedDebugging);
										mergeSubModules(newModule, existingNewModule, advancedDebugging);
										mergeVector2Modules(newModule, existingNewModule);
										mergeVector4Modules(newModule, existingNewModule);
										exist = true;
										break;
									}
								}
								else
								{
									mergeModuleAttributes(newModuleName, newModule, existingNewModule, advancedDebugging);
									mergeSubModules(newModule, existingNewModule, advancedDebugging);
									mergeVector2Modules(newModule, existingNewModule);
									mergeVector4Modules(newModule, existingNewModule);
									exist = true;
									break;
								}
							}
						}
					}
					Debugger.AdvDebug(string.Format("| Module ready to add = {0}", !exist), advancedDebugging);
				}//foreach (ConfigNode existingNewModule in _modulelist)
				if (!exist)
				{
					if (!WeldingHelpers.isArrayContaing(newModuleName, UbioZurWeldingLtd.instance.config.modulesToIgnore))
					{
						addNewModule(partname, newModuleName, newModule, modulelist, advancedDebugging);
					}
				} //if (!exist)
			} //foreach (ConfigNode mod in modules)
		}


		/// <summary>
		/// merges any module that is of the Vector4 kind
		/// </summary>
		/// <param name="newModule"></param>
		/// <param name="existingNewModule"></param>
		private void mergeVector4Modules(ConfigNode newModule, ConfigNode existingNewModule)
		{
			//Debug.Log(string.Format("{0}| Merging Vector4Modules Start", Constants.logPrefix));
			foreach (string subModule in UbioZurWeldingLtd.instance.config.vector4CurveModules)
			{
				if (newModule.HasNode(subModule))
				{
					if (existingNewModule.HasNode(subModule))
					{
						string[] curve = newModule.GetNode(subModule).GetValues(Constants.curveKey);
						string[] cfgcurve = existingNewModule.GetNode(subModule).GetValues(Constants.curveKey);
						Vector4[] cfgcurvevect = MergeVelCurve(curve, cfgcurve);
						existingNewModule.GetNode(subModule).RemoveValues(Constants.curveKey);
						foreach (Vector4 vec in cfgcurvevect)
						{
							existingNewModule.GetNode(subModule).AddValue(Constants.curveKey, ConfigNode.WriteVector(vec));
						}
					}
					else
					{
						existingNewModule.AddNode(newModule.GetNode(subModule));
					}
				}
			}
			//Debug.LogError(string.Format("{0}| Merging Vector4Modules End", Constants.logPrefix));
		}


		/// <summary>
		/// merges the parts of a module that are an vector2
		/// </summary>
		/// <param name="newModule"></param>
		/// <param name="existingNewModule"></param>
		private void mergeVector2Modules(ConfigNode newModule, ConfigNode existingNewModule)
		{
			//Debug.LogError(string.Format("{0}| Merging Vector2Modules Start", Constants.logPrefix));
			foreach (string subModule in UbioZurWeldingLtd.instance.config.vector2CurveModules)
			{
				if (newModule.HasNode(subModule))
				{
					if (existingNewModule.HasNode(subModule))
					{
						string[] curve = newModule.GetNode(subModule).GetValues(Constants.curveKey);
						string[] cfgcurve = existingNewModule.GetNode(subModule).GetValues(Constants.curveKey);
						Vector2[] cfgcurvevect = MergeAtmCurve(curve, cfgcurve);
						existingNewModule.GetNode(subModule).RemoveValues(Constants.curveKey);
						foreach (Vector2 vec in cfgcurvevect)
						{
							existingNewModule.GetNode(subModule).AddValue(Constants.curveKey, ConfigNode.WriteVector(vec));
						}
					}
					else
					{
						existingNewModule.AddNode(newModule.GetNode(subModule));
					}
				}
			}
			//Debug.LogError(string.Format("{0}| Merging Vector2Modules End", Constants.logPrefix));
		}



		/// <summary>
		/// manages the merging of submodules inside the module
		/// </summary>
		/// <param name="newModule"></param>
		/// <param name="existingNewModule"></param>
		private void mergeSubModules(ConfigNode newModule, ConfigNode existingNewModule, bool advancedDebugging)
		{
			//Debug.LogError(string.Format("{0}| Merging SubModules Start", Constants.logPrefix));
			foreach (string subModule in UbioZurWeldingLtd.instance.config.subModules)
			{
				if (newModule.HasNode(subModule) || existingNewModule.HasNode(subModule))
				{
					if (existingNewModule.HasNode(subModule) && newModule.HasNode(subModule))
					{
						Debugger.AdvDebug("| SubModules in both Modules found", advancedDebugging);
						ConfigNode existingNewSubModule = existingNewModule.GetNode(subModule);
						ConfigNode newSubModule = newModule.GetNode(subModule);
						string newSubmoduleName = existingNewSubModule.name;
						mergeModuleAttributes(newSubmoduleName, newSubModule, existingNewSubModule, advancedDebugging);
					}
					else if (!existingNewModule.HasNode(subModule) && newModule.HasNode(subModule))
					{
						Debugger.AdvDebug("| SubModules in one Modules found", advancedDebugging);
						existingNewModule.AddNode(subModule);
						ConfigNode newExistingSubModule = existingNewModule.GetNode(subModule);
						ConfigNode newSubModule = newModule.GetNode(subModule);
						foreach (string valueName in newSubModule.values.DistinctNames())
						{
							newExistingSubModule.AddValue(valueName, newSubModule.GetValue(valueName));
							Debugger.AdvDebug(string.Format("| {0} = {1}", valueName, newExistingSubModule.GetValue(valueName)), advancedDebugging);
						}
					}
				}
			}
			//Debug.LogError(string.Format("{0}| Merging SubModules End", Constants.logPrefix));
		}


		/// <summary>
		/// managed the merging of the attributes of a module
		/// </summary>
		/// <param name="newModuleName"></param>
		/// <param name="exist"></param>
		/// <param name="boolResult"></param>
		/// <param name="floatResult"></param>
		/// <param name="newModule"></param>
		/// <param name="existingNewModule"></param>
		private void mergeModuleAttributes(string newModuleName, ConfigNode newModule, ConfigNode existingNewModule, bool advancedDebugging)
		{
			bool boolResult;
			float floatResult;

			List<string> moduleNames = newModule.values.DistinctNames().ToList<string>();
			foreach (string name in existingNewModule.values.DistinctNames())
			{
				if (!moduleNames.Contains(name))
				{
					moduleNames.Add(name);
				}
			}

			foreach (string ModuleAttribute in moduleNames)
			{
				boolResult = false;
				floatResult = 0f;
				if (bool.TryParse(existingNewModule.GetValue(ModuleAttribute), out boolResult) || bool.TryParse(newModule.GetValue(ModuleAttribute), out boolResult))
				{
					Debugger.AdvDebug(string.Format("| {0} - {1} is bool", newModuleName, ModuleAttribute), advancedDebugging);
					mergeModuleBoolValues(newModuleName, newModule, existingNewModule, ModuleAttribute, advancedDebugging);
				}
				else
				{
					if (float.TryParse(existingNewModule.GetValue(ModuleAttribute), out floatResult) || float.TryParse(newModule.GetValue(ModuleAttribute), out floatResult))
					{
						Debugger.AdvDebug(string.Format("| {0} - {1} is float", newModuleName, ModuleAttribute), advancedDebugging);
						mergeModuleFloatValues(newModuleName, newModule, existingNewModule, ModuleAttribute, advancedDebugging);
					}
					else
					{
						Debugger.AdvDebug(string.Format("| {0} - {1} is string", newModuleName, ModuleAttribute), advancedDebugging);
						mergeModuleStringValues(newModuleName, newModule, existingNewModule, ModuleAttribute, advancedDebugging);
					}
				}
#if (DEBUG)
				//Debug.LogError(string.Format("{0}- Alex Modulemerger - {1} | {2} = {3}", Constants.logPrefix, existingNewModule.GetValue(existingNewModule.values.DistinctNames()[0]), ModuleAttribute, existingNewModule.GetValue(ModuleAttribute)));
#endif
			}
		}


		/// <summary>
		/// handles the correct merging of bool values in the modules
		/// </summary>
		/// <param name="newModuleName"></param>
		/// <param name="newModule"></param>
		/// <param name="existingNewModule"></param>
		/// <param name="ModuleAttribute"></param>
		private void mergeModuleBoolValues(string newModuleName, ConfigNode newModule, ConfigNode existingNewModule, string ModuleAttribute, bool advancedDebugging)
		{
#if (DEBUG)
			//Debug.LogWarning(string.Format("{0}| {1} - {2} is bool", Constants.logPrefix, newModuleName, ModuleAttribute));
#endif
			if (newModule.HasValue(ModuleAttribute))
			{
				if (existingNewModule.HasValue(ModuleAttribute))
				{
					existingNewModule.SetValue(ModuleAttribute, (bool.Parse(newModule.GetValue(ModuleAttribute)) || bool.Parse(existingNewModule.GetValue(ModuleAttribute))).ToString());
				}
				else
				{
					existingNewModule.AddValue(ModuleAttribute, bool.Parse(newModule.GetValue(ModuleAttribute)).ToString());
				}
			}
			Debugger.AdvDebug(string.Format("| {0} - {1} is merged with value {2}", newModuleName, ModuleAttribute, bool.Parse(existingNewModule.GetValue(ModuleAttribute))), advancedDebugging);
		}


		/// <summary>
		/// handles the correct merging of float values in the modules
		/// </summary>
		/// <param name="newModuleName"></param>
		/// <param name="newModule"></param>
		/// <param name="existingNewModule"></param>
		/// <param name="ModuleAttribute"></param>
		private void mergeModuleFloatValues(string newModuleName, ConfigNode newModule, ConfigNode existingNewModule, string ModuleAttribute, bool advancedDebugging)
		{
#if (DEBUG)
			//Debug.LogWarning(string.Format("{0}| {1} - {2} is float", Constants.logPrefix, newModuleName, ModuleAttribute));
#endif
			//merge float values if they are allowed
			if (!WeldingHelpers.isArrayContaing(string.Concat(newModuleName, Constants.underline, ModuleAttribute), UbioZurWeldingLtd.instance.config.unchangedModuleAttributes))
			{
				if (newModule.HasValue(ModuleAttribute) || existingNewModule.HasValue(ModuleAttribute))
				{
					float newValue = float.Parse(newModule.GetValue(ModuleAttribute));
					Debugger.AdvDebug(string.Format("| {0} - newValue - {1} = {2}", newModuleName, ModuleAttribute, newValue), advancedDebugging);

					if (existingNewModule.HasValue(ModuleAttribute) && newModule.HasValue(ModuleAttribute))
					{
						float existingValue = float.Parse(existingNewModule.GetValue(ModuleAttribute));
						Debugger.AdvDebug(string.Format("| {0} - existingValue - {1} = {2}", newModuleName, ModuleAttribute, existingValue), advancedDebugging);

						if (WeldingHelpers.isArrayContaing(string.Concat(newModuleName, Constants.underline, ModuleAttribute), UbioZurWeldingLtd.instance.config.maximizedModuleAttributes))
						{
							Debugger.AdvDebug(string.Format("| {0} - {1} - maximized", newModuleName, ModuleAttribute), advancedDebugging);
							existingNewModule.SetValue(ModuleAttribute, (existingValue > newValue ? existingValue : newValue).ToString());
						}
						else
						{
							if (WeldingHelpers.isArrayContaing(string.Concat(newModuleName, Constants.underline, ModuleAttribute), UbioZurWeldingLtd.instance.config.averagedModuleAttributes) && (newValue != 0 && existingValue != 0))
							{
								Debugger.AdvDebug(string.Format("| {0} - {1} - averaged", newModuleName, ModuleAttribute), advancedDebugging);
								existingNewModule.SetValue(ModuleAttribute, ((newValue + existingValue) * 0.5f).ToString());
							}
							else
							{
								Debugger.AdvDebug(string.Format("| {0} - {1} - added", newModuleName, ModuleAttribute), advancedDebugging);
								existingNewModule.SetValue(ModuleAttribute, (newValue + existingValue).ToString());
							}
						}
					}
					else if (!existingNewModule.HasValue(ModuleAttribute) && newModule.HasValue(ModuleAttribute))
					{
						Debugger.AdvDebug(string.Format("| {0} - setNewValue - {1} = {2}", newModuleName, ModuleAttribute, newValue), advancedDebugging);
						existingNewModule.AddValue(ModuleAttribute, newValue.ToString());
					}
				}
			}
			Debugger.AdvDebug(string.Format("| {0} - {1} is merged with value {2}", newModuleName, ModuleAttribute, float.Parse(existingNewModule.GetValue(ModuleAttribute))), advancedDebugging);
		}


		/// <summary>
		/// handles the correct merging of string values in the modules
		/// </summary>
		/// <param name="newModuleName"></param>
		/// <param name="newModule"></param>
		/// <param name="existingNewModule"></param>
		/// <param name="ModuleAttribute"></param>
		private void mergeModuleStringValues(string newModuleName, ConfigNode newModule, ConfigNode existingNewModule, string ModuleAttribute, bool advancedDebugging)
		{
#if (DEBUG)
			//Debug.LogWarning(string.Format("{0}| {1} - {2} is string", Constants.logPrefix, newModuleName, ModuleAttribute));
#endif
			if (newModule.HasValue(ModuleAttribute))
			{
				if (existingNewModule.HasValue(ModuleAttribute))
				{
					existingNewModule.SetValue(ModuleAttribute, newModule.GetValue(ModuleAttribute));
				}
				else
				{
					existingNewModule.AddValue(ModuleAttribute, newModule.GetValue(ModuleAttribute));
				}
			}
			Debugger.AdvDebug(string.Format("| {0} - {1} is merged with value {2}", newModuleName, ModuleAttribute, existingNewModule.GetValue(ModuleAttribute)), advancedDebugging);
		}


		/// <summary>
		/// Merges values that are of the type Vector4 for curves
		/// </summary>
		/// <param name="set1"></param>
		/// <param name="set2"></param>
		/// <returns></returns>
		public Vector4[] MergeVelCurve(string[] set1, string[] set2)
		{
			Vector4[] curvevect = new Vector4[(set1.Length >= set2.Length) ? set1.Length : set2.Length];
			for (int i = 0; i < curvevect.Length; ++i)
			{
				curvevect[i] = ConfigNode.ParseVector4(set2[i]);
			}
			for (int i = 0; i < set1.Length; ++i)
			{
				Vector4 vect = ConfigNode.ParseVector4(set1[i]);
				int j = 0;
				while (vect.x != curvevect[j].x && j < curvevect.Length && j < set2.Length)
				{
					++j;
				}
				if (j >= curvevect.Length)
				{
					//didn't find it, should add more
				}
				else if (j >= set2.Length)
				{
					curvevect[j] = vect;
				}
				else
				{
					curvevect[j].y = (curvevect[j].y + vect.y) * 0.5f;
				}
			}
			return curvevect;
		}


		/// <summary>
		/// Merges of the type Vector2 for atmosphericCurves
		/// </summary>
		/// <param name="set1"></param>
		/// <param name="set2"></param>
		/// <returns></returns>
		public Vector2[] MergeAtmCurve(string[] set1, string[] set2)
		{
			Vector2[] curvevect = new Vector2[(set1.Length >= set2.Length) ? set1.Length : set2.Length];
			for (int i = 0; i < curvevect.Length; ++i)
			{
				curvevect[i] = ConfigNode.ParseVector2(set2[i]);
			}
			for (int i = 0; i < set1.Length; ++i)
			{
				Vector2 vect = ConfigNode.ParseVector2(set1[i]);
				int j = 0;
				while (vect.x != curvevect[j].x && j < curvevect.Length && j < set2.Length)
				{
					++j;
				}
				if (j >= curvevect.Length)
				{
					//didn't find it, should add more
				}
				else if (j >= set2.Length)
				{
					curvevect[j] = vect;
				}
				else
				{
					curvevect[j].y = (curvevect[j].y + vect.y) * 0.5f;
				}
			}
			return curvevect;
		}


		/// <summary>
		/// handles the correct addition of Modules to the Modulelist of the new Part
		/// </summary>
		/// <param name="partname"></param>
		/// <param name="newModuleName"></param>
		/// <param name="newModule"></param>
		private void addNewModule(string partname, string newModuleName, ConfigNode newModule, List<ConfigNode> modulelist, bool advancedDebugging)
		{
			switch (newModule.GetValue(newModule.values.DistinctNames()[0]))
			{
				case Constants.modStockAnchdec:
					{
						//Decoupler: Change node name
						string decouplename = newModule.GetValue("explosiveNodeID") + partname + modulelist.Count;
						newModule.SetValue("explosiveNodeID", decouplename);
						break;
					}
				case Constants.modStockDocking:
					{
						//Docking port: Change node name if any TODO: FIX This
						if (newModule.HasValue("referenceAttachNode"))
						{
							string dockname = newModule.GetValue("referenceAttachNode") + partname + modulelist.Count;
							newModule.SetValue("referenceAttachNode", dockname);
						}
						break;
					}
				case Constants.modStockJettison:
					{
						//Fairing/Jetisson, change node name
						string jetissonname = newModule.GetValue("bottomNodeName") + partname + modulelist.Count;
						newModule.SetValue("bottomNodeName", jetissonname);
						break;
					}
			}
			modulelist.Add(newModule);
			Debugger.AdvDebug(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModAdd, newModuleName), advancedDebugging);
		}



	}
}
