using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

namespace UbioWeldingLtd
{
	//UbioZurWeldingLtd Class
	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	public class UbioZurWeldingLtd : MonoBehaviour
	{

		enum DisplayState
		{
			none,
			weldError,
			weldWarning,
			infoWindow,
			savedWindow,
			overwriteDial,
            mainWindow,
			partSelection
		}

		public static UbioZurWeldingLtd instance { get; private set; }
		private Rect[] _guiInfoWindowColoumns = new Rect[4];
		private Rect _editorErrorDial;
		private Rect _editorWarningDial;
		private Rect _editorInfoWindow;
		private Rect _editorOverwriteDial;
		private Rect _editorSavedDial;
        private Rect _editorMainWindow;
		private Welder _welder;
		private DisplayState _state;
		private List<GUIContent> _catNames = new List<GUIContent>();
		private GUIDropdown _catDropdown;
		private GUIDropdown _techDropdown;
		private GUIStyle _guiStyle = new GUIStyle();
		private GUISkin _guiskin = HighLogic.Skin;
		private Vector2 _scrollRes;
		private Vector2 _scrollMod;
		private Vector2 _settingsScrollPosition = Vector2.zero;
		private Part _currentSelectedPartbranch;
		private Part _previousSelectedPartbranch;
		private Part _selectedPartbranch;
		private RaycastHit _hit;
		private Ray _ray;
		private EditorFacility _editorFacility;

		private AdvancedGUITextArea _textAreaDescription = new AdvancedGUITextArea();
		private AdvancedGUITextField _textFieldTitle = new AdvancedGUITextField();
		private AdvancedGUITextField _textFieldName = new AdvancedGUITextField();
		private WeldingConfiguration _config;
		private bool _guiVisible = false;
		private bool _mainWindowsSettingsMode = false;
		static public bool isReloading = false;
		private string filepath
		{
			get
			{
				if (_config.useNamedCfgFile)
				{
					return string.Format("{0}{1}/{2}/{3}.cfg", Constants.weldPartPath, _welder.Category.ToString(), _welder.Name, _welder.Name);
				}
				else
				{
					return string.Format("{0}{1}/{2}{3}", Constants.weldPartPath, _welder.Category.ToString(), _welder.Name, Constants.weldPartDefaultFile);
				}
			}
		}

		public Rect editorInfoWindow
		{
			get { return _editorInfoWindow; }
		}

		public GUISkin guiskin
		{
			get { return _guiskin; }
			}

		public GUIStyle guistyle
		{
			get { return _guiStyle; }
		}

		/// <summary>
		/// access to the config of the whole tool
		/// </summary>
		public WeldingConfiguration config
		{
			get { return _config; }
		}

		/*
		 * Called when plug in loaded
		 */
		public void Awake()
		{
			instance = this;
			Debug.Log(string.Format("{0}- {1} => Awake", Constants.logPrefix, instance.GetType()));
            Debug.Log(string.Format("{0} Platform is {1}", Constants.logPrefix, Application.platform));

			initConfig();
			_state = DisplayState.none;
			RenderingManager.AddToPostDrawQueue(0, OnDraw);
			_editorErrorDial = new Rect(Screen.width / 2 - Constants.guiDialogX, Screen.height / 2 - Constants.guiDialogY, Constants.guiDialogW, Constants.guiDialogH);
			_editorWarningDial = new Rect(Screen.width / 2 - Constants.guiDialogX, Screen.height / 2 - Constants.guiDialogY, Constants.guiDialogW, Constants.guiDialogH);
			_editorInfoWindow = new Rect(Screen.width / 2 - Constants.guiInfoWindowX, Screen.height / 2 - Constants.guiInfoWindowY, Constants.guiInfoWindowW, Constants.guiInfoWindowH);
			_editorOverwriteDial = new Rect(Screen.width / 2 - Constants.guiDialogX, Screen.height / 2 - Constants.guiDialogY, Constants.guiDialogW, Constants.guiDialogH);
			_editorSavedDial = new Rect(Screen.width / 2 - Constants.guiDialogX, Screen.height / 2 - Constants.guiDialogY, Constants.guiDialogW, Constants.guiDialogH);
            _editorMainWindow = new Rect(_config.MainWindowXPosition, _config.MainWindowYPosition, Constants.guiMainWindowW, Constants.guiMainWindowH);

			_catNames = WeldingHelpers.initPartCategories(_catNames);
			_guiStyle = WeldingHelpers.initGuiStyle(_guiStyle);
			_catDropdown = WeldingHelpers.initDropDown(_catNames, _guiStyle, _catDropdown);
			DatabaseHandler.initMMAssembly();
		}

		/// <summary>
		/// Welds the whole active craft in the scene in case the stocktoolbar is used
		/// </summary>
		public void stockToolbarButtonUsed()
		{
			if (!EditorLockManager.isEditorLocked())
			{
				if (EditorLogic.RootPart != null)
				{
					if (_state != DisplayState.mainWindow)
					{
						_state = DisplayState.mainWindow;
					}
					else
					{
						_state = DisplayState.none;
						disablePartHighlight(_selectedPartbranch);
						_selectedPartbranch = null;
					}
				}
			}
			//_stockToolbarButton.SetFalse();
		}

		/// <summary>
		/// Loads the config for the Welding or prepares default values and generates a new config
		/// </summary>
		private void initConfig()
		{
			KSP.IO.PluginConfiguration oldConfig = KSP.IO.PluginConfiguration.CreateForType<OldWeldingPluginConfig>();
			bool oldConfigFound = System.IO.File.Exists(string.Concat(Constants.settingRuntimeDirectory, Constants.settingXmlFilePath, Constants.settingXmlOldConfigFileName));
			if (oldConfigFound)
			{
				oldConfig = KSP.IO.PluginConfiguration.CreateForType<OldWeldingPluginConfig>();
				oldConfig.load();
				System.IO.File.Delete(string.Concat(Constants.settingRuntimeDirectory, Constants.settingXmlFilePath, Constants.settingXmlOldConfigFileName));
				Debug.Log(string.Format("{0}old configfile found and deleted", Constants.logPrefix));
			}

			if (!System.IO.File.Exists(string.Concat(Constants.settingRuntimeDirectory, Constants.settingXmlFilePath, Constants.settingXmlConfigFileName)))
			{
				_config = new WeldingConfiguration();
				FileManager.saveConfig(_config);
				_config.vector2CurveModules = Constants.basicVector2CurveModules;
				_config.vector4CurveModules = Constants.basicVector4CurveModules;
				_config.subModules = Constants.basicSubModules;
				_config.modulesToIgnore = Constants.basicModulesToIgnore;
				_config.averagedModuleAttributes = Constants.basicAveragedModuleAttributes;
				_config.unchangedModuleAttributes = Constants.basicUnchangedModuleAttributes;
				_config.breakingModuleAttributes = Constants.basicBreakingModuleAttributes;
			}
			else
			{
				_config = FileManager.loadConfig();
			}

			_config.dataBaseAutoReload = oldConfigFound ? oldConfig.GetValue<bool>(Constants.settingDbAutoReload) : _config.dataBaseAutoReload;
			_config.allowCareerMode = oldConfigFound ? oldConfig.GetValue<bool>(Constants.settingAllowCareer) : _config.allowCareerMode;
			Welder.includeAllNodes = oldConfigFound ? oldConfig.GetValue<bool>(Constants.settingAllNodes) : _config.includeAllNodes;
			Welder.dontProcessMasslessParts = oldConfigFound ? oldConfig.GetValue<bool>(Constants.settingDontProcessMasslessParts) : _config.dontProcessMasslessParts;

			Welder.StrengthCalcMethod = oldConfigFound ? StrengthParamsCalcMethod.ArithmeticMean : _config.StrengthCalcMethod;
			Welder.MaxTempCalcMethod = oldConfigFound ? MaxTempCalcMethod.ArithmeticMean : _config.MaxTempCalcMethod;
			Welder.runInTestMode = oldConfigFound ? false : _config.runInTestMode;
		}

		/// <summary>
		/// Prepares the Stocktoolbar with the Icon or enables the old style button
		/// </summary>
		private void initGUI()
		{
			if (null == EditorLogic.fetch || (!_config.allowCareerMode && HighLogic.fetch.currentGame.Mode == Game.Modes.CAREER))
			{
				_guiVisible = false;
			}
			else
			{
				if (_config.useStockToolbar)
				{
					if (EditorToolbar.instance != null)
					{
						EditorToolbar.instance.initToolbar();
					}
				}
				_guiVisible = true;
			}
		}


		/*
		 * Called once everything in scene is loaded
		 */
		public void Start()
		{
			initGUI();
			EditorLockManager.resetEditorLocks();
			_editorFacility = EditorDriver.editorFacility;
		}


		/// <summary>
		/// Unity default function for stuff that happens every frame
		/// </summary>
		public void Update()
		{
			if (_state == DisplayState.partSelection)
			{
				_ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				if (Physics.Raycast(_ray, out _hit))
				{
					_currentSelectedPartbranch = _hit.transform.gameObject.GetComponent<Part>() as Part;
					if (_previousSelectedPartbranch != null && _previousSelectedPartbranch != _currentSelectedPartbranch)
					{
						disablePartHighlight(_previousSelectedPartbranch);
					}
					enablePartHighlight(_currentSelectedPartbranch);
					_previousSelectedPartbranch = _currentSelectedPartbranch;
					if (Input.GetKeyUp(KeyCode.Mouse0) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
					{
						_selectedPartbranch = _currentSelectedPartbranch;
						_selectedPartbranch.SetHighlightType(Part.HighlightType.AlwaysOn);
						_currentSelectedPartbranch = null;
						_previousSelectedPartbranch = null;
						_state = DisplayState.mainWindow;
					}
				}
			}
			else if (_state != DisplayState.none)
			{
				if (_selectedPartbranch != null)
				{
					enablePartHighlight(_selectedPartbranch);
				}
			}
		}

		/// <summary>
		/// highlights the part at mouseover
		/// </summary>
		/// <param name="part"></param>
		private void enablePartHighlight(Part part)
		{
			if (part != null)
			{
				part.SetHighlightType(Part.HighlightType.OnMouseOver);
				part.SetHighlightColor(Color.magenta);
				part.SetHighlight(true, true);
			}
		}

		private void disablePartHighlight(Part part)
		{
			if (part != null)
			{
				part.SetHighlightDefault();
			}
		}


		/// <summary>
		/// Public Eventcall at the GuiDraw
		/// </summary>
		private void OnDraw()
		{
			if (_guiVisible)
			{
				GUI.skin = _guiskin;

				switch (_state)
				{
					case DisplayState.none :
						EditorLockManager.unlockEditor(Constants.settingPreventClickThroughLock);
						EditorLockManager.unlockEditor(Constants.settingWeldingLock);
						break;
					case DisplayState.weldError :
                        _editorErrorDial = GUILayout.Window((int)_state, _editorErrorDial, OnErrorDisplay, Constants.weldManufacturer);
						break;
					case DisplayState.weldWarning :
                        _editorWarningDial = GUILayout.Window((int)_state, _editorWarningDial, OnWarningDisplay, Constants.weldManufacturer);
						break;
					case DisplayState.infoWindow :
						_editorInfoWindow = GUI.Window((int)_state, _editorInfoWindow, OnInfoWindow, Constants.weldManufacturer);
						PreventClickThrough(_editorInfoWindow);
						break;
					case DisplayState.savedWindow :
                        _editorSavedDial = GUILayout.Window((int)_state, _editorSavedDial, OnSavedDisplay, Constants.weldManufacturer);
						break;
					case DisplayState.overwriteDial :
                        _editorOverwriteDial = GUILayout.Window((int)_state, _editorOverwriteDial, OnOverwriteDisplay, Constants.weldManufacturer);
						break;
                    case DisplayState.mainWindow :
						_editorMainWindow = GUI.Window((int)_state, _editorMainWindow, OnMainWindow, Constants.weldManufacturer);
						PreventClickThrough(_editorMainWindow);
                        break;
					case DisplayState.partSelection:
						ScreenMessages.PostScreenMessage(Constants.guiScreenMessagePartSelection, Time.deltaTime, ScreenMessageStyle.UPPER_CENTER);
						break;
				}
			} //if (_guiVisible)
		} //private void OnDraw()


		private void weldPart(Part partToWeld)
		{
			//Lock editor
			EditorLockManager.lockEditor(Constants.settingWeldingLock);

			//process the welding
#if (DEBUG)
			Debug.ClearDeveloperConsole();

			Debug.Log(string.Format("{0}{1}", Constants.logPrefix, Constants.logVersion));
			Debug.Log(string.Format("{0}{1}", Constants.logPrefix, Constants.logStartWeld));

#endif
			bool warning = false;
			_welder = new Welder(_config.advancedDebug);

			partToWeld.transform.eulerAngles = Vector3.zero;
			WeldingReturn ret = 0;

			if (!WeldingHelpers.DoesTextContainRegex(partToWeld.name, "strutConnector"))
			{
				ret = _welder.weldThisPart(partToWeld);
			}

			if (ret < 0)
			{
#if (DEBUG)
				Debug.Log(string.Format("{0}{1}", Constants.logPrefix, Constants.logEndWeld));
#endif
				_state = DisplayState.weldError;
				return;
			}
			else
			{
				warning = warning || (0 < ret);
			}

			Part[] children = partToWeld.FindChildParts<Part>(true);

			if (children != null)
			{
				foreach (Part child in children)
				{
					if (!WeldingHelpers.DoesTextContainRegex(child.name, "strutConnector"))
					{
						ret = _welder.weldThisPart(child);
					}

					if (ret< 0)
					{
#if (DEBUG)
						Debug.Log(string.Format("{0}{1}", Constants.logPrefix, Constants.logEndWeld));
#endif
						_state = DisplayState.weldError;
						return;
					}
					else
					{
						warning = warning || (0 < ret);
					}
				}
			}
			_welder.processNewCoM();

			_techDropdown = WeldingHelpers.initTechDropDown(_welder.techList, _guiStyle, _techDropdown);

			_scrollMod = Vector2.zero;
			_scrollRes = Vector2.zero;
#if (DEBUG)
			Debug.Log(string.Format("{0} {1} | {2} Parts welded", Constants.logPrefix, Constants.logEndWeld, _welder.NbParts));
#endif
			if (warning)
			{
				Debug.Log(string.Format("{0} {1} | Warning", Constants.logPrefix, Constants.logEndWeld));
				_state = DisplayState.weldWarning;
			}
			else
			{
#if (DEBUG)
				Debug.Log(string.Format("{0} welder.Category: {1}", Constants.logPrefix, (int)_welder.Category));
#endif
//PartCategories.none (has value of "-1") has no corresponding element in dropdown list so it must be replaced with something else
				_catDropdown.SelectedItemIndex = (_welder.Category != PartCategories.none) ? (int)_welder.Category : (int)PartCategories.Structural;
				_state = DisplayState.infoWindow;
			}
		}

		/*
		 * Main window
		 */
        private void OnMainWindow(int windowID)
        {
			GUIStyle _settingsToggleGroupStyle = new GUIStyle(GUI.skin.toggle);
			_settingsToggleGroupStyle.margin.left += 40;

			//save Window Position
			_config.MainWindowXPosition = (int)_editorMainWindow.xMin;
			_config.MainWindowYPosition = (int)_editorMainWindow.yMin;

			GUILayout.BeginVertical();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();

			if (GUILayout.Button(new GUIContent("Settings", "Show/hide settings"), GUILayout.MaxWidth(160)))
			{
				_mainWindowsSettingsMode = !_mainWindowsSettingsMode;
			}
			//Settings
			if (_mainWindowsSettingsMode)
			{
				_editorMainWindow.height = Constants.guiMainWindowHSettingsExpanded;
				_settingsScrollPosition = GUILayout.BeginScrollView(_settingsScrollPosition);
				_config.includeAllNodes = GUILayout.Toggle(_config.includeAllNodes, Constants.guiAllNodesGUIContent);
				_config.dontProcessMasslessParts = GUILayout.Toggle(_config.dontProcessMasslessParts, Constants.guiDontProcessMasslessPartsGUIContent);
				_config.dataBaseAutoReload = GUILayout.Toggle(_config.dataBaseAutoReload, Constants.guiDbAutoReloadGUIContent);
				_config.useNamedCfgFile = GUILayout.Toggle(_config.useNamedCfgFile, Constants.guiUseNamedCfgFileGUIContent);
				_config.advancedDebug = GUILayout.Toggle(_config.advancedDebug, Constants.guiAdvancedDebugGUIContent);
				_config.clearEditor = GUILayout.Toggle(_config.clearEditor, Constants.guiClearEditorGUIContent);
				GUILayout.Space(10.0f);
				GUILayout.Label("Strength params calculation method");
//				_config.StrengthCalcMethod = (StrengthParamsCalcMethod)GUILayout.SelectionGrid((int)_config.StrengthCalcMethod, Constants.StrengthParamsCalcMethodsGUIContent, 1, GUILayout.MaxWidth(140));
				foreach (StrengthParamsCalcMethod Method in Enum.GetValues(typeof(StrengthParamsCalcMethod)))
				{
					if (GUILayout.Toggle((_config.StrengthCalcMethod == Method), Constants.StrengthParamsCalcMethodsGUIContent[(int)Method], _settingsToggleGroupStyle))
					{
						_config.StrengthCalcMethod = Method;
					}
				}
				GUILayout.Space(10.0f);
				GUILayout.Label("MaxTemp calculation method");
//				_config.MaxTempCalcMethod = (MaxTempCalcMethod)GUILayout.SelectionGrid((int)_config.MaxTempCalcMethod, Constants.MaxTempCalcMethodsGUIContent, 1, GUILayout.MaxWidth(140));
				foreach (MaxTempCalcMethod Method in Enum.GetValues(typeof(MaxTempCalcMethod)))
				{
					if (GUILayout.Toggle((_config.MaxTempCalcMethod == Method), Constants.MaxTempCalcMethodsGUIContent[(int)Method], _settingsToggleGroupStyle))
					{
						_config.MaxTempCalcMethod = Method;
					}
				}
				GUILayout.EndScrollView();

//				GUILayout.Space(10.0f);
				if (GUILayout.Button(Constants.guiSaveSettingsButtonGUIContent, GUILayout.MaxWidth(160)))
				{
					FileManager.saveConfig(_config);
				}
			}
			else
			{
				_editorMainWindow.height = Constants.guiMainWindowH;
				GUILayout.Space(20.0f);
			}

			//SelectPArtbranch button
			if (GUILayout.RepeatButton(Constants.guiSelectPartGUIContent, GUILayout.MaxWidth(160)))
			{
				_state = DisplayState.partSelection;
			}

			//Weld button
			if (GUILayout.Button(Constants.guiWeldItButtonGUIContent, GUILayout.MaxWidth(160)))
			{
				FileManager.saveConfig(_config);

				if (EditorLockManager.isEditorLocked())
				{
					if (_selectedPartbranch == null)
					{
						_selectedPartbranch = EditorLogic.RootPart;
					}
					weldPart(_selectedPartbranch);
				}
			}
			//Hints area
			GUILayout.TextArea(GUI.tooltip, GUILayout.ExpandHeight(true), GUILayout.MaxHeight(60));
			GUIStyle VersionLabelGUIStyle = new GUIStyle(GUI.skin.label);
			VersionLabelGUIStyle.fontSize = 12;
			GUILayout.Label(Constants.logVersion, VersionLabelGUIStyle);
            GUILayout.EndVertical();

			GUI.DragWindow();
        } //private void OnMainWindow()

		/*
		 * Error Message
		 */
		private void OnErrorDisplay(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.Label(Constants.guiDialFail);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(Constants.guiOK))
			{
				EditorLockManager.unlockEditor(Constants.settingWeldingLock);
				_state = DisplayState.none;
			}
			GUILayout.EndVertical();

			GUI.DragWindow();
		} //private void OnErrorDisplay()

		/*
		 * Warning Message
		 */
		private void OnWarningDisplay(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.Label(Constants.guiDialWarn);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(Constants.guiOK))
			{
				_state = DisplayState.infoWindow;
			}
			GUILayout.EndVertical();

			GUI.DragWindow();
		} //private void OnErrorDisplay()

