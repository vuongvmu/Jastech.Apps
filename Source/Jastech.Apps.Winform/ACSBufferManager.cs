﻿using Jastech.Apps.Structure;
using Jastech.Apps.Structure.Data;
using Jastech.Apps.Winform.Core;
using Jastech.Apps.Winform.Settings;
using Jastech.Framework.Config;
using Jastech.Framework.Device.Motions;
using Jastech.Framework.Structure;
using Jastech.Framework.Winform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jastech.Apps.Winform
{
    public class ACSBufferManager
    {
        #region 필드
        private static ACSBufferManager _instance = null;

        private bool _activeLafTrigger { get; set; } = false;
        #endregion

        #region 속성
        #endregion

        #region 이벤트
        #endregion

        #region 델리게이트
        #endregion

        #region 생성자
        #endregion

        #region 메서드
        public static ACSBufferManager Instance()
        {
            if (_instance == null)
            {
                _instance = new ACSBufferManager();
            }

            return _instance;
        }

        public void Initialize()
        {
            if (DeviceManager.Instance().MotionHandler.Count > 0)
            {
                var motion = DeviceManager.Instance().MotionHandler.First() as ACSMotion;
                int index = ACSBufferConfig.Instance().CameraTrigger;
                motion?.RunBuffer(index);

                if(AppsConfig.Instance().EnableLafTrigger)
                {
                    _activeLafTrigger = true;

                    ConstructBuffer();
                    SetStopMode();

                    foreach (var trigger in ACSBufferConfig.Instance().LafTriggerBufferList)
                        motion?.RunBuffer(trigger.BufferNumber);
                }
            }
        }

        private void ConstructBuffer()
        {
            if (AppsConfig.Instance().EnableLafTrigger == false)
                return;

            if (DeviceManager.Instance().MotionHandler.Count > 0)
            {
                var motion = DeviceManager.Instance().MotionHandler.First() as ACSMotion;
                var config = ACSBufferConfig.Instance();
                int tabMaxCount = AppsConfig.Instance().TabMaxCount;

                foreach (var buffer in ACSBufferConfig.Instance().LafTriggerBufferList)
                {
                    motion?.WriteRealVariable(config.IoEnableModeName, (int)IoEnableMode.Off, buffer.LafArrayIndex, buffer.LafArrayIndex);
                    motion?.WriteRealVariable(config.IoAddrName, buffer.OutputBit, buffer.LafArrayIndex, buffer.LafArrayIndex);

                    for (int tabNum = 0; tabNum < tabMaxCount; tabNum++)
                    {
                        motion?.WriteRealVariable(config.IoPositionUsagesName, 0, buffer.LafArrayIndex, buffer.LafArrayIndex, tabNum, tabNum);
                        motion?.WriteRealVariable(config.LaserStartPositionsName, 0, buffer.LafArrayIndex, buffer.LafArrayIndex, tabNum, tabNum);
                        motion?.WriteRealVariable(config.LaserEndPositionsName, 0, buffer.LafArrayIndex, buffer.LafArrayIndex, tabNum, tabNum);
                    }
                }
            }
        }

        public void Release()
        {
            if (DeviceManager.Instance().MotionHandler.Count > 0)
            {
                var motion = DeviceManager.Instance().MotionHandler.First() as ACSMotion;
                int index = ACSBufferConfig.Instance().CameraTrigger;
                motion?.StopBuffer(index);

                if(_activeLafTrigger)
                {
                    foreach (var trigger in ACSBufferConfig.Instance().LafTriggerBufferList)
                        motion?.StopBuffer(trigger.BufferNumber);
                }
            }
        }

        public void SetAutoMode()
        {
            if (AppsConfig.Instance().EnableLafTrigger == false)
                return;

            if (DeviceManager.Instance().MotionHandler.Count > 0)
            {
                var motion = DeviceManager.Instance().MotionHandler.First() as ACSMotion;
                var config = ACSBufferConfig.Instance();

                foreach (var buffer in ACSBufferConfig.Instance().LafTriggerBufferList)
                    motion?.WriteRealVariable(config.IoEnableModeName, (int)IoEnableMode.Auto, buffer.LafArrayIndex, buffer.LafArrayIndex);
            }
        }

        public void SetStopMode()
        {
            if (AppsConfig.Instance().EnableLafTrigger == false)
                return;

            if (DeviceManager.Instance().MotionHandler.Count > 0)
            {
                var motion = DeviceManager.Instance().MotionHandler.First() as ACSMotion;
                var config = ACSBufferConfig.Instance();

                foreach (var buffer in ACSBufferConfig.Instance().LafTriggerBufferList)
                    motion?.WriteRealVariable(config.IoEnableModeName, (int)IoEnableMode.Off, buffer.LafArrayIndex, buffer.LafArrayIndex);
            }
        }

        public void SetManualMode(string lafName)
        {
            if (AppsConfig.Instance().EnableLafTrigger == false)
                return;

            SetStopMode();

            var triggerBuffer = ACSBufferConfig.Instance().GetTriggerBuffer(lafName);

            if (DeviceManager.Instance().MotionHandler.Count > 0)
            {
                var motion = DeviceManager.Instance().MotionHandler.First() as ACSMotion;
                var config = ACSBufferConfig.Instance();

                motion?.WriteRealVariable(config.IoEnableModeName, (int)IoEnableMode.On, triggerBuffer.LafArrayIndex, triggerBuffer.LafArrayIndex);
            }
        }

        public void EnableTabCount(string lafName, int tabCount)
        {
            if (AppsConfig.Instance().EnableLafTrigger == false)
                return;

            if (DeviceManager.Instance().MotionHandler.Count > 0)
            {
                var triggerBuffer = ACSBufferConfig.Instance().GetTriggerBuffer(lafName);
                var motion = DeviceManager.Instance().MotionHandler.First() as ACSMotion;
                var config = ACSBufferConfig.Instance();

                int tabMaxCount = AppsConfig.Instance().TabMaxCount;

                for (int tabNum = 0; tabNum < tabMaxCount; tabNum++)
                {
                    if (tabNum < tabCount)
                        motion?.WriteRealVariable(config.IoPositionUsagesName, 1, triggerBuffer.LafArrayIndex, triggerBuffer.LafArrayIndex, tabNum, tabNum);
                    else
                        motion?.WriteRealVariable(config.IoPositionUsagesName, 0, triggerBuffer.LafArrayIndex, triggerBuffer.LafArrayIndex, tabNum, tabNum);
                }
            }
        }

        private void SetTriggerPosition(string lafName, List<IoPositionData> positionList)
        {
            if (AppsConfig.Instance().EnableLafTrigger == false)
                return;

            EnableTabCount(lafName, positionList.Count);

            if (DeviceManager.Instance().MotionHandler.Count > 0)
            {
                var triggerBuffer = ACSBufferConfig.Instance().GetTriggerBuffer(lafName);
                var motion = DeviceManager.Instance().MotionHandler.First() as ACSMotion;
                var config = ACSBufferConfig.Instance();

                int lafIndex = triggerBuffer.LafArrayIndex;
                for (int tabNum = 0; tabNum < positionList.Count; tabNum++)
                {
                    motion?.WriteRealVariable(config.LaserStartPositionsName, positionList[tabNum].Start, lafIndex, lafIndex, tabNum, tabNum);
                    motion?.WriteRealVariable(config.LaserEndPositionsName, positionList[tabNum].End, lafIndex, lafIndex, tabNum, tabNum);
                }
            }
        }

        public void SetLafTriggerPosition(UnitName unitName, string lafName, List<TabScanBuffer> tabScanBufferList, double offset = 0)
        {
            if (ConfigSet.Instance().Operation.VirtualMode || AppsConfig.Instance().EnableLafTrigger == false)
                return;

            var camera = DeviceManager.Instance().CameraHandler.First();
            double resolution_um = camera.PixelResolution_um / camera.LensScale;

            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            var unit = inspModel.GetUnit(unitName);
            
            var posData = unit.GetTeachingInfo(TeachingPosType.Stage1_Scan_Start);
            double teachingStartPos = posData.GetTargetPosition(AxisName.X) + offset;

            double subImageSize = (resolution_um * camera.ImageHeight) / 1000.0;

            List<IoPositionData> dataList = new List<IoPositionData>();

            foreach (var scanBuffer in tabScanBufferList)
            {
                double tempStart = teachingStartPos + (scanBuffer.StartIndex * subImageSize);
                double tempEnd = teachingStartPos + ((scanBuffer.EndIndex + 1) * subImageSize);

                double afLeftOffset = unit.GetTab(scanBuffer.TabNo).LafTriggerOffset.Left;
                double afRightOffset = unit.GetTab(scanBuffer.TabNo).LafTriggerOffset.Right;

                IoPositionData data = new IoPositionData
                {
                    Start = tempStart + afLeftOffset,
                    End = tempEnd + afRightOffset,
                };

                dataList.Add(data);
            }
            dataList.Sort((x, y) => x.Start.CompareTo(y.Start));

            SetTriggerPosition(lafName, dataList);
        }

        //public void SetLafTriggerPosition(UnitName unitName, string lafName, TabScanBuffer tabScanBuffer, double leftOffset, double rightOffset)
        //{
        //    if (ConfigSet.Instance().Operation.VirtualMode || AppsConfig.Instance().EnableLafTrigger == false)
        //        return;

        //    var camera = DeviceManager.Instance().CameraHandler.First();
        //    double resolution_um = camera.PixelResolution_um / camera.LensScale;

        //    AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
        //    var unit = inspModel.GetUnit(unitName);

        //    var posData = unit.GetTeachingInfo(TeachingPosType.Stage1_Scan_Start);
        //    double teachingStartPos = posData.GetTargetPosition(AxisName.X) + offset;

        //    double subImageSize = (resolution_um * camera.ImageHeight) / 1000.0;

        //    List<IoPositionData> dataList = new List<IoPositionData>();

        //    foreach (var scanBuffer in tabScanBufferList)
        //    {
        //        double tempStart = teachingStartPos + (scanBuffer.StartIndex * subImageSize);
        //        double tempEnd = teachingStartPos + ((scanBuffer.EndIndex + 1) * subImageSize);

        //        double afLeftOffset = unit.GetTab(scanBuffer.TabNo).LafTriggerOffset.Left;
        //        double afRightOffset = unit.GetTab(scanBuffer.TabNo).LafTriggerOffset.Right;

        //        IoPositionData data = new IoPositionData
        //        {
        //            Start = tempStart + afLeftOffset,
        //            End = tempEnd + afRightOffset,
        //        };

        //        dataList.Add(data);
        //    }
        //    dataList.Sort((x, y) => x.Start.CompareTo(y.Start));

        //    SetTriggerPosition(lafName, dataList);
        //}
        #endregion
    }
}
