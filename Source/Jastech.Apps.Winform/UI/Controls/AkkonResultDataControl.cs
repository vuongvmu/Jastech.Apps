﻿using Jastech.Apps.Winform.Service;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Jastech.Apps.Winform.UI.Controls
{
    public partial class AkkonResultDataControl : UserControl
    {
        #region 델리게이트
        private delegate void UpdateAkkonResultDelegate();
        #endregion

        #region 생성자
        public AkkonResultDataControl()
        {
            InitializeComponent();
        }
        #endregion

        #region 메서드
        public void UpdateAkkonDaily()
        {
            if (this.InvokeRequired)
            {
                UpdateAkkonResultDelegate callback = UpdateAkkonDaily;
                BeginInvoke(callback);
                return;
            }

            UpdateDataGridView();
        }

        private void UpdateDataGridView()
        {
            dgvAkkonHistory.Rows.Clear();

            var dailyInfo = DailyInfoService.GetDailyInfo();

            List<DailyData> reverseList = new List<DailyData>();
            reverseList = Enumerable.Reverse(dailyInfo.DailyDataList).ToList();

            foreach (var dailyDataList in reverseList)
            {
                foreach (var item in dailyDataList.AkkonDailyInfoList)
                {
                    string inspectionTime = item.InspectionTime.ToString();
                    string panelID = item.PanelID.ToString();
                    string tabNumber = item.TabNo.ToString();
                    string judge = item.Judgement.ToString();
                    string count = item.MinBlobCount.ToString();
                    string length = item.MinLength.ToString("F2");

                    string[] row = { inspectionTime, panelID, tabNumber, judge, count, length };
                    dgvAkkonHistory.Rows.Add(row);
                }
            }
        }
        #endregion
    }
}
