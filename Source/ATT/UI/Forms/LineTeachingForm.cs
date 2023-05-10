﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Jastech.Framework.Winform.VisionPro.Controls;
using Jastech.Apps.Winform.UI.Controls;
using Jastech.Apps.Structure;
using Jastech.Framework.Winform.Forms;
using Jastech.Apps.Winform;
using Jastech.Apps.Winform.UI.Forms;
using ATT.UI.Forms;
using Cognex.VisionPro;
using Jastech.Framework.Imaging.VisionPro;
using Jastech.Apps.Winform.Settings;
using Jastech.Framework.Structure;
using Jastech.Framework.Winform.Controls;
using Jastech.Apps.Winform.Core;
using Jastech.Framework.Winform;
using Jastech.Framework.Device.Cameras;
using Jastech.Apps.Structure.Data;
using System.Runtime.InteropServices;
using Jastech.Framework.Imaging;
using ATT.UI.Controls;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Cognex.VisionPro.Implementation.Internal;
using System.Threading;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace ATT.UI.Forms
{
    public partial class LineTeachingForm : Form
    {
        #region 필드
        private Color _selectedColor;

        private Color _noneSelectedColor;

        private bool _isLoading { get; set; } = false;

        private Control _selectControl { get; set; } = null;

        private DisplayType _displayType { get; set; } = DisplayType.Align;

        private string _currentTabNo { get; set; } = "";
        #endregion

        #region 속성
        public UnitName UnitName { get; set; } = UnitName.Unit0;

        public string TitleCameraName { get; set; } = "";

        public List<Tab> TeachingTabList { get; private set; } = null;

        public Tab CurrentTab { get; set; } = null;

        public CameraName CameraName { get; set; }

        public string TeachingImagePath { get; set; }

        private CogTeachingDisplayControl Display { get; set; } = new CogTeachingDisplayControl();

        private AlignControl AlignControl { get; set; } = new AlignControl() { Dock = DockStyle.Fill };

        private AkkonControl AkkonControl { get; set; } = new AkkonControl() { Dock = DockStyle.Fill };

        private MarkControl MarkControl { get; set; } = new MarkControl() { Dock = DockStyle.Fill };

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }
        #endregion

        #region 이벤트
        #endregion

        #region 델리게이트
        #endregion

        #region 생성자
        public LineTeachingForm()
        {
            InitializeComponent();
        }
        #endregion

        #region 메서드
        private void LineTeachingForm_Load(object sender, EventArgs e)
        {
            _isLoading = true;

            SystemManager.Instance().UpdateTeachingData();
            TeachingTabList = SystemManager.Instance().GetTeachingData().GetUnit(UnitName.ToString()).GetTabList();

            AddControl();
            InitializeTabComboBox();

            _isLoading = false;


            lblStageCam.Text = $"STAGE : {UnitName} / CAM : {TitleCameraName}";

            var appsLineCamera = AppsLineCameraManager.Instance().GetLineCamera(CameraName);
            appsLineCamera.TabImageGrabCompletedEventHandler += LineTeachingForm_TabImageGrabCompletedEventHandler;
            appsLineCamera.GrabDoneEventHanlder += LineTeachingForm_GrabDoneEventHanlder;

            var image = AppsTeachingUIManager.Instance().GetOriginCogImageBuffer(true);

            if (image != null)
                Display.SetImage(image);

            SelectPage(DisplayType.Align);
            AppsLineCameraManager.Instance().GetLineCamera(CameraName).StartMergeTask();
        }

        private void LineTeachingForm_GrabDoneEventHanlder(string cameraName, bool isGrabDone)
        {
        }

        public delegate void UpdateDisplayDele(Mat image);
        private void UpdateDisplay(Mat image)
        {
            if(this.InvokeRequired)
            {
                UpdateDisplayDele callback = UpdateDisplay;
                BeginInvoke(callback, image);
                return;
            }
            if (image == null)
                return;

            int size = image.Width * image.Height * image.NumberOfChannels;
            byte[] dataArray = new byte[size];
            Marshal.Copy(image.DataPointer, dataArray, 0, size);

            ColorFormat format = image.NumberOfChannels == 1 ? ColorFormat.Gray : ColorFormat.RGB24;

            var cogImage = CogImageHelper.CovertImage(dataArray, image.Width, image.Height, format);

            Display.SetImage(cogImage);
            Console.WriteLine("LineTeachingForm 이미지 업데이트.");
        }

        private void InitializeTabComboBox()
        {
            cbxTabList.Items.Clear();

            foreach (var item in TeachingTabList)
                cbxTabList.Items.Add(item.Name);

            cbxTabList.SelectedIndex = 0;
            CurrentTab = TeachingTabList[0];
            _currentTabNo = cbxTabList.SelectedItem as string;
        }

        private void LineTeachingForm_TabImageGrabCompletedEventHandler(string cameraName, TabScanImage tabScanImage)
        {
            if (tabScanImage == null)
                return;
            if (tabScanImage.GetMergeImage() == null)
                return;
            
            SystemManager.Instance().GetTeachingData().ScanImageList.Add(tabScanImage);

            //Thread saveThread = new Thread(new ParameterizedThreadStart(SaveScanImage));
            
            //saveThread.Start(tabScanImage);

            int tabNo = Convert.ToInt32(_currentTabNo);

            if(tabNo == tabScanImage.TabNo)
            {
                var scanImage = SystemManager.Instance().GetTeachingData().GetScanImage(tabNo);
                if (scanImage == null)
                    return;

                var image = scanImage.GetMergeImage();

                UpdateDisplay(image);
            }
        }

        private void SaveScanImage(object tabScanImage)
        {
   
            TeachingImagePath = @"D:\ATT\Jastech.Apps\Runtime\Model\test1\TeachingImage\Test";
            if (Directory.Exists(TeachingImagePath) == false)
                Directory.CreateDirectory(TeachingImagePath);

            for (int i = 0; i < 5; i++)
            {
                string tabPath = string.Format(@"{0}\{1}.bmp", TeachingImagePath, i.ToString());
                var scanImage = SystemManager.Instance().GetTeachingData().GetScanImage(i);
                scanImage.GetMergeImage().Save(tabPath);
            }
        }
        private void AddControl()
        {
            _selectedColor = Color.FromArgb(104, 104, 104);
            _noneSelectedColor = Color.FromArgb(52, 52, 52);

            // Display Control
            Display = new CogTeachingDisplayControl();
            Display.Dock = DockStyle.Fill;

            //Event 연결
            Display.DeleteEventHandler += Display_DeleteEventHandler;
            pnlDisplay.Controls.Add(Display);

            // TeachingUIManager 참조
            AppsTeachingUIManager.Instance().SetDisplay(Display.GetDisplay());
        }

        private void Display_DeleteEventHandler(object sender, EventArgs e)
        {
            if(pnlTeach.Controls.Count > 0)
            {
                if (pnlTeach.Controls[0] as MarkControl != null)
                    MarkControl.DrawROI();
                else if (pnlTeach.Controls[0] as AlignControl != null)
                    AlignControl.DrawROI();
                else if (pnlTeach.Controls[0] as AkkonControl != null)
                    AkkonControl.DrawROI();
            }
        }

        private void btnMark_Click(object sender, EventArgs e)
        {
            SelectPage(DisplayType.Mark);
        }

        private void btnAlign_Click(object sender, EventArgs e)
        {
            SelectPage(DisplayType.Align);
        }

        private void btnAkkon_Click(object sender, EventArgs e)
        {
            SelectPage(DisplayType.Akkon);
        }

        private void SelectPage(DisplayType type)
        {
            if (ModelManager.Instance().CurrentModel == null || UnitName.ToString() == "")
                return;

            ClearSelectedButton();

            _displayType = type;

            pnlTeach.Controls.Clear();

            switch (type)
            {
                case DisplayType.Mark:
                    btnMark.BackColor = _selectedColor;
                    MarkControl.SetParams(CurrentTab);
                    pnlTeach.Controls.Add(MarkControl);
                    break;

                case DisplayType.Align:
                    btnAlign.BackColor = _selectedColor;
                    AlignControl.SetParams(CurrentTab);
                    pnlTeach.Controls.Add(AlignControl);
                    break;

                case DisplayType.Akkon:
                    btnAkkon.BackColor = _selectedColor;
                    AkkonControl.SetParams(CurrentTab);
                    pnlTeach.Controls.Add(AkkonControl);
                    break;

                default:
                    break;
            }
        }

        private void ClearSelectedButton()
        {
            foreach (Control control in tlpTeachingItem.Controls)
            {
                if (control is Button)
                    control.BackColor = _noneSelectedColor;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            AppsInspModel model = ModelManager.Instance().CurrentModel as AppsInspModel;

            if (model == null)
                return;

            SaveModelData(model);

            MessageConfirmForm form = new MessageConfirmForm();
            form.Message = "Save Model Completed.";
            form.ShowDialog();
        }

        private void SaveModelData(AppsInspModel model)
        {
            AkkonControl.SaveAkkonParam();
            model.SetUnitList(SystemManager.Instance().GetTeachingData().UnitList);

            string fileName = System.IO.Path.Combine(AppsConfig.Instance().Path.Model, model.Name, InspModel.FileName);
            SystemManager.Instance().SaveModel(fileName, model);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnLoadImage_Click(object sender, EventArgs e)
        {
            //SaveScanImage(null);
            //return;
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.ReadOnlyChecked = true;
            dlg.Filter = "BMP Files (*.bmp)|*.bmp";
            dlg.ShowDialog();

            if (dlg.FileName != "")
            {
                Mat image = new Mat(dlg.FileName, ImreadModes.Grayscale);

                int size = image.Width * image.Height * image.NumberOfChannels;
                byte[] dataArray = new byte[size];
                Marshal.Copy(image.DataPointer, dataArray, 0, size);

                ColorFormat format = image.NumberOfChannels == 1 ? ColorFormat.Gray : ColorFormat.RGB24;

                var cogImage = CogImageHelper.CovertImage(dataArray, image.Width, image.Height, format);

                //ICogImage cogImage = CogImageHelper.Load(dlg.FileName);
                //CogImageHelper.Save(cogImage, @"D:\Cog1.bmp");

                Display.SetImage(cogImage);
                AppsTeachingUIManager.Instance().SetOrginCogImageBuffer(cogImage);
                AppsTeachingUIManager.Instance().SetOriginMatImageBuffer(new Mat(dlg.FileName, ImreadModes.Grayscale));
            }
        }

        private void btnMotionPopup_Click(object sender, EventArgs e)
        {
            MotionPopupForm motionPopupForm = new MotionPopupForm();
            motionPopupForm.UnitName = UnitName;
            motionPopupForm.ShowDialog();
        }

        private void btnGrabStart_Click(object sender, EventArgs e)
        {
            SystemManager.Instance().GetTeachingData().ClearScanImage();
            var appsLineCamera = AppsLineCameraManager.Instance().GetLineCamera(CameraName);

            AppsInspModel inspModel = ModelManager.Instance().CurrentModel as AppsInspModel;
            TeachingImagePath = Path.Combine(AppsConfig.Instance().Path.Model, inspModel.Name, "TeachingImage", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            
            appsLineCamera.StartGrab();
        }

        private void btnGrabStop_Click(object sender, EventArgs e)
        {
            var appsLineCamera = AppsLineCameraManager.Instance().GetLineCamera(CameraName);
            appsLineCamera.StopGrab();
        }

        private void LineTeachingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            var appsLineCamera = AppsLineCameraManager.Instance().GetLineCamera(CameraName.LinscanMIL0);
            appsLineCamera.TabImageGrabCompletedEventHandler -= LineTeachingForm_TabImageGrabCompletedEventHandler;
            appsLineCamera.GrabDoneEventHanlder -= LineTeachingForm_GrabDoneEventHanlder;
            AppsLineCameraManager.Instance().GetLineCamera(CameraName).StopMergeTask();
        }

        private void cbxTabList_DrawItem(object sender, DrawItemEventArgs e)
        {
            DrawComboboxCenterAlign(sender, e);
        }

        private void DrawComboboxCenterAlign(object sender, DrawItemEventArgs e)
        {
            ComboBox cmb = sender as ComboBox;

            if (cmb != null)
            {
                e.DrawBackground();

                if (cmb.Name.ToString().ToLower().Contains("group"))
                    cmb.ItemHeight = lblPrev.Height - 6;
                else
                    cmb.ItemHeight = lblPrev.Height - 6;

                if (e.Index >= 0)
                {
                    StringFormat sf = new StringFormat();
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Alignment = StringAlignment.Center;

                    Brush brush = new SolidBrush(cmb.ForeColor);

                    if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                        brush = SystemBrushes.HighlightText;

                    e.Graphics.DrawString(cmb.Items[e.Index].ToString(), cmb.Font, brush, e.Bounds, sf);
                }
            }
        }

        private void cbxTabList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            string tabIndex = cbxTabList.SelectedItem as string;
            int tabNo = Convert.ToInt32(tabIndex);

            if (_currentTabNo == tabIndex)
                return;
            CurrentTab = TeachingTabList.Where(x => x.Index == tabNo).First();

            var scanImage = SystemManager.Instance().GetTeachingData().GetScanImage(tabNo);
            if (scanImage != null)
                UpdateDisplay(scanImage.GetMergeImage());
            
            if (_displayType == DisplayType.Mark)
            {
                MarkControl.SetParams(CurrentTab);
                MarkControl.DrawROI();
            }
            else if (_displayType == DisplayType.Align)
            {
                AlignControl.SetParams(CurrentTab);
                AlignControl.DrawROI();
            }
            else if (_displayType == DisplayType.Akkon)
            {
                AkkonControl.SetParams(CurrentTab);
                AkkonControl.DrawROI();
            }

            _currentTabNo = tabIndex;
        }

        private void lblPrev_Click(object sender, EventArgs e)
        {
            if (cbxTabList.SelectedIndex <= 0)
                return;

            cbxTabList.SelectedIndex -= 1;
        }

        private void lblNext_Click(object sender, EventArgs e)
        {
            int nextIndex = cbxTabList.SelectedIndex + 1;

            if (cbxTabList.Items.Count > nextIndex)
                cbxTabList.SelectedIndex = nextIndex;
        }

        private void lblInspection_Click(object sender, EventArgs e)
        {
            if (_displayType == DisplayType.Mark)
                MarkControl.Inspection();
            else if (_displayType == DisplayType.Align)
                AlignControl.Inspection();
            else if (_displayType == DisplayType.Akkon)
                AkkonControl.Inspection();
        }

        private void lblAddROI_Click(object sender, EventArgs e)
        {
            if (_displayType == DisplayType.Mark)
                MarkControl.AddROI();
            else if (_displayType == DisplayType.Align)
                AlignControl.AddROI();
            else if (_displayType == DisplayType.Akkon)
                AkkonControl.AddROI();
        }

        private void lblROIJog_Click(object sender, EventArgs e)
        {
            if (_displayType == DisplayType.Mark)
                MarkControl.ShowROIJog();
            else if (_displayType == DisplayType.Align)
                AlignControl.ShowROIJog();
            else if (_displayType == DisplayType.Akkon)
                AkkonControl.ShowROIJog();
        }
        #endregion
    }

    public enum DisplayType
    {
        Mark,
        Align,
        Akkon,
    }
}
