﻿using Jastech.Framework.Winform.Controls;

namespace Jastech.Apps.Winform.UI.Controls
{
    partial class AkkonTrendControl
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.tlpAkkonTrend = new System.Windows.Forms.TableLayoutPanel();
            this.tlpData = new System.Windows.Forms.TableLayoutPanel();
            this.pnlChart = new System.Windows.Forms.Panel();
            this.tlpTabList = new System.Windows.Forms.TableLayoutPanel();
            this.lblTabSelection = new System.Windows.Forms.Label();
            this.pnlTabs = new System.Windows.Forms.Panel();
            this.tplChartType = new System.Windows.Forms.TableLayoutPanel();
            this.pnlChartTypes = new System.Windows.Forms.Panel();
            this.lblAkkon = new System.Windows.Forms.Label();
            this.lblCount = new System.Windows.Forms.Label();
            this.lblLength = new System.Windows.Forms.Label();
            this.lblChartType = new System.Windows.Forms.Label();
            this.dgvAkkonTrendData = new Jastech.Framework.Winform.Controls.DoubleBufferedDatagridView();
            this.tlpAkkonTrend.SuspendLayout();
            this.tlpData.SuspendLayout();
            this.tlpTabList.SuspendLayout();
            this.tplChartType.SuspendLayout();
            this.pnlChartTypes.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAkkonTrendData)).BeginInit();
            this.SuspendLayout();
            // 
            // tlpAkkonTrend
            // 
            this.tlpAkkonTrend.ColumnCount = 1;
            this.tlpAkkonTrend.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpAkkonTrend.Controls.Add(this.tlpData, 0, 3);
            this.tlpAkkonTrend.Controls.Add(this.tlpTabList, 0, 0);
            this.tlpAkkonTrend.Controls.Add(this.tplChartType, 0, 2);
            this.tlpAkkonTrend.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpAkkonTrend.Location = new System.Drawing.Point(0, 0);
            this.tlpAkkonTrend.Margin = new System.Windows.Forms.Padding(0);
            this.tlpAkkonTrend.Name = "tlpAkkonTrend";
            this.tlpAkkonTrend.RowCount = 4;
            this.tlpAkkonTrend.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.tlpAkkonTrend.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 15F));
            this.tlpAkkonTrend.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.tlpAkkonTrend.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpAkkonTrend.Size = new System.Drawing.Size(860, 540);
            this.tlpAkkonTrend.TabIndex = 1;
            // 
            // tlpData
            // 
            this.tlpData.ColumnCount = 2;
            this.tlpData.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55F));
            this.tlpData.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.tlpData.Controls.Add(this.pnlChart, 0, 0);
            this.tlpData.Controls.Add(this.dgvAkkonTrendData, 1, 0);
            this.tlpData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpData.Location = new System.Drawing.Point(0, 135);
            this.tlpData.Margin = new System.Windows.Forms.Padding(0);
            this.tlpData.Name = "tlpData";
            this.tlpData.RowCount = 1;
            this.tlpData.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpData.Size = new System.Drawing.Size(860, 405);
            this.tlpData.TabIndex = 3;
            // 
            // pnlChart
            // 
            this.pnlChart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlChart.Location = new System.Drawing.Point(0, 0);
            this.pnlChart.Margin = new System.Windows.Forms.Padding(0);
            this.pnlChart.Name = "pnlChart";
            this.pnlChart.Size = new System.Drawing.Size(473, 405);
            this.pnlChart.TabIndex = 2;
            // 
            // tlpTabList
            // 
            this.tlpTabList.ColumnCount = 2;
            this.tlpTabList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tlpTabList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpTabList.Controls.Add(this.lblTabSelection, 0, 0);
            this.tlpTabList.Controls.Add(this.pnlTabs, 1, 0);
            this.tlpTabList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpTabList.Location = new System.Drawing.Point(3, 3);
            this.tlpTabList.Name = "tlpTabList";
            this.tlpTabList.RowCount = 1;
            this.tlpTabList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpTabList.Size = new System.Drawing.Size(854, 54);
            this.tlpTabList.TabIndex = 5;
            // 
            // lblTabSelection
            // 
            this.lblTabSelection.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(104)))), ((int)(((byte)(104)))));
            this.lblTabSelection.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblTabSelection.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTabSelection.Location = new System.Drawing.Point(0, 0);
            this.lblTabSelection.Margin = new System.Windows.Forms.Padding(0);
            this.lblTabSelection.Name = "lblTabSelection";
            this.lblTabSelection.Size = new System.Drawing.Size(120, 54);
            this.lblTabSelection.TabIndex = 6;
            this.lblTabSelection.Text = "Tab Selection";
            this.lblTabSelection.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pnlTabs
            // 
            this.pnlTabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlTabs.Location = new System.Drawing.Point(120, 0);
            this.pnlTabs.Margin = new System.Windows.Forms.Padding(0);
            this.pnlTabs.Name = "pnlTabs";
            this.pnlTabs.Size = new System.Drawing.Size(734, 54);
            this.pnlTabs.TabIndex = 4;
            // 
            // tplChartType
            // 
            this.tplChartType.ColumnCount = 2;
            this.tplChartType.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tplChartType.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tplChartType.Controls.Add(this.pnlChartTypes, 1, 0);
            this.tplChartType.Controls.Add(this.lblChartType, 0, 0);
            this.tplChartType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tplChartType.Location = new System.Drawing.Point(3, 78);
            this.tplChartType.Name = "tplChartType";
            this.tplChartType.RowCount = 1;
            this.tplChartType.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tplChartType.Size = new System.Drawing.Size(854, 54);
            this.tplChartType.TabIndex = 6;
            // 
            // pnlChartTypes
            // 
            this.pnlChartTypes.Controls.Add(this.lblAkkon);
            this.pnlChartTypes.Controls.Add(this.lblCount);
            this.pnlChartTypes.Controls.Add(this.lblLength);
            this.pnlChartTypes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlChartTypes.Location = new System.Drawing.Point(120, 0);
            this.pnlChartTypes.Margin = new System.Windows.Forms.Padding(0);
            this.pnlChartTypes.Name = "pnlChartTypes";
            this.pnlChartTypes.Size = new System.Drawing.Size(734, 54);
            this.pnlChartTypes.TabIndex = 8;
            // 
            // lblAkkon
            // 
            this.lblAkkon.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblAkkon.Location = new System.Drawing.Point(20, 0);
            this.lblAkkon.Margin = new System.Windows.Forms.Padding(20, 0, 0, 0);
            this.lblAkkon.Name = "lblAkkon";
            this.lblAkkon.Size = new System.Drawing.Size(100, 54);
            this.lblAkkon.TabIndex = 0;
            this.lblAkkon.Text = "All Data";
            this.lblAkkon.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblAkkon.Click += new System.EventHandler(this.lblAkkon_Click);
            // 
            // lblCount
            // 
            this.lblCount.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblCount.Location = new System.Drawing.Point(130, 0);
            this.lblCount.Margin = new System.Windows.Forms.Padding(10, 0, 0, 0);
            this.lblCount.Name = "lblCount";
            this.lblCount.Size = new System.Drawing.Size(100, 54);
            this.lblCount.TabIndex = 1;
            this.lblCount.Text = "Count";
            this.lblCount.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblCount.Click += new System.EventHandler(this.lblCount_Click);
            // 
            // lblLength
            // 
            this.lblLength.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblLength.Location = new System.Drawing.Point(240, 0);
            this.lblLength.Margin = new System.Windows.Forms.Padding(10, 0, 0, 0);
            this.lblLength.Name = "lblLength";
            this.lblLength.Size = new System.Drawing.Size(100, 54);
            this.lblLength.TabIndex = 2;
            this.lblLength.Text = "Length";
            this.lblLength.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblLength.Click += new System.EventHandler(this.lblLength_Click);
            // 
            // lblChartType
            // 
            this.lblChartType.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(104)))), ((int)(((byte)(104)))));
            this.lblChartType.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblChartType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblChartType.Location = new System.Drawing.Point(0, 0);
            this.lblChartType.Margin = new System.Windows.Forms.Padding(0);
            this.lblChartType.Name = "lblChartType";
            this.lblChartType.Size = new System.Drawing.Size(120, 54);
            this.lblChartType.TabIndex = 7;
            this.lblChartType.Text = "Chart Type";
            this.lblChartType.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // dgvAkkonTrendData
            // 
            this.dgvAkkonTrendData.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dgvAkkonTrendData.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(52)))), ((int)(((byte)(52)))));
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.Black;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("맑은 고딕", 11.25F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(104)))), ((int)(((byte)(104)))));
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvAkkonTrendData.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dgvAkkonTrendData.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(52)))), ((int)(((byte)(52)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("맑은 고딕", 11.25F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(104)))), ((int)(((byte)(104)))), ((int)(((byte)(104)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvAkkonTrendData.DefaultCellStyle = dataGridViewCellStyle2;
            this.dgvAkkonTrendData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvAkkonTrendData.EnableHeadersVisualStyles = false;
            this.dgvAkkonTrendData.Location = new System.Drawing.Point(476, 3);
            this.dgvAkkonTrendData.Name = "dgvAkkonTrendData";
            this.dgvAkkonTrendData.ReadOnly = true;
            this.dgvAkkonTrendData.RowHeadersVisible = false;
            this.dgvAkkonTrendData.RowTemplate.Height = 23;
            this.dgvAkkonTrendData.Size = new System.Drawing.Size(381, 399);
            this.dgvAkkonTrendData.TabIndex = 0;
            // 
            // AkkonTrendControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(52)))), ((int)(((byte)(52)))));
            this.Controls.Add(this.tlpAkkonTrend);
            this.Font = new System.Drawing.Font("맑은 고딕", 11.25F, System.Drawing.FontStyle.Bold);
            this.ForeColor = System.Drawing.Color.White;
            this.Name = "AkkonTrendControl";
            this.Size = new System.Drawing.Size(860, 540);
            this.Load += new System.EventHandler(this.AkkonTrendControl_Load);
            this.tlpAkkonTrend.ResumeLayout(false);
            this.tlpData.ResumeLayout(false);
            this.tlpTabList.ResumeLayout(false);
            this.tplChartType.ResumeLayout(false);
            this.pnlChartTypes.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvAkkonTrendData)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpAkkonTrend;
        private System.Windows.Forms.Label lblLength;
        private System.Windows.Forms.Label lblCount;
        private System.Windows.Forms.Label lblAkkon;
        private System.Windows.Forms.TableLayoutPanel tlpData;
        private DoubleBufferedDatagridView dgvAkkonTrendData;
        private System.Windows.Forms.Panel pnlTabs;
        private System.Windows.Forms.Panel pnlChart;
        private System.Windows.Forms.TableLayoutPanel tplChartType;
        private System.Windows.Forms.Label lblChartType;
        private System.Windows.Forms.Panel pnlChartTypes;
        private System.Windows.Forms.TableLayoutPanel tlpTabList;
        private System.Windows.Forms.Label lblTabSelection;
    }
}
