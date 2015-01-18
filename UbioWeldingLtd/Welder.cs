using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace UbioWeldingLtd
{
	class ModelInfo
	{
		public string url = string.Empty;
		public Vector3 position = Vector3.zero;
		public Vector3 rotation = Vector3.zero;
		public Vector3 scale = Vector3.one;
		public List<string> textures = new List<string>();
		public string parent = string.Empty;
	}

	enum WeldingReturn
	{
		// Warning
		MultipleLandingLegs = 22,
		MultipleScienceExp = 21,
		MultipleFXAnimateThrottle = 20,
		ModuleUnknown = 19,
		MultipleLaunchClamp = 18,
		MultipleFxPos = 17,
		MultipleFxLookAt = 16,
		MultipleWheel = 15,
		MultipleRetLadder = 14,
		MultipleLight = 13,
		MultipleParachutes = 12,
		MultipleRcs = 11,
		MultipleDocking = 10,
		MultipleDecouple = 9,
		MultipleAnimGen = 8,
		MultipleIntake = 7,
		MultipleEngine = 6,
		MultipleAnimHeat = 5,
		MultipleJettison = 4,
		MultipleSolarPan = 3,
		MultipleSeats = 2,
		MultipleInternal = 1,
		//Success
		Success = 0,
		//error
		MissingCfg = -1,
		MissingModel = -2
	}

	class Welder : ModuleMerger
	{
		private int _partNumber = 0;
		private string _name = Constants.weldDefaultName;
		private string _module = string.Empty;
		private List<ModelInfo> _models = new List<ModelInfo>();
		private float _rescaleFactor = Constants.weldRescaleFactor;
		private int _physicsSignificance = -1;

		private List<AttachNode> _attachNodes = new List<AttachNode>();
		private AttachNode _srfAttachNode = new AttachNode();

		private int _cost = 0;
		private int _crewCapacity = 0;
		private PartCategories _category = PartCategories.none;
		private string _subcat = Constants.weldSubcat;

		private string _title = Constants.weldDefaultTitle;
		private string _description = Constants.weldDefaultDesc;
		private AttachRules _attachrules = new AttachRules();
		private string _techRequire = string.Empty;
		private List<string> _listedTechs = new List<string>();
		private int _entryCost = Constants.weldDefaultEntryCost;

		private float _mass = 0.0f;
		private float _fullmass = 0.0f;
		private string _dragModel = string.Empty;
		private float _minimumDrag = 0.0f;
		private float _maximumDrag = 0.0f;
		private float _angularDrag = 0.0f;
		private float _crashTolerance = 0.0f;
		private float _breakingForce = 0.0f;
		private float _breakingTorque = 0.0f;
		private float _maxTemp = 0.0f;

		private bool _fuelCrossFeed = false;

		private List<ConfigNode> _resourceslist = new List<ConfigNode>();
		private List<ConfigNode> _modulelist = new List<ConfigNode>();
		private ConfigNode _fxData = new ConfigNode();

		private Vector3 _coMOffset = Vector3.zero;
		private Vector3 _com = Vector3.zero;

        private Char _filePathDelimiter;
		private bool _advancedDebug = false;
		public ConfigNode FullConfigNode = new ConfigNode(Constants.weldPartNode);
		private static bool _includeAllNodes = false;
		private static bool _dontProcessMasslessParts = false;
		private static bool _runInTestMode = false;
		private static StrengthParamsCalcMethod _StrengthCalcMethod = StrengthParamsCalcMethod.WeightedAverage;
		private static MaxTempCalcMethod _MaxTempCalcMethod = MaxTempCalcMethod.Lowest;

		public static bool includeAllNodes
		{
			get { return _includeAllNodes; }
			set { _includeAllNodes = value; }
		}

		public static bool dontProcessMasslessParts
		{
			get { return _dontProcessMasslessParts; }
			set { _dontProcessMasslessParts = value; }
		}

		public static bool runInTestMode
		{
			get { return _runInTestMode; }
			set { _runInTestMode = value; }
		}

		public static StrengthParamsCalcMethod StrengthCalcMethod
		{
			get { return _StrengthCalcMethod; }
			set { _StrengthCalcMethod = value; }
		}

		public static MaxTempCalcMethod MaxTempCalcMethod
		{
			get { return _MaxTempCalcMethod; }
			set { _MaxTempCalcMethod = value; }
		}

		public string Name
		{
			get { return _name; }
			set
			{
				_name = value;
				_name = _name.Replace(' ', '-');
				_name = _name.Replace('.', '-');
				_name = _name.Replace('\\', '-');
				_name = _name.Replace('/', '-');
				_name = _name.Replace(':', '-');
				_name = _name.Replace('*', '-');
				_name = _name.Replace('?', '-');
				_name = _name.Replace('<', '-');
				_name = _name.Replace('>', '-');
				_name = _name.Replace('|', '-');
				_name = _name.Replace('_', '-');
			}
		}
		public string Title { get { return _title; } set { _title = value; } }
		public string Description { get { return _description; } set { _description = value; } }
		public int Cost { get { return _cost; } }
		public float Mass { get { return _mass; } }
		public float WetMass { get { return _fullmass; } }
		public bool FuelCrossFeed { get { return _fuelCrossFeed; } set { _fuelCrossFeed = value; } }
		public float MinDrag { get { return _minimumDrag; } }
		public float MaxDrag { get { return _maximumDrag; } }
		public float CrashTolerance { get { return _crashTolerance; } }
		public float BreakingForce { get { return _breakingForce; } }
		public float BreakingTorque { get { return _breakingTorque; } }
		public float MaxTemp { get { return _maxTemp; } }
		public float NbParts { get { return _partNumber; } }

		public string[] Modules
		{
			get
			{
				string[] moduleslist = new string[_modulelist.Count];
				int index = 0;
				foreach (ConfigNode cfgnode in _modulelist)
				{
					moduleslist[index] = cfgnode.GetValue("name");
					++index;
				}
				return moduleslist;
			}
		}

		public string[] Resources
		{
			get
			{
				string[] resourceslist = new string[_resourceslist.Count * 2];
				int index = 0;
				foreach (ConfigNode cfgnode in _resourceslist)
				{
					resourceslist[index++] = cfgnode.GetValue("name");
					resourceslist[index++] = string.Format("{0} / {1}", cfgnode.GetValue("amount"), cfgnode.GetValue("maxAmount"));
				}
				return resourceslist;
			}
		}

		public PartCategories Category
		{
			get { return _category; }
			set { _category = value; }
		}

		public string techRequire
		{
			get { return _techRequire; }
			set { _techRequire = value; }
		}

		public List<string> techList
		{
			get { return _listedTechs; }
		}

		/*
		 * Constructor
		 */
		public Welder(bool advancedDebug)
        {
            //in Linux and OSX delimiters in file path are '/', not '\'
            if ((Application.platform == RuntimePlatform.LinuxPlayer) || (Application.platform == RuntimePlatform.OSXPlayer))
            {
                _filePathDelimiter = '/';
            }
            else
            {
                _filePathDelimiter = '\\';
            }
			_advancedDebug = advancedDebug;
        }


		/*
		 * Set relative position
		 */
		private void setRelativePosition(Part part, ref Vector3 position)
		{
			position += part.transform.position - part.localRoot.transform.position;
		} //private void setRelativePosition(Part part, ref Vector3 position)


		/*
		 * Set relative rotation
		 */
		private void setRelativeRotation(Part part, ref Vector3 rotation)
		{
			rotation += part.transform.eulerAngles - part.localRoot.transform.eulerAngles;

			if (360.0f <= rotation.x) rotation.x -= 360.0f;
			else if (0 > rotation.x) rotation.x += 360.0f;

			if (360.0f <= rotation.y) rotation.y -= 360.0f;
			else if (0 > rotation.y) rotation.y += 360.0f;

			if (360.0f <= rotation.y) rotation.y -= 360.0f;
			else if (0 > rotation.y) rotation.y += 360.0f;
		} //private void setRelativeRotation(Part part, ref Vector3 rotation)


		/*
		 * Process the new center of mass to the models and node
		 */
		public void processNewCoM()
		{
			foreach (ModelInfo model in _models)
			{
				model.position -= _com;
			}
			foreach (AttachNode node in _attachNodes)
			{
				node.position -= _com;
			}
		}


		/*
		 * Get the mesh name
		 */
		private string GetMeshurl(UrlDir.UrlConfig cfgdir)
		{
			string mesh = "model";
			//in case the mesh is not model.mu
			if (cfgdir.config.HasValue("mesh"))
			{
				mesh = cfgdir.config.GetValue("mesh");
				char[] sep = { '.' };
				string[] words = mesh.Split(sep);
				mesh = words[0];
			}
            string filename = string.Format("{0}" + _filePathDelimiter + "{1}.mu", cfgdir.parent.parent.path, mesh);
			string url = string.Format("{0}/{1}", cfgdir.parent.parent.url, mesh);

			//in case the mesh name does not exist (.22 bug)
			if (!File.Exists(filename))
			{
				Debug.LogWarning(string.Format("{0}{1}.!{2} {3}", Constants.logWarning, Constants.logPrefix, Constants.logWarnNoMesh, filename));
				string[] files = Directory.GetFiles(cfgdir.parent.parent.path, "*.mu");
				if (files.Length != 0)
				{
					Debugger.AdvDebug(string.Format("..cfgdir.parent.parent.path {0}", cfgdir.parent.parent.path), _advancedDebug);
					Debugger.AdvDebug(string.Format("..files[0] {0}", files[0]), _advancedDebug);
					Debugger.AdvDebug(string.Format("..cfgdir.parent.parent.path.Length {0}", cfgdir.parent.parent.path.Length), _advancedDebug);
					files[0] = files[0].Remove(0, cfgdir.parent.parent.path.Length);

					Debugger.AdvDebug(string.Format("{0}.New mesh name: {1}", Constants.logPrefix, files[0]), _advancedDebug);

					char[] sep = { '\\','.', '/' };
					string[] words = files[0].Split(sep);

					Debugger.AdvDebug(string.Format("..words[1] {0}", words[1]), _advancedDebug);
					Debugger.AdvDebug(string.Format("..mesh {0}", mesh), _advancedDebug);

//					url = url.Replace(string.Format(@"{0}", mesh), words[1]);
					url = url.Substring(0, url.LastIndexOf('/') + 1) + words[1];

					Debugger.AdvDebug(string.Format("..url {0}", url), _advancedDebug);
				}
				else
				{
					Debug.LogWarning(string.Format("{0}{1}.No mesh found, using default", Constants.logWarning, Constants.logPrefix));
				}
			}
			return url;
		}

		/*
		 * Weld a new part
		 */
		public WeldingReturn weldThisPart(Part newpart)
		{
			_coMOffset = Vector3.zero;
			WeldingReturn ret = WeldingReturn.Success;
			string partname = (string)newpart.partInfo.partPrefab.name.Clone();
			WeldingHelpers.removeTextRegex(ref partname, "(Clone)");

			Debug.Log(string.Format("{0}{1}{2}",Constants.logPrefix,Constants.logWeldingPart,partname));
			Debugger.AdvDebug(string.Format("..part rescaleFactor {0:F}", newpart.rescaleFactor), _advancedDebug);
			Debugger.AdvDebug(string.Format("..part scaleFactor {0:F}", newpart.scaleFactor), _advancedDebug);

			//--- Find all the config file with the name
			List<UrlDir.UrlConfig> matchingPartConfigs = new List<UrlDir.UrlConfig>();
			foreach (UrlDir.UrlConfig config in GameDatabase.Instance.GetConfigs(Constants.weldPartNode))
			{
				string newconfigname = config.name.Replace('_', '.');

//Girka2K - too many spam in LOG from here
				//AdvDebug(tring.Format(".config name {0}", newconfigname));

				if (System.String.Equals(partname, newconfigname, System.StringComparison.Ordinal))
				{
					matchingPartConfigs.Add(config);
				}
			}

			Debugger.AdvDebug(string.Format(".Found {0} config files", matchingPartConfigs.Count), _advancedDebug);

			if (matchingPartConfigs.Count < 1)
			{
				//Missing Config File: Error
				Debug.LogError(string.Format("{0}{1}.{2} {3}", Constants.logError, Constants.logPrefix, Constants.msgCfgMissing, partname));
				return WeldingReturn.MissingCfg;
			}
			else // 0 < matchingPartConfigs.Count
			{
				//Process Config Files
				foreach (UrlDir.UrlConfig cfg in matchingPartConfigs)
				{
					//MODEL
					if (!cfg.config.HasNode(Constants.weldModelNode))
					{
						//Missing Model node
						Debugger.AdvDebug(string.Format("..Config {0} has no {1} node", cfg.name, Constants.weldModelNode), _advancedDebug);

						ModelInfo info = new ModelInfo();
						info.url = GetMeshurl(cfg);
						Debugger.AdvDebug(string.Format("..{0}{1}", Constants.logModelUrl, info.url), _advancedDebug);

						Vector3 position = Vector3.zero;
						setRelativePosition(newpart, ref position);
						info.position = position;

						Vector3 rotation = newpart.localRoot.transform.eulerAngles;
						setRelativeRotation(newpart, ref rotation);
						info.rotation = rotation;

						info.scale = newpart.transform.GetChild(0).localScale;

						Debugger.AdvDebug(string.Format("..newpart position {0}", newpart.transform.position.ToString("F3")), _advancedDebug);
						Debugger.AdvDebug(string.Format("..newpart rotation {0}", newpart.transform.rotation.ToString("F3")), _advancedDebug);
						Debugger.AdvDebug(string.Format("..newpart rotation.eulerAngles {0}", newpart.transform.rotation.eulerAngles.ToString("F3")), _advancedDebug);
						Debugger.AdvDebug(string.Format("..newpart rotation.localEulerAngles {0}", newpart.transform.localEulerAngles.ToString("F3")), _advancedDebug);
						Debugger.AdvDebug(string.Format("..newpart localRoot.rotation {0}", newpart.localRoot.transform.rotation.ToString("F3")), _advancedDebug);
						Debugger.AdvDebug(string.Format("..newpart localRoot.rotation.eulerAngles {0}", newpart.localRoot.transform.rotation.eulerAngles.ToString("F3")), _advancedDebug);
						Debugger.AdvDebug(string.Format("..position {0}", info.position.ToString("F3")), _advancedDebug);
						Debugger.AdvDebug(string.Format("..rotation {0}", info.rotation.ToString("F3")), _advancedDebug);
						Debugger.AdvDebug(string.Format("..scale {0}", info.scale.ToString("F3")), _advancedDebug);

						_models.Add(info);
						_coMOffset += info.position;
					}
					else //cfg.config.HasNode(Constants.weldModelNode)
					{
						ConfigNode[] modelnodes = cfg.config.GetNodes(Constants.weldModelNode);
						Debugger.AdvDebug(string.Format("..Config {0} has {1} {2} node", cfg.name, modelnodes.Length, Constants.weldModelNode), _advancedDebug);

						Vector3 _coMOffsetSum = Vector3.zero;
						foreach (ConfigNode node in modelnodes)
						{
							ModelInfo info = new ModelInfo();

							if (node.HasValue("model"))
							{
								info.url = node.GetValue("model");
							}
							else
							{
								info.url = GetMeshurl(cfg);
							}
							Debugger.AdvDebug(string.Format("..{0}{1}", Constants.logModelUrl, info.url), _advancedDebug);
							Vector3 position = (node.HasValue("position")) ? (ConfigNode.ParseVector3(node.GetValue("position")) * newpart.rescaleFactor) : Vector3.zero;
							Debugger.AdvDebug(string.Format("..node.HasValue(\"position\") {0}", node.HasValue("position")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..node position {0}", position.ToString("F3")), _advancedDebug);
							setRelativePosition(newpart, ref position);

							info.position = position;

							Vector3 rotation = (node.HasValue("rotation")) ? ConfigNode.ParseVector3(node.GetValue("rotation")) : Vector3.zero;
							Debugger.AdvDebug(string.Format("..node.HasValue(\"rotation\") {0}", node.HasValue("rotation")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..node rotation {0}", rotation.ToString("F3")), _advancedDebug);

							setRelativeRotation(newpart, ref rotation);
							info.rotation = rotation;

							Debugger.AdvDebug(string.Format("..node.HasValue(\"scale\") {0}", node.HasValue("scale")), _advancedDebug);
							if (node.HasValue("scale"))
							{
								Debugger.AdvDebug(string.Format("..node scale {0}", node.GetValue("scale")), _advancedDebug);
							}
							Debugger.AdvDebug(string.Format("..Childs count {0}", newpart.transform.childCount), _advancedDebug);

							info.scale = (node.HasValue("scale")) ?
											(ConfigNode.ParseVector3(node.GetValue("scale")) * (newpart.rescaleFactor / _rescaleFactor)) :
											new Vector3(newpart.transform.GetChild(0).localScale.x,
														newpart.transform.GetChild(0).localScale.y,
														newpart.transform.GetChild(0).localScale.z);

							Debugger.AdvDebug(string.Format("..newpart position {0}", newpart.transform.position.ToString("F3")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..newpart rotation {0}", newpart.transform.rotation.ToString("F3")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..newpart rotation.eulerAngles {0}", newpart.transform.rotation.eulerAngles.ToString("F3")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..newpart rotation.localEulerAngles {0}", newpart.transform.localEulerAngles.ToString("F3")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..newpart localRoot.rotation {0}", newpart.localRoot.transform.rotation.ToString("F3")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..newpart localRoot.rotation.eulerAngles {0}", newpart.localRoot.transform.rotation.eulerAngles.ToString("F3")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..position {0}", info.position.ToString("F3")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..rotation {0}", info.rotation.ToString("F3")), _advancedDebug);
							Debugger.AdvDebug(string.Format("..scale {0}", info.scale.ToString("F3")), _advancedDebug);

							if (node.HasValue("texture"))
							{
								foreach (string tex in node.GetValues("texture"))
								{
									info.textures.Add(tex);
									Debugger.AdvDebug(string.Format("..texture {0}", tex), _advancedDebug);
								}
							}
							if (node.HasValue("parent"))
							{
								info.parent = node.GetValue("parent");
							}
							_models.Add(info);
							_coMOffsetSum += info.position;
						} //foreach (ConfigNode node in modelnodes)
						_coMOffset = _coMOffsetSum / modelnodes.Length;
					} // else of if ( !cfg.config.HasNode(Constants.weldModelNode) )

					//RESSOURCE
					ConfigNode[] ressources = cfg.config.GetNodes(Constants.weldResNode);
					Debugger.AdvDebug(string.Format("..Config {0} has {1} {2} node", cfg.name, ressources.Length, Constants.weldResNode), _advancedDebug);

					foreach (ConfigNode orires in ressources)
					{
						ConfigNode res = orires.CreateCopy();
						string resname = res.GetValue("name");
						bool exist = false;
						foreach (ConfigNode rescfg in _resourceslist)
						{
							if (string.Equals(resname, rescfg.GetValue("name")))
							{
								//add the ressource
								float amount = float.Parse(res.GetValue("amount")) + float.Parse(rescfg.GetValue("amount"));
								float max = float.Parse(res.GetValue("maxAmount")) + float.Parse(rescfg.GetValue("maxAmount"));
								rescfg.SetValue("amount", amount.ToString());
								rescfg.SetValue("maxAmount", max.ToString());
								exist = true;
								Debugger.AdvDebug(string.Format("..{0}{1} {2}/{3}", Constants.logResMerge, resname, amount, max), _advancedDebug);
								break;
							}
						}
						if (!exist)
						{
							_resourceslist.Add(res);
							float amount = float.Parse(res.GetValue("amount"));
							float max = float.Parse(res.GetValue("maxAmount"));
							Debugger.AdvDebug(string.Format("..{0}{1} {2}/{3}", Constants.logResAdd, resname, amount, max), _advancedDebug);
						}
					} //foreach (ConfigNode res in ressources)

					//MODULE
					ConfigNode[] originalModules = cfg.config.GetNodes(Constants.weldModuleNode);
					Debugger.AdvDebug(string.Format("..Config {0} has {1} {2} node", cfg.name, originalModules.Length, Constants.weldModuleNode), _advancedDebug);
					Debugger.AdvDebug(string.Format(".. running in Alewx Testmode = {0}", runInTestMode), _advancedDebug);

					if (runInTestMode)
					{
						mergeModules(partname, cfg, _modulelist, _advancedDebug);
					}
					else
					{
						ret = OldModuleMerge(ret, partname, cfg);
					}
					//manage the fx group
					foreach (FXGroup fx in newpart.fxGroups)
					{
						Debugger.AdvDebug(string.Format("..Config {0} has {1} FXEmitters and {2} Sound in {3} FxGroups", cfg.name, fx.fxEmitters.Count, (null != fx.sfx) ? "1" : "0", fx.name), _advancedDebug);

						if (!fx.name.Contains("rcsGroup")) //RCS Fx are not store in the config file
						{
							foreach (ParticleEmitter gobj in fx.fxEmitters)
							{
								string fxname = gobj.name;
								WeldingHelpers.removeTextRegex(ref fxname,"(Clone)");
								string fxvalue = cfg.config.GetValue(fxname);
								string[] allvalue = Regex.Split(fxvalue, ", ");
								Vector3 pos = new Vector3(float.Parse(allvalue[0]), float.Parse(allvalue[1]), float.Parse(allvalue[2]));
								Vector3 ang = new Vector3(float.Parse(allvalue[3]), float.Parse(allvalue[4]), float.Parse(allvalue[5]));
								setRelativePosition(newpart, ref pos);
								fxvalue = string.Format("{0}, {1}, {2}, {3}, {4}, {5}, {6}", pos.x, pos.y, pos.z, ang.x, ang.y, ang.z, allvalue[6]);
								for (int i = 7; i < allvalue.Length; ++i)
								{
									fxvalue = string.Format("{0}, {1}", fxvalue, allvalue[i]);
								}
								_fxData.AddValue(fxname, fxvalue);
								Debugger.AdvDebug(string.Format("..{0}{1}", Constants.logFxAdd, fxname), _advancedDebug);
							}
							if (fx.sfx != null)
							{
								_fxData.AddValue(fx.sfx.name, fx.name);
								Debugger.AdvDebug(string.Format("..{0}{1}", Constants.logFxAdd, fx.sfx.name), _advancedDebug);
							}
						}
					} //foreach (FXGroup fx in newpart.fxGroups)
				} //foreach (UrlDir.UrlConfig cfg in matchingPartConfigs)
			} //else of if (0 >= matchingPartConfigs.Count)

			//ATTACHNODE
			Debugger.AdvDebug(string.Format(".Part {0} has {1} Stack attach node(s)", partname, newpart.attachNodes.Count), _advancedDebug);

			foreach (AttachNode partnode in newpart.attachNodes)
			{
				//only add node if not attached to another part (or if requested in the condig file)
				if (_includeAllNodes || null == partnode.attachedPart)
				{
					AttachNode node = partnode; //make sure we don't overwrite the part node
					node.id += partname + _partNumber;
					Matrix4x4 rot = Matrix4x4.TRS(Vector3.zero, newpart.transform.rotation, Vector3.one);
					node.position = rot.MultiplyVector(node.position);
					node.orientation = rot.MultiplyVector(node.orientation);
					setRelativePosition(newpart, ref node.position);

					_attachNodes.Add(node);
					Debugger.AdvDebug(string.Format(".{0}{1}", Constants.logNodeAdd, node.id), _advancedDebug);
				}
			} //foreach (AttachNode node in newpart.attachNodes)

			//TODO: Tech tree stuff
			if (!_listedTechs.Contains(newpart.partInfo.TechRequired))
			{
				_listedTechs.Add(newpart.partInfo.TechRequired);
			}

			//Cost
			_cost += (int)newpart.partInfo.cost;
			_entryCost += (int)newpart.partInfo.entryCost;
			_crewCapacity += newpart.CrewCapacity;

			// srfAttachNode Rules
			_attachrules.allowDock = _attachrules.allowDock || newpart.attachRules.allowDock;
			_attachrules.allowRotate = _attachrules.allowRotate || newpart.attachRules.allowRotate;
			_attachrules.allowSrfAttach = _attachrules.allowSrfAttach || newpart.attachRules.allowSrfAttach;
			_attachrules.allowStack = _attachrules.allowStack || newpart.attachRules.allowStack;
			_attachrules.srfAttach = _attachrules.srfAttach || newpart.attachRules.srfAttach;
			_attachrules.stack = _attachrules.stack || newpart.attachRules.stack;

			//mass
			float oldmass = _fullmass;
			float olddrymass = _mass;
			float partdrymass = 0.0f;
			// if part's PhysicsSignificance = 1, then this part is "massless" and its mass would be ignored in stock KSP
			if ((!dontProcessMasslessParts) || (newpart.PhysicsSignificance != 1))
			{
				partdrymass = newpart.mass;
			}

			float partwetmass = partdrymass + newpart.GetResourceMass();

			_mass += partdrymass;
			_fullmass += partwetmass;
			_com = ((_com * oldmass) + (_coMOffset * partwetmass)) / _fullmass;
			Debugger.AdvDebug(string.Format("AdvDebug(.New Center of Mass: {0}", _com.ToString()), _advancedDebug);
			//Drag (Add)
			_minimumDrag = (_minimumDrag + newpart.minimum_drag) * 0.5f;
			_maximumDrag = (_maximumDrag + newpart.maximum_drag) * 0.5f;
			_angularDrag = (_angularDrag + newpart.angularDrag) * 0.5f;
			//TODO: modify type
			_dragModel = newpart.dragModelType;

			//average crash, breaking and temp
			switch (_StrengthCalcMethod)
			{
				case StrengthParamsCalcMethod.Legacy:
					_crashTolerance = (_partNumber == 0) ? newpart.crashTolerance : (_crashTolerance + newpart.crashTolerance) * 0.75f;
					_breakingForce = (_partNumber == 0) ? newpart.breakingForce : (_breakingForce + newpart.breakingForce) * 0.75f;
					_breakingTorque = (_partNumber == 0) ? newpart.breakingTorque : (_breakingTorque + newpart.breakingTorque) * 0.75f;
					break;
				case StrengthParamsCalcMethod.WeightedAverage:
					_crashTolerance = (_partNumber == 0) ? newpart.crashTolerance : (_crashTolerance * olddrymass + newpart.crashTolerance * newpart.mass) / (olddrymass + newpart.mass);
					_breakingForce = (_partNumber == 0) ? newpart.breakingForce : (_breakingForce * olddrymass + newpart.breakingForce * newpart.mass) / (olddrymass + newpart.mass);
					_breakingTorque = (_partNumber == 0) ? newpart.breakingTorque : (_breakingTorque * olddrymass + newpart.breakingTorque * newpart.mass) / (olddrymass + newpart.mass);
					break;
				case StrengthParamsCalcMethod.ArithmeticMean:
					_crashTolerance = (_partNumber == 0) ? newpart.crashTolerance : (_crashTolerance + newpart.crashTolerance) * 0.5f;
					_breakingForce = (_partNumber == 0) ? newpart.breakingForce : (_breakingForce + newpart.breakingForce) * 0.5f;
					_breakingTorque = (_partNumber == 0) ? newpart.breakingTorque : (_breakingTorque + newpart.breakingTorque) * 0.5f;
					break;
			}
			switch (_MaxTempCalcMethod)
			{
				case MaxTempCalcMethod.ArithmeticMean:
					_maxTemp = (_partNumber == 0) ? newpart.maxTemp : (_maxTemp + newpart.maxTemp) * 0.5f;
					break;
				case MaxTempCalcMethod.Lowest:
					_maxTemp = (_partNumber == 0) ? newpart.maxTemp : Math.Min(_maxTemp, newpart.maxTemp);
					break;
				case MaxTempCalcMethod.WeightedAverage:
					_maxTemp = (_partNumber == 0) ? newpart.maxTemp : (_maxTemp * olddrymass + newpart.maxTemp * olddrymass) / (olddrymass + newpart.mass);
					break;
			}

			//Phisics signifance
			if (newpart.PhysicsSignificance != 0 && _physicsSignificance != -1)
			{
				_physicsSignificance = newpart.PhysicsSignificance;
			}

			if (_partNumber == 0)
			{
				//TODO: Find where to find it in game. Would that be pre .15 stuff? http://forum.kerbalspaceprogram.com/threads/7529-Plugin-Posting-Rules-And-Official-Documentation?p=156430&viewfull=1#post156430
				_module = "Part";
				//
				Debugger.AdvDebug(string.Format("AdvDebug(weldThisPart - newpart.partInfo.category: {0}", newpart.partInfo.category.ToString()), _advancedDebug);
				_category = newpart.partInfo.category;
				//TODO: better surface node managment
				_srfAttachNode = newpart.srfAttachNode;
				//Fuel crossfeed: TODO: test different ways to managed it
				_fuelCrossFeed = newpart.fuelCrossFeed;
				//
				_physicsSignificance = newpart.PhysicsSignificance;
			}
			_partNumber++;
			return ret;
		}


		/// <summary>
		/// checks if the given Module is included in the part.
		/// returns true when a wanted module is found.
		/// </summary>
		/// <param name="moduleSearchArray"></param>
		/// <param name="moduleType"></param>
		/// <returns></returns>
		private bool hasModuleType(ConfigNode[] moduleSearchArray, string moduleType)
		{
			foreach (ConfigNode singleModule in moduleSearchArray)
			{
				if (moduleType.Equals(singleModule.GetValue(singleModule.values.DistinctNames()[0])))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// provides the wanted module from a inserted list of modules.
		/// returns the first found module of the given type, if no module was found retursn null
		/// </summary>
		/// <param name="moduleSearchArray"></param>
		/// <param name="moduleType"></param>
		/// <returns></returns>
		private ConfigNode getModuleOfType(ConfigNode[] moduleSearchArray, string moduleType)
		{
			foreach (ConfigNode singleModule in moduleSearchArray)
			{
				if (moduleType.Equals(singleModule.GetValue(singleModule.values.DistinctNames()[0])))
				{
					return singleModule;
				}
			}
			return null;
		}


		/// <summary>
		/// the original module merging method.
		/// </summary>
		/// <param name="ret"></param>
		/// <param name="partname"></param>
		/// <param name="configuration"></param>
		/// <returns></returns>
		private WeldingReturn OldModuleMerge(WeldingReturn ret, string partname, UrlDir.UrlConfig configuration)
		{
			ConfigNode[] originalModules = configuration.config.GetNodes(Constants.weldModuleNode);

			foreach (ConfigNode originalModule in originalModules)
			{
				ConfigNode newModule = originalModule.CreateCopy();
				string newModuleName = newModule.GetValue(newModule.values.DistinctNames()[0]);
				bool exist = false;

				foreach (ConfigNode existingNewModule in _modulelist)
				{
					if (string.Equals(newModuleName, existingNewModule.GetValue(existingNewModule.values.DistinctNames()[0])))
					{
						switch (newModuleName)
						{
							//case Constants.modStockSas:
							//	{
							//		// don't add SAS modules together.
							//		break;
							//	}
							case Constants.modStockGear:			//Don't add (.21)
#if (DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModIgnore, newModuleName));
#endif
								exist = true;
								break;
							case Constants.modStockReacWheel:	   //Add reaction wheel force
								float pitch = float.Parse(existingNewModule.GetValue("PitchTorque")) + float.Parse(newModule.GetValue("PitchTorque"));
								float yaw = float.Parse(existingNewModule.GetValue("YawTorque")) + float.Parse(newModule.GetValue("YawTorque"));
								float roll = float.Parse(existingNewModule.GetValue("RollTorque")) + float.Parse(newModule.GetValue("RollTorque"));
								float wheelrate = float.Parse(existingNewModule.GetNode(Constants.weldResNode).GetValue("rate")) + float.Parse(newModule.GetNode(Constants.weldResNode).GetValue("rate"));
								existingNewModule.SetValue("PitchTorque", pitch.ToString());
								existingNewModule.SetValue("YawTorque", yaw.ToString());
								existingNewModule.SetValue("RollTorque", roll.ToString());
								existingNewModule.GetNode(Constants.weldResNode).SetValue("rate", wheelrate.ToString());
#if (DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModMerge, newModuleName));
#endif
								exist = true;
								break;
							case Constants.modStockCommand:		// Add Crew and Electricity ressources //TODO: Manage all used ressources
								int crew = int.Parse(newModule.GetValue("minimumCrew")) + int.Parse(existingNewModule.GetValue("minimumCrew"));
								existingNewModule.SetValue("minimumCrew", crew.ToString());
								if (newModule.HasNode(Constants.weldResNode))
								{
									if (existingNewModule.HasNode(Constants.weldResNode))
									{
										float comrate = float.Parse(newModule.GetNode(Constants.weldResNode).GetValue("rate")) + float.Parse(existingNewModule.GetNode(Constants.weldResNode).GetValue("rate"));
										existingNewModule.GetNode(Constants.weldResNode).SetValue("rate", comrate.ToString());
									}
									else
									{
										existingNewModule.AddNode(newModule.GetNode(Constants.weldResNode));
									}
								}
#if(DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModMerge, newModuleName));
#endif
								exist = true;
								break;
							case Constants.modStockGen:			// Add Generator Values //TODO: Manage output ressource name.
								bool active = bool.Parse(newModule.GetValue("isAlwaysActive")) && bool.Parse(existingNewModule.GetValue("isAlwaysActive"));
								float genrate = float.Parse(newModule.GetNode(Constants.weldOutResNode).GetValue("rate")) + float.Parse(existingNewModule.GetNode(Constants.weldOutResNode).GetValue("rate"));
								existingNewModule.SetValue("isAlwaysActive", active.ToString());
								existingNewModule.GetNode(Constants.weldOutResNode).SetValue("rate", genrate.ToString());
#if(DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModMerge, newModuleName));
#endif
								exist = true;
								break;
							case Constants.modStockAltern:		 //add the alternator value
								float altrate = float.Parse(newModule.GetNode(Constants.weldResNode).GetValue("rate")) + float.Parse(existingNewModule.GetNode(Constants.weldResNode).GetValue("rate"));
								existingNewModule.GetNode(Constants.weldResNode).SetValue("rate", altrate.ToString());
#if(DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModMerge, newModuleName));
#endif
								exist = true;
								break;
							case Constants.modStockGimbal:	  //average the gimbal range TODO: test the gimbal
								int gimbal = (int.Parse(newModule.GetValue("gimbalRange")) + int.Parse(existingNewModule.GetValue("gimbalRange"))) / 2;
								existingNewModule.SetValue("gimbalRange", gimbal.ToString());
#if(DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModMerge, newModuleName));
#endif
								exist = true;
								break;
							case Constants.modStockSensor:	 // Allow one sensor module per different sensor
								exist = string.Equals(newModule.GetValue("sensorType"), existingNewModule.GetValue("sensorType"));
#if(DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, (exist) ? Constants.logModIgnore : Constants.logModMerge, newModuleName));
#endif
								break;
							case Constants.modStockEngine:		// Average/add value and warning
								bool exhaustDamage = bool.Parse(newModule.GetValue("exhaustDamage")) || bool.Parse(existingNewModule.GetValue("exhaustDamage"));
								float ignitionThreshold = (float.Parse(newModule.GetValue("ignitionThreshold")) + float.Parse(existingNewModule.GetValue("ignitionThreshold"))) * 0.5f;
								float minThrust = float.Parse(newModule.GetValue("minThrust")) + float.Parse(existingNewModule.GetValue("minThrust"));
								float maxThrust = float.Parse(newModule.GetValue("maxThrust")) + float.Parse(existingNewModule.GetValue("maxThrust"));
								int heatProduction = (int.Parse(newModule.GetValue("heatProduction")) + int.Parse(existingNewModule.GetValue("heatProduction"))) / 2;
								existingNewModule.SetValue("exhaustDamage", exhaustDamage.ToString());
								existingNewModule.SetValue("ignitionThreshold", ignitionThreshold.ToString());
								existingNewModule.SetValue("minThrust", minThrust.ToString());
								existingNewModule.SetValue("maxThrust", maxThrust.ToString());
								existingNewModule.SetValue("heatProduction", heatProduction.ToString());
								//fx offset
								if (newModule.HasValue("fxOffset"))
								{
									Vector3 fxOffset = ConfigNode.ParseVector3(newModule.GetValue("fxOffset"));
									//setRelativePosition(newpart, ref fxOffset);
									//Vector3 cfgFxOffset = ConfigNode.ParseVector3(existingNewModule.GetValue("fxOffset")) + fxOffset;
									newModule.SetValue("fxOffset", ConfigNode.WriteVector(fxOffset));
								}
								//Propellant nodes
								ConfigNode[] Propellant = newModule.GetNodes(Constants.weldEngineProp);
								foreach (ConfigNode prop in Propellant)
								{
									//look if one exist
									ConfigNode[] cfgPropellant = existingNewModule.GetNodes(Constants.weldEngineProp);
									bool propexist = false;
									foreach (ConfigNode cfgprop in cfgPropellant)
									{
										if (string.Equals(cfgprop.GetValue("name"), prop.GetValue("name")))
										{
											float ratio = float.Parse(prop.GetValue("ratio")) + float.Parse(cfgprop.GetValue("ratio"));
											cfgprop.SetValue("ratio", ratio.ToString());
											propexist = true;
											break;
										}
									}
									if (!propexist)
									{
										existingNewModule.SetNode(Constants.weldEngineProp, prop);
									}
								}
								if (newModule.HasNode(Constants.weldEngineAtmCurve))
								{
									if (existingNewModule.HasNode(Constants.weldEngineAtmCurve))
									{
										//merge
										string[] curve = newModule.GetNode(Constants.weldEngineAtmCurve).GetValues("key");
										string[] cfgcurve = existingNewModule.GetNode(Constants.weldEngineAtmCurve).GetValues("key");
										Vector2[] cfgcurvevect = MergeAtmCurve(curve, cfgcurve);
										existingNewModule.GetNode(Constants.weldEngineAtmCurve).RemoveValues("key");
										foreach (Vector2 vec in cfgcurvevect)
										{
											existingNewModule.GetNode(Constants.weldEngineAtmCurve).AddValue("key", ConfigNode.WriteVector(vec));
										}
									}
									else
									{
										existingNewModule.AddNode(newModule.GetNode(Constants.weldEngineAtmCurve));
									}
								}
								if (newModule.HasNode(Constants.weldEngineVelCurve))
								{
									if (existingNewModule.HasNode(Constants.weldEngineVelCurve))
									{
										//merge
										string[] curve = newModule.GetNode(Constants.weldEngineVelCurve).GetValues("key");
										string[] cfgcurve = existingNewModule.GetNode(Constants.weldEngineVelCurve).GetValues("key");
										Vector4[] cfgcurvevect = MergeVelCurve(curve, cfgcurve);
										existingNewModule.GetNode(Constants.weldEngineVelCurve).RemoveValues("key");
										foreach (Vector4 vec in cfgcurvevect)
										{
											existingNewModule.GetNode(Constants.weldEngineVelCurve).AddValue("key", ConfigNode.WriteVector(vec));
										}
									}
									else
									{
										existingNewModule.AddNode(newModule.GetNode(Constants.weldEngineVelCurve));
									}
								}
#if (DEBUG)
								Debug.Log(string.Format("{0}..{1}{2} !{3}", Constants.logPrefix, Constants.logModMerge, newModuleName, Constants.msgWarnModEngine));
#endif
								exist = true;
								break;
							case Constants.modStockAnimHeat:
								exist = string.Equals(existingNewModule.GetValue("ThermalAnim"), newModule.GetValue("ThermalAnim"));
#if (DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, (exist) ? Constants.logModIgnore : Constants.logModMerge, newModuleName));
#endif
								break;
							case Constants.modStockAnimGen:		// Warning for Multiple Animate Generic
								exist = string.Equals(existingNewModule.GetValue("animationName"), newModule.GetValue("animationName"));
								if (exist)
								{
#if (DEBUG)
									Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModAnimGen));
#endif
									ret = WeldingReturn.MultipleAnimGen;
								}
								break;
							case Constants.modStockInternal:   // Warning for multiple interal and ignore
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}..{2}{3} !{4}", Constants.logWarning, Constants.logPrefix, Constants.logModIgnore, newModuleName, Constants.msgWarnModInternal));
#endif
								ret = WeldingReturn.MultipleInternal;
								exist = true;
								break;
							case Constants.modStockSeat:	   // Warning for Multiple seats //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}..{2}{3} !{4}", Constants.logWarning, Constants.logPrefix, Constants.logModIgnore, newModuleName, Constants.msgWarnModSeat));
#endif
								ret = WeldingReturn.MultipleSeats;
								exist = true;
								break;
							case Constants.modStockSolarPan:	   // Warning for Multiple Deployable Solar Panel //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}..{2}{3} !{4}", Constants.logWarning, Constants.logPrefix, Constants.logModIgnore, newModuleName, Constants.msgWarnModSolPan));
#endif
								ret = WeldingReturn.MultipleSolarPan;
								exist = true;
								break;
							case Constants.modStockJettison:	   // Warning for Multiple Jetison //Only one is working fairing is working.
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModJetttison));
#endif
								ret = WeldingReturn.MultipleJettison;
								exist = false;
								break;
							case Constants.modStockFxAnimThro:	   // Warning for Multiple FX animate. // Only the first one is working, the other are ignore
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModFxAnimTh));
#endif
								ret = WeldingReturn.MultipleFXAnimateThrottle;
								exist = false;
								break;
							case Constants.modStockIntake:		// Warning for Multiple Intake //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModIntake));
#endif
								ret = WeldingReturn.MultipleIntake;
								exist = false;
								break;
							case Constants.modStockDecouple:
							case Constants.modStockAnchdec:		//Warning for Multiple Decoupler, change the node //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModDecouple));
#endif
								ret = WeldingReturn.MultipleDecouple;
								exist = false;
								break;
							case Constants.modStockDocking:		//Warning for Multiple Dockingport
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModDocking));
#endif
								ret = WeldingReturn.MultipleDocking;
								exist = false;
								break;
							case Constants.modStockRCS:		//Warning for Multiple RCS
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}..{2}{3} !{4}", Constants.logWarning, Constants.logPrefix, Constants.logModIgnore, newModuleName, Constants.msgWarnModRcs));
#endif
								//ret = WeldingReturn.MultipleRcs;
								exist = true;
								break;
							case Constants.modStockParachutes:		//Warning for Multiple Parachutes //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModParachute));
#endif
								ret = WeldingReturn.MultipleParachutes;
								exist = false;
								break;
							case Constants.modStockLight:		//Warning for Multiple Light //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModLight));
#endif
								ret = WeldingReturn.MultipleLight;
								exist = false;
								break;
							case Constants.modStockRetLadder:		//Warning for Multiple Retractable ladder //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModRetLadder));
#endif
								ret = WeldingReturn.MultipleRetLadder;
								exist = false;
								break;
							case Constants.modStockWheel:		//Warning for Multiple Wheels //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModWheel));
#endif
								ret = WeldingReturn.MultipleWheel;
								exist = false;
								break;
							case Constants.modStockFxLookAt:		//Warning for Multiple FxLookAt Constraint (wome with wheels) //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModFxLookAt));
#endif
								ret = WeldingReturn.MultipleFxLookAt;
								exist = false;
								break;
							case Constants.modStockFxPos:		//Warning for Multiple Constraint Position (wome with wheels) //TODO: Test
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModFxPos));
#endif
								ret = WeldingReturn.MultipleFxPos;
								exist = false;
								break;
							case Constants.modStockLaunchClamp:		//Warning for Multiple Launching Clamp (I don't even why would it be needed
#if (DEBUG)
								Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModLaunClamp));
#endif
								ret = WeldingReturn.MultipleLaunchClamp;
								exist = false;
								break;
							case Constants.modStockScienceExp:		// Warning for Multiple Science Experiments (.22)
								exist = string.Equals(existingNewModule.GetValue("experimentID"), newModule.GetValue("experimentID"));
								if (exist)
								{
#if (DEBUG)
									Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModScieExp));
