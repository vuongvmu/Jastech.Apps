﻿using Jastech.Apps.Winform.Service.Plc.Maps;
using Jastech.Apps.Winform.Settings;
using System;
using System.Collections.Generic;

namespace Jastech.Apps.Winform.Service.Plc
{
    public partial class PlcAddressService
    {
        public byte[] OrgData { get; set; }

        public List<PlcAddressMap> ResultMapList { get; set; } = new List<PlcAddressMap>();

        public List<PlcAddressMap> AddressMapList { get; set; } = new List<PlcAddressMap>();

        public int MinAddressNumber { get; set; } = 0;

        public int MaxAddressNumber { get; set; } = 800;

        public int AddressLength { get; set; } = 800;
    }

    public partial class PlcAddressService
    {
        public void CreateMap()
        {
            CreateAddressMap();
            CreateResultMap();
        }

        public void Initialize()
        {
            int min = int.MaxValue;
            int max = int.MinValue;
            foreach (var addressMap in AddressMapList)
            {
                int addressNum = (int)addressMap.AddressNum;

                if (min > addressNum)
                    min = addressNum;

                if (max < addressNum)
                    max = addressNum + addressMap.WordSize;
            }

            MaxAddressNumber = max;
            MinAddressNumber = min;
            AddressLength = Math.Abs(min - max);
        }

        private void CreateResultMap()
        {
            // Current Model Name
            ResultMapList.Add(new PlcAddressMap(PlcResultMap.Current_ModelName, WordType.HEX, AppsConfig.Instance().PlcAddressInfo.ResultStart, 20));

            int tabTotabInterval = AppsConfig.Instance().PlcAddressInfo.ResultTabToTabInterval; // AddressMap 참고 

            // Version
            int versionStart = AppsConfig.Instance().PlcAddressInfo.ResultStart + 100;
            ResultMapList.Add(new PlcAddressMap(PlcResultMap.Version_Major, WordType.DEC, versionStart, 1));
            ResultMapList.Add(new PlcAddressMap(PlcResultMap.Version_Minor, WordType.DEC, versionStart + 1, 1));
            ResultMapList.Add(new PlcAddressMap(PlcResultMap.Version_Build, WordType.DEC, versionStart + 2, 1));
            ResultMapList.Add(new PlcAddressMap(PlcResultMap.Version_Revision, WordType.DEC, versionStart + 3, 1));

            CreateAlignParameter(tabTotabInterval);
            // Align Results, Tab Result
            CreateAlignResult(AppsConfig.Instance().PlcAddressInfo.ResultStart_Align, tabTotabInterval);

            // Akkon Results
            CreateAkkonResult(AppsConfig.Instance().PlcAddressInfo.ResultStart_Akkon, tabTotabInterval);

            // Mark Results
            CreateMarkResult(AppsConfig.Instance().PlcAddressInfo.ResultStart_Akkon, tabTotabInterval);
        }

