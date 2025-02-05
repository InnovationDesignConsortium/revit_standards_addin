using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using NLog;
using NLog.Config;
using NLog.Targets;
using Revit.Async;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace RevitDataValidator
{
    internal class Ribbon : Nice3point.Revit.Toolkit.External.ExternalApplication
    {
        public override void OnStartup()
        {
            RevitTask.Initialize(Application);
            RevitTask.RegisterGlobal(new CustomRuleExternalEventHandler());

            var dll = typeof(Ribbon).Assembly.Location;
            Utils.dllPath = Path.GetDirectoryName(dll);

            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(Utils.dllPath, "NLog.config"));
            var logConfig = LogManager.Configuration;
            var targets = logConfig.AllTargets;
            foreach (var target in targets)
            {
                if (target is FileTarget ft)
                {
                    ft.FileName = string.Concat(ft.FileName.ToString().Substring(0, ft.FileName.ToString().Length - 4), " ", DateTime.Now.ToString().Replace(":", "-").Replace("/", "_"), ".log");
                }
            }
            LogManager.Configuration = logConfig;

            Utils.Log($"Running version: {Utils.GetInstalledVersion()}", LogLevel.Info);

            Utils.token_for_GIT_CODE_REPO_OWNER = Utils.GetGithubTokenFromApp(Utils.GIT_CODE_REPO_OWNER);

            Utils.dictCategoryPackSet = new Dictionary<string, string>();
            Utils.dictCustomCode = new Dictionary<string, Type>();
            Utils.app = Application.ControlledApplication;
            Utils.allParameterRules = new List<ParameterRule>();
            Utils.allWorksetRules = new List<WorksetRule>();
            Application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
            Application.ControlledApplication.DocumentSaving += ControlledApplication_DocumentSaving;
            Application.ControlledApplication.DocumentSavedAs += ControlledApplication_DocumentSavedAs;
            Application.ControlledApplication.DocumentSynchronizingWithCentral += ControlledApplication_DocumentSynchronizingWithCentral;
            Application.ViewActivated += Application_ViewActivated;
            Application.DialogBoxShowing += Application_DialogBoxShowing;
            Application.Idling += Application_Idling;
            Utils.eventHandlerWithParameterObject = new EventHandlerWithParameterObject();
            Utils.eventHandlerCreateInstancesInRoom = new EventHandlerCreateInstancesInRoom();

            Utils.paneId = new DockablePaneId(Guid.NewGuid());
            Utils.propertiesPanel = new PropertiesPanel();

            Application.ControlledApplication.DocumentChanged += ControlledApplication_DocumentChanged;

            Application.RegisterDockablePane(Utils.paneId, "Properties Panel", Utils.propertiesPanel as IDockablePaneProvider);
            Application.SelectionChanged += Application_SelectionChanged;

            foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory)))
            {
                if (bic.ToString().Contains("_gbXML_"))
                {
                    continue;
                }
                try
                {
                    Utils.catMap.Add(LabelUtils.GetLabelFor(bic), bic);
                }
                catch
                { }
            }

            DataValidationUpdater dataValidationUpdater = new DataValidationUpdater(Application.ActiveAddInId);
            Utils.DataValidationUpdaterId = dataValidationUpdater.GetUpdaterId();
            UpdaterRegistry.RegisterUpdater(dataValidationUpdater, true);

            Utils.genericFailureId = new FailureDefinitionId(Guid.NewGuid());
            FailureDefinition.CreateFailureDefinition(
                Utils.genericFailureId,
                FailureSeverity.Error,
                Utils.RULE_DEFAULT_MESSAGE);

            var panel = Application.GetRibbonPanels().Find(q => q.Name == Utils.panelName) ?? Application.CreateRibbonPanel(Utils.panelName);

            var showPaneCommand = new PushButtonData("ShowPaneCommand", "Show\nPane", dll, "RevitDataValidator.ShowPaneCommand")
            {
                Image = NewBitmapImage(GetType().Namespace, "show16.png"),
                LargeImage = NewBitmapImage(GetType().Namespace, "show.png")
            };
            panel.AddItem(showPaneCommand);

            var showLogsCommand = new PushButtonData("ShowLogCommand", "Show\nLog", dll, "RevitDataValidator.ShowLogCommand")
            {
                Image = NewBitmapImage(GetType().Namespace, "log16.png"),
                LargeImage = NewBitmapImage(GetType().Namespace, "log.png")
            };
            panel.AddItem(showLogsCommand);

            var aboutCommand = new PushButtonData("AboutCommand", "About", dll, "RevitDataValidator.AboutCommand")
            {
                Image = NewBitmapImage(GetType().Namespace, "about16.png"),
                LargeImage = NewBitmapImage(GetType().Namespace, "about.png")
            };
            panel.AddItem(aboutCommand);

            panel.AddItem(new PushButtonData("ReloadRulesCommand", "Reload\nRules", dll, "RevitDataValidator.ReloadRulesCommand"));

            Update.CheckForUpdates();
        }

        private void ControlledApplication_DocumentSavedAs(object sender, Autodesk.Revit.DB.Events.DocumentSavedAsEventArgs e)
        {
            Utils.doc = e.Document;
        }

        public static BitmapImage NewBitmapImage(string ns, string imageName)
        {
            string imagePath = ns + ".ImageFiles." + imageName;
            Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(imagePath);
            BitmapImage img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = s;
            img.EndInit();
            return img;
        }

        private void ControlledApplication_DocumentSynchronizingWithCentral(object sender, Autodesk.Revit.DB.Events.DocumentSynchronizingWithCentralEventArgs e)
        {
            Utils.RunAllRules(null, WhenToRun.SyncToCentral);
        }

        private void ControlledApplication_DocumentSaving(object sender, Autodesk.Revit.DB.Events.DocumentSavingEventArgs e)
        {
            Utils.RunAllRules(null, WhenToRun.Save);
        }

        public override void OnShutdown()
        {
            if (Utils.MsiToRunOnExit != null)
            {
                if (!File.Exists(Utils.MsiToRunOnExit))
                {
                    Utils.Log($"{Utils.MsiToRunOnExit} does not exist", LogLevel.Warn);
                    return;
                }
                var runinstaller = Path.Combine(Utils.dllPath, "RunInstaller.exe");
                if (!File.Exists(runinstaller))
                {
                    Utils.Log($"{runinstaller} does not exist", LogLevel.Warn);
                    return;
                }
                Utils.Log($"Calling RunInstaller.exe to install {Utils.MsiToRunOnExit}", LogLevel.Info);
                try
                {
                    Utils.StartShell(runinstaller, false, Utils.MsiToRunOnExit);
                }
                catch (Exception ex)
                {
                    Utils.LogException("Could not install new version", ex);
                }
            }
        }

        private void ControlledApplication_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
        {
            Utils.propertiesPanel?.Refresh();
        }

        private void Application_Idling(object sender, IdlingEventArgs e)
        {
            Utils.dialogIdShowing = "";
            Utils.CustomCodeRunning = new List<string>();
            Utils.idsTriggered = new List<ElementId>();
        }

        private void Application_DialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            Utils.dialogIdShowing = e.DialogId;
        }

        private void Application_ViewActivated(object sender, ViewActivatedEventArgs e)
        {
            var currentFilename = Utils.GetFileName(e.Document);
            if (Utils.doc == null || !Utils.doc.IsValidObject || currentFilename != Utils.doc.PathName)
            {
                Utils.doc = e.Document;
                Utils.ReloadRules(false);
            }
        }

        private void Application_SelectionChanged(object sender, Autodesk.Revit.UI.Events.SelectionChangedEventArgs e)
        {
            Utils.selectedIds = e.GetSelectedElements().ToList();
            Utils.SetupPane();
        }

        private void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            Utils.doc = e.Document;
            Utils.RunAllRules(null, WhenToRun.Open);
        }
    }
}