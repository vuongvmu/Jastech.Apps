﻿using System;
using System.Drawing;
using System.Windows.Forms;
using Jastech.Framework.Device.Motions;
using System.Windows.Forms.DataVisualization.Charting;
using AxisName = Jastech.Framework.Device.Motions.AxisName;
using Jastech.Framework.Device.LAFCtrl;
using Jastech.Framework.Winform.Forms;
using static Jastech.Framework.Device.Motions.AxisMovingParam;
using Jastech.Framework.Winform.Controls;
using Jastech.Apps.Structure.Data;
using Jastech.Framework.Structure;

namespace Jastech.Apps.Winform.UI.Controls
{
    public partial class LinescanControl : UserControl
    {
        private Color _selectedColor = new Color();
        private Color _nonSelectedColor = new Color();

        private AutoFocusControl AutoFocusControl { get; set; } = new AutoFocusControl() { Dock = DockStyle.Fill };
        private MotionRepeatControl MotionRepeatControl { get; set; } = new MotionRepeatControl() { Dock = DockStyle.Fill };
        private MotionJogControl MotionJogControl { get; set; } = new MotionJogControl() { Dock = DockStyle.Fill };
        private LAFJogControl LAFJogControl { get; set; } = new LAFJogControl() { Dock = DockStyle.Fill };

        private GrabMode _grabMode = GrabMode.AreaMode;
        public enum GrabMode
        {
            AreaMode,
            LineMode,
        }

        public LinescanControl()
        {
            InitializeComponent();
        }

        private void LinescanControl_Load(object sender, EventArgs e)
        {
            UpdateData();
            AddControl();
            InitializeUI();
        }

        private void InitializeUI()
        {
            _selectedColor = Color.FromArgb(104, 104, 104);
            _nonSelectedColor = Color.FromArgb(52, 52, 52);
        }

        private void AddControl()
        {
            pnlAutoFocus.Controls.Add(AutoFocusControl);

            AxisHandler axisHandler = AppsMotionManager.Instance().GetAxisHandler(AxisHandlerName.Unit0);
            MotionRepeatControl.SetAxisHanlder(axisHandler);
            pnlMotionRepeat.Controls.Add(MotionRepeatControl);

            pnlMotionJog.Controls.Add(MotionJogControl);
            pnlLAFJog.Controls.Add(LAFJogControl);
        }

        private void rdoGrabType_CheckedChanged(object sender, EventArgs e)
        {
            SetSelecteGrabType(sender);
        }

        private void SetSelecteGrabType(object sender)
        {
            RadioButton btn = sender as RadioButton;

            if (btn.Checked)
            {
                if (btn.Text.ToLower().Contains("area"))
                    ShowUpdateUI(GrabMode.AreaMode);
                else
                    ShowUpdateUI(GrabMode.LineMode);

                btn.BackColor = _selectedColor;
            }
            else
                btn.BackColor = _nonSelectedColor;

            UpdateCameraSetting();
        }

        private void ShowUpdateUI(GrabMode grabMode)
        {
            switch (grabMode)
            {
                case GrabMode.AreaMode:
                    ShowAreaMode();
                    break;

                case GrabMode.LineMode:
                    ShowLineMode();
                    break;

                default:
                    break;
            }
        }

        private void ShowAreaMode()
        {
            _grabMode = GrabMode.AreaMode;
            lblCameraExposure.Text = "EXPOSURE [us]";
        }

        private void ShowLineMode()
        {
            _grabMode = GrabMode.LineMode;
            lblCameraExposure.Text = "D GAIN (0 ~ 8[dB])";
        }

        private void UpdateCameraSetting()
        {
            if (GrabMode.AreaMode == _grabMode)
                lblCameraExposureValue.Text = "";
            else
                lblCameraExposureValue.Text = "";

            lblCameraGainValue.Text = "";
            nudLightDimmingLevel.Text = "";
        }

