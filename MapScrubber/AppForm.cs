﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Microsoft.WindowsAPICodePack;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Media;
namespace MapScrubber {

	


	public partial class AppForm : Form {
		public AppForm() {
			InitializeComponent();
			TextWriter tmp = Console.Out; // Save the current console TextWriter. 
			StringRedir r = new StringRedir(ref consoleTextbox);
			Console.SetOut(r); // Set console output to the StringRedir class. 
		}

		public ProgressBar bar {
			get {
				return progressBar;
			}

			set {
				progressBar = value;
			}
		}

		private void browse_asset_Click(object sender, EventArgs e) {
			using(var openFileDialog = new CommonOpenFileDialog() { IsFolderPicker = true }) {
				openFileDialog.RestoreDirectory = true;
				openFileDialog.Title = "Select your asset directory.";

				if(openFileDialog.ShowDialog() == CommonFileDialogResult.Ok) {
					asset_textbox.Text = openFileDialog.FileName;
				}
			}
		}

		private void browse_map_Click(object sender, EventArgs e) {
			OpenFileDialog choofdlog = new OpenFileDialog();
			choofdlog.Filter = "Vmap Files (*.vmap*)|*.vmap*";
			choofdlog.FilterIndex = 1;
			choofdlog.Title = "Select your s&box vmap.";
			choofdlog.Multiselect = false;

			if(choofdlog.ShowDialog() == DialogResult.OK) {
				this.map_textbox.Text = choofdlog.FileName;
				string[] arrAllFiles = choofdlog.FileNames; //used when Multiselect = true
			}
		}

		private void browse_vpk_Click(object sender, EventArgs e) {
			using(var openFileDialog = new CommonOpenFileDialog() { IsFolderPicker = true }) {
				openFileDialog.RestoreDirectory = true;
				openFileDialog.Title = "Select your s&box directory.";

				if(openFileDialog.ShowDialog() == CommonFileDialogResult.Ok) {
					vpk_textbox.Text = openFileDialog.FileName;
				}
			}
		}

		private void packAssets_Click(object sender, EventArgs e) {
			var assetDir = asset_textbox.Text;
			var sboxDir = vpk_textbox.Text;
			var vmapDir = map_textbox.Text;
			
			if(!Directory.Exists(vmapDir)) {
				MessageBox.Show("Vmap directory path invalid!", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			if(!Directory.Exists(assetDir)) {
				MessageBox.Show("Asset directory path invalid!", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			if(!Directory.Exists(sboxDir)) {
				MessageBox.Show("s&box directory path invalid!", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			consoleTextbox.Clear();
			AssetCleaner cleaner = new AssetCleaner(assetDir, sboxDir, vmapDir);
			cleaner.parentForm = this;
			cleaner.GetAssets();
		}
	}

	public class StringRedir : StringWriter {
		private RichTextBox outBox;

		public StringRedir(ref RichTextBox textBox) {
			outBox = textBox;
			outBox.SelectionStart = outBox.Text.Length;
			outBox.ScrollToCaret();
		}

		public override void WriteLine(string x) {
			outBox.Text += x + "\n";
			outBox.SelectionStart = outBox.Text.Length;
			outBox.ScrollToCaret();
			outBox.Refresh();
		}
	}
}
