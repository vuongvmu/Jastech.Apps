﻿using Jastech.Apps.Structure.Data;
using Jastech.Apps.Structure;
using Jastech.Framework.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jastech.Framework.Imaging.Result;
using Jastech.Framework.Util.Helper;
using Cognex.VisionPro;
using Jastech.Apps.Winform;
using Jastech.Framework.Device.Cameras;
using Jastech.Framework.Device.Motions;
using Jastech.Framework.Imaging.VisionPro;
using System.Diagnostics;
using System.Threading;
using Jastech.Apps.Winform.Service.Plc.Maps;
using Jastech.Apps.Winform.Settings;
using Jastech.Apps.Structure.VisionTool;
using Jastech.Apps.Winform.Core.Calibrations;
using Jastech.Framework.Imaging.VisionPro.VisionAlgorithms.Results;
using System.Drawing;
using Jastech.Framework.Winform;
using Jastech.Apps.Winform.Core;
using ATT_UT_Remodeling.Core.Data;
using Emgu.CV;
using System.Windows.Forms;

namespace ATT_UT_Remodeling
{
    public partial class PreAlignRunner
    {
        #region 필드
        private Task SeqTask { get; set; }

        private CancellationTokenSource SeqTaskCancellationTokenSource { get; set; }

        private SeqStep SeqStep { get; set; } = SeqStep.SEQ_IDLE;
        #endregion

        #region 속성
        private MainAlgorithmTool AlgorithmTool = new MainAlgorithmTool();

        private CogImage8Grey VirtualLeftImage = null;

        private CogImage8Grey VirtualRightImage = null;
        #endregion

        #region 이벤트
        #endregion

        #region 델리게이트
        #endregion

        #region 생성자
        #endregion

        #region 메서드
        #endregion

        public void SeqRun()
        {
            if (ModelManager.Instance().CurrentModel == null)
                return;

            SystemManager.Instance().MachineStatus = MachineStatus.RUN;

            if (SeqTask != null)
            {
                SeqStep = SeqStep.SEQ_START;
                return;
            }

            SeqTaskCancellationTokenSource = new CancellationTokenSource();
            SeqTask = new Task(SeqTaskAction, SeqTaskCancellationTokenSource.Token);
            SeqTask.Start();
        }

        public void SeqStop()
        {
            SystemManager.Instance().MachineStatus = MachineStatus.STOP;

            var areaCamera = AreaCameraManager.Instance().GetAppsCamera("PreAlign");
            areaCamera.StopGrab();
            AreaCameraManager.Instance().GetAreaCamera("PreAlign").StopGrab();

            // 조명 off
            LAFManager.Instance().GetLAF("Laf").LafCtrl.SetTrackingOnOFF(false);
            WriteLog("AutoFocus Off.");

            if (SeqTask == null)
                return;

            SeqTaskCancellationTokenSource.Cancel();
            SeqTask.Wait();
            SeqTask = null;

            WriteLog("Stop Sequence.");
        }

