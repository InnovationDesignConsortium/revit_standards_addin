using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RevitDataValidator.Forms
{
    public partial class frmAbout : Form
    {
        GithubResponse latestRelease = null;
        public frmAbout()
        {
            InitializeComponent();
            lblInstalled.Text = Utils.GetInstalledVersion().ToString();

            latestRelease = Utils.GetLatestWebRelase();
            
            if (latestRelease == null)
            {
                lblNewest.Text = "<none>";
            }
            else
            {
                var webVersion = new Version(latestRelease.tag_name.Substring(1));
                lblNewest.Text = webVersion.ToString();
                lblReleaseDate.Text = latestRelease.published_at.ToString();

                if (Utils.IsWebVersionNewer(webVersion))
                {
                    btnDownload.Enabled = true;
                }
                else
                {
                    btnDownload.Enabled = false;
                }
            }

            

        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            var asset = latestRelease.assets.First();
            Utils.DownloadAsset(latestRelease.tag_name, asset);
        }
    }
}