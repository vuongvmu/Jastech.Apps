﻿using ATT_UT_IPAD.Core;
using ATT_UT_IPAD.Core.Data;
using ATT_UT_IPAD.Properties;
using ATT_UT_IPAD.UI.Pages;
using Cognex.VisionPro;
using Emgu.CV.Dnn;
using Jastech.Apps.Structure;
using Jastech.Apps.Structure.Data;
using Jastech.Apps.Winform;
using Jastech.Apps.Winform.Core;
using Jastech.Apps.Winform.Service;
using Jastech.Apps.Winform.Service.Plc;
using Jastech.Apps.Winform.Service.Plc.Maps;
using Jastech.Apps.Winform.Settings;
using Jastech.Apps.Winform.UI.Forms;
using Jastech.Framework.Config;
using Jastech.Framework.Device.Grabbers;
using Jastech.Framework.Device.Motions;
using Jastech.Framework.Matrox;
using Jastech.Framework.Structure;
using Jastech.Framework.Structure.Service;
using Jastech.Framework.Users;
using Jastech.Framework.Util.Helper;
using Jastech.Framework.Winform;
using Jastech.Framework.Winform.Forms;
using Jastech.Framework.Winform.Helper;
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ATT_UT_IPAD
{
    public partial class MainForm : Form
    {
        #region 필드
        private int _virtualImageCount { get; set; } = 0;

        private MachineStatus _prevMachineStatus { get; set; } = MachineStatus.STOP;
        #endregion

        #region 속성
        private MainPage MainPageControl { get; set; } = null;

        private DataPage DataPageControl { get; set; } = null;

        private TeachingPage TeachingPageControl { get; set; } = null;

        private List<UserControl> PageControlList = null;

        private List<Label> PageLabelList = null;

        private Task VirtualInspTask { get; set; }

        private CancellationTokenSource CancelVirtualInspTask { get; set; }

        private Task CheckSafetyDoorlockTask { get; set; }

        private CancellationTokenSource CancelSafetyDoorlockTask { get; set; }

        private Queue<string> VirtualImagePathQueue = new Queue<string>();

        private Queue<ICogImage> AlignLastScanImageQueue = new Queue<ICogImage>();

        private Queue<ICogImage> AkkonLastScanImageQueue = new Queue<ICogImage>();

        public ATTInspModelService ATTInspModelService { get; set; } = new ATTInspModelService();

        public ManualJudgeForm ManualJudgeForm { get; set; } = null;
        #endregion

        #region 델리게이트
        private delegate void UpdateLabelDelegate(string modelname);
        #endregion

        #region 생성자
        public MainForm()
        {
            InitializeComponent();
        }
        #endregion

        #region 메서드
        private void MainForm_Load(object sender, EventArgs e)
        {
            if (UserManager.Instance().CurrentUser.Type == AuthorityType.Maker)
                this.Text = " ";

            lblMachineName.Text = AppsConfig.Instance().MachineName;

            AddControls();
            SelectMainPage();

            TeachingPageControl.SetInspModelService(ATTInspModelService);
            DataPageControl.SetInspModelService(ATTInspModelService);
            DataPageControl.ApplyModelEventHandler += ModelPageControl_ApplyModelEventHandler;
            ModelManager.Instance().CurrentModelChangedEvent += MainForm_CurrentModelChangedEvent;

            if (ModelManager.Instance().CurrentModel != null)
            {
                lblCurrentModel.Text = ModelManager.Instance().CurrentModel.Name;
                ModelManager.Instance().ApplyChangedEvent();
            }

            SystemManager.Instance().Initialize();
            
            PlcScenarioManager.Instance().Initialize(ATTInspModelService);
            PlcScenarioManager.Instance().InspRunnerHandler += MainForm_InspRunnerHandler;
            PlcScenarioManager.Instance().MoveEventHandler += MainForm_MoveEventHandler;
            PlcScenarioManager.Instance().OriginAllEvent += MainForm_OriginAllEvent;
            PlcControlManager.Instance().AxisNegativeLimitEventHandler += MainForm_AxisNegativeLimitEventHandler;
            PlcControlManager.Instance().AxisPositiveLimitEventHandler += MainForm_AxisPositiveLimitEventHandler;
            PlcScenarioManager.Instance().MainTaskHandler += MainForm_MainTaskHandler;

            PlcControlManager.Instance().WritePcCommand(PcCommand.ServoReset_1);
            Thread.Sleep(100);
            PlcControlManager.Instance().WritePcCommand(PcCommand.ServoOn_1);
            Thread.Sleep(100);
            PlcControlManager.Instance().WritePcCommand(PcCommand.ServoReset_2);
            Thread.Sleep(100);
            PlcControlManager.Instance().WritePcCommand(PcCommand.ServoOn_2);

            tmrMainForm.Start();
            tmrUpdateStates.Start();
            StartVirtualInspTask();
            SystemManager.Instance().InitializeInspRunner();
            AddSystemLogMessage("Start Program.");

            PlcControlManager.Instance().WriteVersion();

            ManualJudgeForm = new ManualJudgeForm();
            ManualJudgeForm.ManualJudmentHandler += MainForm_ManualJudgmentHandler;
            ManualJudgeForm.Show();
            ManualJudgeForm.Hide();

            if (ConfigSet.Instance().Operation.VirtualMode == false)
            {
                CancelSafetyDoorlockTask = new CancellationTokenSource();
                CheckSafetyDoorlockTask = new Task(CheckDoorOpenedLoop, CancelSafetyDoorlockTask.Token);
                CheckSafetyDoorlockTask.Start();
            }
        }

        private void MainForm_MainTaskHandler(bool isStart, string message = "")
        {
            if (isStart)
                SystemManager.Instance().SetRunMode();
            else
                SystemManager.Instance().SetStopMode();

            AddSystemLogMessage(message);
        }

        private bool MainForm_MoveEventHandler(PlcCommand plcCommand, TeachingPosType teachingPosType, out string alarmMessage)
        {
            return SystemManager.Instance().PlcScenarioMoveTo(plcCommand, teachingPosType, out alarmMessage);
        }

        private bool MainForm_OriginAllEvent()
        {
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            var teachingPos = inspModel.GetUnit(UnitName.Unit0).GetTeachingInfo(TeachingPosType.Stage1_Scan_Start);
            var akkonLaf = LAFManager.Instance().GetLAF("AkkonLaf");
            var alignLaf = LAFManager.Instance().GetLAF("AlignLaf");

            ProgressForm progressForm = new ProgressForm("Homing All Axes", ProgressForm.RunMode.Batch, true);
            progressForm.Add($"Axis X Homing", SystemManager.Instance().AxisHoming, AxisName.X, SystemManager.Instance().StopAxisHoming);

            akkonLaf.SetHomeStandbyPosition(teachingPos.GetTargetPosition(AxisName.Z0));
            progressForm.Add($"Axis Z1 (Akkon LAF) homing", akkonLaf.HomeSequenceAction, akkonLaf.StopHomeSequence);

            alignLaf.SetHomeStandbyPosition(teachingPos.GetTargetPosition(AxisName.Z1));
            progressForm.Add($"Axis Z2 (Align LAF) homing", alignLaf.HomeSequenceAction, alignLaf.StopHomeSequence);

            progressForm.ShowDialog();

            if (progressForm.IsSuccess)
            {
                akkonLaf.LafCtrl.SetMotionAbsoluteMove(teachingPos.GetTargetPosition(AxisName.Z0));
                alignLaf.LafCtrl.SetMotionAbsoluteMove(teachingPos.GetTargetPosition(AxisName.Z1));
            }

            return progressForm.IsSuccess;
        }

        private void MainForm_InspRunnerHandler(bool isStart)
        {
            AppsStatus.Instance().IsInspRunnerFlagFromPlc = isStart;
        }

        private void AddControls()
        {
            // Page Control List
            PageControlList = new List<UserControl>();

            MainPageControl = new MainPage();
            MainPageControl.Dock = DockStyle.Fill;
            PageControlList.Add(MainPageControl);

            DataPageControl = new DataPage();
            DataPageControl.Dock = DockStyle.Fill;
            PageControlList.Add(DataPageControl);

            TeachingPageControl = new TeachingPage();
            TeachingPageControl.Dock = DockStyle.Fill;
            PageControlList.Add(TeachingPageControl);

            // Button List
            PageLabelList = new List<Label>();
            PageLabelList.Add(lblMainPage);
            PageLabelList.Add(lblTeachingPage);
            PageLabelList.Add(lblDataPage);
            PageLabelList.Add(lblLogPage);
        }

        private void ModelPageControl_ApplyModelEventHandler(string modelName)
        {
            string modelDir = ConfigSet.Instance().Path.Model;
            string filePath = Path.Combine(modelDir, modelName, InspModel.FileName);

            ModelManager.Instance().CurrentModel = ATTInspModelService.Load(filePath);
            SelectMainPage();
            UpdateLabel(modelName);

            ConfigSet.Instance().Operation.LastModelName = modelName;
            ConfigSet.Instance().Operation.Save(ConfigSet.Instance().Path.Config);

            PlcControlManager.Instance().WriteModelData(ModelManager.Instance().CurrentModel as AppsInspModel);

            if (ManualJudgeForm != null)
            {
                ManualJudgeForm.Close();
                ManualJudgeForm.Dispose();
                ManualJudgeForm = null;

                ManualJudgeForm = new ManualJudgeForm();
                ManualJudgeForm.ManualJudmentHandler += MainForm_ManualJudgmentHandler;
                ManualJudgeForm.Hide();
            }
        }

        private void SelectMainPage()
        {
            SetSelectLabel(lblMainPage);
            SetSelectPage(MainPageControl);
        }

        private void SetSelectLabel(object sender)
        {
            foreach (Label label in PageLabelList)
                label.ForeColor = Color.White;

            Label currentLabel = sender as Label;
            int labelIndex = Convert.ToInt32(currentLabel.Parent.Tag);
            PageLabelList[labelIndex].ForeColor = Color.DodgerBlue;
        }

        private void SetSelectPage(UserControl selectedControl)
        {
            foreach (UserControl control in PageControlList)
                control.Visible = false;

            selectedControl.Visible = true;
            selectedControl.Dock = DockStyle.Fill;
            pnlPage.Controls.Add(selectedControl);
        }

        private void MainForm_CurrentModelChangedEvent(InspModel inspModel)
        {
            AppsInspModel appsInspModel = inspModel as AppsInspModel;

            DailyInfoService.Reset();
            DailyInfoService.Load(appsInspModel.Name);

            MainPageControl.ChangeModel(appsInspModel);

            UpdateLabel(appsInspModel.Name);
            ConfigSet.Instance().Operation.LastModelName = appsInspModel.Name;
            ConfigSet.Instance().Operation.Save(ConfigSet.Instance().Path.Config);

            PlcControlManager.Instance().WriteCurrentModelName(appsInspModel.Name);
			Logger.Debug(LogType.Parameter, $"Applied Model name : {appsInspModel.Name}");
			
            AppsInspResult.Instance().Dispose();
        }

        private void UpdateLabel(string modelname)
        {
            if (this.InvokeRequired)
            {
                UpdateLabelDelegate callback = UpdateLabel;
                BeginInvoke(callback, modelname);
                return;
            }

            lblCurrentModel.Text = modelname;
        }

        private void lblMainPage_Click(object sender, EventArgs e)
        {
            SetSelectLabel(sender);
            SetSelectPage(MainPageControl);
        }

        private void lblTeachingPage_Click(object sender, EventArgs e)
        {
            if (ModelManager.Instance().CurrentModel == null)
            {
                MessageConfirmForm form = new MessageConfirmForm();
                form.Message = "Current Model is null.";
                form.ShowDialog();
                return;
            }

            SetSelectLabel(sender);
            SetSelectPage(TeachingPageControl);
        }

        private void lblDataPage_Click(object sender, EventArgs e)
        {
            SetSelectLabel(sender);
            SetSelectPage(DataPageControl);
        }

        private void lblLogPage_Click(object sender, EventArgs e)
        {
            LogForm logForm = new LogForm();

            string logPath = ConfigSet.Instance().Path.Log;
            string resultPath = ConfigSet.Instance().Path.Result;
            string modelName = ConfigSet.Instance().Operation.LastModelName;

            logForm.SetLogViewPath(logPath, resultPath, modelName);
            logForm.ShowDialog();
        }

        private void tmrMainForm_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            lblCurrentTime.Text = now.ToString("yyyy-MM-dd HH:mm:ss");

            var user = UserManager.Instance().CurrentUser;

            if (user.Type == AuthorityType.None)
                lblCurrentUser.Text = "Operator";
            else
                lblCurrentUser.Text = user.Id.ToString();

            if (MainPageControl.Visible)
                MainPageControl.UpdateButton();

            if (PlcControlManager.Instance().MachineStatus == MachineStatus.RUN)
            {
                if (user.Type == AuthorityType.Maker)
                {
                    lblLogPageImage.Enabled = true;
                    lblLogPage.Enabled = true;
                    lblDataPageImage.Enabled = true;
                    lblDataPage.Enabled = true;
                }
                else
                {
                    lblLogPageImage.Enabled = false;
                    lblLogPage.Enabled = false;
                    lblDataPageImage.Enabled = false;
                    lblDataPage.Enabled = false;
                }

                lblTeachingPageImage.Enabled = false;
                lblTeachingPage.Enabled = false;
            }
            else
            {
                if (user.Type == AuthorityType.None)
                {
                    lblTeachingPage.Enabled = false;
                    lblTeachingPageImage.Enabled = false;
                }
                else
                {
                    lblTeachingPage.Enabled = true;
                    lblTeachingPageImage.Enabled = true;
                }

                lblDataPageImage.Enabled = true;
                lblDataPage.Enabled = true;
                lblLogPageImage.Enabled = true;
                lblLogPage.Enabled = true;
            }
        }

        private void tmrUpdateStates_Tick(object sender, EventArgs e)
        {
            var plc = DeviceManager.Instance().PlcHandler;
            bool isPlcConnected = plc.Count > 0 && plc.All(h => h.IsConnected());
            ControlDisplayHelper.DisposeDisplay(lblPLCState);
            lblPLCState.Image = GetStateImage(isPlcConnected);

            var motion = DeviceManager.Instance().MotionHandler;
            bool isMotionConnected = motion.Count > 0 && motion.All(h => h.IsConnected());
            ControlDisplayHelper.DisposeDisplay(lblMotionState);
            lblMotionState.Image = GetStateImage(isMotionConnected);

            var laf = DeviceManager.Instance().LAFCtrlHandler;
            bool isLafConnected = laf.Count > 0 && laf.All(h => h.IsConnected());
            ControlDisplayHelper.DisposeDisplay(lblLafState);
            lblLafState.Image = GetStateImage(isLafConnected);

            var light = DeviceManager.Instance().LightCtrlHandler;
            bool isLightConnected = light.Count > 0 && light.All(h => h.IsConnected());
            ControlDisplayHelper.DisposeDisplay(lblLightState);
            lblLightState.Image = GetStateImage(isLightConnected);
        }

        public void UpdateMainAkkonResult(int tabNo)
        {
            MainPageControl.UpdateMainAkkonResultData(tabNo);
        }

        public void UpdateMainAlignResult(int tabNo)
        {
            MainPageControl.UpdateMainAlignResult(tabNo);
        }

        public void UpdateAllRefreshData()
        {
            MainPageControl.UpdateAllRefreshData();
        }

        public void UpdateAkkonResultTabButton(int tabNo)
        {
            MainPageControl.UpdateAkkonResultTabButton(tabNo);
        }

        public void UpdateAlignResultTabButton(int tabNo)
        {
            MainPageControl.UpdateAlignResultTabButton(tabNo);
        }

        public void TabButtonResetColor()
        {
            MainPageControl.TabButtonResetColor();
        }

        public void AddSystemLogMessage(string logMessage)
        {
            MainPageControl.AddSystemLogMessage(logMessage);
        }

        private void lblCurrentUser_Click(object sender, EventArgs e)
        {
            LoginForm form = new LoginForm();
            form.CurrentUser = UserManager.Instance().CurrentUser;
            form.UserHandler = UserManager.Instance().UserHandler;
            form.StopProgramRequest += SystemManager.Instance().StopAutoMode;
            form.StopProgram += StopProgramEventFunction;
            form.ShowDialog();

            UserManager.Instance().SetCurrentUser(form.CurrentUser.Id);
        }

        private void StopProgramEventFunction()
        {
            this.Close();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            DeviceManager.Instance().Release();
            GrabberMil.Release();
            MilHelper.FreeApplication();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            tmrMainForm.Stop();
            tmrUpdateStates.Stop();
            CancelSafetyDoorlockTask?.Cancel();
            StopVirtualInspTask();

            SystemManager.Instance().ReleaseInspRunner();
            SystemManager.Instance().StopRun();

            PlcControlManager.Instance().WritePcVisionStatus(MachineStatus.STOP);

            LAFManager.Instance().Release();
            PlcControlManager.Instance().Release();
            PlcScenarioManager.Instance().Release();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & ~(Keys.Shift | Keys.Control);

            if (ConfigSet.Instance().Operation.VirtualMode)
            {
                switch (key)
                {
                    case Keys.D:
                        if ((keyData & Keys.Control) != 0)
                        {
                            LoadTabImage();
                            return true;
                        }
                        break;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected void LoadTabImage()
        {
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            if (inspModel == null)
                return;

            if (PlcControlManager.Instance().MachineStatus != MachineStatus.RUN)
            {
                MessageConfirmForm form = new MessageConfirmForm();
                form.Message = "Change Auto Run.";
                form.ShowDialog();
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = true;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _virtualImageCount = 0;
                string[] fileNames = dialog.FileNames;

                if (inspModel.TabCount != fileNames.Count())
                {
                    MessageConfirmForm form = new MessageConfirmForm();
                    form.Message = "The number of Tabs is Different.";
                    form.ShowDialog();
                    return;
                }

                LoadImage(fileNames);
                AppsStatus.Instance().IsInspRunnerFlagFromPlc = true;
            }
        }

        private void LoadImage(string[] fileNames)
        {
            foreach (var fileName in fileNames)
                AddVirtualImagePath(fileName);
        }

        private void AddVirtualImagePath(string filePath)
        {
            lock (VirtualImagePathQueue)
                VirtualImagePathQueue.Enqueue(filePath);
        }

        private void StartVirtualInspTask()
        {
            if (ConfigSet.Instance().Operation.VirtualMode)
            {
                CancelVirtualInspTask = new CancellationTokenSource();
                VirtualInspTask = new Task(VirtualInspTaskLoop, CancelVirtualInspTask.Token);
                VirtualInspTask.Start();
            }
        }

        private void StopVirtualInspTask()
        {
            if (ConfigSet.Instance().Operation.VirtualMode)
            {
                CancelVirtualInspTask.Cancel();
                VirtualInspTask.Wait();
                VirtualInspTask = null;
            }
        }

        public void VirtualInspTaskLoop()
        {
            while (true)
            {
                if (CancelVirtualInspTask.IsCancellationRequested)
                    break;

                if (GetVirtualImagePath() is string filePath)
                {
                    string text = "TAB_";
                    string fileName = Path.GetFileNameWithoutExtension(filePath).ToUpper();
                    fileName = fileName.Replace("_OK", "");
                    fileName = fileName.Replace("_NG", "");

                    int index = fileName.IndexOf(text);

                    if (index < 0)
                    {
                        MessageConfirmForm form = new MessageConfirmForm();
                        form.Message = "The format of the file name is not correct.";
                        form.ShowDialog();
                    }
                    else
                    {
                        string tabNoString = fileName.Substring(index + text.Length);
                        SystemManager.Instance().SetVirtualImage(Convert.ToInt32(tabNoString), filePath);
                    }

                    _virtualImageCount++;

                    var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

                    if (_virtualImageCount == inspModel.TabCount)
                        AppsStatus.Instance().IsInspRunnerFlagFromPlc = true;
                }

                Thread.Sleep(50);
            }
        }

        public void CheckDoorOpenedLoop()
        {
            while (true)
            {
                if (CancelSafetyDoorlockTask.IsCancellationRequested)
                    break;

                if (PlcControlManager.Instance().GetValue(PlcCommonMap.PLC_DoorStatus) == "2" && PlcControlManager.Instance().IsDoorOpened == false)
                {
                    _prevMachineStatus = PlcControlManager.Instance().MachineStatus;
                    PlcControlManager.Instance().IsDoorOpened = true;

                    MotionManager.Instance().GetAxisHandler(AxisHandlerName.Handler0).StopMove();
                    LAFManager.Instance().GetLAF("AkkonLaf").LafCtrl.SetMotionStop();
                    LAFManager.Instance().GetLAF("AlignLaf").LafCtrl.SetMotionStop();
                    SystemManager.Instance().SetStopMode();

                    lblDoorlockState.BackColor = Color.Red;
                    lblDoorlockState.ForeColor = Color.Yellow;

                    AddSystemLogMessage("Door is opened.");
                }
                else if (PlcControlManager.Instance().GetValue(PlcCommonMap.PLC_DoorStatus) != "2" && PlcControlManager.Instance().IsDoorOpened == true)
                {
                    PlcControlManager.Instance().IsDoorOpened = false;

                    if (_prevMachineStatus == MachineStatus.RUN)
                        SystemManager.Instance().SetRunMode();

                    lblDoorlockState.BackColor = BackColor;
                    lblDoorlockState.ForeColor = BackColor;

                    AddSystemLogMessage("Door is closed.");
                }

                Thread.Sleep(500);
            }
        }

        private string GetVirtualImagePath()
        {
            lock (VirtualImagePathQueue)
            {
                if (VirtualImagePathQueue.Count > 0)
                    return VirtualImagePathQueue.Dequeue();
                else
                    return null;
            }
        }

        private Image GetStateImage(bool isNormalState)
        {
            return isNormalState ? Resources.Circle_Green : Resources.Circle_Red;
        }

        private void lblDoorlockState_Click(object sender, EventArgs e)
        {
            if (PlcControlManager.Instance().MachineStatus != MachineStatus.RUN)
                return;

            if (UserManager.Instance().CurrentUser.Type != AuthorityType.Maker)
                return;

            if (AppsStatus.Instance().IsInspRunnerFlagFromPlc == false)
            {
                MessageYesNoForm form = new MessageYesNoForm();
                form.Message = "Would you like to do a test run?";

                if (form.ShowDialog() == DialogResult.Yes)
                {
                    AppsStatus.Instance().IsInspRunnerFlagFromPlc = true;
                    AppsStatus.Instance().AutoRunTest = true;
                }
            }
        }

        public void Enable(bool isEnable)
        {
            MainPageControl?.Enable(isEnable);
        }

        public void SetManualJudgeData(List<ManualJudge> manualJudgeList)
        {
            ManualJudgeForm.SetManualJudge(manualJudgeList);
            ManualJudgeForm.SetInspectionResult();
        }

        public void ShowManualJudgeForm()
        {
            if (ManualJudgeForm.InvokeRequired)
            {
                ManualJudgeForm.Invoke(new MethodInvoker(delegate
                {
                    ManualJudgeForm.Show();
                }));
            }
            else
                ManualJudgeForm.Show();
        }

        private void MainForm_ManualJudgmentHandler(bool isManualJudgeCompleted)
        {
            AppsStatus.Instance().IsManualJudgeCompleted = isManualJudgeCompleted;
        }

        public void MessageConfirm(string message)
        {
            this.Invoke((MethodInvoker)delegate
            {
                new MessageConfirmForm { Message = message }.ShowDialog();
            });
        }

        public bool MainForm_AxisNegativeLimitEventHandler(AxisName axisName)
        {
            return SystemManager.Instance().IsNegativeLimitStatus(axisName);
        }

        public bool MainForm_AxisPositiveLimitEventHandler(AxisName axisName)
        {
            return SystemManager.Instance().IsPositiveLimitStatus(axisName);
        }

        private void lblMachineName_DoubleClick(object sender, EventArgs e)
        {
            OpenResultDirectory();
        }

        private void OpenResultDirectory()
        {
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            if (inspModel == null)
            {
                if (UserManager.Instance().CurrentUser.Type != AuthorityType.None)
                    Process.Start(ConfigSet.Instance().Path.Result);
            }
            else
            {
                string path = Path.Combine(ConfigSet.Instance().Path.Result, inspModel.Name);

                if (Directory.Exists(path) == false)
                    Directory.CreateDirectory(path);

                if (UserManager.Instance().CurrentUser.Type != AuthorityType.None)
                    Process.Start(path);
            }
        }
        #endregion
    }
}
