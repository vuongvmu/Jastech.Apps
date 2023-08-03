﻿using Cognex.VisionPro;
using Jastech.Apps.Structure.Data;
using Jastech.Framework.Winform.VisionPro.Controls;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace Jastech.Apps.Winform.UI.Controls
{
    public partial class PreAlignDisplayControl : UserControl
    {
        #region 필드
        #endregion

        #region 속성
        private CogPreAlignDisplayControl PreAlignDisplay { get; set; } = null;
        #endregion

        #region 이벤트
        #endregion

        #region 델리게이트
        #endregion

        #region 생성자
        public PreAlignDisplayControl()
        {
            InitializeComponent();
        }
        #endregion

        #region 메서드
        private void PreAlignDisplayControl_Load(object sender, System.EventArgs e)
        {
            AddControl();
        }

        private void AddControl()
        {
            PreAlignDisplay = new CogPreAlignDisplayControl();
            PreAlignDisplay.Dock = DockStyle.Fill;
            pnlPreAlignDisplay.Controls.Add(PreAlignDisplay);
        }

        public void UpdateLeftDisplay(PreAlignResult result)
        {
            List<CogCompositeShape> resultList = new List<CogCompositeShape>();

            var matchResult = result.MatchResult;
            var graphics = matchResult.MaxMatchPos.ResultGraphics;
            resultList.Add(graphics);
            var deepCopyImage = result.CogImage.CopyBase(CogImageCopyModeConstants.CopyPixels);

            PreAlignDisplay.UpdateLeftDisplay(deepCopyImage, resultList);
        }

        public void UpdateRightDisplay(PreAlignResult result)
        {
            List<CogCompositeShape> resultList = new List<CogCompositeShape>();

            var matchResult = result.MatchResult;
            if (matchResult == null)
                return;

            var graphics = matchResult.MaxMatchPos.ResultGraphics;
            resultList.Add(graphics);
            var deepCopyImage = result.CogImage.CopyBase(CogImageCopyModeConstants.CopyPixels);

            PreAlignDisplay.UpdateRightDisplay(deepCopyImage, resultList);
        }

        public delegate void UpdateLeftResultDelegate(PreAlignResult result);
        public void UpdateLeftResult(PreAlignResult result)
        {
            if (this.InvokeRequired)
            {
                UpdateLeftResultDelegate callback = UpdateLeftResult;
                BeginInvoke(callback, result);
                return;
            }

            UpdateLeftMessage(result);
        }

        private void UpdateLeftMessage(PreAlignResult result)
        {
            string resultMessage = string.Format("Judge : {0}\nScore : {1}%\nX : {2}\nY : {3}",
                                                result.MatchResult.Judgement.ToString(), (result.MatchResult.MaxScore * 100).ToString("F2"),
                                                result.MatchResult.MaxMatchPos.FoundPos.X.ToString("F2"), result.MatchResult.MaxMatchPos.FoundPos.Y.ToString("F2"));

            lblLeftPreAlignResult.Text = resultMessage;
        }

        public delegate void UpdateRightResultDelegate(PreAlignResult result);
        public void UpdateRightResult(PreAlignResult result)
        {
            if (this.InvokeRequired)
            {
                UpdateRightResultDelegate callback = UpdateRightResult;
                BeginInvoke(callback, result);
                return;
            }

            UpdateRightMessage(result);
        }

        public void UpdateRightMessage(PreAlignResult result)
        {
            string resultMessage = string.Format("Judge : {0}\nScore : {1}%\nX : {2}\nY : {3}",
                                            result.MatchResult.Judgement.ToString(), (result.MatchResult.MaxScore * 100).ToString("F2"),
                                            result.MatchResult.MaxMatchPos.FoundPos.X.ToString("F2"), result.MatchResult.MaxMatchPos.FoundPos.Y.ToString("F2"));

            lblRightPreAlignResult.Text = resultMessage;
        }

        public delegate void UpdatePreAlignResultDelegate(AppsPreAlignResult result);
        public void UpdatePreAlignResult(AppsPreAlignResult result)
        {
            if (this.InvokeRequired)
            {
                UpdatePreAlignResultDelegate callback = UpdatePreAlignResult;
                BeginInvoke(callback, result);
                return;
            }

            UpdatePreAlignResultMessage(result);
        }

        private void UpdatePreAlignResultMessage(AppsPreAlignResult result)
        {
            string resultMessage = string.Format("Offset X : {0}\nOffset Y : {1}\nOffset T : {2}",
                                            result.OffsetX.ToString("F4"), result.OffsetY.ToString("F4"), result.OffsetT.ToString("F4"));

            lblPreAlignResult.Text = resultMessage;
        }

        public delegate void ClearPreAlignResultDelegate();
        public void ClearPreAlignResult()
        {
            if (this.InvokeRequired)
            {
                ClearPreAlignResultDelegate callback = ClearPreAlignResult;
                BeginInvoke(callback);
                return;
            }

            ClearImage();
            ClearPreAlignResultMessage();
        }

        private void ClearPreAlignResultMessage()
        {
            string resultMessage = string.Format("Judge : {0}\nScore : {1}\nX : {2}\nY : {3}", "-", "-", "-", "-");

            lblLeftPreAlignResult.Text = resultMessage;
            lblRightPreAlignResult.Text = resultMessage;
            lblPreAlignResult.Text = "-";
        }

        public void ClearImage()
        {
            PreAlignDisplay.ClearImage();
        }
        #endregion
    }
}
