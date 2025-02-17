﻿using ATT.Core.AppTask;
using ATT.Core.Data;
using Cognex.VisionPro;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Jastech.Apps.Structure;
using Jastech.Apps.Structure.Data;
using Jastech.Apps.Winform;
using Jastech.Apps.Winform.Core;
using Jastech.Apps.Winform.Service;
using Jastech.Apps.Winform.Service.Plc.Maps;
using Jastech.Apps.Winform.Settings;
using Jastech.Framework.Algorithms.Akkon.Parameters;
using Jastech.Framework.Config;
using Jastech.Framework.Device.Cameras;
using Jastech.Framework.Device.LAFCtrl;
using Jastech.Framework.Device.LightCtrls;
using Jastech.Framework.Device.Motions;
using Jastech.Framework.Imaging;
using Jastech.Framework.Imaging.Helper;
using Jastech.Framework.Imaging.Result;
using Jastech.Framework.Imaging.VisionPro;
using Jastech.Framework.Util.Helper;
using Jastech.Framework.Winform;
using Jastech.Framework.Winform.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ATT.Core
{
    public partial class ATTInspRunner
    {
        #region 필드
        private const int SAVE_IMAGE_MAX_WIDTH = 65000;

        private Axis _axis { get; set; } = null;

        private object _akkonLock = new object();

        private object _inspLock = new object();

        private Thread _deleteThread { get; set; } = null;

        private Thread _ClearBufferThread { get; set; } = null;

        private Thread _saveThread { get; set; } = null;

        private Thread _updateThread { get; set; } = null;
        #endregion

        #region 속성
        private LineCamera LineCamera { get; set; } = null;

        private LAFCtrl LAFCtrl { get; set; } = null;

        private LightCtrlHandler LightCtrlHandler { get; set; } = null;

        private Task SeqTask { get; set; }

        private CancellationTokenSource SeqTaskCancellationTokenSource { get; set; }

        private SeqStep SeqStep { get; set; } = SeqStep.SEQ_IDLE;

        private bool IsGrabDone { get; set; } = false;

        private Stopwatch LastInspSW { get; set; } = new Stopwatch();

        public InspProcessTask InspProcessTask { get; set; } = new InspProcessTask();
        #endregion

        #region 이벤트
        #endregion

        #region 델리게이트
        #endregion

        #region 생성자
        public ATTInspRunner()
        {
        }
        #endregion

        #region 메서드
        public void SetVirtualmage(int tabNo, string fileName)
        {
            InspProcessTask.VirtualQueue.Enqueue(new VirtualData
            {
                TabNo = tabNo,
                FilePath = fileName,
            });
        }

        public void StartVirtualMode()
        {
            InspProcessTask.StartVirtual();
        }

        private void ATTSeqRunner_GrabDoneEventHandler(string cameraName, bool isGrabDone)
        {
             if (LineCamera.Camera.Name == cameraName)
            {
                IsGrabDone = isGrabDone;

                if (IsGrabDone == false)
                {
                    LineCamera.StopGrab();
                    if (AppsStatus.Instance().IsRepeat == false)
                        LAFCtrl.SetTrackingOnOFF(false);

                    WriteLog("Received Akkon Camera Grab Done Event.");
                }
            }
        }

        public void StartSeqTask()
        {
            if (SeqTask != null)
                return;

            SeqTaskCancellationTokenSource = new CancellationTokenSource();
            SeqTask = new Task(SeqTaskAction, SeqTaskCancellationTokenSource.Token);
            SeqTask.Start();
        }

        public void StopSeqTask()
        {
            if (SeqTask != null)
            {
                SeqTaskCancellationTokenSource.Cancel();
                SeqTask.Wait();
                SeqTask = null;
            }
        }

        public void StartUpdateThread()
        {
            if (_updateThread == null)
            {
                _updateThread = new Thread(UpdateUI);
                _updateThread.Start();
            }
        }

        private void UpdateUI()
        {
            StartSaveThread();

            GetAkkonResultImage();
            WriteLog("Make Akkon ResultImage.", true);

            SystemManager.Instance().UpdateMainResult();

            AppsStatus.Instance().IsInspRunnerFlagFromPlc = false;
            AppsStatus.Instance().AutoRunTest = false;

            SystemManager.Instance().EnableMainView(true);

            _updateThread = null;

            WriteLog("Update UI Inspection Result.", true);
        }

        public bool IsInspectionDone()
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            if (InspProcessTask.InspCount == inspModel.TabCount)
                return true;

            return false;
        }

        public void Initialize()
        {
            LineCamera = LineCameraManager.Instance().GetLineCamera("LineCamera");
            LineCamera.GrabDoneEventHandler += ATTSeqRunner_GrabDoneEventHandler;

            LAFCtrl = LAFManager.Instance().GetLAF("Laf").LafCtrl;
            LightCtrlHandler = DeviceManager.Instance().LightCtrlHandler;

            InspProcessTask.StartTask();
            StartSeqTask();
        }

        public void Release()
        {
            StopDevice();

            InspProcessTask.StopTask();
            StopSeqTask();
        }

        private void StopDevice()
        {
            LightCtrlHandler.TurnOff();

            LAFCtrl.SetTrackingOnOFF(false);
            WriteLog("AutoFocus Off.");

            LineCamera.GrabDoneEventHandler -= ATTSeqRunner_GrabDoneEventHandler;
            LineCamera.StopGrab();
            WriteLog("AkkonCamera Stop Grab.");
        }

        public void SeqRun()
        {
            if (ModelManager.Instance().CurrentModel == null)
                return;

            PlcControlManager.Instance().MachineStatus = MachineStatus.RUN;
            SeqStep = SeqStep.SEQ_INIT;

            WriteLog("Start Sequence.");
        }

        public void SeqStop()
        {
            PlcControlManager.Instance().MachineStatus = MachineStatus.STOP;
            SeqStep = SeqStep.SEQ_IDLE;

            WriteLog("Stop Sequence.");
        }

        private void SeqTaskAction()
        {
            var cancellationToken = SeqTaskCancellationTokenSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            SeqStep = SeqStep.SEQ_IDLE;

            while (true)
            {
                // 작업 취소
                if (cancellationToken.IsCancellationRequested)
                {
                    StopDevice();
                    InspProcessTask.DisposeInspTabList();

                    SeqStep = SeqStep.SEQ_IDLE;
                    break;
                }

                SeqTaskLoop();
                Thread.Sleep(50);
            }
        }

        private void SeqTaskLoop()
        {
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            if (inspModel == null)
                return;

            var unit = inspModel.GetUnit(UnitName.Unit0);
            if (unit == null)
                return;

            var tab = unit.GetTab(0);

            string systemLogMessage = string.Empty;
            string errorMessage = string.Empty;

            Stopwatch sw = new Stopwatch();
            switch (SeqStep)
            {
                case SeqStep.SEQ_IDLE:
                    AppsStatus.Instance().IsInspRunnerFlagFromPlc = false;
                    break;

                case SeqStep.SEQ_INIT:
                    ClearBufferThread();

                    LineCamera.IsLive = false;
                    LineCamera.StopGrab();
                    WriteLog("Stop Grab.");

                    LightCtrlHandler.TurnOff();
                    WriteLog("Light Off.");

                    LAFCtrl.SetTrackingOnOFF(false);
                    WriteLog("Laf Off.");

                    SeqStep = SeqStep.SEQ_MOVE_START_POS;
                    break;

                case SeqStep.SEQ_MOVE_START_POS:
                    MotionManager.Instance().MoveAxisZ(UnitName.Unit0, TeachingPosType.Stage1_Scan_Start, LAFCtrl, AxisName.Z0);

                    if (_ClearBufferThread != null || _updateThread != null)
                        break;

                    WriteLog("Wait Inspection Start Signal From PLC.", true);
                    SeqStep = SeqStep.SEQ_WAITING;
                    break;

                case SeqStep.SEQ_WAITING:
                    AppsStatus.Instance().IsInspRunnerRunning = false;

                    if (AppsStatus.Instance().IsInspRunnerFlagFromPlc == false)
                        break;

                    AppsStatus.Instance().IsInspRunnerRunning = true;

                    if (MoveTo(TeachingPosType.Stage1_Scan_Start, out errorMessage) == false)
                        break;

                    MotionManager.Instance().MoveAxisZ(UnitName.Unit0, TeachingPosType.Stage1_Scan_Start, LAFCtrl, AxisName.Z0);

                    SystemManager.Instance().EnableMainView(false);
                    SystemManager.Instance().TabButtonResetColor();

                    WriteLog("Receive Inspection Start Signal From PLC.", true);

                    LAFCtrl.SetTrackingOnOFF(true);
                    WriteLog("LAF Tracking ON.");

                    Thread.Sleep(300);

                    SeqStep = SeqStep.SEQ_BUFFER_INIT;
                    break;

                case SeqStep.SEQ_BUFFER_INIT:
                    InitializeBuffer();
                    WriteLog("Initialize Buffer.");

                    AppsInspResult.Instance().ClearResult();
                    WriteLog("Clear Result.");

                    AppsInspResult.Instance().StartInspTime = DateTime.Now;
                    AppsInspResult.Instance().Cell_ID = GetCellID();

                    if (ConfigSet.Instance().Operation.VirtualMode || AppsStatus.Instance().AutoRunTest)
                    {
                        DateTime dateTime = DateTime.Now;
                        string timeStamp = dateTime.ToString("yyyyMMddHHmmss");
                        string cellId = $"Test_{timeStamp}";

                        AppsInspResult.Instance().StartInspTime = dateTime;
                        AppsInspResult.Instance().Cell_ID = cellId;
                    }

                    WriteLog("Cell ID : " + AppsInspResult.Instance().Cell_ID, true);
                    SeqStep = SeqStep.SEQ_SCAN_START;
                    break;

                case SeqStep.SEQ_SCAN_START:
                    IsGrabDone = false;

                    if (unit.LightParam != null)
                    {
                        LightCtrlHandler?.TurnOn(unit.LightParam);
                        WriteLog("Light Turn On.", true);
                    }

                    LineCamera.StartGrab();
                    WriteLog("Start Akkon LineScanner Grab.", true);

                    if (ConfigSet.Instance().Operation.VirtualMode)
                    {
                        InspProcessTask.StartVirtual();
                        IsGrabDone = true;
                    }

                    SeqStep = SeqStep.SEQ_MOVE_END_POS;
                    break;

                case SeqStep.SEQ_MOVE_END_POS:
                    if (MoveTo(TeachingPosType.Stage1_Scan_End, out errorMessage) == false)
                        SeqStep = SeqStep.SEQ_ERROR;
                    else
                        SeqStep = SeqStep.SEQ_WAITING_AKKON_SCAN_COMPLETED;
                    break;

                case SeqStep.SEQ_WAITING_AKKON_SCAN_COMPLETED:
                    if (IsGrabDone == false)
                        break;

                    WriteLog("Complete Akkon LineScanner Grab.", true);
                    SeqStep = SeqStep.SEQ_WAITING_ALIGN_SCAN_COMPLETED;
                    break;

                case SeqStep.SEQ_WAITING_ALIGN_SCAN_COMPLETED:
                    if (IsGrabDone == false)
                        break;
					WriteLog("Complete Align LineScanner Grab.", true);

                    LightCtrlHandler.TurnOff();
                    WriteLog("Light Off.", false);

                    LastInspSW.Restart();

                    SeqStep = SeqStep.SEQ_WAITING_INSPECTION_DONE;
                    break;

                case SeqStep.SEQ_WAITING_INSPECTION_DONE:
                    if (IsInspectionDone() == false)
                        break;

                    LastInspSW.Stop();
                    AppsInspResult.Instance().EndInspTime = DateTime.Now;
                    AppsInspResult.Instance().LastInspTime = LastInspSW.ElapsedMilliseconds.ToString();

                    string message = $"Grab End to Insp Completed Time.({LastInspSW.ElapsedMilliseconds.ToString()}ms)";
                    WriteLog(message, true);

                    SeqStep = SeqStep.SEQ_SEND_RESULT;
                    break;

                case SeqStep.SEQ_SEND_RESULT:

                    SeqStep = SeqStep.SEQ_WAIT_UI_RESULT_UPDATE;
                    break;

                case SeqStep.SEQ_WAIT_UI_RESULT_UPDATE:

                    MoveTo(TeachingPosType.Stage1_Scan_Start, out errorMessage);

                    StartUpdateThread();

                    SeqStep = SeqStep.SEQ_SAVE_RESULT_DATA;
                    break;
               
                case SeqStep.SEQ_SAVE_RESULT_DATA:
                    DailyInfoService.Save(inspModel.Name);
                    SaveInspResultCSV();
                    WriteLog("Save inspection result.");

                    SeqStep = SeqStep.SEQ_SAVE_IMAGE;
                    break;

                case SeqStep.SEQ_SAVE_IMAGE:

                    UpdateDailyInfo();
                    SaveInspResultCSV();

                    SeqStep = SeqStep.SEQ_DELETE_DATA;
                    break;

                case SeqStep.SEQ_DELETE_DATA:
                    StartDeleteData();
                    WriteLog("Delete the old data");

                    SeqStep = SeqStep.SEQ_CHECK_STANDBY;
                    break;

                case SeqStep.SEQ_CHECK_STANDBY:

                    AppsStatus.Instance().IsInspRunnerFlagFromPlc = false;

                    ClearBufferThread();
                    SeqStep = SeqStep.SEQ_INIT;
                    break;

                case SeqStep.SEQ_ERROR:
                    short command = PlcControlManager.Instance().WritePcStatus(PlcCommand.StartInspection, true);
                    Logger.Debug(LogType.Device, $"Sequence Error StartInspection.[{command}]");
                    // 추가 필요
                    IsGrabDone = false;
                    AppsStatus.Instance().IsInspRunnerFlagFromPlc = false;
                    SystemManager.Instance().EnableMainView(true);
                    WriteLog("Sequnce Error.", true);
                    ClearBuffer();

                    SeqStep = SeqStep.SEQ_IDLE;
                    break;

                default:
                    break;
            }
        }

        private void GetAkkonResultImage()
        {
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            for (int tabNo = 0; tabNo < inspModel.TabCount; tabNo++)
            {
                var tabResult = AppsInspResult.Instance().Get(tabNo);

                if (tabResult != null)
                {
                    if (tabResult.MarkResult.Judgement == Judgement.OK)
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Restart();

                        var tab = inspModel.GetUnit(UnitName.Unit0).GetTab(tabNo);

                        // Overlay Image
                        if (tabResult.AkkonInspMatImage != null)
                        {
                            Mat resultMat = GetResultImage(tabResult.AkkonInspMatImage, tabResult.AkkonResult.LeadResultList, tab.AkkonParam.AkkonAlgoritmParam, ref tabResult.AkkonNGAffineList);
                            ICogImage cogImage = ConvertCogColorImage(resultMat);
                            tabResult.AkkonResultCogImage = cogImage;
                            resultMat.Dispose();

                            // AkkonInspCogImage
                            tabResult.AkkonInspCogImage = ConvertCogGrayImage(tabResult.AkkonInspMatImage);
                        }

                        sw.Stop();
                        Console.WriteLine(string.Format("Get Akkon Result Image_Tab{0} : {1}ms", tabNo, sw.ElapsedMilliseconds.ToString()));
                    }
                }
                else
                    Console.WriteLine(string.Format("Get Akkon Result Image_Tab{0} Fail.", tabNo));
            }
        }

        private string GetCellID()
        {
            string cellId = PlcControlManager.Instance().GetAddressMap(PlcCommonMap.PLC_Cell_Id).Value;
            
            if (cellId == "0" || cellId == null || cellId == "")
                return DateTime.Now.ToString("yyyyMMddHHmmss");
            else
            {
                cellId = cellId.Replace(" ", string.Empty);
                return cellId;
            }
        }

        private TabJudgement GetJudgemnet(TabInspResult tabInspResult)
        {
            if (tabInspResult.IsManualOK)
                return TabJudgement.Manual_OK;
            else
            {
                if (tabInspResult.MarkResult.Judgement != Judgement.OK)
                    return TabJudgement.Mark_NG;

                if (tabInspResult.AlignResult.Judgement != Judgement.OK)
                    return TabJudgement.NG;

                if (tabInspResult.AkkonResult == null)
                    return TabJudgement.NG;

                if (tabInspResult.AkkonResult.Judgement != Judgement.OK)
                    return TabJudgement.NG;

                return TabJudgement.OK;
            }
        }

        public void StartSaveThread()
        {
            if (_saveThread == null)
            {
                _saveThread = new Thread(SaveImage);
                _saveThread.Start();
            }
        }

        private void InitializeBuffer()
        {
            LineCamera.InitGrabSettings();
            InspProcessTask.InitalizeInspBuffer(LineCamera.Camera.Name, LineCamera.TabScanBufferList);
            ACSBufferManager.Instance().SetLafTriggerPosition(UnitName.Unit0, LAFCtrl.Name, LineCamera.TabScanBufferList, false, 0);
        }

        public void RunVirtual()
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            Tab tab = inspModel.GetUnit(UnitName.Unit0).GetTab(0);
        }

        public void StartDeleteData()
        {
            if (_deleteThread == null)
            {
                _deleteThread = new Thread(DeleteData);
                _deleteThread.Start();
            }
        }
        #endregion
    }

    public partial class ATTInspRunner
    {
        #region 메서드
        private void UpdateDailyInfo()
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            var dailyInfo = DailyInfoService.GetDailyInfo();

            if (dailyInfo == null || inspModel == null)
                return;

            var dailyData = new DailyData();

            UpdateAlignDailyInfo(ref dailyData);
            UpdateAkkonDailyInfo(ref dailyData);

            dailyInfo.AddDailyDataList(dailyData);

            SystemManager.Instance().UpdateDailyInfo();
            DailyInfoService.Save(inspModel.Name);
        }

        private void UpdateAlignDailyInfo(ref DailyData dailyData)
        {
            int tabCount = (ModelManager.Instance().CurrentModel as AppsInspModel).TabCount;

            for (int tabNo = 0; tabNo < tabCount; tabNo++)
            {
                AlignDailyInfo alignInfo = new AlignDailyInfo();

                var tabInspResult = AppsInspResult.Instance().Get(tabNo);

                alignInfo.InspectionTime = AppsInspResult.Instance().EndInspTime.ToString("HH:mm:ss");
                alignInfo.PanelID = AppsInspResult.Instance().Cell_ID;
                alignInfo.TabNo = tabInspResult.TabNo;
                alignInfo.Judgement = tabInspResult.AlignResult.Judgement;
                alignInfo.PreHead = tabInspResult.AlignResult.PreHead;
                alignInfo.FinalHead = AppsInspResult.Instance().FinalHead;
                alignInfo.LX = tabInspResult.AlignResult.GetStringLx_um();
                alignInfo.LY = tabInspResult.AlignResult.GetStringLy_um();
                alignInfo.CX = tabInspResult.AlignResult.GetStringCx_um();
                alignInfo.RX = tabInspResult.AlignResult.GetStringRx_um();
                alignInfo.RY = tabInspResult.AlignResult.GetStringRy_um();
                alignInfo.ImagePath = GetAlignResultPath(alignInfo.Judgement, "Align");

                dailyData.AddAlignInfo(alignInfo);
            }
        }

        private void UpdateAkkonDailyInfo(ref DailyData dailyData)
        {
            int tabCount = (ModelManager.Instance().CurrentModel as AppsInspModel).TabCount;

            for (int tabNo = 0; tabNo < tabCount; tabNo++)
            {
                AkkonDailyInfo akkonInfo = new AkkonDailyInfo();

                var tabInspResult = AppsInspResult.Instance().Get(tabNo);
                var akkonResult = tabInspResult.AkkonResult;

                akkonInfo.InspectionTime = AppsInspResult.Instance().EndInspTime.ToString("HH:mm:ss");
                akkonInfo.PanelID = AppsInspResult.Instance().Cell_ID;
                akkonInfo.TabNo = tabInspResult.TabNo;

                int minCount = 0;
                float minLength = 0.0F;
                if (akkonResult != null)
                {
                    akkonInfo.Judgement = akkonResult.Judgement;
                    minCount = akkonResult.LeftCount_Avg > akkonResult.RightCount_Min ? akkonResult.RightCount_Min : akkonResult.LeftCount_Avg;
                    minLength = akkonResult.Length_Left_Min_um > akkonResult.Length_Right_Min_um ? akkonResult.Length_Right_Min_um : akkonResult.Length_Left_Min_um;
                }

                akkonInfo.MinBlobCount = minCount;
                akkonInfo.MinLength = minLength;

                dailyData.AddAkkonInfo(akkonInfo);
            }
        }

        private void SaveInspResultCSV()
        {
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            DateTime currentTime = AppsInspResult.Instance().StartInspTime;

            string date = currentTime.ToString("yyyyMMdd");
            string folderPath = AppsInspResult.Instance().Cell_ID;

            string path = Path.Combine(ConfigSet.Instance().Path.Result, inspModel.Name, date);

            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            SaveAlignResult(path, inspModel.TabCount);
            SaveAkkonResult(path, inspModel.TabCount);
            SaveUPHResult(path, inspModel.TabCount);

            if (AppsConfig.Instance().EnableAkkonLeadResultLog == true)
                SaveAkkonLeadResults(path, inspModel.TabCount);

            if (AppsConfig.Instance().EnableMsaSummary == true)
            {
                SaveAkkonResultAsMsaSummary(path, inspModel.TabCount);
                SaveAlignResultAsMsaSummary(path, inspModel.TabCount);
            }
        }

        private void SaveAlignResult(string resultPath, int tabCount)
        {
            DateTime currentTime = AppsInspResult.Instance().StartInspTime;
            string date = currentTime.ToString("yyyyMMdd");
            string csvFileName = $"Align_{date}.csv";
            string csvFile = Path.Combine(resultPath, csvFileName);
            if (File.Exists(csvFile) == false)
            {
                List<string> header = new List<string>
                {
                    "Inspection Time",
                    "Panel ID",
                    "Stage No",
                    "F",
                };
                for (int index = 0; index < tabCount; index++)
                {
                    header.Add($"Tab");
                    header.Add($"Judge");
                    header.Add($"P");
                    header.Add($"Cx");
                    header.Add($"Lx");
                    header.Add($"Rx");
                    header.Add($"Ly");
                    header.Add($"Ry");
                }

                CSVHelper.WriteHeader(csvFile, header);
            }

            List<string> body = new List<string>();

            var programType = StringHelper.StringToEnum<ProgramType>(AppsConfig.Instance().ProgramType);
            body.Add($"{AppsInspResult.Instance().EndInspTime:HH:mm:ss}");                  // Insp Time
            body.Add($"{AppsInspResult.Instance().Cell_ID}");                               // Panel ID
            body.Add($"{(int)programType + 1}");                                            // Stage No
            body.Add($"{AppsInspResult.Instance().FinalHead}");                             // Final Head

            for (int tabNo = 0; tabNo < tabCount; tabNo++)
            {
                var tabInspResult = AppsInspResult.Instance().Get(tabNo);
                var alignResult = tabInspResult.AlignResult;

                var lx = tabInspResult.AlignResult.GetStringLx_um();
                var ly = tabInspResult.AlignResult.GetStringLy_um();
                var rx = tabInspResult.AlignResult.GetStringRx_um();
                var ry = tabInspResult.AlignResult.GetStringRy_um();
                var cx = tabInspResult.AlignResult.GetStringCx_um();

                body.Add($"{tabInspResult.TabNo + 1}");                                     // Tab No
                body.Add($"{alignResult.Judgement}");                                       // Judge
                body.Add($"{alignResult.PreHead}");                                         // Pre Head
                body.Add($"{lx}");                                                       // Align Lx
                body.Add($"{ly}");                                                       // Align Ly
                body.Add($"{cx}");                                                                          // Align Cx
                body.Add($"{rx}");                                                       // Align Rx
                body.Add($"{ry}");                                                       // Align Ry
            }

            CSVHelper.WriteData(csvFile, body);
        }

        private void SaveAkkonResult(string resultPath, int tabCount)
        {
            DateTime currentTime = AppsInspResult.Instance().StartInspTime;
            string date = currentTime.ToString("yyyyMMdd");
            string csvFileName = $"Akkon_{date}.csv";
            string csvFile = Path.Combine(resultPath, csvFileName);
            if (File.Exists(csvFile) == false)
            {
                List<string> header = new List<string>
                {
                    "Inspection Time",
                    "Panel ID",
                    "Stage No",
                };
                for (int index = 0; index < tabCount; index++)
                {
                    header.Add($"Tab_{index + 1}");
                    header.Add($"Judge_{index + 1}");
                    header.Add($"Min Count_{index + 1}");
                    header.Add($"Max Count_{index + 1}");
                    header.Add($"Avg Count_{index + 1}");
                    header.Add($"Avg Length_{index + 1}");
                }

                CSVHelper.WriteHeader(csvFile, header);
            }

            List<string> body = new List<string>();

            var programType = StringHelper.StringToEnum<ProgramType>(AppsConfig.Instance().ProgramType);
            body.Add($"{AppsInspResult.Instance().EndInspTime:HH:mm:ss}");                  // Insp Time
            body.Add($"{AppsInspResult.Instance().Cell_ID}");                               // Panel ID
            body.Add($"{(int)programType + 1}");                                            // Stage No

            for (int tabNo = 0; tabNo < tabCount; tabNo++)
            {
                var tabInspResult = AppsInspResult.Instance().Get(tabNo);
                var akkonResult = tabInspResult.AkkonResult;

                int minCount = Math.Min(akkonResult.LeftCount_Min, akkonResult.RightCount_Min);
                int maxCount = Math.Max(akkonResult.LeftCount_Max, akkonResult.RightCount_Max);

                int avgCount = (akkonResult.LeftCount_Avg + akkonResult.RightCount_Avg) / 2;
                float avgLength = (akkonResult.Length_Left_Avg_um + akkonResult.Length_Right_Avg_um) / 2.0F;

                body.Add($"{tabInspResult.TabNo + 1}");                                     // Tab No
                body.Add($"{akkonResult.Judgement}");                                       // Judge
                body.Add($"{minCount}");                                                    // Min Count
                body.Add($"{maxCount}");                                                    // Max Count
                body.Add($"{avgCount}");                                                    // Average Count
                body.Add($"{avgLength:F3}");                                                // Average Length

            }

            CSVHelper.WriteData(csvFile, body);
        }

        private void SaveUPHResult(string resultPath, int tabCount)
        {
            string filename = string.Format("UPH.csv");
            string csvFile = Path.Combine(resultPath, filename);
            if (File.Exists(csvFile) == false)
            {
                List<string> header = new List<string>
                {
                    "Inspection Time",
                    "Panel ID",
                    "Stage No",
                    "Tab No.",

                    "Count Min",
                    "Count Avg",
                    "Length Min",
                    "Length Avg",

                    "Pre Head",
                    "Main Head",

                    "Left Align X",
                    "Left Align Y",
                    "Center Align X",
                    "Right Align X",
                    "Right Align Y",

                    "AkkonJudge",
                    "AlignJudge",
                };

                CSVHelper.WriteHeader(csvFile, header);
            }

            List<List<string>> body = new List<List<string>>();
            for (int tabNo = 0; tabNo < tabCount; tabNo++)
            {
                var tabInspResult = AppsInspResult.Instance().Get(tabNo);

                int countMin = Math.Min(tabInspResult.AkkonResult.LeftCount_Min, tabInspResult.AkkonResult.RightCount_Min);
                float countAvg = (tabInspResult.AkkonResult.LeftCount_Avg + tabInspResult.AkkonResult.RightCount_Avg) / 2.0F;
                float lengthMin = Math.Min(tabInspResult.AkkonResult.Length_Left_Min_um, tabInspResult.AkkonResult.Length_Right_Min_um);
                float lengthAvg = (tabInspResult.AkkonResult.Length_Left_Avg_um + tabInspResult.AkkonResult.Length_Right_Avg_um) / 2.0F;

                string preHead = tabInspResult.AlignResult.PreHead;
                string finalHead = AppsInspResult.Instance().FinalHead;
                var alignResult = tabInspResult.AlignResult;

                var programType = StringHelper.StringToEnum<ProgramType>(AppsConfig.Instance().ProgramType);
                List<string> tabData = new List<string>
                {
                    $"{AppsInspResult.Instance().EndInspTime:HH:mm:ss}",                    // Insp Time
                    $"{AppsInspResult.Instance().Cell_ID}",                                 // Panel ID
                    $"{(int)programType + 1}",                                              // Unit No
                    $"{tabInspResult.TabNo + 1}",                                          // Tab

                    $"{countMin}",                                                          // Count Min
                    $"{countAvg:F3}",                                                       // Count Avg
                    $"{lengthMin:F3}",                                                      // Length Min
                    $"{lengthAvg:F3}",                                                      // Length Avg

                    $"{preHead}",                                                           // Pre Head
                    $"{finalHead}",                                                         // Final Head

                    $"{alignResult.GetStringLx_um()}",                                                             // Left Align X
                    $"{alignResult.GetStringLy_um()}",                                                             // Left Align Y
                    $"{alignResult.GetStringCx_um()}",                                                             // Center Align X
                    $"{alignResult.GetStringRx_um()}",                                                             // Right Align X
                    $"{alignResult.GetStringRy_um()}",                                                             // Right Align Y

                    $"{tabInspResult.AkkonResult.Judgement}",                                          // Akkon Judge
                    $"{tabInspResult.AlignResult.Judgement}",                                          // Align Judge
                };

                body.Add(tabData);
            }

            CSVHelper.WriteData(csvFile, body);
        }

        private void SaveAkkonLeadResults(string resultPath, int tabCount)
        {
            for (int tabIndex = 0; tabIndex < tabCount; tabIndex++)
            {
                var tabResult = AppsInspResult.Instance().Get(tabIndex).AkkonResult;
                string csvFile = Path.Combine(resultPath, $"AkkonLeadResult_Tab{tabIndex + 1}.csv");

                if (File.Exists(csvFile) == false)
                {
                    List<string> header = new List<string>() { "Panel ID" };
                    for (int index = 0; index < 2000; index++)
                        header.Add($"Lead{index + 1}");

                    CSVHelper.WriteHeader(csvFile, header);
                }

                List<string> body = new List<string>() { AppsInspResult.Instance().Cell_ID };
                for (int index = 0; index < tabResult.LeadResultList.Count; index++)
                    body.Add($"{tabResult.LeadResultList[index].AkkonCount}");
                CSVHelper.WriteData(csvFile, body);
            }
        }

        private void SaveAkkonResultAsMsaSummary(string resultPath, int tabCount)
        {
            // Write Summary for each tabs
            for (int tabNo = 0; tabNo < tabCount; tabNo++)
            {
                var tabInspResult = AppsInspResult.Instance().Get(tabNo);
                var positions = new string[] { "Left", "Center", "Right" };

                // Add header strings
                var header = new List<string>
                {
                    "Panel ID",
                    "Inspection Time",
                };
                foreach (string position in positions)
                {
                    for (int index = 0; index < tabInspResult.ResultSamplingCount; index++)
                        header.Add($"{position}{index}");
                }

                // Add Body strings
                var body = new List<string>
                {
                    AppsInspResult.Instance().Cell_ID,
                    $"{AppsInspResult.Instance().EndInspTime:HH:mm:ss}",
                };
                foreach (string position in positions)
                {
                    var akkonCount = tabInspResult.GetAkkonCounts(position).Select(count => count.ToString());
                    body.AddRange(akkonCount);
                }

                if (header.Count != body.Count)
                    Console.WriteLine($"[SaveAkkonResultAsMsaSummary()] Column counts are not matched");

                // Write a CSV file
                string filename = string.Format($"AkkonSummary_Tab{tabNo}.csv");
                string csvFile = Path.Combine(resultPath, filename);
                CSVHelper.WriteHeader(csvFile, header);
                CSVHelper.WriteData(csvFile, body);
            }
        }

        private void SaveAlignResultAsMsaSummary(string resultPath, int tabCount)
        {
            // Write Summary for each tabs
            for (int tabNo = 0; tabNo < tabCount; tabNo++)
            {
                var tabInspResult = AppsInspResult.Instance().Get(tabNo).AlignResult;

                // Add header strings
                var header = new List<string>
                {
                    "Panel ID",
                    "Inspection Time",
                    "Pre Head",
                    "Final Head",
                    "LeftX",
                    "LeftY",
                    "CenteX",
                    "RightX",
                    "RightY",
                };

                // Add Body strings
                var cx = (CheckAlignResultValue(tabInspResult.LeftX) + CheckAlignResultValue(tabInspResult.RightX)) / 2.0F;
                var body = new List<string>
                {
                    AppsInspResult.Instance().Cell_ID,
                    $"{AppsInspResult.Instance().EndInspTime:HH:mm:ss}",
                    $"{tabInspResult.PreHead}",
                    $"{AppsInspResult.Instance().FinalHead}",
                    $"{tabInspResult.GetStringLx_um()}",
                    $"{tabInspResult.GetStringLy_um()}",
                    $"{tabInspResult.GetStringCx_um()}",
                    $"{tabInspResult.GetStringRx_um()}",
                    $"{tabInspResult.GetStringRy_um()}",
                };

                if (header.Count != body.Count)
                    Console.WriteLine($"[SaveAlignResultAsMsaSummary()] Column counts are not matched");

                // Write a CSV file
                string filename = string.Format($"AlignSummary_Tab{tabNo}.csv");
                string csvFile = Path.Combine(resultPath, filename);
                CSVHelper.WriteHeader(csvFile, header);
                CSVHelper.WriteData(csvFile, body);
            }
        }

        private double CheckAlignResultValue(AlignResult alignResult)
        {
            float resolution = LineCamera.Camera.PixelResolution_um / LineCamera.Camera.LensScale;
            if (alignResult == null)
                return 0.0F;
            else
                return MathHelper.GetFloorDecimal(alignResult.ResultValue_pixel * resolution, 4);
        }

        private Axis GetAxis(AxisHandlerName axisHandlerName, AxisName axisName)
        {
            return MotionManager.Instance().GetAxis(axisHandlerName, axisName);
        }

        public bool IsAxisInPosition(UnitName unitName, TeachingPosType teachingPos, Axis axis)
        {
            return MotionManager.Instance().IsAxisInPosition(unitName, teachingPos, axis);
        }

        public bool MoveTo(TeachingPosType teachingPos, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (ConfigSet.Instance().Operation.VirtualMode)
                return true;

            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            var teachingInfo = inspModel.GetUnit(UnitName.Unit0).GetTeachingInfo(teachingPos);

            Axis axisX = GetAxis(AxisHandlerName.Handler0, AxisName.X);
            Axis axisY = GetAxis(AxisHandlerName.Handler0, AxisName.Y);

            var movingParamX = teachingInfo.GetMovingParam(AxisName.X.ToString());
            var movingParamY = teachingInfo.GetMovingParam(AxisName.Y.ToString());
            var movingParamZ = teachingInfo.GetMovingParam(AxisName.Z0.ToString());

            if (MoveAxis(teachingPos, axisX, movingParamX) == false)
            {
                errorMessage = string.Format("Move To Axis X TimeOut!({0})", movingParamX.MovingTimeOut.ToString());
                WriteLog(errorMessage);
                return false;
            }
						
            if (MoveAxis(teachingPos, axisY, movingParamY) == false)
            {
                errorMessage = string.Format("Move To Axis Y TimeOut!({0})", movingParamY.MovingTimeOut.ToString());
                WriteLog(errorMessage);
                return false;
            }

            string message = string.Format("Move Completed.(Teaching Pos : {0})", teachingPos.ToString());
            WriteLog(message);

            return true;
        }

        private bool MoveAxis(TeachingPosType teachingPos, Axis axis, AxisMovingParam movingParam)
        {
            MotionManager manager = MotionManager.Instance();
            double cameraGap = 0;
            if (teachingPos == TeachingPosType.Stage1_Scan_End)
                cameraGap = AppsConfig.Instance().CameraGap_mm;
                
            if (manager.IsAxisInPosition(UnitName.Unit0, teachingPos, axis, cameraGap) == false)
            {
                Stopwatch sw = new Stopwatch();
                sw.Restart();

                manager.StartAbsoluteMove(UnitName.Unit0, teachingPos, axis, cameraGap);

                while (manager.IsAxisInPosition(UnitName.Unit0, teachingPos, axis, cameraGap) == false)
                {
                    if (sw.ElapsedMilliseconds >= movingParam.MovingTimeOut)
                        return false;

                    Thread.Sleep(10);
                }
            }

            return true;
        }

        public Mat GetResultImage(Mat resizeMat, List<AkkonLeadResult> leadResultList, AkkonAlgoritmParam akkonParameters, ref List<CogRectangleAffine> akkonNGAffineList)
        {
            if (resizeMat == null)
                return null;

            Mat colorMat = new Mat();
            CvInvoke.CvtColor(resizeMat, colorMat, ColorConversion.Gray2Bgr);

            MCvScalar redColor = new MCvScalar(50, 50, 230, 255);
            MCvScalar greenColor = new MCvScalar(50, 230, 50, 255);
            MCvScalar orangeColor = new MCvScalar(0, 165, 255);

            DrawParam autoDrawParam = new DrawParam();
            autoDrawParam.ContainLeadCount = true;

            foreach (var result in leadResultList)
            {
                var lead = result.Roi;
                var startPoint = new Point((int)result.Offset.ToWorldX, (int)result.Offset.ToWorldY);

                Point leftTop = new Point((int)lead.LeftTopX + startPoint.X, (int)lead.LeftTopY + startPoint.Y);
                Point leftBottom = new Point((int)lead.LeftBottomX + startPoint.X, (int)lead.LeftBottomY + startPoint.Y);
                Point rightTop = new Point((int)lead.RightTopX + startPoint.X, (int)lead.RightTopY + startPoint.Y);
                Point rightBottom = new Point((int)lead.RightBottomX + startPoint.X, (int)lead.RightBottomY + startPoint.Y);

                // 향후 Main 페이지 ROI 보여 달라고 하면 ContainLeadROI = true로 속성 변경
                if (autoDrawParam.ContainLeadROI)
                {
                    CvInvoke.Line(colorMat, leftTop, leftBottom, new MCvScalar(50, 230, 50, 255), 1);
                    CvInvoke.Line(colorMat, leftTop, rightTop, new MCvScalar(50, 230, 50, 255), 1);
                    CvInvoke.Line(colorMat, rightTop, rightBottom, new MCvScalar(50, 230, 50, 255), 1);
                    CvInvoke.Line(colorMat, rightBottom, leftBottom, new MCvScalar(50, 230, 50, 255), 1);
                }
                if (result.Judgement == Judgement.NG)
                {
                    CvInvoke.Line(colorMat, leftTop, leftBottom, redColor, 1);
                    CvInvoke.Line(colorMat, leftTop, rightTop, redColor, 1);
                    CvInvoke.Line(colorMat, rightTop, rightBottom, redColor, 1);
                    CvInvoke.Line(colorMat, rightBottom, leftBottom, redColor, 1);

                    var rect = VisionProShapeHelper.ConvertToCogRectAffine(leftTop, rightTop, leftBottom);
                    akkonNGAffineList.Add(rect);
                }

                int blobCount = 0;
                foreach (var blob in result.BlobList)
                {
                    Rectangle rectRect = new Rectangle();
                    rectRect.X = (int)(blob.BoundingRect.X + result.Offset.ToWorldX + result.Offset.X);
                    rectRect.Y = (int)(blob.BoundingRect.Y + result.Offset.ToWorldY + result.Offset.Y);
                    rectRect.Width = blob.BoundingRect.Width;
                    rectRect.Height = blob.BoundingRect.Height;

                    Point center = new Point(rectRect.X + (rectRect.Width / 2), rectRect.Y + (rectRect.Height / 2));
                    int radius = rectRect.Width > rectRect.Height ? rectRect.Width : rectRect.Height;

                    int size = blob.BoundingRect.Width * blob.BoundingRect.Height;

                    if (blob.IsAkkonShape)
                    {
                        blobCount++;
                        CvInvoke.Circle(colorMat, center, radius / 2, greenColor, 1);
                    }
                    else
                    {
                        double strengthValue = Math.Abs(blob.Strength - akkonParameters.ShapeFilterParam.MinAkkonStrength);
                        if (strengthValue <= 1)
                        {
                            int temp = (int)(radius / 2.0);
                            Point pt = new Point(center.X + temp, center.Y - temp);
                            string strength = blob.Strength.ToString("F1");

                            CvInvoke.Circle(colorMat, center, radius / 2, orangeColor, 1);
                            CvInvoke.PutText(colorMat, strength, pt, FontFace.HersheySimplex, 0.3, orangeColor);
                        }
                    }
                }

                if (autoDrawParam.ContainLeadCount)
                {
                    string leadIndexString = result.Roi.Index.ToString();
                    string blobCountString = string.Format("[{0}]", blobCount);

                    Point centerPoint = new Point((int)((leftBottom.X + rightBottom.X) / 2.0), leftBottom.Y);

                    int baseLine = 0;
                    Size textSize = CvInvoke.GetTextSize(leadIndexString, FontFace.HersheyComplex, 0.3, 1, ref baseLine);
                    int textX = centerPoint.X - (textSize.Width / 2);
                    int textY = centerPoint.Y + (baseLine / 2);
                    CvInvoke.PutText(colorMat, leadIndexString, new Point(textX, textY + 30), FontFace.HersheyComplex, 0.3, new MCvScalar(50, 230, 50, 255));

                    textSize = CvInvoke.GetTextSize(blobCountString, FontFace.HersheyComplex, 0.3, 1, ref baseLine);
                    textX = centerPoint.X - (textSize.Width / 2);
                    textY = centerPoint.Y + (baseLine / 2);
                    CvInvoke.PutText(colorMat, blobCountString, new Point(textX, textY + 60), FontFace.HersheyComplex, 0.3, new MCvScalar(50, 230, 50, 255));
                }
            }

            return colorMat;
        }

        public ICogImage ConvertCogColorImage(Mat mat)
        {
            Mat matR = MatHelper.ColorChannelSeperate(mat, MatHelper.ColorChannel.R);
            Mat matG = MatHelper.ColorChannelSeperate(mat, MatHelper.ColorChannel.G);
            Mat matB = MatHelper.ColorChannelSeperate(mat, MatHelper.ColorChannel.B);

            byte[] dataR = new byte[matR.Width * matR.Height];
            Marshal.Copy(matR.DataPointer, dataR, 0, matR.Width * matR.Height);

            byte[] dataG = new byte[matG.Width * matG.Height];
            Marshal.Copy(matG.DataPointer, dataG, 0, matG.Width * matG.Height);

            byte[] dataB = new byte[matB.Width * matB.Height];
            Marshal.Copy(matB.DataPointer, dataB, 0, matB.Width * matB.Height);

            var cogImage = VisionProImageHelper.CovertImage(dataR, dataG, dataB, matB.Width, matB.Height);

            matR.Dispose();
            matG.Dispose();
            matB.Dispose();

            return cogImage;
        }

        private CogImage8Grey ConvertCogGrayImage(Mat mat)
        {
            if (mat == null)
                return null;

            int size = mat.Width * mat.Height * mat.NumberOfChannels;
            var cogImage = VisionProImageHelper.CovertImage(mat.DataPointer, mat.Width, mat.Height, mat.Step, ColorFormat.Gray) as CogImage8Grey;
            return cogImage;
        }

        private void SaveImage()
        {
            try
            {
                if (ConfigSet.Instance().Operation.VirtualMode)
                {
                    _saveThread = null;
                    return;
                }

                var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

                Stopwatch sw = new Stopwatch();
                sw.Restart();

                string path = GetResultPath();
                for (int tabNo = 0; tabNo < inspModel.TabCount; tabNo++)
                    SaveResultImage(path, tabNo);

                sw.Stop();
                Console.WriteLine("Save Image : " + sw.ElapsedMilliseconds.ToString() + "ms");
                WriteLog("Save Image : " + sw.ElapsedMilliseconds.ToString() + "ms");
                _saveThread = null;
            }
            catch (Exception err)
            {
                _saveThread = null;
                Logger.Error(ErrorType.Etc, "SaveImage Error : " + err.Message);
            }
        }

        private void SaveResultImage(string resultPath, int tabNo)
        {
            var tabInspResult = AppsInspResult.Instance().Get(tabNo);

            string imageName = $"{AppsInspResult.Instance().Cell_ID}_Tab_{tabInspResult.TabNo}";
            var operation = ConfigSet.Instance().Operation;

            string path = Path.Combine(resultPath, "Inspection");
            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            string okExtension = operation.GetExtensionOKImage();
            string ngExtension = operation.GetExtensionNGImage();

            if (tabInspResult.Judgement == TabJudgement.OK || tabInspResult.Judgement == TabJudgement.Manual_OK)
            {
                if (ConfigSet.Instance().Operation.SaveImageOK)
                {
                    string filePath = Path.Combine(path, imageName);

                    if (operation.ExtensionOKImage == ImageExtension.Bmp)
                        SaveImage(tabInspResult.Image, filePath, imageName, Judgement.OK, ImageExtension.Bmp, false);
                    else if (operation.ExtensionOKImage == ImageExtension.Jpg)
                    {
                        if (tabInspResult.Image.Width > SAVE_IMAGE_MAX_WIDTH)
                            SaveImage(tabInspResult.Image, filePath, imageName, Judgement.OK, ImageExtension.Jpg, true);
                        else
                            SaveImage(tabInspResult.Image, filePath, imageName, Judgement.OK, ImageExtension.Jpg, false);
                    }
                }
            }
            else
            {
                if (ConfigSet.Instance().Operation.SaveImageNG)
                {
                    string filePath = Path.Combine(path, imageName);
                    if (operation.ExtensionNGImage == ImageExtension.Bmp)
                        SaveImage(tabInspResult.Image, filePath, imageName, Judgement.NG, ImageExtension.Bmp, false);
                    else if (operation.ExtensionNGImage == ImageExtension.Jpg)
                    {
                        if (tabInspResult.Image.Width > SAVE_IMAGE_MAX_WIDTH)
                            SaveImage(tabInspResult.Image, filePath, imageName, Judgement.NG, ImageExtension.Jpg, true);
                        else
                            SaveImage(tabInspResult.Image, filePath, imageName, Judgement.NG, ImageExtension.Jpg, false);
                    }

                    if (tabInspResult.AkkonResult.Judgement == Judgement.NG)
                    {
                        string akkonLeadImagePath = Path.Combine(resultPath, "Inspection", "AkkonLeadImage");
                        if (Directory.Exists(akkonLeadImagePath) == false)
                            Directory.CreateDirectory(akkonLeadImagePath);
                        SaveAkkonDefectLeadImage(akkonLeadImagePath, tabInspResult);
                    }
                }
            }

            SaveAlignResult(tabInspResult, path);
        }

        public void SaveAlignResult(TabInspResult tabInspResult, string path)
        {
            if (tabInspResult == null)
                return;

            string savePath = string.Empty;
            if (tabInspResult.AlignResult.Judgement == Judgement.OK)
                savePath = Path.Combine(path, Judgement.OK.ToString(), "Result");
            else
                savePath = Path.Combine(path, Judgement.NG.ToString(), "Result");

            if (Directory.Exists(savePath) == false)
                Directory.CreateDirectory(savePath);

            if (tabInspResult.AlignResult.CenterImage != null)
            {
                string fileName = string.Format("Center_Align_Tab_{0}.bmp", tabInspResult.TabNo);
                string filePath = Path.Combine(savePath, fileName);
                VisionProImageHelper.Save(tabInspResult.AlignResult.CenterImage, filePath);
            }

            string lxData = tabInspResult.AlignResult.GetStringLx_um();
            string lyData = tabInspResult.AlignResult.GetStringLy_um();
            string rxData = tabInspResult.AlignResult.GetStringRx_um();
            string ryData = tabInspResult.AlignResult.GetStringRy_um();
            string cxData = tabInspResult.AlignResult.GetStringCx_um();

            var leftAlignShapeList = tabInspResult.GetLeftAlignShapeList();
            if (leftAlignShapeList.Count() > 0)
            {
                PointF offset = new PointF();
                string orgFileName = string.Format("Left_Align_Tab_{0}_Org.bmp", tabInspResult.TabNo);
                string orgFilePath = Path.Combine(savePath, orgFileName);

                Mat cropLeftImage = GetAlignResultImage(tabInspResult, leftAlignShapeList, out offset, orgFilePath);

                var leftFpcMark = tabInspResult.MarkResult.FpcMark.FoundedMark.Left;
                if (leftFpcMark != null)
                {
                    if (leftFpcMark.Found)
                    {
                        var resultGraphics = leftFpcMark.MaxMatchPos.ResultGraphics;
                        DrawPatternShape(ref cropLeftImage, leftFpcMark.Judgement, resultGraphics, offset);
                    }
                }

                var leftPanelMark = tabInspResult.MarkResult.PanelMark.FoundedMark.Left;
                if (leftPanelMark != null)
                {
                    if (leftPanelMark.Found)
                    {
                        var resultGraphics = leftPanelMark.MaxMatchPos.ResultGraphics;
                        DrawPatternShape(ref cropLeftImage, leftPanelMark.Judgement, resultGraphics, offset);
                    }
                }

                if (tabInspResult.AlignResult != null)
                {
                    DrawAlignResultString(ref cropLeftImage, $"{AlignResultType.Lx} : {lxData}um", 1);
                    DrawAlignResultString(ref cropLeftImage, $"{AlignResultType.Ly} : {lyData}um", 2);
                    DrawAlignResultString(ref cropLeftImage, $"{AlignResultType.Cx} : {cxData}um", 3);
                }

                string fileName = string.Format("Left_Align_Tab_{0}.bmp", tabInspResult.TabNo);
                string filePath = Path.Combine(savePath, fileName);
                cropLeftImage?.Save(filePath);
            }

            var rightAlignShapeList = tabInspResult.GetRightAlignShapeList();
            if (rightAlignShapeList.Count() > 0)
            {
                PointF offset = new PointF();
                string orgFileName = string.Format("Right_Align_Tab_{0}_Org.bmp", tabInspResult.TabNo);
                string orgFilePath = Path.Combine(savePath, orgFileName);

                Mat cropRightImage = GetAlignResultImage(tabInspResult, rightAlignShapeList, out offset, orgFilePath);

                var rightFpcMark = tabInspResult.MarkResult.FpcMark.FoundedMark.Right;
                if (rightFpcMark != null)
                {
                    if (rightFpcMark.Found)
                    {
                        var resultGraphics = rightFpcMark.MaxMatchPos.ResultGraphics;
                        DrawPatternShape(ref cropRightImage, rightFpcMark.Judgement, resultGraphics, offset);
                    }
                }

                var rightPanelMark = tabInspResult.MarkResult.PanelMark.FoundedMark.Right;
                if (rightPanelMark != null)
                {
                    if (rightPanelMark.Found)
                    {
                        var resultGraphics = rightPanelMark.MaxMatchPos.ResultGraphics;
                        DrawPatternShape(ref cropRightImage, rightPanelMark.Judgement, resultGraphics, offset);
                    }
                }

                if (tabInspResult.AlignResult != null)
                {
                    DrawAlignResultString(ref cropRightImage, $"{AlignResultType.Rx} : {rxData}um", 1);
                    DrawAlignResultString(ref cropRightImage, $"{AlignResultType.Ry} : {ryData}um", 2);
                    DrawAlignResultString(ref cropRightImage, $"{AlignResultType.Cx} : {cxData}um", 3);
                }

                string fileName = string.Format("Right_Align_Tab_{0}.bmp", tabInspResult.TabNo);
                string filePath = Path.Combine(savePath, fileName);
                cropRightImage?.Save(filePath);
            }
        }

        private void DrawPatternShape(ref Mat mat, Judgement judgement, CogCompositeShape resultGraphics, PointF cropOffset)
        {
            var drawColor = new MCvScalar(50, 230, 50, 255);

            if (judgement == Judgement.OK)
                drawColor = new MCvScalar(50, 230, 50, 255);
            else
                drawColor = new MCvScalar(50, 50, 230, 255);

            foreach (var shape in resultGraphics.Shapes)
            {
                if (shape is CogPointMarker marker)
                {
                    int newX = (int)(marker.X - cropOffset.X);
                    int newY = (int)(marker.Y - cropOffset.Y);

                    CvInvoke.DrawMarker(mat, new Point(newX, newY), drawColor, MarkerTypes.Cross);
                }

                if (shape is CogRectangleAffine pattern)
                {
                    Rectangle boundRect = VisionProShapeHelper.ConvertAffineRectToRect(pattern, -cropOffset.X, -cropOffset.Y);
                    CvInvoke.Rectangle(mat, boundRect, drawColor);
                }
            }
        }

        private void DrawAlignResultString(ref Mat mat, string resultString, int lineIndex)
        {
            double fontScale = 3;
            int lineOffset = 100;
            Point coord = new Point((int)fontScale * 10, lineIndex * (lineOffset + (int)fontScale));
            MCvScalar color = new MCvScalar(255, 255, 255, 255);

            CvInvoke.PutText(mat, resultString, coord, FontFace.HersheySimplex, fontScale, color);
        }

        private Mat GetAlignResultImage(TabInspResult tabInspResult, List<AlignGraphicPosition> graphicList, out PointF offsetPoint, string orgSavePath)
        {
            offsetPoint = new PointF();
            MCvScalar fpcColor = new MCvScalar(255, 0, 0);
            MCvScalar panelColor = new MCvScalar(0, 165, 255);

            var roi = GetCropROI(graphicList, tabInspResult.Image.Width, tabInspResult.Image.Height);
            var cropImage = new Mat(tabInspResult.Image, roi);

            if (orgSavePath != null)
                cropImage?.Save(orgSavePath);

            CvInvoke.CvtColor(cropImage, cropImage, ColorConversion.Gray2Bgr);

            offsetPoint.X = roi.X;
            offsetPoint.Y = roi.Y;

            foreach (var shape in graphicList)
            {
                Point startPoint = new Point((int)shape.StartX - roi.X, (int)shape.StartY - roi.Y);
                Point endPoint = new Point((int)shape.EndX - roi.X, (int)shape.EndY - roi.Y);

                if (shape.IsFpc)
                    CvInvoke.Line(cropImage, startPoint, endPoint, fpcColor);
                else
                    CvInvoke.Line(cropImage, startPoint, endPoint, panelColor);
            }

            return cropImage;
        }

        private string GetResultPath()
        {
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            DateTime currentTime = AppsPreAlignResult.Instance().StartInspTime;

            string date = currentTime.ToString("yyyyMMdd");
            string timeStamp = currentTime.ToString("yyyyMMddHHmmss");
            string cellId = AppsPreAlignResult.Instance().Cell_ID + "_" + timeStamp;

            string path = Path.Combine(ConfigSet.Instance().Path.Result, inspModel.Name, date, cellId);

            return path;
        }

        private string GetAlignResultPath(Judgement judgement, string inspType)
        {
            string path = GetResultPath();
            if (judgement == Judgement.OK)
                return Path.Combine(path, inspType, Judgement.OK.ToString());
            else
                return Path.Combine(path, inspType, Judgement.NG.ToString());
        }

        private Rectangle GetCropROI(List<AlignGraphicPosition> alignShapeList, int imageWidth, int imageHeight)
        {
            List<PointF> pointList = new List<PointF>();

            foreach (var shape in alignShapeList)
            {
                pointList.Add(new PointF((float)shape.StartX, (float)shape.StartY));
                pointList.Add(new PointF((float)shape.EndX, (float)shape.EndY));
            }

            float minX = pointList.Select(point => point.X).Min();
            float maxX = pointList.Select(point => point.X).Max();

            float minY = pointList.Select(point => point.Y).Min();
            float maxY = pointList.Select(point => point.Y).Max();

            float width = Math.Abs(maxX - minX) / 2.0f;
            float height = Math.Abs(maxY - minY) / 2.0f;

            PointF centerPoint = new PointF((minX + width), (minY + height));

            float size = 2000.0f;

            var roi = new Rectangle((int)centerPoint.X - (int)(size / 2.0f), (int)centerPoint.Y - (int)(size / 2.0f), (int)size, (int)size);
            if (roi.X < 0)
                roi.X = 0;

            if (roi.Y < 0)
                roi.Y = 0;

            if (roi.X + roi.Width > imageWidth)
                roi.Width = imageWidth - roi.X;

            if (roi.Y + roi.Height > imageHeight)
                roi.Height = imageHeight - roi.Y;

            return roi;
        }

        private void SaveAkkonDefectLeadImage(string resultPath, TabInspResult tabInspResult)
        {
            int maxSaveCount = 20;
            int saveCount = 0;
            foreach (var leadResult in tabInspResult.AkkonResult.LeadResultList)
            {
                if (saveCount > maxSaveCount)
                    break;

                string akkonNGDirName = string.Format("Tab{0}_NG", tabInspResult.TabNo);
                string akkonNGDir = Path.Combine(resultPath, TabJudgement.NG.ToString(), akkonNGDirName);
                if (Directory.Exists(akkonNGDir) == false)
                    Directory.CreateDirectory(akkonNGDir);

                if (leadResult.Judgement == Judgement.NG)
                {
                    AkkonROI roi = tabInspResult.AkkonResult.TrackingROIList.Where(x => x.Index == leadResult.Id).FirstOrDefault();
                    if (roi != null)
                    {
                        try
                        {
                            string fileName = string.Format("{0}_Count_{1}.jpg", leadResult.Id, leadResult.AkkonCount);
                            var boundRect = roi.GetBoundRect();
                            var centerPoint = new Point(boundRect.X + (boundRect.Width / 2), boundRect.Y + (boundRect.Height / 2));

                            Mat cropAkkonMat = MatHelper.CropRoi(tabInspResult.Image, boundRect);
                            string savePath = Path.Combine(akkonNGDir, fileName);
                            cropAkkonMat.Save(savePath);
                            cropAkkonMat.Dispose();
                            saveCount++;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ErrorType.Etc, "Save Akkon defect lead image error : " + ex.Message);
                        }
                    }
                }
            }
        }

        private void SaveImage(Mat image, string filePath, string imageName, Judgement judgement, ImageExtension extension, bool isHalfSave)
        {
            if (extension == ImageExtension.Bmp)
            {
                filePath += $"\\{imageName}_{judgement}.bmp";
                image.Save(filePath);
            }
            else if (extension == ImageExtension.Jpg)
            {
                if (isHalfSave)
                {
                    string leftPath = $"{filePath}_Left_{judgement}.jpg";
                    string rightPath = $"{filePath}_Right_{judgement}.jpg";

                    int half = image.Width / 2;
                    Rectangle leftRect = new Rectangle(0, 0, half, image.Height);
                    Rectangle rightRect = new Rectangle(half, 0, image.Width - half, image.Height);

                    Mat leftMat = new Mat(image, leftRect);
                    Mat rightMat = new Mat(image, rightRect);

                    leftMat.Save(leftPath);
                    rightMat.Save(rightPath);

                    leftMat.Dispose();
                    rightMat.Dispose();
                }
                else
                {
                    filePath += $"\\{imageName}_{judgement}.jpg";
                    image.Save(filePath);
                }
            }
        }

        private void DeleteData()
        {
            try
            {
                var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
                if (inspModel == null)
                {
                    _deleteThread = null;
                    return;
                }

                string resultPath = ConfigSet.Instance().Path.Result;
                string logPath = ConfigSet.Instance().Path.Log;

                int capacity = ConfigSet.Instance().Operation.DataStoringCapacity;

                DeleteDirectoryByCapacity(Path.Combine(resultPath, inspModel.Name), capacity);
                //DeleteDirectoryByCapacity(logPath, capacity);     logPath삭제는 설정한 Duration으로 삭제

                int duration = ConfigSet.Instance().Operation.DataStoringDuration;
                //FileHelper.DeleteFilesInDirectory(resultPath, ".*", duration);
                FileHelper.DeleteFilesInDirectory(logPath, ".*", duration);

                _deleteThread = null;
            }
            catch (Exception err)
            {
                Logger.Error(ErrorType.Etc, "Delete Data Error : " + err.Message);
                _deleteThread = null;
            }
        }

        private void WriteLog(string logMessage, bool isSystemLog = false)
        {
            if (isSystemLog)
                SystemManager.Instance().AddSystemLogMessage(logMessage);

            Logger.Write(LogType.Seq, logMessage);
        }

        private void DeleteDirectoryByCapacity(string folderPath, int capacity)
        {
            double useRate = FileHelper.CheckCapacity("D");
            if (useRate >= capacity)
            {
                var directoryList = FileHelper.GetDirectoryList(folderPath);
                string directoryPath = directoryList.FirstOrDefault();
                var oldDirectory = FileHelper.GetOldDirectory(directoryPath);
                if (oldDirectory == null)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
                    directoryInfo.Delete(true);
                }
                else
                    oldDirectory.Delete(true);
            }
        }

        public void VirtualGrabDone()
        {
            IsGrabDone = true;
        }

        public bool ClearBufferThread()
        {
            if (_ClearBufferThread == null)
            {
                _ClearBufferThread = new Thread(ClearBuffer);
                _ClearBufferThread.Start();
                return true;
            }
            return false;
        }

        private void ClearBuffer()
        {
            Console.WriteLine("ClearBuffer");
            LineCamera.IsLive = false;

            LineCamera.SetOperationMode(TDIOperationMode.TDI);

            LightCtrlHandler?.TurnOff();

            LAFCtrl?.SetTrackingOnOFF(false);
            LAFCtrl?.SetDefaultParameter();
            LineCamera.ClearTabScanBuffer();

            MotionManager.Instance().MoveAxisZ(UnitName.Unit0, TeachingPosType.Stage1_Scan_Start, LAFCtrl, AxisName.Z0);

            _ClearBufferThread = null;
            WriteLog("Clear Buffer.");
        }
        #endregion
    }

    public enum SeqStep
    {
        SEQ_IDLE,
        SEQ_INIT,
        SEQ_WAITING,
        SEQ_BUFFER_INIT,
        SEQ_MOVE_START_POS,
        SEQ_SCAN_START,
        SEQ_MOVE_END_POS,
        SEQ_WAITING_AKKON_SCAN_COMPLETED,
        SEQ_WAITING_ALIGN_SCAN_COMPLETED,
        SEQ_WAITING_INSPECTION_DONE,
        SEQ_MANUAL_CHECK,
        SEQ_SEND_RESULT,
        SEQ_WAIT_UI_RESULT_UPDATE,
        SEQ_SAVE_RESULT_DATA,
        SEQ_SAVE_IMAGE,
        SEQ_DELETE_DATA,
        SEQ_CHECK_STANDBY,
        SEQ_ERROR,
    }
}