        private void CreateAlignParameter(int tabTotabInterval)
        {
            int maxCount = AppsConfig.Instance().TabMaxCount;

            for (int i = 0; i < maxCount; i++)
            {
                string left_FpcX = string.Format("Tab0_Left_FPC_X_Threshold", i);
                string left_FpcY = string.Format("Tab0_Left_FPC_Y_Threshold", i);
                string left_PanelX = string.Format("Tab0_Left_PANEL_X_Threshold", i);
                string left_PanelY = string.Format("Tab0_Left_PANEL_Y_Threshold", i);

                string right_FpcX = string.Format("Tab0_Right_FPC_X_Threshold", i);
                string right_FpcY = string.Format("Tab0_Right_FPC_Y_Threshold", i);
                string right_PanelX = string.Format("Tab0_Right_PANEL_X_Threshold", i);
                string right_PanelY = string.Format("Tab0_Right_PANEL_Y_Threshold", i);

                int addressNum = AppsConfig.Instance().PlcAddressInfo.ResultStart + 200;
                int addressIndex = addressNum + (tabTotabInterval * i);

                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), left_FpcX), WordType.DEC, addressIndex, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), left_FpcY), WordType.DEC, addressIndex, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), left_PanelX), WordType.DEC, addressIndex, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), left_PanelY), WordType.DEC, addressIndex, 1));

                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), right_FpcX), WordType.DEC, addressIndex, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), right_FpcY), WordType.DEC, addressIndex, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), right_PanelX), WordType.DEC, addressIndex, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), right_PanelY), WordType.DEC, addressIndex, 1));
            }
        }

        private void CreateAlignResult(int alignStartIndex, int tabTotabInterval)
        {
            int maxCount = AppsConfig.Instance().TabMaxCount;
            int addressNum = alignStartIndex;

            for (int i = 0; i < maxCount; i++)
            {
                string tabJudgement = string.Format("Tab{0}_Judgement", i);
                string alignJudement = string.Format("Tab{0}_Align_Judgement", i);
                string alignLeftX = string.Format("Tab{0}_Align_Left_X", i);
                string alignLeftY = string.Format("Tab{0}_Align_Left_Y", i);
                string alignRightX = string.Format("Tab{0}_Align_Right_X", i);
                string alignRightY = string.Format("Tab{0}_Align_Right_Y", i);
                string alignCx = string.Format("Tab{0}_Align_Cx", i);

                int addressIndex = addressNum + (tabTotabInterval * i);
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), tabJudgement), WordType.DEC, addressIndex, 1));

                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), alignJudement), WordType.DEC, addressIndex, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), alignLeftX), WordType.DoubleWord, addressIndex + 1, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), alignLeftY), WordType.DoubleWord, addressIndex + 2, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), alignRightX), WordType.DoubleWord, addressIndex + 3, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), alignRightY), WordType.DoubleWord, addressIndex + 4, 2));
                
                int tempAddresIndex = addressNum + (tabTotabInterval * i) + 100;
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), alignCx), WordType.DoubleWord, tempAddresIndex, 2));
            }
        }

        private void CreateAkkonResult(int akkonStartIndex, int tabTotabInterval)
        {
            int maxCount = AppsConfig.Instance().TabMaxCount;
            int addressNum = akkonStartIndex;

            for (int i = 0; i < maxCount; i++)
            {
                string akkonJudgement = string.Format("Tab{0}_Akkon_Judgement", i);
                string akkonCountLeftAvg = string.Format("Tab{0}_Akkon_Count_Left_Avg", i);
                string akkonCountLeftMin = string.Format("Tab{0}_Akkon_Count_Left_Min", i);
                string akkonCountLeftMax = string.Format("Tab{0}_Akkon_Count_Left_Max", i);
                string akkonCountRightAvg = string.Format("Tab{0}_Akkon_Count_Right_Avg", i);
                string akkonCountRightMin = string.Format("Tab{0}_Akkon_Count_Right_Min", i);
                string akkonCountRightMax = string.Format("Tab{0}_Akkon_Count_Right_Max", i);

                int addressIndex = addressNum + (tabTotabInterval * i);
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonJudgement), WordType.DEC, addressIndex, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonCountLeftAvg), WordType.DEC, addressIndex + 2, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonCountLeftMin), WordType.DEC, addressIndex + 3, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonCountLeftMax), WordType.DEC, addressIndex + 4, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonCountRightAvg), WordType.DEC, addressIndex + 5, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonCountRightMin), WordType.DEC, addressIndex + 6, 1));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonCountRightMax), WordType.DEC, addressIndex + 7, 1));


                string akkonLengthLeftAvg = string.Format("Tab{0}_Akkon_Length_Left_Avg", i);
                string akkonLengthLeftMin = string.Format("Tab{0}_Akkon_Length_Left_Min", i);
                string akkonLengthLeftMax = string.Format("Tab{0}_Akkon_Length_Left_Max", i);
                string akkonLengthRightAvg = string.Format("Tab{0}_Akkon_Length_Right_Avg", i);
                string akkonLengthRightMin = string.Format("Tab{0}_Akkon_Length_Right_Min", i);
                string akkonLengthRightMax = string.Format("Tab{0}_Akkon_Length_Right_Max", i);

                addressIndex = addressNum + 10 + (tabTotabInterval * i);
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonLengthLeftAvg), WordType.DoubleWord, addressIndex + 8, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonLengthLeftMin), WordType.DoubleWord, addressIndex + 10, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonLengthLeftMax), WordType.DoubleWord, addressIndex + 12, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonLengthRightAvg), WordType.DoubleWord, addressIndex + 14, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonLengthRightMin), WordType.DoubleWord, addressIndex + 16, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), akkonLengthRightMax), WordType.DoubleWord, addressIndex + 18, 2));
            }
        }

        private void CreateMarkResult(int akkonStartIndex, int tabTotabInterval)
        {
            int maxCount = AppsConfig.Instance().TabMaxCount;
            int addressNum = akkonStartIndex;

            for (int i = 0; i < maxCount; i++)
            {
                string panelLeftMarkSocre = string.Format("Tab{0}_Panel_Mark_Left_Score", i);
                string panelRightMarkSocre = string.Format("Tab{0}_Panel_Mark_Right_Score", i);
                string fpcLeftMarkSocre = string.Format("Tab{0}_Fpc_Mark_Left_Score", i);
                string fpcRightMarkSocre = string.Format("Tab{0}_Fpc_Mark_Right_Score", i);

                int addressIndex = addressNum + (tabTotabInterval * i);
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), panelLeftMarkSocre), WordType.DoubleWord, addressIndex + 20, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), panelRightMarkSocre), WordType.DoubleWord, addressIndex + 22, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), fpcLeftMarkSocre), WordType.DoubleWord, addressIndex + 24, 2));
                ResultMapList.Add(new PlcAddressMap((PlcResultMap)Enum.Parse(typeof(PlcResultMap), fpcRightMarkSocre), WordType.DoubleWord, addressIndex + 26, 2));
            }
        }

        private void CreateAddressMap()
        {
            int index = AppsConfig.Instance().PlcAddressInfo.CommonStart;

            // 0~9
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Alive, WordType.DEC, index, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_AxisX_Busy, WordType.DEC, index + 1, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_AxisX_CurPos, WordType.DEC, index + 2, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_AxisX_ServoOn, WordType.DEC, index + 3, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Ready, WordType.DEC, index + 4, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Status_Common, WordType.DEC, index + 6, 1));

            // 10~19
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_X_NegativeLimit, WordType.DEC, index + 10, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_X_PositiveLimit, WordType.DEC, index + 11, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Z0_NegativeLimit, WordType.DEC, index + 14, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Z0_PositiveLimit, WordType.DEC, index + 15, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Z1_NegativeLimit, WordType.DEC, index + 16, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Z1_PositiveLimit, WordType.DEC, index + 17, 1));

            // 100~109
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Alive, WordType.DEC, index + 100, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_RunMode, WordType.DEC, index + 101, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_DoorStatus, WordType.DEC, index + 102, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Ready, WordType.DEC, index + 104, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Command_Common, WordType.DEC, index + 106, 1));

            // 200~209
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PPID_ModelName, WordType.HEX, index + 200, 20));

            // 300~309
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Time_Year, WordType.DEC, index + 300, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Time_Month, WordType.DEC, index + 301, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Time_Day, WordType.DEC, index + 302, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Time_Hour, WordType.DEC, index + 303, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Time_Minute, WordType.DEC, index + 304, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Time_Second, WordType.DEC, index + 305, 1));

            // 10~19
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_ErrorCode, WordType.DEC, index + 19, 1));

            #region Model 정보
            // 400~419
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab0_Offset_Left, WordType.DoubleWord, index + 400, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab1_Offset_Left, WordType.DoubleWord, index + 402, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab2_Offset_Left, WordType.DoubleWord, index + 404, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab3_Offset_Left, WordType.DoubleWord, index + 406, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab4_Offset_Left, WordType.DoubleWord, index + 408, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab5_Offset_Left, WordType.DoubleWord, index + 410, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab6_Offset_Left, WordType.DoubleWord, index + 412, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab7_Offset_Left, WordType.DoubleWord, index + 414, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab8_Offset_Left, WordType.DoubleWord, index + 416, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab9_Offset_Left, WordType.DoubleWord, index + 418, 2));

            // 500~519
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab0_Offset_Right, WordType.DoubleWord, index + 500, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab1_Offset_Right, WordType.DoubleWord, index + 502, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab2_Offset_Right, WordType.DoubleWord, index + 504, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab3_Offset_Right, WordType.DoubleWord, index + 506, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab4_Offset_Right, WordType.DoubleWord, index + 508, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab5_Offset_Right, WordType.DoubleWord, index + 510, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab6_Offset_Right, WordType.DoubleWord, index + 512, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab7_Offset_Right, WordType.DoubleWord, index + 514, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab8_Offset_Right, WordType.DoubleWord, index + 516, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab9_Offset_Right, WordType.DoubleWord, index + 518, 2));

            // 610~619
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab0_Width, WordType.DoubleWord, index + 600, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab1_Width, WordType.DoubleWord, index + 602, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab2_Width, WordType.DoubleWord, index + 604, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab3_Width, WordType.DoubleWord, index + 606, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab4_Width, WordType.DoubleWord, index + 608, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab5_Width, WordType.DoubleWord, index + 610, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab6_Width, WordType.DoubleWord, index + 612, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab7_Width, WordType.DoubleWord, index + 614, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab8_Width, WordType.DoubleWord, index + 616, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Tab9_Width, WordType.DoubleWord, index + 618, 2));

            // 710~719
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance0, WordType.DoubleWord, index + 700, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance1, WordType.DoubleWord, index + 702, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance2, WordType.DoubleWord, index + 704, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance3, WordType.DoubleWord, index + 706, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance4, WordType.DoubleWord, index + 708, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance5, WordType.DoubleWord, index + 710, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance6, WordType.DoubleWord, index + 712, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance7, WordType.DoubleWord, index + 714, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabtoTab_Distance8, WordType.DoubleWord, index + 716, 2));

            // 800~809
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PanelX_Size, WordType.DoubleWord, index + 800, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_MarkToMarkDistance, WordType.DoubleWord, index + 802, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PanelLeftEdgeToTab1LeftEdgeDistance, WordType.DoubleWord, index + 804, 2));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_TabCount, WordType.DEC, index + 806, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Axis_X_Speed, WordType.DoubleWord, index + 807, 2));

            // 810~814
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Akkon_Count, WordType.DEC, index + 810, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Akkon_Length, WordType.DEC, index + 811, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Akkon_Strength, WordType.DEC, index + 812, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Akkon_Min_Size, WordType.DEC, index + 813, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Akkon_Max_Size, WordType.DEC, index + 814, 1));
            #endregion

            // 20~29
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_GrabDone, WordType.DEC, index + 25, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Command, WordType.DEC, index + 26, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Status, WordType.DEC, index + 27, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_Move_REQ, WordType.DEC, index + 28, 1));

            // 120~129
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_AlignZ_ServoOnOff, WordType.DEC, index + 120, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_AlignZ_Alarm, WordType.DEC, index + 121, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Status, WordType.DEC, index + 126, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Command, WordType.DEC, index + 127, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Move_END, WordType.DEC, index + 128, 1));

            // 220~229
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_FinalBond, WordType.DEC, index + 220, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_ManualMatch, WordType.DEC, index + 229, 1));

            // 320~339
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Cell_Id, WordType.HEX, index + 320, 20));

            // 420~429
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab0, WordType.DEC, index + 420, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab1, WordType.DEC, index + 421, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab2, WordType.DEC, index + 422, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab3, WordType.DEC, index + 423, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab4, WordType.DEC, index + 424, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab5, WordType.DEC, index + 425, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab6, WordType.DEC, index + 426, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab7, WordType.DEC, index + 427, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab8, WordType.DEC, index + 428, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_PreBond_Tab9, WordType.DEC, index + 429, 1));

            // 130~139
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_AkkonZ_ServoOnOff, WordType.DEC, index + 130, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_AkkonZ_Alarm, WordType.DEC, index + 131, 1));

            // 50~59
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_AlignDataX, WordType.DoubleWord, index + 50, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_AlignDataY, WordType.DoubleWord, index + 52, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PC_AlignDataT, WordType.DoubleWord, index + 54, 1));

            // 150~159
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Position_AxisY, WordType.DoubleWord, index + 152, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_Position_AxisT, WordType.DoubleWord, index + 154, 1));

            // 250~259
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_AlignDataX, WordType.DoubleWord, index + 250, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_OffsetDataX, WordType.DEC, index + 256, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_OffsetDataY, WordType.DEC, index + 257, 1));
            AddressMapList.Add(new PlcAddressMap(PlcCommonMap.PLC_OffsetDataT, WordType.DEC, index + 258, 1));
        }
    }
}
