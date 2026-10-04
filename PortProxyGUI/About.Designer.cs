namespace PortProxyGUI;

partial class About
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

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(About));
        this.linkLabel1 = new System.Windows.Forms.LinkLabel();
        this.label1 = new System.Windows.Forms.Label();
        this.label_version = new System.Windows.Forms.Label();
        this.label_Star = new System.Windows.Forms.Label();
        this.linkLabelFork = new System.Windows.Forms.LinkLabel();
        var layout = new System.Windows.Forms.TableLayoutPanel();
        this.SuspendLayout();
        // 
        // linkLabel1
        // 
        this.linkLabel1.AutoSize = true;
        this.linkLabel1.Margin = new System.Windows.Forms.Padding(0, 0, 0, 16);
        this.linkLabel1.Name = "linkLabel1";
        this.linkLabel1.TabStop = true;
        this.linkLabel1.Click += new System.EventHandler(this.linkLabel1_Click);
        // 
        // label1
        // 
        this.label1.AutoSize = true;
        this.label1.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
        this.label1.Name = "label1";
        // 
        // label_version
        // 
        this.label_version.AutoSize = true;
        this.label_version.Margin = new System.Windows.Forms.Padding(0, 0, 0, 16);
        this.label_version.Name = "label_version";
        // 
        // label_Star
        // 
        this.label_Star.AutoSize = true;
        this.label_Star.Margin = new System.Windows.Forms.Padding(0, 0, 0, 10);
        this.label_Star.Name = "label_Star";
        this.linkLabelFork.AutoSize = true;
        this.linkLabelFork.Name = "linkLabelFork";
        this.linkLabelFork.TabStop = true;
        this.linkLabelFork.Click += new System.EventHandler(this.linkLabel1_Click);
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 1;
        layout.RowCount = 6;
        layout.Padding = new System.Windows.Forms.Padding(20);
        layout.Controls.Add(this.label_version, 0, 0);
        layout.Controls.Add(this.label1, 0, 1);
        layout.Controls.Add(this.linkLabel1, 0, 2);
        layout.Controls.Add(this.label_Star, 0, 3);
        layout.Controls.Add(this.linkLabelFork, 0, 4);
        layout.Controls.Add(new System.Windows.Forms.Label
        {
            AutoSize = true,
            Text = "MIT License · Original copyright and license retained.",
            Margin = new System.Windows.Forms.Padding(0, 16, 0, 0),
        }, 0, 5);
        // 
        // About
        // 
        resources.ApplyResources(this, "$this");
        this.Font = System.Drawing.SystemFonts.MessageBoxFont;
        this.AutoSize = true;
        this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        this.ClientSize = new System.Drawing.Size(560, 270);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.Controls.Add(layout);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Name = "About";
        this.TopMost = true;
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.About_FormClosing);
        this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.LinkLabel linkLabel1;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.Label label_version;
    private System.Windows.Forms.Label label_Star;
    private System.Windows.Forms.LinkLabel linkLabelFork;
}