		/*
		 * Overwrite Message
		 */
		private void OnOverwriteDisplay(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			GUILayout.Label(Constants.guiDialOverwrite);
			GUILayout.Label(filepath);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(Constants.guiOK))
			{
				WriteCfg(filepath);
				_state = DisplayState.savedWindow;
			}
			if (GUILayout.Button(Constants.guiCancel))
			{
				_state = DisplayState.infoWindow;
			}
			GUILayout.EndVertical();

			GUI.DragWindow();
		} //private void OnErrorDisplay()

		/*
		 * Saved Message
		 */
		private void OnSavedDisplay(int windowID)
		{
			bool MMPathLoaderIsReady = DatabaseHandler.isModuleManagerInstalled ? (bool)DatabaseHandler.DynaInvokeMMPatchLoaderMethod("IsReady") : false;
			GUILayout.BeginVertical();
			if (DatabaseHandler.isReloading)
			{
				ScreenMessages.PostScreenMessage(string.Concat(Constants.guiDBReloading1, "\n", Constants.guiDBReloading2), Time.deltaTime, ScreenMessageStyle.UPPER_CENTER);
				GUILayout.Label(Constants.guiDBReloading1);
				GUILayout.Label(Constants.guiDBReloading2);
				if (!MMPathLoaderIsReady)
				{
					GUILayout.Label(String.Format("ModuleManager progress: {0:P0}", (float)DatabaseHandler.DynaInvokeMMPatchLoaderMethod("ProgressFraction")));
//					GUILayout.Label(String.Format("{0}", (string)DatabaseHandler.DynaInvokeMMPatchLoaderMethod("ProgressTitle")));
				}
			}
			else
			{
				GUILayout.Label(Constants.guiDialSaved);
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(Constants.guiOK))
				{
					_state = DisplayState.none;
					ClearEditor();
				}
			}
			GUILayout.EndVertical();

			GUI.DragWindow();
		} //private void OnErrorDisplay()


		/// <summary>
		/// Darws the Info window where the new saved partfile can be configured
		/// </summary>
		/// <param name="windowID"></param>
		void OnInfoWindow(int windowID)
		{
			float margin = 5f;
			float height = 20;
			float posH = height;
			float columnWidth = (Constants.guiInfoWindowW - (margin * 5)) / 4;
			float columnHeight = (Constants.guiInfoWindowH - ((height + margin) * 2));
			float scrollwidth = columnWidth - 20.0f;
			GUIStyle style = new GUIStyle();

			for (int i = 0; i < 4; i++)
			{
				_guiInfoWindowColoumns[i] = new Rect((margin * (i + 1)) + (columnWidth * i), height, columnWidth, columnHeight);
				posH = height;
				switch (i)
				{
					case 0:
						{
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), "Name:");
							posH += height + margin;
							//_welder.Name = _textFieldTitle.DrawAdvancedGUITextField(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), _welder.Name, 100, (int)_state);
							_welder.Name = GUI.TextField(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), _welder.Name, 100);
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), "Title:");
							posH += height + margin;
							//_welder.Title = _textFieldTitle.DrawAdvancedGUITextField(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), _welder.Title, 100, (int)_state);
							_welder.Title = GUI.TextField(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), _welder.Title, 100);
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), "Description:");
							posH += height + margin;
							//_welder.Description = _textAreaDescription.DrawAdvancedGUITextArea(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, 7 * height + 6 * margin), _welder.Description, 600, (int)_state);
							_welder.Description = GUI.TextArea(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, 7 * height + 6 * margin), _welder.Description, 600);
						}
						break;
					case 1:
						{
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), "Category:");
							posH += height + margin;
							Rect _cetegoryBox = new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height);
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), "RequiredTech:");
							posH += height + margin;
							Rect _techBox = new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height);
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), string.Format("Nb Parts: {0}", _welder.NbParts));
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), string.Format("Cost: {0:F2}", _welder.Cost));
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), string.Format("Mass: {0:F3} / {1:F3}", _welder.Mass, _welder.WetMass));
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), string.Format("Temp: {0:F1}", _welder.MaxTemp));
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), string.Format("B Force: {0:F3}", _welder.BreakingForce));
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), string.Format("B Torque: {0:F3}", _welder.BreakingTorque));
							posH += height + margin;
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), string.Format("Drag: {0:F3} / {1:F3}", _welder.MinDrag, _welder.MaxDrag));

							if (_catDropdown.IsOpen)
							{
								GUI.Box(_techBox, _welder.techRequire);
							}
							else
							{
								_welder.techRequire = _welder.techList[_techDropdown.Show(_techBox)];
							}
							_catDropdown.SelectedItemIndex = (int)_welder.Category;
							_welder.Category = (PartCategories)_catDropdown.Show(_cetegoryBox);

						}
						break;
					case 2:
						{
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), "Modules:");
							posH += height + margin;
							string[] modulenames = _welder.Modules;
							_scrollMod = GUI.BeginScrollView(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, (height * 11 + margin * 10)), _scrollMod, new Rect(0, 0, scrollwidth, modulenames.Length > 11 ? 270 + (modulenames.Length - 11) * (height + margin) : 270), false, true);
							style.wordWrap = false;
							style.normal.textColor = Color.white;
							posH = 0;
							foreach (string modulename in modulenames)
							{
								GUI.Label(new Rect(2, posH, scrollwidth, height), modulename, style);
								posH += height + margin;
							}
							GUI.EndScrollView();
						}
						break;
					case 3:
						{
							GUI.Label(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, height), "Resources:");
							posH += height + margin;
							string[] resourcesdata = _welder.Resources;
							_scrollRes = GUI.BeginScrollView(new Rect(_guiInfoWindowColoumns[i].x, posH, columnWidth, (height * 11 + margin * 10)), _scrollRes, new Rect(0, 0, scrollwidth, resourcesdata.Length > 11 ? 270 + (resourcesdata.Length - 11) * (height + margin) : 270), false, true);
							style.wordWrap = false;
							style.normal.textColor = Color.white;
							posH = 0;
							foreach (string resname in resourcesdata)
							{
								GUI.Label(new Rect(2, posH, scrollwidth, height), resname, style);
								posH += height + margin;
							}
							GUI.EndScrollView();
						}
						break;
					default:
						{
							GUI.Label(new Rect(0, posH, columnWidth, height), "Broken GUI Column:");
						}
						break;
				}
			}

			bool nameOk = WelderNameNotUsed();
			if (!nameOk)
			{
				style.normal.textColor = Color.red;
				GUI.Label(new Rect(margin, height + columnHeight + margin, columnWidth * 1.5f, height), Constants.guiNameUsed, style);
			}
			if (!string.IsNullOrEmpty(_welder.Name))
			{
				if (GUI.Button(new Rect(_guiInfoWindowColoumns[1].x + columnWidth * 0.5f, height + columnHeight + margin, columnWidth * 0.5f, height), Constants.guiSave))
				{
					//check if the file exist
					string dirpath = string.Format("{0}{1}/{2}", Constants.weldPartPath, _welder.Category.ToString(), _welder.Name);
					if (!System.IO.File.Exists(filepath))
					{
						if (!Directory.Exists(dirpath))
						{
							Directory.CreateDirectory(dirpath);
						}
						//create the file
						StreamWriter partfile = System.IO.File.CreateText(filepath);
						partfile.Close();
						WriteCfg(filepath);
						_state = DisplayState.savedWindow;
					}
					else
					{
						_state = DisplayState.overwriteDial;
					}
				}
			}
			if (GUI.Button(new Rect(_guiInfoWindowColoumns[2].x, height + columnHeight + margin, columnWidth * 0.5f, height), Constants.guiCancel))
			{
				_state = DisplayState.none;
				ClearEditor();
			}
			GUI.DragWindow();
		}


		/*
		 * Test if the name is not already in use
		 */
		private bool WelderNameNotUsed()
		{
			foreach (UrlDir.UrlConfig part in GameDatabase.Instance.GetConfigs(Constants.weldPartNode))
			{
				if ( string.Equals(part.name, _welder.Name) )
				{
					return false;
				}
			}
			return true;
		}

		/*
		 * Writing the cfg File
		 */
		private void WriteCfg( string filepath)
		{
#if (DEBUG)
			Debug.Log(string.Format("{0}{1}{2}", Constants.logPrefix, Constants.logWritingFile, filepath));
#endif

			_welder.CreateFullConfigNode();
			_welder.FullConfigNode.Save(filepath);

			if (_config.dataBaseAutoReload)
			{
				StartCoroutine(DatabaseHandler.DatabaseReloadWithMM());
			}
		}

		/*
		 * Free and clear the editor
		 */
		private void ClearEditor()
		{
			if (_config.clearEditor)
			{
				EditorLockManager.resetEditorLocks();
				EditorPartList.Instance.Refresh();
				if (_selectedPartbranch != null)
				{
					disablePartHighlight(_selectedPartbranch);
					EditorLogic.fetch.OnSubassemblyDialogDismiss(EditorLogic.RootPart);
					Debug.Log(string.Format("{0}{1} {2} - {3}", Constants.logPrefix, _config.clearEditor, _selectedPartbranch, EditorLogic.SelectedPart));
					EditorLogic.DeletePart(EditorLogic.RootPart);
					_selectedPartbranch = null;
				}
			}
		}

		/*
		 * Lock editor if mouse pointer is inside window rect
		 */
		private void PreventClickThrough(Rect rect)
		{
			Vector2 pointerPos = Mouse.screenPos;
			//            if (rect.Contains(pointerPos) && !EditorLogic.softLock)
			if (rect.Contains(pointerPos))
			{
				EditorLockManager.lockEditor(Constants.settingPreventClickThroughLock);
			}
			//            else if (!rect.Contains(pointerPos) && EditorLogic.softLock)
			else if (!rect.Contains(pointerPos))
			{
				EditorLockManager.unlockEditor(Constants.settingPreventClickThroughLock);
			}
		}
	} //public class UbioZurWeldingLtd : MonoBehaviour

} //namespace UbioWeldingLtd
