﻿using Cognex.VisionPro;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Jastech.Apps.Structure;
using Jastech.Apps.Structure.Data;
using Jastech.Apps.Structure.VisionTool;
using Jastech.Apps.Winform;
using Jastech.Apps.Winform.Core;
using Jastech.Apps.Winform.Core.Calibrations;
using Jastech.Apps.Winform.Service;
using Jastech.Apps.Winform.Service.Plc.Maps;
using Jastech.Apps.Winform.Settings;
using Jastech.Framework.Algorithms.Akkon;
using Jastech.Framework.Algorithms.Akkon.Parameters;
using Jastech.Framework.Algorithms.Akkon.Results;
using Jastech.Framework.Config;
using Jastech.Framework.Device.Cameras;
using Jastech.Framework.Device.Motions;
using Jastech.Framework.Imaging;
using Jastech.Framework.Imaging.Helper;
using Jastech.Framework.Imaging.Result;
using Jastech.Framework.Imaging.VisionPro;
using Jastech.Framework.Imaging.VisionPro.VisionAlgorithms.Results;
using Jastech.Framework.Util.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Jastech.Framework.Modeller.Controls.ModelControl;

namespace ATT_UT_Remodeling.Core
{
    public partial class ATTInspRunner
    {
        #region 필드
        private Axis _axis { get; set; } = null;

        private object _akkonLock = new object();

        private object _inspLock = new object();

        private Task _changedModelTask { get; set; } = null;

        private CancellationTokenSource _changedModelTaskCancellationTokenSource { get; set; }
        #endregion

        #region 속성
        private Task SeqTask { get; set; }

        private CancellationTokenSource SeqTaskCancellationTokenSource { get; set; }

        private SeqStep SeqStep { get; set; } = SeqStep.SEQ_IDLE;

        public bool IsPanelIn { get; set; } = false;

        private bool IsGrabDone { get; set; } = false;

        private AppsInspResult AppsInspResult { get; set; } = null;

        private Stopwatch LastInspSW { get; set; } = new Stopwatch();

        public Task AkkonInspTask { get; set; }

        public CancellationTokenSource CancelAkkonInspTask { get; set; }

        public Queue<AkkonThreadParam> AkkonInspQueue = new Queue<AkkonThreadParam>();

        public Queue<ATTInspTab> InspTabQueue = new Queue<ATTInspTab>();

        public AkkonAlgorithm AkkonAlgorithm { get; set; } = new AkkonAlgorithm();

        public List<ATTInspTab> InspTabList { get; set; } = new List<ATTInspTab>();
        #endregion

        #region 이벤트
        #endregion

        #region 델리게이트
        #endregion

        #region 생성자
        public ATTInspRunner()
        {
            //if(AppsConfig.Instance().AkkonAlgorithmType == AkkonAlgorithmType.Macron)
            //    MacronAkkonAlgorithmTool = new MacronAkkonAlgorithmTool();
            //else
            //    AkkonAlgorithm = new AkkonAlgorithm();
        }
        #endregion

        #region 메서드
        private void RunPreAlign(AppsInspResult inspResult)
        {
            MainAlgorithmTool algorithmTool = new MainAlgorithmTool();

            if (inspResult.PreAlignResult.PreAlignMark.FoundedMark.Left.Judgement == Judgement.OK && inspResult.PreAlignResult.PreAlignMark.FoundedMark.Right.Judgement == Judgement.OK)
            {
                PointF leftVisionCoordinates = inspResult.PreAlignResult.PreAlignMark.FoundedMark.Left.MaxMatchPos.FoundPos;
                PointF rightVisionCoordinates = inspResult.PreAlignResult.PreAlignMark.FoundedMark.Right.MaxMatchPos.FoundPos;

                List<PointF> realCoordinateList = new List<PointF>();
                PointF leftRealCoordinates = CalibrationData.Instance().ConvertVisionToReal(leftVisionCoordinates);
                PointF righttRealCoordinates = CalibrationData.Instance().ConvertVisionToReal(rightVisionCoordinates);

                realCoordinateList.Add(leftRealCoordinates);
                realCoordinateList.Add(righttRealCoordinates);

                var unit = TeachingData.Instance().GetUnit("Unit0");
                PointF calibrationStartPosition = CalibrationData.Instance().GetCalibrationStartPosition();

                inspResult.PreAlignResult = algorithmTool.ExecuteAlignment(unit, realCoordinateList, calibrationStartPosition);
            }
        }

        private VisionProPatternMatchingResult RunPreAlignMark(Unit unit, ICogImage cogImage, MarkDirection markDirection)
        {
            var preAlignParam = unit.PreAlignParamList.Where(x => x.Direction == markDirection).FirstOrDefault();

            AlgorithmTool algorithmTool = new AlgorithmTool();

            VisionProPatternMatchingResult result = algorithmTool.RunPatternMatch(cogImage, preAlignParam.InspParam);

            return result;
        }

        private void Run(ATTInspTab inspTab)
        {
            Stopwatch sw = new Stopwatch();
            sw.Restart();
            string unitName = UnitName.Unit0.ToString();
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            Tab tab = inspModel.GetUnit(unitName).GetTab(inspTab.TabScanBuffer.TabNo);

            MainAlgorithmTool algorithmTool = new MainAlgorithmTool();

            TabInspResult inspResult = new TabInspResult();
            inspResult.TabNo = inspTab.TabScanBuffer.TabNo;
            inspResult.Image = inspTab.MergeMatImage;
            inspResult.CogImage = inspTab.MergeCogImage;

            Coordinate fpcCoordinate = new Coordinate();
            Coordinate panelCoordinate = new Coordinate();

            #region Mark 검사
            algorithmTool.MainMarkInspect(inspTab.MergeCogImage, tab, ref inspResult);

            if (inspResult.IsMarkGood() == false)
            {
                // 검사 실패
                string message = string.Format("Mark Inspection NG !!! Tab_{0} / Fpc_{1}, Panel_{2}", tab.Index, inspResult.FpcMark.Judgement, inspResult.PanelMark.Judgement);
                Logger.Debug(LogType.Inspection, message);
                //return;
            }
            else
            {
                #region Add mark data
                // fpc
                SetCoordinateData(fpcCoordinate, inspResult);

                // panel
                SetCoordinateData(panelCoordinate, inspResult);
                #endregion
            }
            #endregion

            var lineCamera = LineCameraManager.Instance().GetLineCamera("LineCamera").Camera;
            double resolution_um = lineCamera.PixelResolution_um / lineCamera.LensScale;
            double judgementX = resolution_um * tab.AlignSpec.LeftSpecX_um;
            double judgementY = resolution_um * tab.AlignSpec.LeftSpecY_um;

            #region Left Align
            if (AppsConfig.Instance().EnableAlign)
            {
                inspResult.LeftAlignX = algorithmTool.RunMainLeftAlignX(inspTab.MergeCogImage, tab, fpcCoordinate, panelCoordinate, judgementX);
                if (inspResult.IsLeftAlignXGood() == false)
                {
                    var leftAlignX = inspResult.LeftAlignX;
                    string message = string.Format("Left AlignX Inspection NG !!! Tab_{0} / Fpc_{1}, Panel_{2}", tab.Index, leftAlignX.Fpc.Judgement, leftAlignX.Panel.Judgement);
                    Logger.Debug(LogType.Inspection, message);
                }

                inspResult.LeftAlignY = algorithmTool.RunMainLeftAlignY(inspTab.MergeCogImage, tab, fpcCoordinate, panelCoordinate, judgementY);
                if (inspResult.IsLeftAlignYGood() == false)
                {
                    var leftAlignY = inspResult.LeftAlignY;
                    string message = string.Format("Left AlignY Inspection NG !!! Tab_{0} / Fpc_{1}, Panel_{2}", tab.Index, leftAlignY.Fpc.Judgement, leftAlignY.Panel.Judgement);
                    Logger.Debug(LogType.Inspection, message);
                }
            }
            else
            {
                inspResult.LeftAlignX = new AlignResult();
                inspResult.LeftAlignY = new AlignResult();
            }
            #endregion

            #region Right Align
            if (AppsConfig.Instance().EnableAlign)
            {
                inspResult.RightAlignX = algorithmTool.RunMainRightAlignX(inspTab.MergeCogImage, tab, fpcCoordinate, panelCoordinate, judgementX);
                if (inspResult.IsRightAlignXGood() == false)
                {
                    var rightAlignX = inspResult.RightAlignX;
                    string message = string.Format("Right AlignX Inspection NG !!! Tab_{0} / Fpc_{1}, Panel_{2}", tab.Index, rightAlignX.Fpc.Judgement, rightAlignX.Panel.Judgement);
                    Logger.Debug(LogType.Inspection, message);
                }

                inspResult.RightAlignY = algorithmTool.RunMainRightAlignY(inspTab.MergeCogImage, tab, fpcCoordinate, panelCoordinate, judgementY);
                if (inspResult.IsRightAlignYGood() == false)
                {
                    var rightAlignY = inspResult.RightAlignY;
                    string message = string.Format("Right AlignY Inspection NG !!! Tab_{0} / Fpc_{1}, Panel_{2}", tab.Index, rightAlignY.Fpc.Judgement, rightAlignY.Panel.Judgement);
                    Logger.Debug(LogType.Inspection, message);
                }
            }
            else
            {
                inspResult.RightAlignX = new AlignResult();
                inspResult.RightAlignY = new AlignResult();
            }
            #endregion

            #region Center Align
            // EnableAlign false 일때 구조 생각
            inspResult.CenterX = Math.Abs(inspResult.LeftAlignX.ResultValue_pixel - inspResult.RightAlignX.ResultValue_pixel);
            #endregion

            if (AppsConfig.Instance().EnableAkkon)
            {
                var roiList = tab.AkkonParam.GetAkkonROIList();
                var leadResultList = AkkonAlgorithm.Run(inspTab.MergeMatImage, roiList, tab.AkkonParam.AkkonAlgoritmParam, resolution_um);

                inspResult.AkkonResult = CreateAkkonResult(unitName, tab.Index, leadResultList);
            }
            AppsInspResult.TabResultList.Add(inspResult);

            sw.Stop();
            string resultMessage = string.Format("Inspection Completed. {0}({1}ms)", inspTab.TabScanBuffer.TabNo, sw.ElapsedMilliseconds);
            Console.WriteLine(resultMessage);
        }

