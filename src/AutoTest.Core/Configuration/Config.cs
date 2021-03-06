using System;
using System.Configuration;
using System.IO;
using AutoTest.Core.Messaging;
using System.Collections.Generic;
using AutoTest.Core.Caching.Projects;
using AutoTest.Core.FileSystem;
using AutoTest.Core.DebugLog;

namespace AutoTest.Core.Configuration
{
    public class Config : IConfiguration
    {
        private IMessageBus _bus;
		private string _ignoreFile;
		
        private string[] _watchDirectories;
        private List<KeyValuePair<string, string>> _buildExecutables = new List<KeyValuePair<string, string>>();
        private List<KeyValuePair<string, string>> _nunitTestRunners = new List<KeyValuePair<string, string>>();
        private List<KeyValuePair<string, string>> _msTestRunner = new List<KeyValuePair<string, string>>();
        private List<KeyValuePair<string, string>> _xunitTestRunner = new List<KeyValuePair<string, string>>();
        private CodeEditor _codeEditor;
        private bool _debuggingEnabled;
		
		public string GrowlNotify { get; private set; }
		public bool NotifyOnRunStarted { get; private set; }
		public bool NotifyOnRunCompleted { get; private set; }
		public string[] WatchIgnoreList { get; private set; }
		public bool ShouldUseIgnoreLists { get { return _buildExecutables.Count > 0; } }
		public int FileChangeBatchDelay { get; private set; }
		public string[] TestAssembliesToIgnore { get; private set; }
		public string[] TestCategoriesToIgnore { get; private set; }
		
        public Config(IMessageBus bus)
        {
            _bus = bus;
			var core = getConfiguration();
            tryToConfigure(core);
        }
		
		public void SetBuildProvider()
		{
			if (_buildExecutables == null)
				return;
			
			if (_buildExecutables.Count == 0)
			{
				FileChangeBatchDelay = 2000;
				_bus.SetBuildProvider("NoBuild");
			}
		}
		
		public void AnnounceTrackerType()
		{
			var trackerType = "file change tracking";;
			if (_buildExecutables.Count == 0)
				trackerType = "assembly tracking";
			_bus.Publish(new InformationMessage(string.Format("Tracker type: {0}", trackerType)));
		}
		
		public void Merge(string configuratoinFile)
		{
			var core = getConfiguration(configuratoinFile);
			mergeVersionedItem(_buildExecutables, core.BuildExecutables);
			mergeVersionedItem(_nunitTestRunners, core.NUnitTestRunner);
			mergeVersionedItem(_msTestRunner, core.MSTestRunner);
			mergeVersionedItem(_xunitTestRunner, core.XUnitTestRunner);
            mergeCodeEditor(core.CodeEditor);
			if (core.DebuggingEnabled.WasReadFromConfig)
				_debuggingEnabled = core.DebuggingEnabled.Value;
			if (core.GrowlNotify.WasReadFromConfig)
				GrowlNotify = mergeValueItem(core.GrowlNotify, null);
			if (core.NotifyOnRunStarted.WasReadFromConfig)
				NotifyOnRunStarted = core.NotifyOnRunStarted.Value;
			if (core.NotifyOnRunCompleted.WasReadFromConfig)
				NotifyOnRunCompleted = core.NotifyOnRunCompleted.Value;
			if (core.TestAssembliesToIgnore.WasReadFromConfig)
				TestAssembliesToIgnore = mergeValues(TestAssembliesToIgnore, core.TestAssembliesToIgnore);
			if (core.TestCategoriesToIgnore.WasReadFromConfig)
				TestCategoriesToIgnore = mergeValues(TestCategoriesToIgnore, core.TestCategoriesToIgnore);
			if (core.WatchIgnoreFile.WasReadFromConfig)
				_ignoreFile = mergeValueItem(core.WatchIgnoreFile, "");
			if (core.FileChangeBatchDelay.WasReadFromConfig)
				FileChangeBatchDelay = core.FileChangeBatchDelay.Value;
		}
		
		private string[] mergeValues(string[] setting, ConfigItem<string[]> settingToMerge)
		{
			if (settingToMerge.ShouldExclude)
				return new string[] {};
			if (settingToMerge.ShouldMerge)
			{
				var list = new List<string>();
				list.AddRange(setting);
				list.AddRange(settingToMerge.Value);
				return list.ToArray();
			}
			return settingToMerge.Value;
		}
		
		private string mergeValueItem(ConfigItem<string> settingToMerge, string defaultValue)
		{
			if (settingToMerge.ShouldExclude)
				return defaultValue;
			return settingToMerge.Value;
		}
		
		private void mergeCodeEditor(ConfigItem<CodeEditor> settingToMerge)
		{
			if (!settingToMerge.WasReadFromConfig)
				return;
			if (settingToMerge.ShouldExclude)
			{
				_codeEditor = new CodeEditor("", "");
				return;
			}
			_codeEditor = settingToMerge.Value;
		}
		
		private void mergeVersionedItem(List<KeyValuePair<string, string>> setting, ConfigItem<KeyValuePair<string, string>[]> settingToMerge)
		{
			if (!settingToMerge.WasReadFromConfig)
				return;
			if (settingToMerge.ShouldExclude)
			{
				setting.Clear();
				return;
			}
			if (settingToMerge.ShouldMerge)
			{
				foreach (var mergedItem in settingToMerge.Value)
				{
					setting.RemoveAll(x => x.Key.Equals(mergedItem.Key));
					setting.Add(mergedItem);
				}
				return;
			}
			setting.Clear();
			setting.AddRange(settingToMerge.Value);
		}