#endif
									ret = WeldingReturn.MultipleScienceExp;
								}
								break;
							case Constants.modstockTransData:		// Merge transmition data (.22)
								float packetInterval = (float.Parse(newModule.GetValue("packetInterval")) + float.Parse(existingNewModule.GetValue("packetInterval"))) * 0.5f;
								float packetSize = (float.Parse(newModule.GetValue("packetSize")) + float.Parse(existingNewModule.GetValue("packetSize")));
								float packetResourceCost = (float.Parse(newModule.GetValue("packetResourceCost")) + float.Parse(existingNewModule.GetValue("packetResourceCost")));
								//TODO: requiredResource / DeployFxModules 

								existingNewModule.SetValue("packetInterval", packetInterval.ToString());
								existingNewModule.SetValue("packetSize", packetSize.ToString());
								existingNewModule.SetValue("packetResourceCost", packetResourceCost.ToString());
#if (DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModMerge, newModuleName));
#endif
								exist = true;
								break;
							case Constants.modStockLandingLegs:		// Waring Multiple same landing legs
								exist = string.Equals(existingNewModule.GetValue("animationName"), newModule.GetValue("animationName"));
								if (exist)
								{
#if (DEBUG)
									Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModLandLegs));
#endif
									ret = WeldingReturn.MultipleLandingLegs;
								}
								break;
							case Constants.modStockScienceCont:		// Merge Science Container (.22)
								bool evaOnlyStorage = bool.Parse(newModule.GetValue("evaOnlyStorage")) || bool.Parse(existingNewModule.GetValue("evaOnlyStorage"));
								float storageRange = (float.Parse(newModule.GetValue("storageRange")) + float.Parse(existingNewModule.GetValue("storageRange")));
								//TODO: requiredResource / DeployFxModules 

								existingNewModule.SetValue("evaOnlyStorage", evaOnlyStorage.ToString());
								existingNewModule.SetValue("storageRange", storageRange.ToString());