        private AkkonResult CreateAkkonResult(string unitName, int tabNo, List<AkkonLeadResult> leadResultList)
        {
            AkkonResult akkonResult = new AkkonResult();
            akkonResult.UnitName = unitName;
            akkonResult.TabNo = tabNo;
            akkonResult.LeadResultList = leadResultList;

            List<int> leftCountList = new List<int>();
            List<int> rightCountList = new List<int>();

            List<double> leftLengthList = new List<double>();
            List<double> rightLengthList = new List<double>();

            bool leftCountNG = false;
            bool leftLengthNG = false;
            bool rightCountNG = false;
            bool rightLengthNG = false;

            foreach (var leadResult in leadResultList)
            {
                if (leadResult.ContainPos == LeadContainPos.Left)
                {
                    leftCountNG |= leadResult.CountJudgement == Judgement.NG ? true : false;
                    leftCountList.Add(leadResult.DetectCount);

                    leftLengthNG |= leadResult.LengthJudgement == Judgement.NG ? true : false;
                    leftLengthList.Add(leadResult.LengthY_um);
                }
                else
                {
                    rightCountNG |= leadResult.CountJudgement == Judgement.NG ? true : false;
                    rightCountList.Add(leadResult.DetectCount);

                    rightLengthNG |= leadResult.LengthJudgement == Judgement.NG ? true : false;
                    rightLengthList.Add(leadResult.LengthY_um);
                }
            }

            akkonResult.AkkonCountJudgement = (leftCountNG || rightCountNG) == true ? AkkonJudgement.NG_Akkon : AkkonJudgement.OK;
            akkonResult.LeftCount_Avg = (int)leftCountList.Average();
            akkonResult.LeftCount_Min = (int)leftCountList.Min();
            akkonResult.LeftCount_Max = (int)leftCountList.Max();
            akkonResult.RightCount_Avg = (int)rightCountList.Average();
            akkonResult.RightCount_Min = (int)rightCountList.Min();
            akkonResult.RightCount_Max = (int)rightCountList.Max();

            akkonResult.LengthJudgement = (leftLengthNG || rightLengthNG) == true ? Judgement.NG : Judgement.OK;
            akkonResult.Length_Left_Avg_um = (float)leftLengthList.Average();
            akkonResult.Length_Left_Min_um = (float)leftLengthList.Min();
            akkonResult.Length_Left_Max_um = (float)leftLengthList.Max();
            akkonResult.Length_Right_Avg_um = (float)rightLengthList.Average();
            akkonResult.Length_Right_Min_um = (float)rightLengthList.Min();
            akkonResult.Length_Right_Max_um = (float)rightLengthList.Max();

            akkonResult.LeadResultList = leadResultList;

            return akkonResult;
        }

        public void InitalizeInspTab(List<TabScanBuffer> bufferList)
        {
            DisposeInspTabList();
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            foreach (var buffer in bufferList)
            {
                ATTInspTab inspTab = new ATTInspTab();
                inspTab.TabScanBuffer = buffer;
                inspTab.InspectEvent += AddInspectEventFuction;
                inspTab.StartInspTask();
                InspTabList.Add(inspTab);
            }
        }

        private void AddInspectEventFuction(ATTInspTab inspTab)
        {
            lock (_inspLock)
            {
                InspTabQueue.Enqueue(inspTab);
            }
        }

        public void DisposeInspTabList()
        {
            foreach (var inspTab in InspTabList)
            {
                inspTab.StopInspTask();
                inspTab.InspectEvent -= AddInspectEventFuction;
                inspTab.Dispose();
            }
            InspTabList.Clear();
        }

        private void ATTSeqRunner_GrabDoneEventHanlder(string cameraName, bool isGrabDone)
        {
            IsGrabDone = isGrabDone;
        }

        private void AkkonInspection()
        {
            while (true)
            {
                if (CancelAkkonInspTask.IsCancellationRequested)
                {
                    break;
                }

                if (GetInspTab() is ATTInspTab inspTab)
                {
                    Run(inspTab);
                }

                Thread.Sleep(50);
            }
        }

        private ATTInspTab GetInspTab()
        {
            lock (_inspLock)
            {
                if (InspTabQueue.Count() > 0)
                    return InspTabQueue.Dequeue();
                else
                    return null;
            }
        }

        private AkkonThreadParam GetAkkonThreadParam()
        {
            lock (_akkonLock)
            {
                if (AkkonInspQueue.Count > 0)
                    return AkkonInspQueue.Dequeue();
                else
                    return null;
            }
        }

        public void StartAkkonInspTask()
        {
            if (AkkonInspTask != null)
                return;

            CancelAkkonInspTask = new CancellationTokenSource();
            AkkonInspTask = new Task(AkkonInspection, CancelAkkonInspTask.Token);
            AkkonInspTask.Start();
        }

        public void StopAkkonInspTask()
        {
            if (AkkonInspTask == null)
                return;

            while (InspTabQueue.Count > 0)
            {
                var data = InspTabQueue.Dequeue();
                data.Dispose();
            }

            CancelAkkonInspTask.Cancel();
            AkkonInspTask.Wait();
            AkkonInspTask = null;
        }

        public void ClearResult()
        {
            if (AppsInspResult == null)
                AppsInspResult = new AppsInspResult();

            if (AppsInspResult != null)
                AppsInspResult.Dispose();

            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
        }

        public bool IsInspectionDone()
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            if (ConfigSet.Instance().Operation.VirtualMode)
            {
                RunVirtual();
                return true;
            }
            lock (AppsInspResult)
            {
                if (AppsInspResult.TabResultList.Count() == inspModel.TabCount)
                    return true;
            }
            return false;
        }