        private void tryToConfigure(CoreSection core)
        {
            try
            {
                _watchDirectories = core.WatchDirectories.Value;
                _buildExecutables.AddRange(core.BuildExecutables.Value);
                _nunitTestRunners.AddRange(core.NUnitTestRunner.Value);
                _msTestRunner.AddRange(core.MSTestRunner.Value);
                _xunitTestRunner.AddRange(core.XUnitTestRunner.Value);
                _codeEditor = core.CodeEditor.Value;
                _debuggingEnabled = core.DebuggingEnabled.Value;
				GrowlNotify = core.GrowlNotify.Value;
				NotifyOnRunStarted = core.NotifyOnRunStarted.Value;
				NotifyOnRunCompleted = core.NotifyOnRunCompleted.Value;
				TestAssembliesToIgnore = core.TestAssembliesToIgnore.Value;
				TestCategoriesToIgnore = core.TestCategoriesToIgnore.Value;
				_ignoreFile = core.WatchIgnoreFile.Value;
				FileChangeBatchDelay = core.FileChangeBatchDelay.Value;
            }
            catch (Exception ex)
            {
                DebugLog.Debug.FailedToConfigure(ex);
                throw;
            }
        }
		
		private CoreSection getConfiguration()
		{
			return getConfiguration(Path.Combine(PathParsing.GetRootDirectory(), "AutoTest.config"));
		}
		
		private CoreSection getConfiguration(string configFile)
		{
			var core = new CoreSection();
			if (!File.Exists(configFile))
			{
				Debug.ConfigurationFileMissing();
				return core;
			}
			core.Read(configFile);
			return core;
		}

        public void ValidateSettings()
        {
            if (noneExists(_buildExecutables))
                _bus.Publish(new WarningMessage("Invalid build executable specified in the configuration file. Builds will not be run."));
            if (noneExists(_nunitTestRunners))
                _bus.Publish(new WarningMessage("NUnit test runner not specified. NUnit tests will not be run."));
            if (noneExists(_msTestRunner))
                _bus.Publish(new WarningMessage("MSTest test runner not specified. MSTest tests will not be run."));
			if (noneExists(_xunitTestRunner))
                _bus.Publish(new WarningMessage("XUnit test runner not specified. XUnit tests will not be run."));
            if (_codeEditor == null || !File.Exists(_codeEditor.Executable))
                _bus.Publish(new WarningMessage("Code editor not specified"));
        }
		
		public void BuildIgnoreListFromPath(string watchPath)
		{
			var file = _ignoreFile;
			if (!File.Exists(file))
				file = Path.Combine(watchPath, _ignoreFile);
			
			if (File.Exists(file))
				WatchIgnoreList = getLineArrayFromFile(file);
			else
				WatchIgnoreList = new string[] { };
		}

		private string[] getLineArrayFromFile(string file)
		{
			var lines = new List<string>();
			using (var reader = new StreamReader(file))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					var trimmedLine = line.Trim();
					if (trimmedLine.Length == 0)
						continue;
					if (trimmedLine.StartsWith("!"))
						continue;
					if (trimmedLine.StartsWith("#"))
						continue;
					lines.Add(trimmedLine);
				}
			}
			return lines.ToArray();
		}
		
        private bool noneExists(List<KeyValuePair<string, string>> files)
        {
			if (files == null)
				return true;
			
            foreach (var file in files)
            {
                if (File.Exists(file.Value))
                    return false;
            }
            return true;
        }

        public string[] WatchDirectores
        {
            get { return _watchDirectories; }
            set { _watchDirectories = value; }
        }

        public string NunitTestRunner(string version)
        {
            return getVersionedSetting(version, _nunitTestRunners);
        }

        public string MSTestRunner(string version)
        {
            return getVersionedSetting(version, _msTestRunner);
        }

        public string XunitTestRunner(string version)
        {
            return getVersionedSetting(version, _xunitTestRunner);
        }

        public CodeEditor CodeEditor
        {
            get { return _codeEditor; }
            set { _codeEditor = value; }
        }

        public bool DebuggingEnabled
        {
            get { return _debuggingEnabled; }
            set { _debuggingEnabled = value; }
        }

        public string BuildExecutable(ProjectDocument project)
        {
            if (_buildExecutables.Count == 0)
                return "";
            int index;
            if ((index = _buildExecutables.FindIndex(0, b => b.Key.Equals(project.ProductVersion))) >= 0)
                return _buildExecutables[index].Value;
            if ((index = _buildExecutables.FindIndex(0, b => b.Key.Equals(project.Framework))) >= 0)
                return _buildExecutables[index].Value;
            if ((index = _buildExecutables.FindIndex(0, b => b.Key.Equals(""))) >= 0)
                return _buildExecutables[index].Value;
            return _buildExecutables[0].Value;
        }

        private string getVersionedSetting(string version, List<KeyValuePair<string, string>> setting)
        {
            if (setting.Count == 0)
                return "";
            int index;
            if ((index = setting.FindIndex(0, b => b.Key.Equals(version))) >= 0)
                return setting[index].Value;
            if ((index = setting.FindIndex(0, b => b.Key.Equals(""))) >= 0)
                return setting[index].Value;
            return setting[0].Value;
        }
    }
}