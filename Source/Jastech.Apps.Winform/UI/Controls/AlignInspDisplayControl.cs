﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Jastech.Apps.Structure;
using Jastech.Framework.Winform.VisionPro.Controls;
using static Jastech.Apps.Winform.UI.Controls.ResultChartControl;
using Jastech.Apps.Structure.Data;
using Cognex.VisionPro;
using Emgu.CV;
using System.Runtime.InteropServices;
using Jastech.Framework.Imaging;
using Jastech.Framework.Imaging.VisionPro;

namespace Jastech.Apps.Winform.UI.Controls
{
    public partial class AlignInspDisplayControl : UserControl
    {
        #region 필드
        private int _prevTabCount { get; set; } = -1;

        private Color _selectedColor;

        private Color _noneSelectedColor;
        #endregion

        #region 속성
        public List<TabBtnControl> TabBtnControlList { get; private set; } = new List<TabBtnControl>();

        public CogInspAlignDisplayControl InspAlignDisplay { get; private set; } = new CogInspAlignDisplayControl();

        public AlignInspResultControl AlignInspResultControl { get; private set; } = new AlignInspResultControl();

        public ResultChartControl ResultChartControl { get; private set; } = new ResultChartControl();

        public AppsInspResult InspResult { get; set; } = null;
        #endregion

        #region 이벤트
        #endregion

        #region 델리게이트
        #endregion

        #region 생성자
        public AlignInspDisplayControl()
        {
            InitializeComponent();
        }
        #endregion

        #region 메서드
        private void AlignInspDisplayControl_Load(object sender, EventArgs e)
        {
            _selectedColor = Color.FromArgb(104, 104, 104);
            _noneSelectedColor = Color.FromArgb(52, 52, 52);

            AddControls();

            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            if (inspModel == null)
                UpdateTabCount(1);
            else
                UpdateTabCount(inspModel.TabCount);
        }

        private void AddControls()
        {
            InspAlignDisplay.Dock = DockStyle.Fill;
            pnlInspDisplay.Controls.Add(InspAlignDisplay);

            AlignInspResultControl.Dock = DockStyle.Fill;
            pnlAlignResult.Controls.Add(AlignInspResultControl);

            ResultChartControl.Dock = DockStyle.Fill;
            ResultChartControl.SetInspChartType(InspChartType.Align);
            pnlAlignGraph.Controls.Add(ResultChartControl);
        }

        public void UpdateTabCount(int tabCount)
        {
            if (_prevTabCount == tabCount)
                return;

            ClearTabBtnList();

            int controlWidth = 100;
            Point point = new Point(0, 0);

            for (int i = 0; i < tabCount; i++)
            {
                TabBtnControl buttonControl = new TabBtnControl();
                buttonControl.SetTabIndex(i);
                buttonControl.SetTabEventHandler += ButtonControl_SetTabEventHandler;
                buttonControl.Size = new Size(controlWidth, (int)(pnlTabButton.Height * 0.7));
                buttonControl.Location = point;

                pnlTabButton.Controls.Add(buttonControl);
                point.X += controlWidth;
                TabBtnControlList.Add(buttonControl);
            }

            if (TabBtnControlList.Count > 0)
                TabBtnControlList[0].UpdateData();
        }

        private void ClearTabBtnList()
        {
            foreach (var btn in TabBtnControlList)
            {
                btn.SetTabEventHandler -= ButtonControl_SetTabEventHandler;
            }
            TabBtnControlList.Clear();
            pnlTabButton.Controls.Clear();
        }

        private void ButtonControl_SetTabEventHandler(int tabNum)
        {
            TabBtnControlList.ForEach(x => x.BackColor = _noneSelectedColor);
            TabBtnControlList[tabNum].BackColor = _selectedColor;
        }

        public void UpdateMainResult(AppsInspResult result)
        {
            if(InspResult != null)
            {
                InspResult.Dispose();
                InspResult = null;
            }

            InspResult = result;
            //CogCompositeShape

            UpdateAlignResult(InspResult);
            //InspAlignDisplay.UpdateResult(InspResult);
            //InspAlignDisplay.UpdateResult(InspResult);

        }

        private void UpdateAlignResult(AppsInspResult result)
        {
            List<CogCompositeShape> leftResultList = new List<CogCompositeShape>();
            List<PointF> pointList = new List<PointF>();

            var leftAlignX = result.LeftAlignX;

            if (leftAlignX.Fpc.CogAlignResult.Count > 0)
            {
                foreach (var fpc in leftAlignX.Fpc.CogAlignResult)
                {
                    pointList.Add(fpc.MaxCaliperMatch.FoundPos);

                    var leftFpcX = fpc.MaxCaliperMatch.ResultGraphics;
                    leftResultList.Add(leftFpcX);
                }
            }
            if (leftAlignX.Panel.CogAlignResult.Count() > 0)
            {
                foreach (var panel in leftAlignX.Panel.CogAlignResult)
                {
                    pointList.Add(panel.MaxCaliperMatch.FoundPos);

                    var leftPanelX = panel.MaxCaliperMatch.ResultGraphics;
                    leftResultList.Add(leftPanelX);
                }
            }

            var leftAlignY = result.LeftAlignY;
            if (leftAlignY.Fpc.CogAlignResult[0].MaxCaliperMatch != null)
            {
                pointList.Add(leftAlignY.Fpc.CogAlignResult[0].MaxCaliperMatch.FoundPos);

                var leftFpcY = leftAlignY.Fpc.CogAlignResult[0].MaxCaliperMatch.ResultGraphics;
                leftResultList.Add(leftFpcY);
            }
            if (leftAlignY.Panel.CogAlignResult[0].MaxCaliperMatch != null)
            {
                pointList.Add(leftAlignY.Panel.CogAlignResult[0].MaxCaliperMatch.FoundPos);
                var leftPanelY = leftAlignY.Panel.CogAlignResult[0].MaxCaliperMatch.ResultGraphics;
                leftResultList.Add(leftPanelY);
            }

            InspAlignDisplay.UpdateLeftDisplay(result.CogImage, leftResultList, GetCenterPoint(pointList));
        }

        private Point GetCenterPoint(List<PointF> pointList)
        {
            float minX = pointList.Select(point => point.X).Min();
            float maxX = pointList.Select(point => point.X).Max();

            float minY = pointList.Select(point => point.Y).Min();
            float maxY = pointList.Select(point => point.Y).Max();

            float width = (maxX - minX) / 2.0f;
            float height = (maxY - minY) / 2.0f;

            return new Point((int)(minX + width), (int)(minY + height));
        }

        public ICogImage ConvertCogImage(Mat image)
        {
            if (image == null)
                return null;

            int size = image.Width * image.Height * image.NumberOfChannels;
            byte[] dataArray = new byte[size];
            Marshal.Copy(image.DataPointer, dataArray, 0, size);

            ColorFormat format = image.NumberOfChannels == 1 ? ColorFormat.Gray : ColorFormat.RGB24;

            var cogImage = CogImageHelper.CovertImage(dataArray, image.Width, image.Height, format);

            return cogImage;
        }

        #endregion
    }
}