        public void SeqRun()
        {
            if (ModelManager.Instance().CurrentModel == null)
                return;
            //SeqStop();
            SystemManager.Instance().MachineStatus = MachineStatus.RUN;

            var lineCamera = LineCameraManager.Instance().GetAppsCamera("LineCamera");

            lineCamera.GrabDoneEventHanlder += ATTSeqRunner_GrabDoneEventHanlder;
            StartAkkonInspTask();

            if (SeqTask != null)
            {
                SeqStep = SeqStep.SEQ_START;
                return;
            }

            SeqTaskCancellationTokenSource = new CancellationTokenSource();
            SeqTask = new Task(SeqTaskAction, SeqTaskCancellationTokenSource.Token);
            SeqTask.Start();

            WriteLog("Start Sequence.", true);
        }

        public void SeqStop()
        {
            SystemManager.Instance().MachineStatus = MachineStatus.STOP;

            var lineCamera = LineCameraManager.Instance().GetAppsCamera("LineCamera");
            lineCamera.StopGrab();
            lineCamera.GrabDoneEventHanlder -= ATTSeqRunner_GrabDoneEventHanlder;
            LineCameraManager.Instance().GetLineCamera("LineCamera").StopGrab();

            var areaCamera = AreaCameraManager.Instance().GetAppsCamera("PreAlign");
            areaCamera.StopGrab();
            AreaCameraManager.Instance().GetAreaCamera("PreAlign").StopGrab();

            StopAkkonInspTask();

            // 조명 off
            LAFManager.Instance().AutoFocusOnOff("Laf", false);
            WriteLog("AutoFocus Off.");

            LineCameraManager.Instance().Stop("LineCamera");
            WriteLog("Stop Grab.");

            if (SeqTask == null)
                return;

            //foreach (var item in AppsInspResult.TabResultList)
            //{
            //    item.Dispose();
            //}
            //AppsInspResult.TabResultList.Clear();
            SeqTaskCancellationTokenSource.Cancel();
            SeqTask.Wait();
            SeqTask = null;

            WriteLog("Stop Sequence.");
        }

