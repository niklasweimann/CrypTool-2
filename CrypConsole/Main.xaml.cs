﻿/*
   Copyright 2020 Nils Kopal <kopal<AT>CrypTool.org>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using CrypTool.PluginBase;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using WorkspaceManager.Execution;
using WorkspaceManager.Model;
using Path = System.IO.Path;

namespace CrypTool.CrypConsole
{
    public partial class Main : Window
    {
        private static readonly string[] subfolders = new string[]
        {
            "",
            "CrypPlugins",
            "Lib",
        };

        private bool _verbose = false;
        private int _timeout = int.MaxValue;
        private TerminationType _terminationType = TerminationType.GlobalProgress;
        private readonly Dictionary<IPlugin, double> _pluginProgressValues = new Dictionary<IPlugin, double>();
        private readonly Dictionary<IPlugin, string> _pluginNames = new Dictionary<IPlugin, string>();
        private WorkspaceModel _workspaceModel = null;
        private ExecutionEngine _engine = null;
        private int _globalProgress;
        private DateTime _startTime;
        private readonly object _progressLockObject = new object();
        private bool _jsonoutput = false;
        private NotificationLevel _loglevel = NotificationLevel.Warning;

        /// <summary>
        /// Constructor
        /// </summary>
        public Main()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called, after "ui" is initialized. From this point, we should have a running ui thread
        /// Thus, we start the execution of the CrypConsole
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Main_Initialized(object sender, EventArgs e)
        {
            Start(CrypConsole.Args);
        }

        /// <summary>
        /// Starts the execution of the defined workspace
        /// 1) Parses the commandline parameters
        /// 2) Creates CT2 model and execution engine
        /// 3) Starts execution
        /// 4) Gives data as defined by user to the model
        /// 5) Retrieves results for output and outputs these
        /// 6) [terminates]
        /// </summary>
        /// <param name="args"></param>
        public void Start(string[] args)
        {
            _startTime = DateTime.Now;

            //Step 0: Set locale to English
            CultureInfo cultureInfo = new CultureInfo("en-us", false);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;

            //Step 1: Check, if Help needed
            if (ArgsHelper.GetShowHelp(args))
            {
                Environment.Exit(0);
            }

            //Step 2: Get cwm_file to open
            string cwm_file = ArgsHelper.GetCWMFileName(args);
            if (cwm_file == null)
            {
                Console.WriteLine("Please specify a cwm file using -cwm=filename");
                Environment.Exit(-1);
            }
            if (!File.Exists(cwm_file))
            {
                Console.WriteLine("Specified cwm file \"{0}\" does not exist", cwm_file);
                Environment.Exit(-2);
            }

            //Step 3: Get additional parameters
            _verbose = ArgsHelper.CheckVerboseMode(args);
            try
            {
                _timeout = ArgsHelper.GetTimeout(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(-2);
            }
            try
            {
                _loglevel = ArgsHelper.GetLoglevel(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(-2);
            }
            try
            {
                _terminationType = ArgsHelper.GetTerminationType(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(-2);
            }
            try
            {
                _jsonoutput = ArgsHelper.CheckJsonOutput(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(-2);
            }

            //Step 4: Check, if discover mode was selected
            if (ArgsHelper.CheckDiscoverMode(args))
            {
                DiscoverCWMFile(cwm_file);
                Environment.Exit(0);
            }

            //Step 5: Get input parameters
            List<Parameter> inputParameters = null;
            try
            {
                inputParameters = ArgsHelper.GetInputParameters(args);
                if (_verbose)
                {
                    foreach (Parameter param in inputParameters)
                    {
                        Console.WriteLine("Input parameter given: " + param);
                    }
                }
            }
            catch (InvalidParameterException ipex)
            {
                Console.WriteLine(ipex.Message);
                Environment.Exit(-3);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured while parsing parameters: {0}", ex.Message);
                Environment.Exit(-3);
            }

            //Step 6: Get output parameters
            List<Parameter> outputParameters = null;
            try
            {
                outputParameters = ArgsHelper.GetOutputParameters(args);
                if (_verbose)
                {
                    foreach (Parameter param in inputParameters)
                    {
                        Console.WriteLine("Output parameter given: " + param);
                    }
                }
            }
            catch (InvalidParameterException ipex)
            {
                Console.WriteLine(ipex.Message);
                Environment.Exit(-3);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured while parsing parameters: {0}", ex.Message);
                Environment.Exit(-3);
            }

            //Step 7: Update application domain. This allows loading additional .net assemblies
            try
            {
                UpdateAppDomain();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured while updating AppDomain: {0}", ex.Message);
                Environment.Exit(-4);
            }

            //Step 8: Load cwm file and create model            
            try
            {
                ModelPersistance modelPersistance = new ModelPersistance();
                _workspaceModel = modelPersistance.loadModel(cwm_file, true);

                foreach (PluginModel pluginModel in _workspaceModel.GetAllPluginModels())
                {
                    pluginModel.Plugin.OnGuiLogNotificationOccured += OnGuiLogNotificationOccured;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured while loading model from cwm file: {0}", ex.Message);
                Environment.Exit(-5);
            }

            //Step 9: Set input parameters
            foreach (Parameter param in inputParameters)
            {
                string name = param.Name;
                bool found = false;
                foreach (PluginModel component in _workspaceModel.GetAllPluginModels())
                {
                    //we also memorize here the name of each plugin
                    if (!_pluginNames.ContainsKey(component.Plugin))
                    {
                        _pluginNames.Add(component.Plugin, component.GetName());
                    }

                    if (component.GetName().ToLower().Equals(param.Name.ToLower()))
                    {
                        if (component.PluginType.FullName.Equals("CrypTool.TextInput.TextInput"))
                        {
                            ISettings settings = component.Plugin.Settings;
                            PropertyInfo textProperty = settings.GetType().GetProperty("Text");

                            if (param.ParameterType == ParameterType.Text)
                            {
                                textProperty.SetValue(settings, param.Value);
                            }
                            else if (param.ParameterType == ParameterType.File)
                            {
                                try
                                {
                                    if (!File.Exists(param.Value))
                                    {
                                        Console.WriteLine("Input file does not exist: {0}", param.Value);
                                        Environment.Exit(-7);
                                    }
                                    string value = File.ReadAllText(param.Value);
                                    textProperty.SetValue(settings, value);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Exception occured while reading file {0}: {0}", param.Value, ex.Message);
                                    Environment.Exit(-7);
                                }
                            }
                            //we need to call initialize to get the new text to the ui of the TextInput component
                            //otherwise, it will output the value retrieved by deserialization
                            component.Plugin.Initialize();
                            found = true;
                        }
                        else if (component.PluginType.FullName.Equals("CrypTool.Plugins.Numbers.NumberInput"))
                        {
                            ISettings settings = component.Plugin.Settings;
                            PropertyInfo textProperty = settings.GetType().GetProperty("Number");

                            if (param.ParameterType == ParameterType.Number)
                            {
                                textProperty.SetValue(settings, param.Value);
                            }
                            //we need to call initialize to get the new text to the ui of the TextInput component
                            //otherwise, it will output the value retrieved by deserialization
                            component.Plugin.Initialize();
                            found = true;
                        }
                    }
                }
                if (!found)
                {
                    Console.WriteLine("Component for setting input parameter not found: {0}", param);
                    Environment.Exit(-7);
                }
            }

            //Step 10: Set output parameters
            foreach (Parameter param in outputParameters)
            {
                string name = param.Name;
                bool found = false;
                foreach (PluginModel component in _workspaceModel.GetAllPluginModels())
                {
                    if (component.GetName().ToLower().Equals(param.Name.ToLower()))
                    {
                        if (component.PluginType.FullName.Equals("TextOutput.TextOutput"))
                        {
                            component.Plugin.PropertyChanged += Plugin_PropertyChanged;
                            found = true;
                        }
                    }
                }
                if (!found)
                {
                    Console.WriteLine("TextOutput for setting output parameter not found: {0}", param);
                    Environment.Exit(-7);
                }
            }

            //Step 11: add OnPluginProgressChanged handlers
            foreach (PluginModel plugin in _workspaceModel.GetAllPluginModels())
            {
                plugin.Plugin.OnPluginProgressChanged += OnPluginProgressChanged;
            }

            //Step 12: Create execution engine            
            try
            {
                _engine = new ExecutionEngine(null);
                _engine.OnGuiLogNotificationOccured += OnGuiLogNotificationOccured;
                _engine.Execute(_workspaceModel, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured while executing model: {0}", ex.Message);
                Environment.Exit(-7);
            }

            //Step 13: Start execution in a dedicated thread
            DateTime endTime = DateTime.Now.AddSeconds(_timeout);
            Thread t = new Thread(() =>
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-Us", false);
                while (_engine.IsRunning())
                {
                    Thread.Sleep(100);
                    if (_engine.IsRunning() && _timeout < int.MaxValue && DateTime.Now >= endTime)
                    {
                        Console.WriteLine("Timeout ({0} seconds) reached. Kill process hard now", _timeout);
                        Environment.Exit(-8);
                    }
                }
                if (_verbose)
                {
                    Console.WriteLine("Execution engine stopped. Terminate now");
                    Console.WriteLine("Total execution took: {0}", DateTime.Now - _startTime);
                }
                Environment.Exit(0);
            });
            t.Start();
        }

        /// <summary>
        /// This method analyses a given cwm file and returns all parameters
        /// </summary>
        /// <param name="cwm_file"></param>
        private void DiscoverCWMFile(string cwm_file)
        {
            try
            {
                UpdateAppDomain();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured while updating AppDomain: {0}", ex.Message);
                Environment.Exit(-4);
            }

            ModelPersistance modelPersistance = new ModelPersistance();
            try
            {
                _workspaceModel = modelPersistance.loadModel(cwm_file, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured during loading of cwm file: {0}", ex.Message);
                Environment.Exit(0);
            }
            DiscoverWorkspaceModel(cwm_file);
        }

        private void DiscoverWorkspaceModel(string cwm_file)
        {
            if (_jsonoutput)
            {
                Console.Write("{\"components\":[");
            }
            else
            {
                Console.WriteLine("Discovery of cwm_file \"{0}\"", cwm_file);
                Console.WriteLine();
            }
            int counter = 0;
            System.Collections.ObjectModel.ReadOnlyCollection<PluginModel> allPluginModels = _workspaceModel.GetAllPluginModels();
            foreach (PluginModel pluginModel in allPluginModels)
            {
                counter++;
                if (!_jsonoutput)
                {
                    Console.WriteLine("\"{0}\" (\"{1}\")", pluginModel.GetName(), pluginModel.Plugin.GetType().FullName);
                }

                System.Collections.ObjectModel.ReadOnlyCollection<ConnectorModel> inputs = pluginModel.GetInputConnectors();
                System.Collections.ObjectModel.ReadOnlyCollection<ConnectorModel> outputs = pluginModel.GetOutputConnectors();
                ISettings settings = pluginModel.Plugin.Settings;
                TaskPaneAttribute[] taskPaneAttributes = settings.GetSettingsProperties(pluginModel.Plugin);

                if (_jsonoutput)
                {
                    Console.Write("{0}", JsonHelper.GetPluginDiscoveryString(pluginModel, inputs, outputs, taskPaneAttributes));
                    if (counter < allPluginModels.Count)
                    {
                        Console.Write(",");
                    }
                    continue;
                }
                if (inputs.Count > 0)
                {
                    Console.WriteLine("- Input connectors:");
                    foreach (ConnectorModel input in inputs)
                    {
                        Console.WriteLine("-- \"{0}\" (\"{1}\")", input.GetName(), input.ConnectorType.FullName);
                    }
                }
                if (outputs.Count > 0)
                {
                    Console.WriteLine("- Output connectors:");
                    foreach (ConnectorModel output in outputs)
                    {
                        Console.WriteLine("-- \"{0}\" (\"{1}\")", output.GetName(), output.ConnectorType.FullName);
                    }
                }
                if (taskPaneAttributes != null && taskPaneAttributes.Length > 0)
                {
                    Console.WriteLine("- Settings:");
                    foreach (TaskPaneAttribute taskPaneAttribute in taskPaneAttributes)
                    {
                        Console.WriteLine("-- \"{0}\" (\"{1}\")", taskPaneAttribute.PropertyName, taskPaneAttribute.PropertyInfo.PropertyType.FullName);
                    }
                }
                Console.WriteLine();
            }
            if (_jsonoutput)
            {
                Console.Write("]}");
            }
        }

        /// <summary>
        /// Called, when progress on a single plugin changed        
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnPluginProgressChanged(IPlugin sender, PluginProgressEventArgs args)
        {
            if (_terminationType == TerminationType.GlobalProgress)
            {
                HandleGlobalProgress(sender, args);
            }
            if (_terminationType == TerminationType.PluginProgress)
            {

            }
        }

        /// <summary>
        /// Handles the global progress
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleGlobalProgress(IPlugin sender, PluginProgressEventArgs args)
        {
            lock (_progressLockObject)
            {
                if (!_pluginProgressValues.ContainsKey(sender))
                {
                    _pluginProgressValues.Add(sender, args.Value / args.Max);
                }
                else
                {
                    _pluginProgressValues[sender] = args.Value / args.Max;
                }
                double numberOfPlugins = _workspaceModel.GetAllPluginModels().Count;
                double totalProgress = 0;
                foreach (double value in _pluginProgressValues.Values)
                {
                    totalProgress += value;
                }
                if (totalProgress == numberOfPlugins && _engine.IsRunning())
                {
                    if (_verbose)
                    {
                        Console.WriteLine("Global progress reached 100%, stop execution engine now");
                    }
                    _engine.Stop();
                }
                int newProgress = (int)((totalProgress / numberOfPlugins) * 100);
                if (_globalProgress < newProgress)
                {
                    _globalProgress = newProgress;
                    if (_verbose)
                    {
                        Console.WriteLine("Global progress change: {0}%", _globalProgress);
                    }
                    if (_jsonoutput)
                    {
                        Console.WriteLine(JsonHelper.GetProgressJson(_globalProgress));
                    }
                }
            }
        }

        /// <summary>
        /// Property changed on plugin
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pceargs"></param>
        private void Plugin_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs pceargs)
        {
            IPlugin plugin = (IPlugin)sender;
            PropertyInfo property = sender.GetType().GetProperty(pceargs.PropertyName);
            if (!property.Name.ToLower().Equals("input"))
            {
                return;
            }
            if (_verbose)
            {
                Console.WriteLine("Output:" + property.GetValue(plugin).ToString());
            }
            if (_jsonoutput)
            {
                object value = property.GetValue(plugin);
                if (value != null)
                {
                    Console.WriteLine(JsonHelper.GetOutputJsonString(value.ToString(), _pluginNames[plugin]));
                }
            }
        }

        /// <summary>
        /// Logs guilog to console based on error level and verbosity
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnGuiLogNotificationOccured(IPlugin sender, GuiLogEventArgs args)
        {
            if (args.NotificationLevel < _loglevel)
            {
                return;
            }
            if (_verbose)
            {
                Console.WriteLine("GuiLog:{0}:{1}:{2}:{3}", DateTime.Now, args.NotificationLevel, (sender != null ? sender.GetType().Name : "null"), args.Message);
            }
            if (_jsonoutput)
            {
                Console.WriteLine(JsonHelper.GetLogJsonString(sender, args));
            }
        }

        /// <summary>
        /// Updates app domain with user defined assembly resolver routine
        /// </summary>
        private void UpdateAppDomain()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(LoadAssembly);
        }

        /// <summary>
        /// Loads assemblies defined by subfolders definition
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Assembly LoadAssembly(object sender, ResolveEventArgs args)
        {
            string folderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (string subfolder in subfolders)
            {
                string assemblyPath = Path.Combine(folderPath, (Path.Combine(subfolder, new AssemblyName(args.Name).Name + ".dll")));
                if (File.Exists(assemblyPath))
                {
                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    if (_verbose)
                    {
                        Console.WriteLine("Loaded assembly: " + assemblyPath);
                    }
                    return assembly;
                }
                assemblyPath = Path.Combine(folderPath, (Path.Combine(subfolder, new AssemblyName(args.Name).Name + ".exe")));
                if (File.Exists(assemblyPath))
                {
                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    if (_verbose)
                    {
                        Console.WriteLine("Loaded assembly: " + assemblyPath);
                    }
                    return assembly;
                }
            }
            return null;
        }
    }
}