        private delegate void UpdateUIDelegate(object obj);
        public void UpdateUI(object obj)
        {
            if (this.InvokeRequired)
            {
                UpdateUIDelegate callback = UpdateUI;
                BeginInvoke(callback, obj);
                return;
            }

            UpdateMotionStatus();
            MotionRepeatControl.UpdateRepeatCount();
        }

        public void SetParams()
        {

        }
        private void UpdateData()
        {
            var axisHandler = AppsMotionManager.Instance().GetAxisHandler(AxisHandlerName.Unit0);
            SetAxisHandler(axisHandler);

            //var posData = SystemManager.Instance().GetTeachingData().GetUnit(UnitName).TeachingPositions[(int)TeachingPositionType];
            //var posData = GetUnit(UnitName).TeachingPositions[(int)TeachingPositionType];
            //SetTeachingPosition(posData);

            var lafCtrl = AppsLAFManager.Instance().GetLAFCtrl(LAFName.Akkon);
            SetLAFCtrl(lafCtrl);
        }

        private AxisHandler AxisHandler { get; set; } = null;
        private void SetAxisHandler(AxisHandler axisHandler)
        {
            AxisHandler = axisHandler;
        }

        private TeachingPosition TeachingPositionInfo { get; set; } = null;
        private void SetTeachingPosition(TeachingPosition teacingPosition)
        {
            TeachingPositionInfo = teacingPosition.DeepCopy();
        }

        private LAFCtrl LAFCtrl { get; set; } = null;
        private void SetLAFCtrl(LAFCtrl lafCtrl)
        {
            LAFCtrl = lafCtrl;
        }

        private void UpdateMotionStatus()
        {
            UpdateStatusMotionX();
            UpdateStatusMotionY();
            UpdateStatusMotionZ();
        }

        private void UpdateStatusMotionX()
        {
            var axis = AxisHandler.AxisList[(int)AxisName.X];

            if (axis == null || !axis.IsConnected())
                return;

            lblCurrentPositionX.Text = axis.GetActualPosition().ToString();

            if (axis.IsNegativeLimit())
                lblNegativeLimitX.BackColor = Color.Red;
            else
                lblNegativeLimitX.BackColor = _nonSelectedColor;

            if (axis.IsPositiveLimit())
                lblPositiveLimitX.BackColor = _nonSelectedColor;
            else
                lblPositiveLimitX.BackColor = _nonSelectedColor;
        }

        private void UpdateStatusMotionY()
        {
            var axis = AxisHandler.AxisList[(int)AxisName.Y];

            if (axis == null || !axis.IsConnected())
                return;

            lblCurrentPositionY.Text = axis.GetActualPosition().ToString();

            if (axis.IsNegativeLimit())
                lblNegativeLimitY.BackColor = Color.Red;
            else
                lblNegativeLimitY.BackColor = _nonSelectedColor;

            if (axis.IsPositiveLimit())
                lblPositiveLimitY.BackColor = Color.Red;
            else
                lblPositiveLimitY.BackColor = _nonSelectedColor;
        }

        private void UpdateStatusMotionZ()
        {
            var status = LAFCtrl.Status;

            if (status == null)
                return;

            lblCurrentPositionZ.Text = status.MPos.ToString("F3");

            if (status.IsNegativeLimit)
                lblNegativeLimitZ.BackColor = Color.Red;
            else
                lblNegativeLimitZ.BackColor = _nonSelectedColor;

            if (status.IsNegativeLimit)
                lblPositiveLimitZ.BackColor = Color.Red;
            else
                lblPositiveLimitZ.BackColor = _nonSelectedColor;

        }

        private void lblCameraExposureValue_Click(object sender, EventArgs e)
        {
            int exposureTime = 0;
            int digitalGain = 0;
            if (_grabMode == GrabMode.AreaMode)
            {
                exposureTime = SetLabelIntegerData(sender);
            }
            else
            {
                digitalGain = SetLabelIntegerData(sender);
            }
        }