        private void SeqTaskAction()
        {
            var cancellationToken = SeqTaskCancellationTokenSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            SeqStep = SeqStep.SEQ_START;

            while (true)
            {
                // 작업 취소
                if (cancellationToken.IsCancellationRequested)
                {
                    SeqStep = SeqStep.SEQ_IDLE;
                    //조명 Off
                    LineCameraManager.Instance().Stop("LineCamera");
                    DisposeInspTabList();
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
            if (tab == null)
                return;

            var areaCamera = AreaCameraManager.Instance().GetAreaCamera("PreAlign");
            if (areaCamera == null)
                return;

            var lineCamera = LineCameraManager.Instance().GetLineCamera("LineCamera");
            if (lineCamera == null)
                return;

            var laf = LAFManager.Instance().GetLAFCtrl("Laf");
            if (laf == null)
                return;

            string systemLogMessage = string.Empty;
            string errorMessage = string.Empty;

            switch (SeqStep)
            {
                case SeqStep.SEQ_IDLE:
                    // Check model changed
                    var receivedModelName = PlcControlManager.Instance().GetAddressMap(PlcCommonMap.PLC_PPID_ModelName).Value;
                    if (inspModel.Name != receivedModelName)
                    {
                        // Change model
                        break;
                    }

                    SeqStep = SeqStep.SEQ_READY;
                    break;

                case SeqStep.SEQ_READY:
                    // 조명
                    WriteLog("Light off.");

                    //PlcControlManager.Instance().ClearAlignData();
                    WriteLog("Clear PLC data.");

                    // LAF
                    LAFManager.Instance().AutoFocusOnOff("Laf", false);
                    laf.SetMotionAbsoluteMove(0);
                    WriteLog("Laf off.");

                    SeqStep = SeqStep.SEQ_START;
                    break;

                case SeqStep.SEQ_START:
                    // Wait for prealign start signal
                    string preAlignStart = PlcControlManager.Instance().GetAddressMap(PlcCommonMap.PLC_Command_Common).Value;
                    if (Convert.ToInt32(preAlignStart) == (int)PlcCommand.StartPreAlign)
                        break;

                    WriteLog("Receive prealign start signal from PLC.");

                    SeqStep = SeqStep.SEQ_PREALIGN_R;
                    break;

                case SeqStep.SEQ_PREALIGN_R:
                    // Move to prealign right position
                    if (MoveTo(TeachingPosType.Stage1_PreAlign_Right, out errorMessage) == false)
                    {
                        // Alarm
                        break;
                    }

                    // Set camera property
                    areaCamera.Camera.SetExposureTime(0);
                    areaCamera.Camera.SetAnalogGain(0);
                    WriteLog("Set camera property.");

                    // Light on
                    //preAlignParam.LightParams.Where(x => x.Map == )
                    WriteLog("Prealign light on.");

                    // Grab
                    var preAlignLeftImage = GetAreaCameraImage(areaCamera.Camera);
                    AppsInspResult.PreAlignResult.PreAlignMark.FoundedMark.Right = RunPreAlignMark(unit, preAlignLeftImage, MarkDirection.Right);
                    WriteLog("Complete prealign right mark search.");

                    // Set prealign motion position
                    SetMarkMotionPosition(unit, MarkDirection.Right);
                    WriteLog("Set axis information for prealign right mark position.");

                    SeqStep = SeqStep.SEQ_PREALIGN_L;
                    break;

                case SeqStep.SEQ_PREALIGN_L:
                    // Move to prealign left position
                    if (MoveTo(TeachingPosType.Stage1_PreAlign_Left, out errorMessage) == false)
                    {
                        // Alarm
                        break;
                    }

                    // Grab start
                    var preAlignRightImage = GetAreaCameraImage(areaCamera.Camera);
                    AppsInspResult.PreAlignResult.PreAlignMark.FoundedMark.Left = RunPreAlignMark(unit, preAlignRightImage, MarkDirection.Right);
                    WriteLog("Complete prealign left mark search.");

                    // Set prealign motion position
                    SetMarkMotionPosition(unit, MarkDirection.Left);
                    WriteLog("Set axis information for prealign left mark position.");

                    SeqStep = SeqStep.SEQ_SEND_PREALIGN_DATA;
                    break;

                case SeqStep.SEQ_SEND_PREALIGN_DATA:
                    WriteLog("Prealign light off.");

                    // Execute prealign
                    RunPreAlign(AppsInspResult);
                    WriteLog("Complete prealign.");

                    // Set prealign result
                    var offsetX = AppsInspResult.PreAlignResult.OffsetX;
                    var offsetY = AppsInspResult.PreAlignResult.OffsetY;
                    var offsetT = AppsInspResult.PreAlignResult.OffsetT;

                    // Check Tolerance
                    if (Math.Abs(offsetX) <= AppsConfig.Instance().PreAlignToleranceX
                        || Math.Abs(offsetX) <= AppsConfig.Instance().PreAlignToleranceY
                        || Math.Abs(offsetX) <= AppsConfig.Instance().PreAlignToleranceTheta)
                    {
                        WriteLog("Prealign results are spec-in.");
                    }
                    else
                        WriteLog("Prealign results are spec-out.");

                    // Send prealign offset results
                    PlcControlManager.Instance().WriteAlignData(offsetX, offsetY, offsetT);
                    WriteLog("Write prealign results.");

                    // Send prealign score results
                    var leftScore = AppsInspResult.PreAlignResult.PreAlignMark.FoundedMark.Left.MaxMatchPos.Score;
                    var rightScore = AppsInspResult.PreAlignResult.PreAlignMark.FoundedMark.Right.MaxMatchPos.Score;
                    PlcControlManager.Instance().WritePreAlignResult(leftScore, rightScore);
                    WriteLog("Send prealign results.");

                    SeqStep = SeqStep.SEQ_WAITING;
                    break;

                case SeqStep.SEQ_WAITING:
                    // Wait for scan start signal command
                    PlcControlManager.Instance().GetAddressMap(PlcCommonMap.PLC_Command);

                    // Move to scan start position
                    if (MoveTo(TeachingPosType.Stage1_Scan_Start, out errorMessage) == false)
                    {
                        // Alarm
                        break;
                    }

                    //if (IsPanelIn == false)
                    //    break;
                    SeqStep = SeqStep.SEQ_SCAN_READY;
                    break;

                case SeqStep.SEQ_SCAN_READY:
                    LineCameraManager.Instance().GetLineCamera("LineCamera").IsLive = false;

                    // Clear results
                    ClearResult();
                    WriteLog("Clear result.");

                    // Init camera buffer
                    InitializeBuffer();
                    WriteLog("Initialize buffer.");

                    // Write panel information
                    AppsInspResult.StartInspTime = DateTime.Now;
                    AppsInspResult.Cell_ID = DateTime.Now.ToString("yyyyMMddHHmmss");

                    // LAF on
                    LAFManager.Instance().AutoFocusOnOff("Akkon", true);
                    WriteLog("Laf On.");

                    SeqStep = SeqStep.SEQ_SCAN_START;
                    break;

                case SeqStep.SEQ_SCAN_START:
                    IsGrabDone = false;

                    // 조명 코드 작성 요망

                    // Grab
                    lineCamera.SetOperationMode(TDIOperationMode.TDI);
                    lineCamera.StartGrab();
                    WriteLog("Start grab.");

                    // Move to scan end position
                    if (MoveTo(TeachingPosType.Stage1_Scan_End, out errorMessage) == false)
                    {
                        // Alarm
                        // 조명 Off
                        LineCameraManager.Instance().Stop("LineCamera");
                        WriteLog("Stop grab.");
                        break;
                    }

                    SeqStep = SeqStep.SEQ_WAITING_SCAN_COMPLETED;
                    break;

                case SeqStep.SEQ_WAITING_SCAN_COMPLETED:
                    if (ConfigSet.Instance().Operation.VirtualMode == false)
                    {
                        if (IsGrabDone == false)
                            break;
                    }

                    WriteLog("Complete linescanner grab.");

                    //AppsLAFManager.Instance().AutoFocusOnOff(LAFName.Akkon.ToString(), false);
                    //Logger.Write(LogType.Seq, "AutoFocus Off.");

                    // Grab stop
                    LineCameraManager.Instance().Stop("LineCamera");
                    WriteLog("Stop grab.");

                    LastInspSW.Restart();

                    SeqStep = SeqStep.SEQ_WAITING_INSPECTION_DONE;
                    break;

                case SeqStep.SEQ_WAITING_INSPECTION_DONE:
                    // Wait for inspection
                    if (IsInspectionDone() == false)
                        break;

                    LastInspSW.Stop();
                    AppsInspResult.EndInspTime = DateTime.Now;
                    AppsInspResult.LastInspTime = LastInspSW.ElapsedMilliseconds.ToString();
                    Console.WriteLine("Total tact time : " + LastInspSW.ElapsedMilliseconds.ToString());

                    SeqStep = SeqStep.SEQ_UI_RESULT_UPDATE;
                    break;

                case SeqStep.SEQ_UI_RESULT_UPDATE:
                    // Update result data
                    GetAkkonResultImage();
                    UpdateDailyInfo(AppsInspResult);
                    WriteLog("Update inspectinon result.");
                    
                    // Update main viewer
                    SystemManager.Instance().UpdateMainResult(AppsInspResult);
                    Console.WriteLine("Scan End to Insp Complete : " + LastInspSW.ElapsedMilliseconds.ToString());

                    SeqStep = SeqStep.SEQ_SAVE_RESULT_DATA;
                    break;

                case SeqStep.SEQ_SAVE_RESULT_DATA:
                    // Save result datas
                    DailyInfoService.Save();

                    // Send result datas
                    SaveInspectionResult(AppsInspResult);
                    WriteLog("Save inspection result.");

                    SeqStep = SeqStep.SEQ_SAVE_IMAGE;
                    break;

                case SeqStep.SEQ_SAVE_IMAGE:
                    // Save reuslt images
                    SaveImage(AppsInspResult);
                    WriteLog("Save inspection images.");

                    SeqStep = SeqStep.SEQ_DELETE_DATA;
                    break;

                case SeqStep.SEQ_DELETE_DATA:
                    // Delete result datas
                    WriteLog("Delete the old data");

                    SeqStep = SeqStep.SEQ_CHECK_STANDBY;
                    break;

                case SeqStep.SEQ_CHECK_STANDBY:

                    //if (!AppsMotionManager.Instance().IsMotionInPosition(UnitName.Unit0, AxisHandlerName.Handler0, AxisName.X, TeachingPosType.Standby))
                    //    break;

                    SeqStep = SeqStep.SEQ_IDLE;
                    break;

                default:
                    break;
            }
        }

        private void WriteLog(string logMessage, bool isSystemLog = false)
        {
            if (isSystemLog)
                SystemManager.Instance().AddSystemLogMessage(logMessage);

            Logger.Write(LogType.Seq, logMessage);
        }

        private void GetAkkonResultImage()
        {
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            var unit = inspModel.GetUnit(UnitName.Unit0);

            for (int i = 0; i < AppsInspResult.TabResultList.Count(); i++)
            {
                var tabResult = AppsInspResult.TabResultList[i];
                Tab tab = unit.GetTab(tabResult.TabNo);

                Mat resultMat = GetResultImage(tabResult.Image, tabResult.AkkonResult.LeadResultList, tab.AkkonParam.AkkonAlgoritmParam);
                ICogImage cogImage = ConvertCogColorImage(resultMat);
                tabResult.AkkonResultImage = cogImage;
                resultMat.Dispose();
            }

            sw.Stop();
            Console.WriteLine("Get Akkon Result Image : " + sw.ElapsedMilliseconds.ToString() + "ms");
        }

        public ICogImage ConvertCogColorImage(Mat mat)
        {
            Mat matR = MatHelper.ColorChannelSprate(mat, MatHelper.ColorChannel.R);
            Mat matG = MatHelper.ColorChannelSprate(mat, MatHelper.ColorChannel.G);
            Mat matB = MatHelper.ColorChannelSprate(mat, MatHelper.ColorChannel.B);

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

        public Mat GetResultImage(Mat mat, List<AkkonLeadResult> leadResultList, AkkonAlgoritmParam AkkonParameters)
        {
            if (mat == null)
                return null;

            Mat resizeMat = new Mat();
            Size newSize = new Size((int)(mat.Width * AkkonParameters.ImageFilterParam.ResizeRatio), (int)(mat.Height * AkkonParameters.ImageFilterParam.ResizeRatio));
            CvInvoke.Resize(mat, resizeMat, newSize);
            Mat colorMat = new Mat();
            CvInvoke.CvtColor(resizeMat, colorMat, ColorConversion.Gray2Bgr);
            resizeMat.Dispose();

            DrawParam autoDrawParam = new DrawParam();
            autoDrawParam.ContainLeadCount = true;

            foreach (var result in leadResultList)
            {
                var lead = result.Lead;
                var startPoint = new Point((int)result.OffsetToWorldX, (int)result.OffsetToWorldY);

                Point leftTop = new Point((int)lead.LeftTopX + startPoint.X, (int)lead.LeftTopY + startPoint.Y);
                Point leftBottom = new Point((int)lead.LeftBottomX + startPoint.X, (int)lead.LeftBottomY + startPoint.Y);
                Point rightTop = new Point((int)lead.RightTopX + startPoint.X, (int)lead.RightTopY + startPoint.Y);
                Point rightBottom = new Point((int)lead.RightBottomX + startPoint.X, (int)lead.RightBottomY + startPoint.Y);

                if (autoDrawParam.ContainLeadROI)
                {
                    CvInvoke.Line(colorMat, leftTop, leftBottom, new MCvScalar(50, 230, 50, 255), 1);
                    CvInvoke.Line(colorMat, leftTop, rightTop, new MCvScalar(50, 230, 50, 255), 1);
                    CvInvoke.Line(colorMat, rightTop, rightBottom, new MCvScalar(50, 230, 50, 255), 1);
                    CvInvoke.Line(colorMat, rightBottom, leftBottom, new MCvScalar(50, 230, 50, 255), 1);
                }

                int blobCount = 0;
                foreach (var blob in result.BlobList)
                {
                    Rectangle rectRect = new Rectangle();
                    rectRect.X = (int)(blob.BoundingRect.X + result.OffsetToWorldX + result.OffsetX);
                    rectRect.Y = (int)(blob.BoundingRect.Y + result.OffsetToWorldY + result.OffsetY);
                    rectRect.Width = blob.BoundingRect.Width;
                    rectRect.Height = blob.BoundingRect.Height;

                    Point center = new Point(rectRect.X + (rectRect.Width / 2), rectRect.Y + (rectRect.Height / 2));
                    int radius = rectRect.Width > rectRect.Height ? rectRect.Width : rectRect.Height;

                    int size = blob.BoundingRect.Width * blob.BoundingRect.Height;
                    double calcMinArea = AkkonParameters.ResultFilterParam.MinArea_um * AkkonParameters.ResultFilterParam.Resolution_um;
                    double calcMaxArea = AkkonParameters.ResultFilterParam.MaxArea_um * AkkonParameters.ResultFilterParam.Resolution_um;

                    if (calcMinArea <= size && size <= calcMaxArea)
                    {
                        blobCount++;
                        CvInvoke.Circle(colorMat, center, radius / 2, new MCvScalar(255), 1);
                    }
                    else
                    {
                        //if (AkkonParameters.DrawOption.ContainNG)
                        //    CvInvoke.Circle(colorMat, center, radius / 2, new MCvScalar(0), 1);
                    }

                }

                if (autoDrawParam.ContainLeadCount)
                {
                    string leadIndexString = result.Index.ToString();
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

        private void InitializeBuffer()
        {
            var lineCamera = LineCameraManager.Instance().GetLineCamera("LineCamera");
            lineCamera.InitGrabSettings();
            InitalizeInspTab(lineCamera.TabScanBufferList);
        }

        public void RunVirtual()
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            Tab tab = inspModel.GetUnit(UnitName.Unit0).GetTab(0);

            //Mat tabMatImage = new Mat(@"D:\Tab1.bmp", Emgu.CV.CvEnum.ImreadModes.Grayscale);

            // ICogImage tabCogImage = ConvertCogImage(tabMatImage);
            // MainAlgorithmTool tool = new MainAlgorithmTool();

            //var result = tool.MainRunInspect(tab, tabMatImage, 30.0f, 80.0f);

            // AppsInspResult.TabResultList.Add(result);
        }

        private void SetCoordinateData(Coordinate coordinate, TabInspResult tabInspResult)
        {
            PointF teachedLeftPoint = tabInspResult.FpcMark.FoundedMark.Left.MaxMatchPos.ReferencePos;
            PointF teachedRightPoint = tabInspResult.FpcMark.FoundedMark.Right.MaxMatchPos.ReferencePos;
            PointF searchedLeftPoint = tabInspResult.FpcMark.FoundedMark.Left.MaxMatchPos.FoundPos;
            PointF searchedRightPoint = tabInspResult.FpcMark.FoundedMark.Right.MaxMatchPos.FoundPos;
            coordinate.SetCoordinateParam(teachedLeftPoint, teachedRightPoint, searchedLeftPoint, searchedRightPoint);
        }

        private void UpdateModelTask()
        {
            if (_changedModelTask != null)
                return;

            _changedModelTaskCancellationTokenSource = new CancellationTokenSource();
            _changedModelTask = new Task(CheckChangedModel, _changedModelTaskCancellationTokenSource.Token);
            _changedModelTask.Start();
        }

        private void CheckChangedModel()
        {
            var cancellationToken = _changedModelTaskCancellationTokenSource.Token;
            cancellationToken.ThrowIfCancellationRequested();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (IsChangedModel())
                {
                    WriteLog("Occur request model change.", true);
                    SetModelData();
                }

                Thread.Sleep(50);
            }
        }

        private bool IsChangedModel()
        {
            var currentModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            var requestModelName = PlcControlManager.Instance().GetAddressMap(PlcCommonMap.PLC_PPID_ModelName).Value;

            return currentModel.Name != requestModelName;
        }

        public event ApplyModelDelegate ApplyModelEventHandler;

        private void SetModelData()
        {
            //AppsInspModel beforeModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            //var plcControlManager = PlcControlManager.Instance();
            //if (plcControlManager == null)
            //    return;

            //var requestModelName = plcControlManager.GetValue(PlcCommonMap.PLC_PPID_ModelName);
            //if (requestModelName == string.Empty)
            //    return;

            //ApplyModelEventHandler?.Invoke(requestModelName);
            //Thread.Sleep(500);
            
            //AppsInspModel requestModel = ModelManager.Instance().CurrentModel as AppsInspModel;

            //string message = string.Format("Model change : {0} -> {1}", beforeModel.Name, requestModel.Name);
            //WriteLog(message, true);


            //MaterialInfo materialInfo = requestModel.MaterialInfo;

            //// Model information
            //var modelName = requestModelName;

            //var tabCount = plcControlManager.GetValue(PlcCommonMap.PLC_TabCount);
            //requestModel.TabCount = Convert.ToInt32(tabCount);

            //var panelSizeX = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_PanelX_Size);
            //materialInfo.PanelXSize_mm = Convert.ToDouble(panelSizeX);

            //var markToMarkDistance = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_MarkToMarkDistance);
            //materialInfo.MarkToMark_mm = Convert.ToDouble(markToMarkDistance);

            //var edgeDistance = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_PanelLeftEdgeToTab1LeftEdgeDistance);
            //materialInfo.PanelEdgeToFirst_mm = Convert.ToDouble(edgeDistance);

            //var axisSpeed = plcControlManager.GetValue(PlcCommonMap.PLC_Axis_X_Speed);
            //var teachingInfo = requestModel.GetUnit(UnitName.Unit0).GetTeachingInfo(TeachingPosType.Stage1_Scan_Start);
            //var movingParamX = teachingInfo.GetMovingParam(AxisName.X.ToString());
            //movingParamX.Velocity = Convert.ToDouble(axisSpeed);
            //teachingInfo.SetMovingParams(AxisName.X, movingParamX);

            //// Recipe data
            //var tabWidth_0 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab0_Width);
            //materialInfo.Tab0_Width_mm = Convert.ToDouble(tabWidth_0);

            //var tabWidth_1 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab1_Width);
            //materialInfo.Tab1_Width_mm = Convert.ToDouble(tabWidth_1);

            //var tabWidth_2 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab2_Width);
            //materialInfo.Tab2_Width_mm = Convert.ToDouble(tabWidth_2);

            //var tabWidth_3 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab3_Width);
            //materialInfo.Tab3_Width_mm = Convert.ToDouble(tabWidth_3);

            //var tabWidth_4 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab4_Width);
            //materialInfo.Tab4_Width_mm = Convert.ToDouble(tabWidth_4);

            //var tabWidth_5 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab5_Width);
            //materialInfo.Tab5_Width_mm = Convert.ToDouble(tabWidth_5);

            //var tabWidth_6 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab6_Width);
            //materialInfo.Tab6_Width_mm = Convert.ToDouble(tabWidth_6);

            //var tabWidth_7 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab7_Width);
            //materialInfo.Tab7_Width_mm = Convert.ToDouble(tabWidth_7);

            //var tabWidth_8 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab8_Width);
            //materialInfo.Tab8_Width_mm = Convert.ToDouble(tabWidth_8);

            //var tabWidth_9 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab9_Width);
            //materialInfo.Tab9_Width_mm = Convert.ToDouble(tabWidth_9);

            //var leftOffset_0 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab0_Offset_Left);
            //var leftOffset_1 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab1_Offset_Left);
            //var leftOffset_2 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab2_Offset_Left);
            //var leftOffset_3 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab3_Offset_Left);
            //var leftOffset_4 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab4_Offset_Left);
            //var leftOffset_5 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab5_Offset_Left);
            //var leftOffset_6 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab6_Offset_Left);
            //var leftOffset_7 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab7_Offset_Left);
            //var leftOffset_8 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab8_Offset_Left);
            //var leftOffset_9 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab9_Offset_Left);

            //var rightOffset_0 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab0_Offset_Right);
            //var rightOffset_1 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab1_Offset_Right);
            //var rightOffset_2 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab2_Offset_Right);
            //var rightOffset_3 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab3_Offset_Right);
            //var rightOffset_4 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab4_Offset_Right);
            //var rightOffset_5 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab5_Offset_Right);
            //var rightOffset_6 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab6_Offset_Right);
            //var rightOffset_7 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab7_Offset_Right);
            //var rightOffset_8 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab8_Offset_Right);
            //var rightOffset_9 = plcControlManager.GetValue(PlcCommonMap.PLC_Tab9_Offset_Right);

            //var EdgeToEdgeDistance_0 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance0);
            //materialInfo.Tab0ToTab1_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_0);

            //var EdgeToEdgeDistance_1 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance1);
            //materialInfo.Tab1ToTab2_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_1);

            //var EdgeToEdgeDistance_2 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance2);
            //materialInfo.Tab2ToTab3_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_2);

            //var EdgeToEdgeDistance_3 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance3);
            //materialInfo.Tab3ToTab4_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_3);

            //var EdgeToEdgeDistance_4 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance4);
            //materialInfo.Tab4ToTab5_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_4);

            //var EdgeToEdgeDistance_5 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance5);
            //materialInfo.Tab5ToTab6_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_5);

            //var EdgeToEdgeDistance_6 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance6);
            //materialInfo.Tab6ToTab7_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_6);

            //var EdgeToEdgeDistance_7 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance7);
            //materialInfo.Tab7ToTab8_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_7);

            //var EdgeToEdgeDistance_8 = plcControlManager.ConvertDoubleWordStringFormat_mm(PlcCommonMap.PLC_TabtoTab_Distance8);
            //materialInfo.Tab8ToTab9_Distance_mm = Convert.ToDouble(EdgeToEdgeDistance_8);
        }
        #endregion
    }

    public partial class ATTInspRunner
    {
        #region 메서드
        private void SaveImage(AppsInspResult inspResult)
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            DateTime currentTime = inspResult.StartInspTime;

            string month = currentTime.ToString("MM");
            string day = currentTime.ToString("dd");
            string folderPath = inspResult.Cell_ID;

            string path = Path.Combine(ConfigSet.Instance().Path.Result, inspModel.Name, month, day, folderPath);

            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            SaveResultImage(path, inspResult.TabResultList);
        }

        private void SaveResultImage(string resultPath, List<TabInspResult> insTabResultList)
        {
            if (ConfigSet.Instance().Operation.VirtualMode)
                return;

            string path = Path.Combine(resultPath, "Orgin");
            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            string okExtension = ".bmp";

            if(ConfigSet.Instance().Operation.ExtensionOKImage == ImageExtension.Bmp)
                okExtension = ".bmp";
            else if (ConfigSet.Instance().Operation.ExtensionOKImage == ImageExtension.Jpg)
                okExtension = ".jpg";
            else if (ConfigSet.Instance().Operation.ExtensionOKImage == ImageExtension.Png)
                okExtension = ".png";

            string ngExtension = ".bmp";

            if (ConfigSet.Instance().Operation.ExtensionNGImage == ImageExtension.Bmp)
                ngExtension = ".bmp";
            else if (ConfigSet.Instance().Operation.ExtensionNGImage == ImageExtension.Jpg)
                ngExtension = ".jpg";
            else if (ConfigSet.Instance().Operation.ExtensionNGImage == ImageExtension.Png)
                ngExtension = ".png";


            foreach (var result in insTabResultList)
            {
                if (result.Judgement == Judgement.OK)
                {
                    if(ConfigSet.Instance().Operation.SaveImageOK)
                    {
                        string imageName = "Tab_" + result.TabNo.ToString() +"_OK_" + okExtension;
                        string imagePath = Path.Combine(path, imageName);
                        result.Image.Save(imagePath);
                    }
                }
                else
                {
                    if (ConfigSet.Instance().Operation.SaveImageNG)
                    {
                        string imageName = "Tab_" + result.TabNo.ToString() + "_NG_" + ngExtension;
                        string imagePath = Path.Combine(path, imageName);
                        result.Image.Save(imagePath);
                    }
                }
            }
        }

        private void UpdateDailyInfo(AppsInspResult inspResult)
        {
            var dailyData = new DailyData();
            UpdateAlignDailyInfo(inspResult, ref dailyData);
            UpdateAkkonDailyInfo(inspResult, ref dailyData);

            AddDailyInfo(dailyData);
        }

        private void UpdateAlignDailyInfo(AppsInspResult inspResult, ref DailyData dailyData)
        {
            foreach (var item in inspResult.TabResultList)
            {
                AlignDailyInfo alignInfo = new AlignDailyInfo();

                alignInfo.InspectionTime = inspResult.EndInspTime.ToString("HH:mm:ss");
                alignInfo.PanelID = inspResult.Cell_ID;
                alignInfo.TabNo = item.TabNo;
                alignInfo.Judgement = item.Judgement;
                alignInfo.LX = item.LeftAlignX.ResultValue_pixel;
                alignInfo.LY = item.LeftAlignY.ResultValue_pixel;
                alignInfo.RX = item.RightAlignX.ResultValue_pixel;
                alignInfo.RY = item.RightAlignY.ResultValue_pixel;
                alignInfo.CX = item.CenterX;

                dailyData.AddAlignInfo(alignInfo);
            }
        }

        private void UpdateAkkonDailyInfo(AppsInspResult inspResult, ref DailyData dailyData)
        {
            foreach (var item in inspResult.TabResultList)
            {
                AkkonDailyInfo akkonInfo = new AkkonDailyInfo();

                akkonInfo.InspectionTime = inspResult.EndInspTime.ToString("HH:mm:ss");
                akkonInfo.PanelID = inspResult.Cell_ID;
                akkonInfo.TabNo = item.TabNo;
                akkonInfo.Judgement = item.Judgement;
                //akkonInfo.AvgBlobCount = item.MacronAkkonResult.AvgBlobCount;
                //akkonInfo.AvgLength = item.MacronAkkonResult.AvgLength;
                //akkonInfo.AvgStrength = item.MacronAkkonResult.AvgStrength;
                //akkonInfo.AvgSTD = item.MacronAkkonResult.AvgStd;

                akkonInfo.AvgBlobCount = 10;
                akkonInfo.AvgLength = 10;
                akkonInfo.AvgStrength = 10;
                akkonInfo.AvgSTD = 10;

                dailyData.AddAkkonInfo(akkonInfo);
            }
        }

        private void AddDailyInfo(DailyData dailyData)
        {
            var dailyInfo = DailyInfoService.GetDailyInfo();

            if (dailyInfo == null)
                return;

            dailyInfo.AddDailyDataList(dailyData);
        }

        private void SaveInspectionResult(AppsInspResult inspResult)
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            DateTime currentTime = inspResult.StartInspTime;

            string month = currentTime.ToString("MM");
            string day = currentTime.ToString("dd");
            string folderPath = inspResult.Cell_ID;

            string path = Path.Combine(ConfigSet.Instance().Path.Result, inspModel.Name, month, day);

            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            SaveAlignResult(path, inspResult);
            SaveAkkonResult(path, inspResult);
            SaveUPHResult(path, inspResult);
        }

        private void SaveAlignResult(string resultPath, AppsInspResult inspResult)
        {
            string filename = string.Format("Align.csv");
            string csvFile = Path.Combine(resultPath, filename);
            if (File.Exists(csvFile) == false)
            {
                List<string> header = new List<string>
                {
                    "Inspection Time",
                    "Panel ID",
                    "Tab",
                    "Judge",
                    "Lx",
                    "Ly",
                    "Cx",
                    "Rx",
                    "Ry"
                };

                CSVHelper.WriteHeader(csvFile, header);
            }

            List<List<string>> dataList = new List<List<string>>();
            for (int tabNo = 0; tabNo < inspResult.TabResultList.Count; tabNo++)
            {
                List<string> tabData = new List<string>
                {
                    inspResult.EndInspTime.ToString("HH:mm:ss"),                                    // Insp Time
                    inspResult.Cell_ID,                                                             // Panel ID
                    tabNo.ToString(),                                                               // Tab
                    inspResult.TabResultList[tabNo].AlignJudgment.ToString(),                       // Judge
                    inspResult.TabResultList[tabNo].LeftAlignX.ResultValue_pixel.ToString("F3"),          // Left Align X
                    inspResult.TabResultList[tabNo].LeftAlignY.ResultValue_pixel.ToString("F3"),          // Left Align Y
                    inspResult.TabResultList[tabNo].CenterX.ToString("F3"),                         // Center Align X
                    inspResult.TabResultList[tabNo].RightAlignX.ResultValue_pixel.ToString("F3"),         // Right Align X
                    inspResult.TabResultList[tabNo].RightAlignY.ResultValue_pixel.ToString("F3"),         // Right Align Y
                };

                dataList.Add(tabData);
            }

            CSVHelper.WriteData(csvFile, dataList);
        }

        //private void SaveAlignResult(string resultPath, AppsInspResult inspResult)
        //{
        //    string filename = string.Format("Align.csv");
        //    string csvFile = Path.Combine(resultPath, filename);
        //    if (File.Exists(csvFile) == false)
        //    {
        //        List<string> header = new List<string>
        //        {
        //            "Inspection Time",
        //            "Panel ID",
        //        };

        //        for (int tabNo = 0; tabNo < inspResult.TabResultList.Count; tabNo++)
        //        {
        //            header.Add("Tab");
        //            header.Add("Judge");
        //            header.Add("Lx");
        //            header.Add("Ly");
        //            header.Add("Cx");
        //            header.Add("Rx");
        //            header.Add("Ry");
        //        }

        //        CSVHelper.WriteHeader(csvFile, header);
        //    }

        //    List<string> dataList = new List<string>
        //    {
        //        inspResult.EndInspTime.ToString("HH:mm:ss"),
        //        inspResult.Cell_ID.ToString()
        //    };

        //    foreach (var tabResult in inspResult.TabResultList)
        //    {
        //        int tabNo = tabResult.TabNo;
        //        var judge = tabResult.AlignJudgment;
        //        float lx = tabResult.LeftAlignX.ResultValue;
        //        float ly = tabResult.LeftAlignY.ResultValue;
        //        float rx = tabResult.RightAlignX.ResultValue;
        //        float ry = tabResult.RightAlignY.ResultValue;
        //        float cx = (lx + rx) / 2.0f;

        //        dataList.Add(tabNo.ToString());
        //        dataList.Add(judge.ToString());
        //        dataList.Add(lx.ToString("F3"));
        //        dataList.Add(ly.ToString("F3"));
        //        dataList.Add(cx.ToString("F3"));
        //        dataList.Add(rx.ToString("F3"));
        //        dataList.Add(ry.ToString("F3"));
        //    }

        //    CSVHelper.WriteData(csvFile, dataList);
        //}

        private void SaveAkkonResult(string resultPath, AppsInspResult inspResult)
        {
            string filename = string.Format("Akkon.csv");
            string csvFile = Path.Combine(resultPath, filename);
            if (File.Exists(csvFile) == false)
            {
                List<string> header = new List<string>
                {
                    "Inspection Time",
                    "Panel ID",
                    "Tab",
                    "Judge",
                    "Count",
                    "Length",
                    "Strength",
                    "STD"
                };

                CSVHelper.WriteHeader(csvFile, header);
            }

            List<List<string>> dataList = new List<List<string>>();
            for (int tabNo = 0; tabNo < inspResult.TabResultList.Count; tabNo++)
            {
                List<string> tabData = new List<string>
                {
                    inspResult.EndInspTime.ToString("HH:mm:ss"),                                    // Insp Time
                    inspResult.Cell_ID,                                                             // Panel ID
                    tabNo.ToString(),                                                               // Tab
                    inspResult.TabResultList[tabNo].AlignJudgment.ToString(),                       // Judge
                    inspResult.TabResultList[tabNo].LeftAlignX.ResultValue_pixel.ToString("F3"),          // Left Align X
                    inspResult.TabResultList[tabNo].LeftAlignY.ResultValue_pixel.ToString("F3"),          // Left Align Y
                    inspResult.TabResultList[tabNo].CenterX.ToString("F3"),                         // Center Align X
                    inspResult.TabResultList[tabNo].RightAlignX.ResultValue_pixel.ToString("F3"),         // Right Align X
                    inspResult.TabResultList[tabNo].RightAlignY.ResultValue_pixel.ToString("F3"),         // Right Align Y
                };

                dataList.Add(tabData);
            }

            for (int tabNo = 0; tabNo < inspResult.TabResultList.Count; tabNo++)
            {
                List<string> tabData = new List<string>
                {
                    inspResult.EndInspTime.ToString("HH:mm:ss"),
                    inspResult.Cell_ID,
                    tabNo.ToString(),
                    
                    //inspResult.TabResultList[tabNo].MacronAkkonResult.Judgement
                    //inspResult.TabResultList[tabNo].MacronAkkonResult.AvgBlobCount.ToString(),
                    //inspResult.TabResultList[tabNo].MacronAkkonResult.AvgLength.ToString("F3"),
                    //inspResult.TabResultList[tabNo].MacronAkkonResult.AvgStrength.ToString("F3"),

                    "OK",
                    (1 + tabNo).ToString(),             // Count
                    (2.2 + tabNo).ToString("F3"),       // Length
                    (4.4 + tabNo).ToString("F3"),       // Strength
                };

                dataList.Add(tabData);
            }

            CSVHelper.WriteData(csvFile, dataList);
        }

        //private void SaveAkkonResult(string resultPath, AppsInspResult inspResult)
        //{
        //    string filename = string.Format("Akkon.csv");
        //    string csvFile = Path.Combine(resultPath, filename);
        //    if (File.Exists(csvFile) == false)
        //    {
        //        List<string> header = new List<string>
        //        {
        //            "Inspection Time",
        //            "Panel ID",
        //        };

        //        for (int tabNo = 0; tabNo < inspResult.TabResultList.Count; tabNo++)
        //        {
        //            header.Add("Tab");
        //            header.Add("Judge");
        //            header.Add("Count");
        //            header.Add("Length");
        //            header.Add("Strength");
        //            header.Add("STD");
        //        }

        //        CSVHelper.WriteHeader(csvFile, header);
        //    }

        //    List<string> dataList = new List<string>
        //    {
        //        inspResult.EndInspTime.ToString("HH:mm:ss"),
        //        inspResult.Cell_ID.ToString()
        //    };

        //    foreach (var tabResult in inspResult.TabResultList)
        //    {
        //        if(AppsConfig.Instance().AkkonAlgorithmType == AkkonAlgorithmType.Macron)
        //        {
        //            int tabNo = tabResult.TabNo;
        //            var judge = tabResult.MacronAkkonResult.Judgement;
        //            int count = tabResult.MacronAkkonResult.AvgBlobCount;
        //            float length = tabResult.MacronAkkonResult.AvgLength;
        //            float strength = tabResult.MacronAkkonResult.AvgStrength;
        //            float std = tabResult.MacronAkkonResult.AvgStd;
        //            dataList.Add(tabNo.ToString());
        //            dataList.Add(judge.ToString());
        //            dataList.Add(count.ToString());
        //            dataList.Add(length.ToString("F3"));
        //            dataList.Add(strength.ToString("F3"));
        //            dataList.Add(std.ToString("F3"));
        //        }
        //        else
        //        {
        //            int tabNo = tabResult.TabNo;
        //            var judge = "OK";
        //            int count = 10;
        //            float length = 0.0f;
        //            float strength = 0.0f;
        //            float std = 0.0f;

        //            dataList.Add(tabNo.ToString());
        //            dataList.Add(judge.ToString());
        //            dataList.Add(count.ToString());
        //            dataList.Add(length.ToString("F3"));
        //            dataList.Add(strength.ToString("F3"));
        //            dataList.Add(std.ToString("F3"));
        //        }
        //    }

        //    CSVHelper.WriteData(csvFile, dataList);
        //}

        private void SaveUPHResult(string resultPath, AppsInspResult inspResult)
        {
            string filename = string.Format("UPH.csv");
            string csvFile = Path.Combine(resultPath, filename);
            if (File.Exists(csvFile) == false)
            {
                List<string> header = new List<string>
                {
                    "Inspection Time",
                    "Panel ID",
                    "Stage No.",
                    "Tab No.",

                    "Count Min",
                    "Count Avg",
                    "Length Min",
                    "Length Avg",
                    "Strength Min",
                    "Strength Avg",

                    "Left Align X",
                    "Left Align Y",
                    "Center Align X",
                    "Right Align X",
                    "Right Align Y",

                    "ACF Head",
                    "Pre Head",
                    "Main Head",

                    "Judge",
                    "Cause",
                    "Op Judge"
                };

                CSVHelper.WriteHeader(csvFile, header);
            }

            List<List<string>> dataList = new List<List<string>>();
            for (int tabNo = 0; tabNo < inspResult.TabResultList.Count; tabNo++)
            {
                List<string> tabData = new List<string>
                {
                    inspResult.EndInspTime.ToString("HH:mm:ss"),                                    // Insp Time
                    inspResult.Cell_ID,                                                             // Panel ID
                    1.ToString(),                                                                   // Stage
                    tabNo.ToString(),                                                               // Tab

                    //inspResult.TabResultList[tabNo].MacronAkkonResult.AvgBlobCount.ToString(),
                    //inspResult.TabResultList[tabNo].MacronAkkonResult.AvgLength.ToString("F3"),
                    //inspResult.TabResultList[tabNo].MacronAkkonResult.AvgStrength.ToString("F3"),
                    //inspResult.TabResultList[tabNo].MacronAkkonResult.AvgStd.ToString("F3"),
                    (tabNo + 1).ToString(),                                                         // Count Min
                    (tabNo + 2).ToString("F3"),                                                     // Count Avg
                    (tabNo + 3).ToString(),                                                         // Length Min
                    (tabNo + 4).ToString("F3"),                                                     // Length Avg
                    (tabNo + 5).ToString(),                                                         // Strength Min
                    (tabNo + 6).ToString("F3"),                                                     // Strength Avg

                    inspResult.TabResultList[tabNo].LeftAlignX.ResultValue_pixel.ToString("F3"),    // Left Align X
                    inspResult.TabResultList[tabNo].LeftAlignY.ResultValue_pixel.ToString("F3"),    // Left Align Y
                    inspResult.TabResultList[tabNo].CenterX.ToString("F3"),                         // Center Align X
                    inspResult.TabResultList[tabNo].RightAlignX.ResultValue_pixel.ToString("F3"),   // Right Align X
                    inspResult.TabResultList[tabNo].RightAlignY.ResultValue_pixel.ToString("F3"),   // Right Align Y

                    (tabNo + 7).ToString(),                                                         // ACF Head
                    (tabNo + 8).ToString(),                                                         // Pre Head
                    (tabNo + 9).ToString(),                                                         // Main Head

                    inspResult.TabResultList[tabNo].Judgement.ToString(),                           // Judge
                    "Count",                                                                        // Cause
                    "OP_OK"                                                                         // OP Judge
                };

                dataList.Add(tabData);
            }

            CSVHelper.WriteData(csvFile, dataList);
        }

        private string GetExtensionOKImage()
        {
            return "." + ConfigSet.Instance().Operation.ExtensionOKImage;
        }

        private string GetExtensionNGImage()
        {
            return "." + ConfigSet.Instance().Operation.ExtensionNGImage;
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
            MotionManager manager = MotionManager.Instance();

            var teachingInfo = inspModel.GetUnit(UnitName.Unit0).GetTeachingInfo(teachingPos);

            Axis axisX = GetAxis(AxisHandlerName.Handler0, AxisName.X);
            Axis axisY = GetAxis(AxisHandlerName.Handler0, AxisName.Y);
            //Axis axisZ = GetAxis(AxisHandlerName.Handler0, AxisName.Z);

            var movingParamX = teachingInfo.GetMovingParam(AxisName.X.ToString());
            var movingParamY = teachingInfo.GetMovingParam(AxisName.Y.ToString());
            var movingParamZ = teachingInfo.GetMovingParam(AxisName.Z.ToString());

            //if (MoveAxis(teachingPos, axisZ, movingParamZ) == false)
            //{
            //    error = string.Format("Move To Axis Z TimeOut!({0})", movingParamZ.MovingTimeOut.ToString());
            //    Logger.Write(LogType.Seq, error);
            //    return false;
            //}
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
            if (manager.IsAxisInPosition(UnitName.Unit0, teachingPos, axis) == false)
            {
                Stopwatch sw = new Stopwatch();
                sw.Restart();

                manager.StartAbsoluteMove(UnitName.Unit0, teachingPos, axis);

                while (manager.IsAxisInPosition(UnitName.Unit0, teachingPos, axis) == false)
                {
                    if (sw.ElapsedMilliseconds >= movingParam.MovingTimeOut)
                        return false;

                    Thread.Sleep(10);
                }
            }

            return true;
        }

        private ICogImage GetAreaCameraImage(Camera camera)
        {
            camera.GrabOnce();
            byte[] dataArrayRight = camera.GetGrabbedImage();
            Thread.Sleep(50);

            // Right PreAlign Pattern Matching
            var cogImage = VisionProImageHelper.ConvertImage(dataArrayRight, camera.ImageWidth, camera.ImageHeight, camera.ColorFormat);

            return cogImage;
        }

        private void SetMarkMotionPosition(Unit unit, MarkDirection markDirection)
        {
            var preAlignParam = unit.PreAlignParamList.Where(x => x.Direction == markDirection).FirstOrDefault();

            var motionX = MotionManager.Instance().GetAxis(AxisHandlerName.Handler0, AxisName.X).GetActualPosition();
            var motionY = PlcControlManager.Instance().GetReadPosition(AxisName.Y) / 1000;
            var motionT = PlcControlManager.Instance().GetReadPosition(AxisName.T) / 1000;

            preAlignParam.SetMotionData(motionX, motionY, motionT);
        }
        #endregion
    }

    public enum SeqStep
    {
        SEQ_IDLE,
        SEQ_READY,
        SEQ_START,
        SEQ_PREALIGN_R,
        SEQ_PREALIGN_L,
        SEQ_SEND_PREALIGN_DATA,
        SEQ_WAITING,
        SEQ_SCAN_READY,
        SEQ_SCAN_START,
        SEQ_WAITING_SCAN_COMPLETED,
        SEQ_WAITING_INSPECTION_DONE,
        SEQ_PATTERN_MATCH,
        SEQ_ALIGN_INSPECTION,
        SEQ_ALIGN_INSPECTION_COMPLETED,
        SEQ_AKKON_INSPECTION,
        SEQ_AKKON_INSPECTION_COMPLETED,
        SEQ_UI_RESULT_UPDATE,
        SEQ_SAVE_RESULT_DATA,
        SEQ_SAVE_IMAGE,
        SEQ_DELETE_DATA,
        SEQ_CHECK_STANDBY,
    }

    public class AkkonThreadParam
    {
        public TabInspResult TabInspResult { get; set; } = null;

        public Tab Tab { get; set; } = null;
    }
}