        private void SeqTaskAction()
        {
            var cancellationToken = SeqTaskCancellationTokenSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            SeqStep = SeqStep.SEQ_INIT;

            while (true)
            {
                // 작업 취소
                if (cancellationToken.IsCancellationRequested)
                {
                    SeqStep = SeqStep.SEQ_IDLE;

                    // Light off
                    DeviceManager.Instance().LightCtrlHandler.TurnOff();

                    // Camera stop
                    LineCameraManager.Instance().Stop("LineCamera");
                    AreaCameraManager.Instance().Stop("PreAlign");
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

            var areaCamera = AreaCameraManager.Instance().GetAreaCamera("PreAlign");
            if (areaCamera == null)
                return;

            var laf = LAFManager.Instance().GetLAF("Laf").LafCtrl;
            if (laf == null)
                return;

            var light = DeviceManager.Instance().LightCtrlHandler;
            
            string systemLogMessage = string.Empty;
            string errorMessage = string.Empty;

            switch (SeqStep)
            {
                case SeqStep.SEQ_IDLE:
                    break;

                case SeqStep.SEQ_INIT:
                  
                    light.TurnOff();
                    WriteLog("Light Off.");

                    PlcControlManager.Instance().ClearAlignData();
                    WriteLog("Clear PLC Data.");

                    laf.SetTrackingOnOFF(false);
                    laf.SetMotionAbsoluteMove(0);
                    WriteLog("LAF Off.");

                    var camera = areaCamera.Camera;
                    camera.Stop();
                    WriteLog($"Set Camera Property. Expose : {camera.Exposure}, AnalogGain : {camera.AnalogGain}");

                    // CELL ID 넣는곳이 없음
                    SeqStep = SeqStep.SEQ_WAITING;
                    break;

                case SeqStep.SEQ_WAITING:
                    if (AppsStatus.Instance().IsPreAlignRunnerFlagFromPlc == false)
                        break;

                    WriteLog("Receive PreAlign Start Signal From PLC.", true);

                    AppsPreAlignResult.Instance().ClearResult();
                    WriteLog("Clear PreAlign Result.");

                    SystemManager.Instance().ClearPreAlignResult();
                    WriteLog("Clear PreAlign Display");

                    AppsInspResult.Instance().StartInspTime = DateTime.Now;
                    AppsInspResult.Instance().Cell_ID = GetCellID();
                    WriteLog("Cell ID : " + AppsInspResult.Instance().Cell_ID, true);

                    SeqStep = SeqStep.SEQ_PREALIGN_R;
                    break;
           
                case SeqStep.SEQ_PREALIGN_R:

                    if (MoveTo(TeachingPosType.Stage1_PreAlign_Right, out errorMessage) == false)
                        SeqStep = SeqStep.SEQ_ERROR;
                    else
                    {
                        // Light On
                        light.TurnOn(unit.PreAlign.RightLightParam);
                        Thread.Sleep(100);
                        WriteLog("Left Prealign Light On.");

                        // Grab
                        if(ConfigSet.Instance().Operation.VirtualMode == false)
                        {
                            var preAlignRightImage = GetAreaCameraImage(areaCamera.Camera) as CogImage8Grey;
                            AppsPreAlignResult.Instance().Right.CogImage = preAlignRightImage;
                            AppsPreAlignResult.Instance().Right.MatchResult = RunPreAlignMark(unit, preAlignRightImage, MarkDirection.Right);
                        }
                        else
                        {
                            AppsPreAlignResult.Instance().Right.CogImage = VirtualRightImage;
                            AppsPreAlignResult.Instance().Right.MatchResult = RunPreAlignMark(unit, VirtualRightImage, MarkDirection.Right);
                        }

                        if (AppsPreAlignResult.Instance().Right.MatchResult == null)
                            SeqStep = SeqStep.SEQ_ERROR;
                        else
                        {
                            WriteLog("Complete PreAlign Right Mark Search.", true);

                            SystemManager.Instance().UpdateRightPreAlignResult(AppsPreAlignResult.Instance());
                            WriteLog("Update Right PreAlign Image.", true);

                            // Set prealign motion position
                            SetMarkMotionPosition(unit, MarkDirection.Right);
                            WriteLog("Set Axis Information For PreAlign Right Mark Position.");

                            SeqStep = SeqStep.SEQ_PREALIGN_L;
                        }
                    }
                    break;

                case SeqStep.SEQ_PREALIGN_L:

                    if (MoveTo(TeachingPosType.Stage1_PreAlign_Left, out errorMessage) == false)
                        SeqStep = SeqStep.SEQ_ERROR;
                    else
                    {
                        // Light On
                        light.TurnOn(unit.PreAlign.LeftLightParam);
                        Thread.Sleep(100);
                        WriteLog("Left PreAlign Light On.", true);

                        // Grab
                        if (ConfigSet.Instance().Operation.VirtualMode == false)
                        {
                            var preAlignLeftImage = GetAreaCameraImage(areaCamera.Camera);
                            AppsPreAlignResult.Instance().Left.CogImage = preAlignLeftImage;
                            AppsPreAlignResult.Instance().Left.MatchResult = RunPreAlignMark(unit, preAlignLeftImage, MarkDirection.Left);
                        }
                        else
                        {
                            AppsPreAlignResult.Instance().Left.CogImage = VirtualLeftImage;
                            AppsPreAlignResult.Instance().Left.MatchResult = RunPreAlignMark(unit, VirtualLeftImage, MarkDirection.Left);
                        }

                        if (AppsPreAlignResult.Instance().Right.MatchResult == null)
                            SeqStep = SeqStep.SEQ_ERROR;
                        else
                        {
                            WriteLog("Complete PreAlign Left Mark Search.", true);

                            SystemManager.Instance().UpdateLeftPreAlignResult(AppsPreAlignResult.Instance());
                            WriteLog("Update Left PreAlign Image.", true);

                            // Set prealign pattern match result
                            SetPreAlignPatternResult();

                            // Set prealign motion position
                            SetMarkMotionPosition(unit, MarkDirection.Left);
                            WriteLog("Set Axis Information For Prealign Left Mark Position.");

                            SeqStep = SeqStep.SEQ_SEND_PREALIGN_DATA;
                        }
                    }
                    break;

                case SeqStep.SEQ_SEND_PREALIGN_DATA:

                    light.TurnOff();
                    WriteLog("PreAlign Light Off.",true);

                    if (RunPreAlign() == true)
                    {
                        var offsetX = AppsPreAlignResult.Instance().OffsetX;
                        var offsetY = AppsPreAlignResult.Instance().OffsetY;
                        var offsetT = AppsPreAlignResult.Instance().OffsetT;

                        PlcControlManager.Instance().WriteAlignData(offsetX, offsetY, offsetT);
                        WriteLog($"Write PreAlign Data.(OffsetX : {offsetX:F4}, OffsetY : {offsetY:F4}, OffsetT : {offsetT:F4})", true);

                        // Check Tolerance
                        if (Math.Abs(offsetX) <= AppsConfig.Instance().PreAlignToleranceX
                            || Math.Abs(offsetY) <= AppsConfig.Instance().PreAlignToleranceY
                            || Math.Abs(offsetT) <= AppsConfig.Instance().PreAlignToleranceTheta)
                        {
                            PlcControlManager.Instance().WritePcStatus(PlcCommand.StartPreAlign);
                            WriteLog($"Send PreAlign OK Complete Signal.(SpecIn) {(int)PlcCommand.StartPreAlign}", true);
                        }
                        else
                        {
                            PlcControlManager.Instance().WritePcStatus(PlcCommand.StartPreAlign, true);
                            WriteLog($"Send PreAlign NG Complete Signal.(SpecOut) {(int)PlcCommand.StartPreAlign * -1}", true);
                        }
                    }
                    else
                    {
                        PlcControlManager.Instance().WritePcStatus(PlcCommand.StartPreAlign, true);
                        WriteLog($"Send PreAlign NG Complete Signal.(Mark Fail) {(int)PlcCommand.StartPreAlign * -1}", true);
                    }

                    AppsInspResult.Instance().EndInspTime = DateTime.Now;

                    SystemManager.Instance().UpdatePreAlignResult(AppsPreAlignResult.Instance());
                    SeqStep = SeqStep.SEQ_SAVE_RESULT_DATA;
                    break;

                case SeqStep.SEQ_SAVE_RESULT_DATA:
                    SavePreAlignResultCSV();
                    WriteLog("Save PreAlign result.", true);
                    SeqStep = SeqStep.SEQ_SAVE_IMAGE;
                    break;

                case SeqStep.SEQ_SAVE_IMAGE:
                    AddOverlay();

                    SaveResultImage();
                    SeqStep = SeqStep.SEQ_END;
                    break;

                case SeqStep.SEQ_END:
                    if (ConfigSet.Instance().Operation.VirtualMode)
                        AppsStatus.Instance().IsPreAlignRunnerFlagFromPlc = false;

                    SeqStep = SeqStep.SEQ_INIT;
                    break;

                case SeqStep.SEQ_ERROR:

                    WriteLog("SEQ_ERROR.", true);

                    // Light off
                    DeviceManager.Instance().LightCtrlHandler.TurnOff();
                    // Camera stop
                    AreaCameraManager.Instance().Stop("PreAlign");

                    PlcControlManager.Instance().WritePcStatus(PlcCommand.StartPreAlign, false);
                    WriteLog($"Send PreAlign NG Complete Signal.{(int)PlcCommand.StartPreAlign * -1}", true);

                    Thread.Sleep(300);

                    SeqStep = SeqStep.SEQ_IDLE;
                    break;
                default:
                    break;
            }
        }

        private string GetCellID()
        {
            string cellId = PlcControlManager.Instance().GetAddressMap(PlcCommonMap.PLC_Cell_Id).Value;

            if (cellId == "0" || cellId == null)
                return DateTime.Now.ToString("yyyyMMddHHmmss");
            else
                return cellId;
        }

        private void SetPreAlignPatternResult()
        {
            var preAlignResult = AppsPreAlignResult.Instance();

            if (preAlignResult.Left.MatchResult.Judgement == Judgement.OK && preAlignResult.Right.MatchResult.Judgement == Judgement.OK)
                preAlignResult.Judgement = Judgement.OK;

            if (preAlignResult.Left.MatchResult.Judgement == Judgement.NG || preAlignResult.Right.MatchResult.Judgement == Judgement.NG)
                preAlignResult.Judgement = Judgement.NG;
        }

        private bool RunPreAlign()
        {
            var preAlignResult = AppsPreAlignResult.Instance();

            if (preAlignResult.Judgement == Judgement.OK)
            {
                PointF leftVisionCoordinates = preAlignResult.Left.MatchResult.MaxMatchPos.FoundPos;
                PointF rightVisionCoordinates = preAlignResult.Right.MatchResult.MaxMatchPos.FoundPos;

                List<PointF> realCoordinateList = new List<PointF>();
                PointF leftRealCoordinates = CalibrationData.Instance().ConvertVisionToReal(leftVisionCoordinates);
                PointF righttRealCoordinates = CalibrationData.Instance().ConvertVisionToReal(rightVisionCoordinates);

                realCoordinateList.Add(leftRealCoordinates);
                realCoordinateList.Add(righttRealCoordinates);

                var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
                if (inspModel == null)
                    return false;

                var unit = inspModel.GetUnit(UnitName.Unit0);
                if (unit == null)
                    return false;

                PointF calibrationStartPosition = CalibrationData.Instance().GetCalibrationStartPosition();

                var alignmentResult = AlgorithmTool.ExecuteAlignment(unit, realCoordinateList, calibrationStartPosition);

                preAlignResult.SetPreAlignResult(alignmentResult.OffsetX, alignmentResult.OffsetY, alignmentResult.OffsetT);

                Judgement leftJudgement = preAlignResult.Left.MatchResult.Judgement;
                Judgement rightJudgement = preAlignResult.Right.MatchResult.Judgement;
                var leftScore = preAlignResult.Left.MatchResult.MaxMatchPos.Score;
                var rightScore = preAlignResult.Right.MatchResult.MaxMatchPos.Score;

                WriteLog($"OK Mark Search For PreAlign. (Left : {leftJudgement.ToString()}, Right : {rightJudgement.ToString()})", true);
                WriteLog($"FoundedMark Score. (Left : {(leftScore * 100).ToString("F2")}, Right : {(rightScore * 100).ToString("F2")})", true);
                return true;
            }
            else
            {
                Judgement leftJudgement = preAlignResult.Left.MatchResult.Judgement;
                Judgement rightJudgement = preAlignResult.Right.MatchResult.Judgement;
                var leftScore = preAlignResult.Left.MatchResult.MaxMatchPos.Score;
                var rightScore = preAlignResult.Right.MatchResult.MaxMatchPos.Score;

                WriteLog($"NG Mark Search For PreAlign. (Left : {leftJudgement.ToString()}, Right : {rightJudgement.ToString()})", true);
                WriteLog($"FoundedMark Score. (Left : {(leftScore * 100).ToString("F2")}, Right : {(rightScore * 100).ToString("F2")})", true);
                return false;
            } 
        }

        private VisionProPatternMatchingResult RunPreAlignMark( Unit unit, ICogImage cogImage, MarkDirection markDirection)
        {
            var preAlignParam = unit.PreAlign.AlignParamList.Where(x => x.Direction == markDirection).FirstOrDefault();

            VisionProPatternMatchingResult result = AlgorithmTool.RunPatternMatch(cogImage, preAlignParam.InspParam);

            return result;
        }
    }

    public partial class PreAlignRunner
    {
        private void SaveResultImage()
        {
            //if (ConfigSet.Instance().Operation.VirtualMode)
            //    return;

            var appsInspResult = AppsInspResult.Instance();

            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            DateTime currentTime = appsInspResult.StartInspTime;

            string month = currentTime.ToString("MM");
            string day = currentTime.ToString("dd");
            string folderPath = appsInspResult.Cell_ID;

            string path = Path.Combine(ConfigSet.Instance().Path.Result, inspModel.Name, month, day, folderPath, "Orgin");

            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            var operation = ConfigSet.Instance().Operation;
            string okExtension = operation.GetExtensionOKImage();
            string ngExtension = operation.GetExtensionNGImage();

            var appsPreAlignResult = AppsPreAlignResult.Instance();

            //appsPreAlignResult.Left.CogImage
            if (appsPreAlignResult.Judgement == Judgement.OK)
            {
                if (operation.SaveImageOK)
                {
                    string leftImageName = "PreAlign_Left_OK" + okExtension;
                    string leftImagePath = Path.Combine(path, leftImageName);
                    SaveImage(appsPreAlignResult.Left.CogImage, leftImagePath);

                    string rightImageName = "PreAlign_Right_OK" + okExtension;
                    string rightImagePath = Path.Combine(path, rightImageName);
                    SaveImage(appsPreAlignResult.Right.CogImage, rightImagePath);
                }
            }
            else
            {
                if (operation.SaveImageNG)
                {
                    string leftImageName = "PreAlign_Left_NG" + ngExtension;
                    string leftImagePath = Path.Combine(path, leftImageName);
                    SaveImage(appsPreAlignResult.Left.CogImage, leftImagePath);

                    string rightImageName = "PreAlign_Right_OK" + ngExtension;
                    string rightImagePath = Path.Combine(path, rightImageName);
                    SaveImage(appsPreAlignResult.Right.CogImage, rightImagePath);
                }
            }
        }

        private void SaveImage(Mat image, string filePath)
        {
            if (image == null)
                return;

            image.Save(filePath);
        }

        private void SaveImage(ICogImage image, string filePath, bool isOverlay = false)
        {
            if (image == null)
                return;

            if (isOverlay)
            {
            }
            else
                VisionProImageHelper.Save(image, filePath);
        }

        private void SavePreAlignResultCSV()
        {
            var inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            DateTime currentTime = AppsInspResult.Instance().StartInspTime;

            string month = currentTime.ToString("MM");
            string day = currentTime.ToString("dd");
            string folderPath = AppsInspResult.Instance().Cell_ID;

            string path = Path.Combine(ConfigSet.Instance().Path.Result, inspModel.Name, month, day);
            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            string fileName = string.Format("PreAlign.csv");
            string csvFile = Path.Combine(path, fileName);
            
            if (File.Exists(csvFile) == false)
            {
                List<string> header = new List<string>
                {
                    "Inspection Time",
                    "Panel ID",
                    "OffsetX",
                    "OffsetY",
                    "OffsetT",
                };

                CSVHelper.WriteHeader(csvFile, header);
            }

            var appsPreAlignResult = AppsPreAlignResult.Instance();

            List<string> dataList = new List<string>
            {
                AppsInspResult.Instance().EndInspTime.ToString("HH:mm:ss"),
                AppsInspResult.Instance().Cell_ID,
                appsPreAlignResult.OffsetX.ToString("F4"),
                appsPreAlignResult.OffsetY.ToString("F4"),
                appsPreAlignResult.OffsetT.ToString("F4"),
            };

            CSVHelper.WriteData(csvFile, dataList);
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
            Axis axisZ = GetAxis(AxisHandlerName.Handler0, AxisName.Z0);

            var movingParamX = teachingInfo.GetMovingParam(AxisName.X.ToString());
            var movingParamZ = teachingInfo.GetMovingParam(AxisName.Z0.ToString());

            if (MoveAxis(teachingPos, axisX, movingParamX) == false)
            {
                errorMessage = string.Format("Move To Axis X TimeOut!({0})", movingParamX.MovingTimeOut.ToString());
                WriteLog(errorMessage);
                return false;
            }

            if (MoveAxis(teachingPos, axisZ, movingParamZ) == false)
            {
                errorMessage = string.Format("Move To Axis Z TimeOut!({0})", movingParamZ.MovingTimeOut.ToString());
                Logger.Write(LogType.Seq, errorMessage);
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

            // Right PreAlign Pattern Matching
            var cogImage = VisionProImageHelper.ConvertImage(dataArrayRight, camera.ImageWidth, camera.ImageHeight, camera.ColorFormat);

            return cogImage;
        }

        public void SetPreAlignLeftImage(CogImage8Grey cogImage)
        {
            if (VirtualLeftImage != null)
                (VirtualLeftImage as CogImage8Grey).Dispose();

            VirtualLeftImage = null;
            VirtualLeftImage = cogImage;
        }

        public void SetPreAlignRightImage(CogImage8Grey cogImage)
        {
            if (VirtualRightImage != null)
                (VirtualRightImage as CogImage8Grey).Dispose();

            VirtualRightImage = null;
            VirtualRightImage = cogImage;
        }

        private void SetMarkMotionPosition(Unit unit, MarkDirection markDirection)
        {
            var preAlignParam = unit.PreAlign.AlignParamList.Where(x => x.Direction == markDirection).FirstOrDefault();

            var motionX = MotionManager.Instance().GetAxis(AxisHandlerName.Handler0, AxisName.X).GetActualPosition();
            var motionY = PlcControlManager.Instance().GetReadPosition(AxisName.Y) / 1000;
            var motionT = PlcControlManager.Instance().GetReadPosition(AxisName.T) / 1000;

            preAlignParam.SetMotionData(motionX, motionY, motionT);
        }

        private void WriteLog(string logMessage, bool isSystemLog = false)
        {
            if (isSystemLog)
                SystemManager.Instance().AddSystemLogMessage(logMessage);

            Logger.Write(LogType.Seq, logMessage);
        }

        private ICogImage AddOverlay()
        {
            var result = AppsPreAlignResult.Instance().Left;
            List<CogCompositeShape> shapes = new List<CogCompositeShape>();

            var graphics = result.MatchResult.MaxMatchPos.ResultGraphics;

            shapes.Add(graphics);

            var deepCopyImage = result.CogImage.CopyBase(CogImageCopyModeConstants.CopyPixels);

            CogGraphicInteractiveCollection collect = new CogGraphicInteractiveCollection();

            foreach (var item in shapes)
                collect.Add(item);

            return null;
        }
    }

    public enum SeqStep
    {
        SEQ_IDLE,
        SEQ_INIT,
        SEQ_WAITING,
        SEQ_START,
        SEQ_PREALIGN_R,
        SEQ_PREALIGN_L,
        SEQ_SEND_PREALIGN_DATA,
        SEQ_SAVE_RESULT_DATA,
        SEQ_SAVE_IMAGE,
        SEQ_END,
        SEQ_ERROR,
    }
}