        private void lblCameraGainValue_Click(object sender, EventArgs e)
        {
            int analogGain = SetLabelIntegerData(sender);
        }

        private void trbDimmingLevelValue_Scroll(object sender, EventArgs e)
        {
            int level = trbDimmingLevelValue.Value;
            nudLightDimmingLevel.Text = level.ToString();
        }

        private void nudLightDimmingLevel_ValueChanged(object sender, EventArgs e)
        {
            int level = Convert.ToInt32(nudLightDimmingLevel.Text);
            trbDimmingLevelValue.Value = level;
        }

        private void lblLightOn_Click(object sender, EventArgs e)
        {
            LightOnOff(true);
        }

        private void lblLightOff_Click(object sender, EventArgs e)
        {
            LightOnOff(false);
        }

        private void LightOnOff(bool isOn)
        {

        }

        private void rdoJogSlowMode_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoJogSlowMode.Checked)
            {
                SetSelectJogSpeedMode(JogSpeedMode.Slow);
                rdoJogSlowMode.BackColor = _selectedColor;
            }
            else
                rdoJogSlowMode.BackColor = _nonSelectedColor;
        }

        private void rdoJogFastMode_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoJogFastMode.Checked)
            {
                SetSelectJogSpeedMode(JogSpeedMode.Fast);
                rdoJogFastMode.BackColor = _selectedColor;
            }
            else
                rdoJogFastMode.BackColor = _nonSelectedColor;
        }

        private void SetSelectJogSpeedMode(JogSpeedMode jogSpeedMode)
        {
            MotionJogControl.JogSpeedMode = jogSpeedMode;
            LAFJogControl.JogSpeedMode = jogSpeedMode;
        }

        private void rdoJogMode_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoJogMode.Checked)
            {
                SetSelectJogMode(JogMode.Jog);
                rdoJogMode.BackColor = _selectedColor;
            }
            else
                rdoJogMode.BackColor = _nonSelectedColor;
        }

        private void rdoIncreaseMode_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoIncreaseMode.Checked)
            {
                SetSelectJogMode(JogMode.Increase);
                rdoIncreaseMode.BackColor = _selectedColor;
            }
            else
                rdoIncreaseMode.BackColor = _nonSelectedColor;
        }

        private void SetSelectJogMode(JogMode jogMode)
        {
            MotionJogControl.JogMode = jogMode;
            LAFJogControl.JogMode = jogMode;
        }

        private void lblPitchXYValue_Click(object sender, EventArgs e)
        {
            double pitchXY = SetLabelDoubleData(sender);
            MotionJogControl.JogPitch = pitchXY;
        }

        private void lblPitchZValue_Click(object sender, EventArgs e)
        {
            double pitchZ = SetLabelDoubleData(sender);
            LAFJogControl.MoveAmount = pitchZ;
        }

        private int SetLabelIntegerData(object sender)
        {
            Label lbl = sender as Label;
            int prevData = Convert.ToInt32(lbl.Text);

            KeyPadForm keyPadForm = new KeyPadForm();
            keyPadForm.PreviousValue = (double)prevData;
            keyPadForm.ShowDialog();

            int inputData = Convert.ToInt16(keyPadForm.PadValue);

            Label label = (Label)sender;
            label.Text = inputData.ToString();

            return inputData;
        }

        private double SetLabelDoubleData(object sender)
        {
            Label lbl = sender as Label;
            double prevData = Convert.ToDouble(lbl.Text);

            KeyPadForm keyPadForm = new KeyPadForm();
            keyPadForm.PreviousValue = prevData;
            keyPadForm.ShowDialog();

            double inputData = keyPadForm.PadValue;

            Label label = (Label)sender;
            label.Text = inputData.ToString();

            return inputData;
        }
    }
}
