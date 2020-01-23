﻿namespace Orts.ActivityRunner.Viewer3D.Dispatcher
{
    partial class DispatcherViewControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pnlMessaging = new System.Windows.Forms.Panel();
            this.statusStripDispatcher = new System.Windows.Forms.StatusStrip();
            this.toolStripFPS = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripSize = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripPosition = new System.Windows.Forms.ToolStripStatusLabel();
            this.pnlAvatar = new System.Windows.Forms.Panel();
            this.pnlDispatcherView = new System.Windows.Forms.Panel();
            this.pbDispatcherView = new System.Windows.Forms.PictureBox();
            this.statusStripDispatcher.SuspendLayout();
            this.pnlDispatcherView.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbDispatcherView)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlMessaging
            // 
            this.pnlMessaging.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(128)))));
            this.pnlMessaging.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMessaging.Location = new System.Drawing.Point(0, 0);
            this.pnlMessaging.Margin = new System.Windows.Forms.Padding(4);
            this.pnlMessaging.Name = "pnlMessaging";
            this.pnlMessaging.Size = new System.Drawing.Size(1000, 123);
            this.pnlMessaging.TabIndex = 1;
            // 
            // statusStripDispatcher
            // 
            this.statusStripDispatcher.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStripDispatcher.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripFPS,
            this.toolStripSize,
            this.toolStripPosition});
            this.statusStripDispatcher.Location = new System.Drawing.Point(0, 552);
            this.statusStripDispatcher.Name = "statusStripDispatcher";
            this.statusStripDispatcher.Padding = new System.Windows.Forms.Padding(1, 0, 19, 0);
            this.statusStripDispatcher.Size = new System.Drawing.Size(1000, 26);
            this.statusStripDispatcher.TabIndex = 15;
            this.statusStripDispatcher.Text = "statusStrip1";
            // 
            // toolStripFPS
            // 
            this.toolStripFPS.Name = "toolStripFPS";
            this.toolStripFPS.Size = new System.Drawing.Size(32, 20);
            this.toolStripFPS.Text = "FPS";
            // 
            // toolStripSize
            // 
            this.toolStripSize.Name = "toolStripSize";
            this.toolStripSize.Size = new System.Drawing.Size(36, 20);
            this.toolStripSize.Text = "Size";
            // 
            // toolStripPosition
            // 
            this.toolStripPosition.Name = "toolStripPosition";
            this.toolStripPosition.Size = new System.Drawing.Size(61, 20);
            this.toolStripPosition.Text = "Position";
            // 
            // pnlAvatar
            // 
            this.pnlAvatar.BackColor = System.Drawing.Color.Green;
            this.pnlAvatar.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlAvatar.Location = new System.Drawing.Point(752, 123);
            this.pnlAvatar.Margin = new System.Windows.Forms.Padding(4);
            this.pnlAvatar.Name = "pnlAvatar";
            this.pnlAvatar.Size = new System.Drawing.Size(248, 429);
            this.pnlAvatar.TabIndex = 16;
            // 
            // pnlDispatcherView
            // 
            this.pnlDispatcherView.Controls.Add(this.pbDispatcherView);
            this.pnlDispatcherView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlDispatcherView.Location = new System.Drawing.Point(0, 123);
            this.pnlDispatcherView.Margin = new System.Windows.Forms.Padding(4);
            this.pnlDispatcherView.Name = "pnlDispatcherView";
            this.pnlDispatcherView.Size = new System.Drawing.Size(752, 429);
            this.pnlDispatcherView.TabIndex = 17;
            // 
            // pbDispatcherView
            // 
            this.pbDispatcherView.BackColor = System.Drawing.Color.White;
            this.pbDispatcherView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pbDispatcherView.Location = new System.Drawing.Point(0, 0);
            this.pbDispatcherView.Margin = new System.Windows.Forms.Padding(4);
            this.pbDispatcherView.Name = "pbDispatcherView";
            this.pbDispatcherView.Size = new System.Drawing.Size(752, 429);
            this.pbDispatcherView.TabIndex = 0;
            this.pbDispatcherView.TabStop = false;
            this.pbDispatcherView.SizeChanged += new System.EventHandler(this.PictureBoxDispatcherView_SizeChanged);
            this.pbDispatcherView.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.PictureBoxDispatcherView_MouseDoubleClick);
            this.pbDispatcherView.MouseEnter += new System.EventHandler(this.PictureBoxDispatcherView_MouseEnter);
            this.pbDispatcherView.MouseMove += new System.Windows.Forms.MouseEventHandler(this.DispatcherViewControl_MouseMove);
            // 
            // DispatcherViewControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pnlDispatcherView);
            this.Controls.Add(this.pnlAvatar);
            this.Controls.Add(this.statusStripDispatcher);
            this.Controls.Add(this.pnlMessaging);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "DispatcherViewControl";
            this.Size = new System.Drawing.Size(1000, 578);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.DispatcherViewControl_MouseMove);
            this.statusStripDispatcher.ResumeLayout(false);
            this.statusStripDispatcher.PerformLayout();
            this.pnlDispatcherView.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pbDispatcherView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Panel pnlMessaging;
        private System.Windows.Forms.StatusStrip statusStripDispatcher;
        private System.Windows.Forms.ToolStripStatusLabel toolStripFPS;
        private System.Windows.Forms.ToolStripStatusLabel toolStripSize;
        private System.Windows.Forms.Panel pnlAvatar;
        private System.Windows.Forms.Panel pnlDispatcherView;
        private System.Windows.Forms.PictureBox pbDispatcherView;
        private System.Windows.Forms.ToolStripStatusLabel toolStripPosition;
    }
}