#if (DEBUG)
								Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModMerge, newModuleName));
#endif
								exist = true;
								break;
							default:
								{
									// New update module or mods! not managed
#if (DEBUG)
									Debug.LogWarning(string.Format("{0}{1}.. !{2}", Constants.logWarning, Constants.logPrefix, Constants.msgWarnModUnknown));
#endif
									ret = WeldingReturn.ModuleUnknown;
									exist = false;
									break;
								}
						}
					}
				}//foreach (ConfigNode existingNewModule in _modulelist)
				if (!exist)
				{
					switch (newModule.GetValue("name"))
					{
						//case Constants.modStockDecouple:
						//	{
						//		break;
						//	}
						case Constants.modStockAnchdec:
							{//Decoupler: Change node name
								string decouplename = newModule.GetValue("explosiveNodeID") + partname + _partNumber;
								newModule.SetValue("explosiveNodeID", decouplename);
								break;
							}
						case Constants.modStockDocking:
							{//Docking port: Change node name if any TODO: FIX This
								if (newModule.HasValue("referenceAttachNode"))
								{
									string dockname = newModule.GetValue("referenceAttachNode") + partname + _partNumber;
									newModule.SetValue("referenceAttachNode", dockname);
								}
								break;
							}
						case Constants.modStockJettison:
							{//Fairing/Jetisson, change node name
								string jetissonname = newModule.GetValue("bottomNodeName") + partname + _partNumber;
								newModule.SetValue("bottomNodeName", jetissonname);
								break;
							}
					}
					_modulelist.Add(newModule);
#if (DEBUG)
					Debug.Log(string.Format("{0}..{1}{2}", Constants.logPrefix, Constants.logModAdd, newModuleName));
#endif
				} //if (!exist)
			} //foreach (ConfigNode mod in modules)
			return ret;
		}

		/*
		 * Get the full ConfigNode
		 */
		public void CreateFullConfigNode()
		{
			FullConfigNode = new ConfigNode(_name);
			FullConfigNode.AddNode(Constants.weldPartNode);
			ConfigNode partconfig = FullConfigNode.GetNode(Constants.weldPartNode);
			// add name, module and author
			partconfig.AddValue("name", _name);
			partconfig.AddValue("module", _module);
			partconfig.AddValue("author", Constants.weldAuthor);

			//add model information
			foreach (ModelInfo model in _models)
			{
				ConfigNode node = new ConfigNode(Constants.weldModelNode);
				node.AddValue("model", model.url);
				node.AddValue("position", ConfigNode.WriteVector(WeldingHelpers.RoundVector3(model.position))); ;
				node.AddValue("scale", ConfigNode.WriteVector(WeldingHelpers.RoundVector3(model.scale)));
				node.AddValue("rotation", ConfigNode.WriteVector(WeldingHelpers.RoundVector3(model.rotation)));
				foreach (string tex in model.textures)
				{
					node.AddValue("texture", tex);
				}
				if (!string.IsNullOrEmpty(model.parent))
				{
					node.AddValue("parent", model.parent);
				}
				partconfig.AddNode(node);
			}

			//add rescale factor
			partconfig.AddValue("rescaleFactor", WeldingHelpers.RoundFloat(_rescaleFactor));

			//add PhysicsSignificance
			partconfig.AddValue("PhysicsSignificance", _physicsSignificance);

			//add nodes stack
			if (_attachNodes.Count() > 2)
			{
				float topmostMark = float.MinValue;
				float lowestMark = float.MaxValue;
				AttachNode topmostNode = _attachNodes[0];
				AttachNode lowestNode = _attachNodes[1];
				foreach (AttachNode node in _attachNodes)
				{
					if (node.position.y > topmostMark)
					{
						topmostMark = node.position.y;
						topmostNode = node;
					}
					if (node.position.y < lowestMark)
					{
						lowestMark = node.position.y;
						lowestNode = node;
					}
				}
				//				_attachNodes.Add(_attachNodes[0]);
				//				_attachNodes.Insert(_attachNodes.Count-1, _attachNodes[0]);
				_attachNodes.Add(topmostNode);
				_attachNodes.Add(lowestNode);
				_attachNodes.Remove(topmostNode);
				_attachNodes.Remove(lowestNode);
			}
			foreach (AttachNode node in _attachNodes)
			{
				//Make sure the orintation is an int
				Vector3 orientation = Vector3.zero;
				orientation.x = node.orientation.x;// (int)Mathf.FloorToInt(node.orientation.x + 0.5f);
				orientation.y = node.orientation.y;// (int)Mathf.FloorToInt(node.orientation.y + 0.5f);
				orientation.z = node.orientation.z;// (int)Mathf.FloorToInt(node.orientation.z + 0.5f);
				if (orientation == Vector3.zero)
				{
					orientation = Vector3.up;
				}
				orientation.Normalize();
				partconfig.AddValue(string.Format("node_stack_{0}", node.id), string.Format("{0},{1},{2}", ConfigNode.WriteVector(WeldingHelpers.RoundVector3(node.position)), ConfigNode.WriteVector(WeldingHelpers.RoundVector3(orientation)), node.size));
			}
			//add surface attach node
			partconfig.AddValue("node_attach", string.Format("{0},{1},{2}", ConfigNode.WriteVector(WeldingHelpers.RoundVector3(_srfAttachNode.originalPosition)), ConfigNode.WriteVector(WeldingHelpers.RoundVector3(_srfAttachNode.originalOrientation)), _srfAttachNode.size));

			//merge fx
			ConfigNode.Merge(partconfig, _fxData);
			partconfig.name = Constants.weldPartNode; //Because it get removed during the merge!?
			//Add CrewCapacity
			partconfig.AddValue("CrewCapacity", _crewCapacity);

			//Add R&D (.22)
			partconfig.AddValue("TechRequired", _techRequire);
			partconfig.AddValue("entryCost", _entryCost);

			//add cost
			partconfig.AddValue("cost", _cost);

			//add category
			partconfig.AddValue("category", _category.ToString());
			partconfig.AddValue("subcategory", _subcat);

			//add title desc and manufacturer
			partconfig.AddValue("title", _title);
			partconfig.AddValue("manufacturer", Constants.weldManufacturer);
			partconfig.AddValue("description", _description);

			//add attachement rules
			partconfig.AddValue("attachRules", _attachrules.String());

			//Add the mass
			partconfig.AddValue("mass", _mass);

			//add drag
			partconfig.AddValue("dragModelType", _dragModel);
			partconfig.AddValue("maximum_drag", WeldingHelpers.RoundFloat(_maximumDrag));
			partconfig.AddValue("minimum_drag", WeldingHelpers.RoundFloat(_minimumDrag));
			partconfig.AddValue("angularDrag", WeldingHelpers.RoundFloat(_angularDrag));

			//add crash and breaking data
			partconfig.AddValue("crashTolerance", WeldingHelpers.RoundFloat(_crashTolerance));
			partconfig.AddValue("breakingForce", WeldingHelpers.RoundFloat(_breakingForce));
			partconfig.AddValue("breakingTorque", WeldingHelpers.RoundFloat(_breakingTorque));
			partconfig.AddValue("maxTemp", WeldingHelpers.RoundFloat(_maxTemp));

			//add if crossfeed
			partconfig.AddValue("fuelCrossFeed", _fuelCrossFeed);

			//add RESOURCE
			foreach (ConfigNode res in _resourceslist)
			{
				partconfig.AddNode(res);
			}

			//add MODULE
			foreach (ConfigNode mod in _modulelist)
			{
				partconfig.AddNode(mod);
			}
		}

	} //class Welder
}