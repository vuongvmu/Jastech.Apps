﻿namespace Jastech.Apps.Winform.UI.Controls
{
    partial class TabBtnControl
    {
        /// <summary> 
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 구성 요소 디자이너에서 생성한 코드

        /// <summary> 
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnTab = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnTab
            // 
            this.btnTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnTab.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.btnTab.ForeColor = System.Drawing.Color.White;
            this.btnTab.Location = new System.Drawing.Point(0, 0);
            this.btnTab.Name = "btnTab";
            this.btnTab.Size = new System.Drawing.Size(186, 67);
            this.btnTab.TabIndex = 0;
            this.btnTab.Text = "button1";
            this.btnTab.UseVisualStyleBackColor = false;
            this.btnTab.Click += new System.EventHandler(this.btnTab_Click);
            // 
            // TabBtnControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnTab);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "TabBtnControl";
            this.Size = new System.Drawing.Size(186, 67);
            this.Load += new System.EventHandler(this.TabBtnControl_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnTab;
    }
}
