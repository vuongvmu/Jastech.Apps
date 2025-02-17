﻿using Cognex.VisionPro;
using Emgu.CV;
using Jastech.Apps.Structure;
using Jastech.Apps.Structure.Data;
using Jastech.Apps.Winform.Core;
using Jastech.Framework.Config;
using Jastech.Framework.Device.Cameras;
using Jastech.Framework.Device.Motions;
using Jastech.Framework.Imaging;
using Jastech.Framework.Imaging.Helper;
using Jastech.Framework.Imaging.VisionPro;
using Jastech.Framework.Util.Helper;
using Jastech.Framework.Winform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jastech.Apps.Winform
{
    public class LineCamera
    {
        #region 필드
        private int _curGrabCount { get; set; } = 0;

        private int _stackTabNo { get; set; } = 0;

        private object _lock = new object();

        private object _dataLock = new object();

        private Thread _trackingOnThread { get; set; } = null;

        private bool _isStopTrackingOn { get; set; } = false;
        #endregion

        #region 속성
        public Camera Camera { get; private set; } = null;

        public bool IsLive { get; set; } = false;

        public int GrabCount { get; private set; } = 0;

        public int DelayGrabIndex { get; private set; } = -1;

        public double LAFTrackingPos_mm { get; private set; } = -1;

        public List<TabScanBuffer> TabScanBufferList { get; private set; } = new List<TabScanBuffer>();

        public Queue<byte[]> LiveDataQueue = new Queue<byte[]>();

        public Queue<byte[]> DataQueue = new Queue<byte[]>();

        public Task LiveTask { get; set; }

        public CancellationTokenSource CancelLiveTask { get; set; }

        public int CameraGab { get; set; } = -1;
        #endregion

        #region 이벤트
        public event TeachingImageGrabbedDelegate TeachingLiveImageGrabbed;

        public event GrabDoneDelegate GrabDoneEventHandler;

        public event GrabOnceDelegate GrabOnceEventHandler;
        #endregion

        #region 델리게이트
        public delegate void TeachingImageGrabbedDelegate(string cameraName, Mat image);

        public delegate void GrabDoneDelegate(string cameraName, bool isGrabDone);

        public delegate void GrabOnceDelegate(TabScanBuffer tabScanBuffer);

        public delegate void GrabDelayStartDelegate(string cameraName);

        public delegate void LAFTrackingOnOffDelegate(bool isOn);
        #endregion

        #region 생성자
        public LineCamera(Camera camera)
        {
            Camera = camera;
        }
        #endregion

        #region 메서드
        public void ClearTabScanBuffer()
        {
            lock (TabScanBufferList)
            {
                foreach (var buffer in TabScanBufferList)
                    buffer.Dispose();

                TabScanBufferList.Clear();
            }
            _curGrabCount = 0;
            _stackTabNo = 0;
            DelayGrabIndex = -1;
            LAFTrackingPos_mm = -1;
        }

        public void InitGrabSettings()
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            MaterialInfo materialInfo = inspModel.MaterialInfo;

            int tabCount = inspModel.TabCount;
            if (inspModel == null)
                return;

            double plcAlignDataX_mm = PlcControlManager.Instance().ConvertDoubleWordDoubleFormat_mm(Service.Plc.Maps.PlcCommonMap.PLC_AlignDataX);
            string alignLog = string.Format("Align X from PLC : {0} mm", plcAlignDataX_mm);
            Logger.Write(LogType.Device, alignLog);

            float resolution_mm = (float)(Camera.PixelResolution_um / Camera.LensScale) / 1000; // ex) 3.5 um / 5 / 1000 = 0.0007mm
            int totalScanSubImageCount = (int)Math.Ceiling(materialInfo.PanelXSize_mm / resolution_mm / Camera.ImageHeight); // ex) 500mm / 0.0007mm / 1024 pixel

            GrabCount = totalScanSubImageCount;
            _curGrabCount = 0;
            _stackTabNo = 0;

            double tempPos = 0.0;
            int maxEndIndex = 0;

            for (int i = 0; i < tabCount; i++)
            {
                if (i == 0)
                {
                    tempPos -= plcAlignDataX_mm;
                    tempPos += inspModel.MaterialInfo.PanelEdgeToFirst_mm;
                    LAFTrackingPos_mm = tempPos - ((inspModel.MaterialInfo.PanelEdgeToFirst_mm / 2.0));
                }

                double tabLeftOffset = materialInfo.GetLeftOffset(i);

                double startPos = tempPos + tabLeftOffset;
                int startIndex = (int)(startPos / resolution_mm / Camera.ImageHeight);

                double tabWidth = materialInfo.GetTabWidth(i);
                double tabRightOffset = materialInfo.GetRightOffset(i);

                tempPos += tabWidth;

                double endPos = tempPos;
                endPos += tabRightOffset;

                int endIndex = (int)(endPos / resolution_mm / Camera.ImageHeight);
                if (maxEndIndex <= endIndex)
                    maxEndIndex = endIndex;

                tempPos += materialInfo.GetTabToTabDistance(i, tabCount);

                TabScanBuffer scanImage = new TabScanBuffer(i, startIndex, endIndex, Camera.ImageWidth, Camera.ImageHeight);
                lock (TabScanBufferList)
                    TabScanBufferList.Add(scanImage);
            }

            GrabCount = maxEndIndex;
        }

        public void InitGrabSettings(float delayStart_mm)
        {
            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            MaterialInfo materialInfo = inspModel.MaterialInfo;

            int tabCount = inspModel.TabCount;
            if (inspModel == null)
                return;

            double plcAlignDataX_mm = PlcControlManager.Instance().ConvertDoubleWordDoubleFormat_mm(Service.Plc.Maps.PlcCommonMap.PLC_AlignDataX);
            float resolution_mm = (float)(Camera.PixelResolution_um / Camera.LensScale) / 1000; // ex) 3.5 um / 5 / 1000 = 0.0007mm
            int totalScanSubImageCount = (int)Math.Ceiling(materialInfo.PanelXSize_mm / resolution_mm / Camera.ImageHeight); // ex) 500mm / 0.0007mm / 1024 pixel

            GrabCount = totalScanSubImageCount;

            _curGrabCount = 0;
            _stackTabNo = 0;

            double tempPos = 0.0;
            int maxEndIndex = 0;

            for (int i = 0; i < tabCount; i++)
            {
                if (i == 0)
                {
                    tempPos -= plcAlignDataX_mm;
                    tempPos += delayStart_mm;
                    tempPos += inspModel.MaterialInfo.PanelEdgeToFirst_mm;
                }

                double tabLeftOffset = materialInfo.GetLeftOffset(i);
                double startPos = tempPos + tabLeftOffset;

                int startIndex = (int)(startPos / resolution_mm / Camera.ImageHeight);

                double tabWidth = materialInfo.GetTabWidth(i);
                double tabRightOffset = materialInfo.GetRightOffset(i);

                tempPos += tabWidth;

                double endPos = tempPos;
                endPos += tabRightOffset;

                int endIndex = (int)(endPos / resolution_mm / Camera.ImageHeight);
                if (maxEndIndex <= endIndex)
                    maxEndIndex = endIndex;

                tempPos += materialInfo.GetTabToTabDistance(i, tabCount);

                TabScanBuffer scanImage = new TabScanBuffer(i, startIndex, endIndex, Camera.ImageWidth, Camera.ImageHeight);
                lock (TabScanBufferList)
                    TabScanBufferList.Add(scanImage);
            }

            GrabCount = maxEndIndex;
        }

        private double GetCurrentAxisXPosition()
        {
            Axis axisX = MotionManager.Instance().GetAxis(AxisHandlerName.Handler0, AxisName.X);
            return axisX.GetActualPosition();
        }

        public void StartGrab()
        {
            if (Camera == null)
                return;

            Camera.Stop();
            //Thread.Sleep(50);

            Camera.GrabMulti(GrabCount);
            //Thread.Sleep(50);
        }

        public void StartGrab(float scanLength_mm)
        {
            if (Camera == null)
                return;

            if (Camera.IsGrabbing())
                Camera.Stop();

            //ClearTabScanBuffer();

            float resolution_mm = (float)(Camera.PixelResolution_um / Camera.LensScale) / 1000;
            int totalScanSubImageCount = (int)Math.Ceiling(scanLength_mm / resolution_mm / Camera.ImageHeight);

            TabScanBuffer buffer = new TabScanBuffer(0, 0, totalScanSubImageCount, Camera.ImageWidth, Camera.ImageHeight);
            lock(TabScanBufferList)
                TabScanBufferList.Add(buffer);

            GrabCount = totalScanSubImageCount;
            // LineScan Page에서 Line 모드 GrabStart 할 때 Height Set 해줘야함
            Console.WriteLine("Length Grab Count :" + GrabCount);
            Camera.GrabMulti(GrabCount);
        }

        public void StartGrabContinous()
        {
            if (Camera == null)
                return;

            if (Camera.IsGrabbing())
                Camera.Stop();

            Camera.GrabContinous();
        }

        public void StopGrab()
        {
            if (Camera == null)
                return;

            lock(_lock)
                LiveDataQueue.Clear();

            Camera.Stop();
        }

        public void AddSubImage(byte[] data, int grabCount)
        {
            if (IsLive)
            {
                lock (_lock)
                    LiveDataQueue.Enqueue(data);
            }
            else
            {
                TabScanBuffer tabScanBuffer = GetTabScanBuffer(_stackTabNo);
                if (tabScanBuffer == null)
                    return;

                if (tabScanBuffer.StartIndex <= _curGrabCount && _curGrabCount <= tabScanBuffer.EndIndex)
                {
                    tabScanBuffer.AddData(data);
                }

                if (tabScanBuffer.IsAddDataDone())
                {
                    _stackTabNo++;
                }

                if (_curGrabCount == GrabCount - 1)
                {
                    Camera.Stop();
                    GrabDoneEventHandler?.Invoke(Camera.Name, true);
                    GrabOnceEventHandler?.Invoke(tabScanBuffer);
                }

                _curGrabCount++;
            }   
        }

        private TabScanBuffer GetTabScanBuffer(int tabNo)
        {
            if (tabNo < TabScanBufferList.Count)
                return TabScanBufferList[tabNo];

            return null;
        }

        public void StartLiveTask()
        {
            if (LiveTask != null)
                return;

            CancelLiveTask = new CancellationTokenSource();
            LiveTask = new Task(UpdateLiveImage, CancelLiveTask.Token);
            LiveTask.Start();
        }

        public void StopLiveTask()
        {
            if (LiveTask == null)
                return;

            CancelLiveTask.Cancel();
            LiveTask.Wait();
            LiveTask = null;
        }

        public void UpdateLiveImage()
        {
            while (true)
            {
                if (CancelLiveTask.IsCancellationRequested)
                {
                    ClearTabScanBuffer();
                    break;
                }

                lock (_lock)
                {
                    if (LiveDataQueue.Count() > 0)
                    {
                        byte[] data = LiveDataQueue.Dequeue();
                        Mat mat = MatHelper.ByteArrayToMat(data, Camera.ImageWidth, Camera.ImageHeight, 1);

                        Mat rotatedMat = MatHelper.Transpose(mat);
  
                        if (mat != null)
                            TeachingLiveImageGrabbed?.Invoke(Camera.Name, rotatedMat);

                        mat.Dispose();
                    }
                }

                Thread.Sleep(0);
            }
        }

        public void SetOperationMode(TDIOperationMode operationMode)
        {
            if (Camera == null)
                return;

            if (Camera is CameraMil milCamera)
            {
                if (operationMode == TDIOperationMode.TDI)
                    milCamera.SetTriggerMode(TriggerMode.Hardware);
                else
                    milCamera.SetTriggerMode(TriggerMode.Software);

                milCamera.SetTDIOperationMode(operationMode);
            }
        }

        public TDIOperationMode GetTDIOperationMode()
        {
            if (Camera is CameraMil milCamera)
            {
                return milCamera.TDIOperationMode;
            }
            return TDIOperationMode.TDI;
        }

        public ICogImage ConvertCogGrayImage(Mat mat)
        {
            if (mat == null)
                return null;

            int size = mat.Width * mat.Height * mat.NumberOfChannels;
            ColorFormat format = mat.NumberOfChannels == 1 ? ColorFormat.Gray : ColorFormat.RGB24;
            var cogImage = VisionProImageHelper.CovertImage(mat.DataPointer, mat.Width, mat.Height, mat.Step, format);
            return cogImage;
        }
        #endregion
    }
}
