﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Media;
using System.Windows;

namespace MapPacker {
	class AssetPacker{

		public static HashSet<string> assets = new HashSet<string>();
		private string assetPath;
		public string outputDirectory;
		private string vpkDirectory;
		private string vmapFile;

		public MainWindow parentForm;

		public AssetPacker(string assetDir, string sboxDir, string vmapFile) {
			this.assetPath = assetDir;
			this.vpkDirectory = sboxDir + "\\bin\\win64\\vpk.exe";
			this.vmapFile = vmapFile;
			outputDirectory = Path.GetDirectoryName(vmapFile) + "\\" + Path.GetFileNameWithoutExtension(vmapFile);
		}

		private bool noNotf = false;
		private bool vpkFailed = false;

		public void GetAssets() {
			GetAssets(false);
		}

		public void GetAssets(bool noNotf = false) {

			if(noNotf)
				this.noNotf = noNotf;

			if(parentForm.Pack) {
				Directory.CreateDirectory(outputDirectory);
			}

			parentForm.SetProgress(10);

			// path where the map is
			string pathToMap = vmapFile;

			parentForm.PrintToConsole($"reading map file: {pathToMap}. This might take some time for big maps!");
			GetAssetsFromMap(pathToMap, true);

			parentForm.SetProgress(30);

			if(assets.Count > 0) {
				parentForm.PrintToConsole("Found assets:");
			} else {
				parentForm.PrintToConsole("No assets found in provided asset directory!");
				parentForm.SetProgress(0);
				if(!this.noNotf)
					MessageBox.Show("No assets could be found!", "Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			foreach(string asset in assets) {
				parentForm.PrintToConsole($"\t{asset}", "steam2004ControlText");
			}

			parentForm.SetProgress(40);
			CopyFiles();
		}

		public void ExecuteCommandAsync(string command) {
			try {
				//Asynchronously start the Thread to process the Execute command request.
				Thread objThread = new Thread(new ParameterizedThreadStart(ExecuteCommandSync));
				//Make the thread as background thread.
				objThread.IsBackground = true;
				//Set the Priority of the thread.
				objThread.Priority = ThreadPriority.AboveNormal;
				//Start the thread.
				objThread.Start(command);
			} catch {
			}
		}

		public void ExecuteCommandSync(object command) {
			try {
				System.Diagnostics.ProcessStartInfo procStartInfo =
					_ = new System.Diagnostics.ProcessStartInfo(vpkDirectory, "/c");

				procStartInfo.Arguments = $"{command}";
				procStartInfo.RedirectStandardOutput = true;
				procStartInfo.UseShellExecute = false;
				procStartInfo.CreateNoWindow = true;
				System.Diagnostics.Process proc = new System.Diagnostics.Process();
				proc.StartInfo = procStartInfo;
				proc.Start();
				// Get the output into a string
				string result = proc.StandardOutput.ReadToEnd();
				//parentForm.PrintToConsole($"{result}", "steam2004ControlText");

				//vpk.exe does not output any important information and normal packing/unpacking does not allow for the verbose option.....
				
			} catch {
			}
		}

		public void CopyFiles() {

			if(!parentForm.Pack) {
				outputDirectory += "_content";
				parentForm.PrintToConsole("\nAssets Copied: ");
			} else {
				ExtractVPK();  // extract first, pack after copying
				if(vpkFailed) { // treat as nopack operation of vpk.exe didn't run properly (FOR SOME BLOODY REASON)
					parentForm.PrintToConsole("\nAssets Copied: ");
				} else {
					parentForm.PrintToConsole("\nAssets Packed: ");
				}
			}

			int index = 0;
			foreach(string asset in assets) {
				index++;

				parentForm.SetProgress(40 + 30 * (int)Math.Round(index / (float)assets.Count));

				string fileName = asset;
				try {
					string source = Path.Combine(assetPath, fileName);
					string destination = Path.Combine(outputDirectory, fileName);
					string directory = destination.Substring(0, destination.LastIndexOf("\\"));

					if(File.Exists(source)) {
						Directory.CreateDirectory(directory);
						File.Copy(source, destination, true);
						parentForm.PrintToConsole($"\t{asset}", "steam2004ControlText");
					} else {
						// asset is in core files or another addon, ignore
					}
				} catch {
					// asset not found, broken or otherwise defunct, ignore
				}
			}
			if(parentForm.Pack && !vpkFailed) {
				PackVPK();
			} else {
				parentForm.SetProgress(0);

				parentForm.PrintToConsole("\nAsset copy completed.");
				if(!this.noNotf)
					MessageBox.Show($"Content successfully copied! {(vpkFailed ? "\nvpk.exe failed! Content is located separate from the vpk." : "")}", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
				parentForm.SetCheckBoxEnabled(true);
			}
		}

		public void ExtractVPK() {
			parentForm.PrintToConsole("\nUnpacking vpk...\n");
			//execute vpk file.vpk to extract
			string command = $"{vmapFile.Replace(".vmap", ".vpk")}";
			ExecuteCommandSync(command);
			if(!Directory.Exists(outputDirectory) || vpkFailed) { //hacky check to see if vpk.exe ran properly... extra check is for debug
				vpkFailed = true;
				outputDirectory += "_content";
				parentForm.PrintToConsole("\tvpk.exe failed to run! Treating this as a nopack operation instead... ");
			}
			if(parentForm.Pack && !vpkFailed) { // only "make" a backup if the original was extracted successfully, meaning it will be packed with content
				File.Move($"{vmapFile.Replace(".vmap", ".vpk")}", $"{vmapFile.Replace(".vmap", ".vpk.backup")}", true);
				parentForm.PrintToConsole("\nvpk unpacked\n");
			}
			parentForm.SetProgress(95);
		}

		public void PackVPK() {
			// execute vpk outputDirectory to repack
			string command = $"{outputDirectory}";
			ExecuteCommandSync(command);
			//parentForm.PrintToConsole("\nPacked vpk\n");
			parentForm.SetProgress(0);

			// delete temp directory
			Directory.Delete(outputDirectory, true);

			//SoundPlayer player = new SoundPlayer(Properties.Resources.steam_message);
			//player.Play();
			parentForm.PrintToConsole("\nAsset pack completed.\n");
			if(!this.noNotf)
				MessageBox.Show("Map Successfully packed!", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
			parentForm.SetCheckBoxEnabled(true);
		}

		public void GetAssetsFromMap(string map, bool rootMap = false) { // this uses a full path, since it's kinda for the original map
			try {
				var mapData = File.ReadAllBytes($"{map}");
				if(rootMap)
					parentForm.PrintToConsole($"Read Vmap file, parsing...");

				AssetFile mapFile = AssetFile.From(mapData);

				mapFile.TrimAssetList(); // trim it to the space where assets are actually referenced, using "map_assets_references" markers


				bool found = true;
				foreach(var item in mapFile.SplitNull()) {
					found = true;

					if(item.EndsWith("vmat")) {
						GetAssetsFromMaterial(item);
					} else if(item.EndsWith("vmdl")) {
						GetAssetsFromModel(item);
					} else if(item.EndsWith("vpcf")) {
						GetAssetsFromParticle(item);
					} else if(item.EndsWith("vmap")) {
						var mapItem = CleanAssetPath(item);
						var path = TrimAddonPath(map);
						if(!($"{path}\\{item}" == map)) { // this will also deal with 3d skybox content
							GetAssetsFromMap($"{path}\\{item}"); // we can assume that prefabs will also be wherever the original map is
							GetAssetsFromMap($"{assetPath}\\{item}"); // prefabs *could* also be in the asset directory however
						}
					} else if (item.EndsWith("vpost")) {
						AddAsset(item); // post processing file
					} else {
						found = false;
					}

					if(found) {
						parentForm.PrintToConsole("Found asset " + item + "...");
					}
				}
			} catch {
				// asset not in asset directory or not found
			}
		}

		// this is for all Model related asset types, including legacy support; vmdl, vmesh, vphys, vseq, vagrp and vanim
		public void GetAssetsFromModel(string item) {
			try {
				var data = File.ReadAllBytes($"{assetPath}\\{item}_c");
				AddAsset(item); // add vmdl_c (or legacy reference)
				var material = AssetFile.From(data);
				foreach(var matItem in material.SplitNull()) {
					if(matItem.EndsWith("vmat")) {
						// add vmat_c referenced by the vmdl_c
						GetAssetsFromMaterial(matItem); // add vtex_c referenced by the vmat_c

					// LEGACY IMPORT SUPPORT 
					} else if(matItem.EndsWith("vmesh")) { 
						// add vmesh_c referenced by the vmdl_c
						GetAssetsFromModel(matItem); // add vmat_c referenced by the vmesh_c
					} else if(matItem.EndsWith("vphys")) {
						AddAsset(matItem); // add vphys_c referenced by a vmesh_c
					} else if(matItem.EndsWith("vseq")) {
						AddAsset(matItem); // add vseq_c referenced by a vmdl_c
					} else if(matItem.EndsWith("vagrp")) {
						// add vagrp_c referenced by a vmdl_c
						GetAssetsFromModel(matItem); // add vanim_c referenced by a vagrp
					} else if(matItem.EndsWith("vanim")) {
						AddAsset(matItem); // add vanim_c referenced by a vagrp_c
					}
				}
			} catch {
				// asset not in asset directory or not found
			}
		}

		public void GetAssetsFromMaterial(string item) {
			try {
				var materialData = File.ReadAllBytes($"{assetPath}\\{item}_c");
				AddAsset(item); // add vmat_c
				var material = AssetFile.From(materialData);
				foreach(var texItem in material.SplitNull()) {
					if(texItem.EndsWith("vtex")) {
						AddAsset(texItem); // add vtex_c referenced by the vmat_c
					}
				}
			} catch {
				// asset not in asset directory or not found
			}
		}

		public void GetAssetsFromParticle(string item) {
			try {
				var materialData = File.ReadAllBytes($"{assetPath}\\{item}_c");
				AddAsset(item); // add vpcf_c
				var material = AssetFile.From(materialData);
				foreach(var assetItem in material.SplitNull()) {
					if(assetItem.EndsWith("vtex")) {
						AddAsset(assetItem); // add vtex_c referenced by the vpcf_c
					} else if(assetItem.EndsWith("vmat")) {
						GetAssetsFromMaterial(assetItem);
					} else if(assetItem.EndsWith("vmdl")) {
						GetAssetsFromModel(assetItem);
					} else if(assetItem.EndsWith("vpcf")) {
						if(!(item == assetItem)) // if there's a self reference in the particle. this is often the case for some reason
							GetAssetsFromParticle(assetItem);
					}
				}
			} catch {
				// asset not in asset directory or not found
			}
		}

		public static void AddAsset(string item) {
			// basic clean
			var asset = CleanAssetPath(item);

			if(!asset.EndsWith("_c")) {
				// make sure the file is an asset file as we'd want it
				return;
			}
			assets.Add(asset);		
		}

		public static string CleanAssetPath(string item) {
			var asset = item.Replace("\r\n", "").Replace("\r", "").Replace("\n", "").Replace("/", "\\");

			// this is dumb
			if(asset.EndsWith("vmat")) {
				asset = asset.Replace("vmat", "vmat_c");
			} else if(asset.EndsWith("vmdl")) {
				asset = asset.Replace("vmdl", "vmdl_c");
			} else if(asset.EndsWith("vtex")) {
				asset = asset.Replace("vtex", "vtex_c");
			} else if(asset.EndsWith("vpcf")) {
				asset = asset.Replace("vpcf", "vpcf_c");
			} else if(asset.EndsWith("vsnd")) {
				asset = asset.Replace("vsnd", "vsnd_c");
			} else if(asset.EndsWith("vphys")) {
				asset = asset.Replace("vphys", "vphys_c");
			} else if(asset.EndsWith("vmesh")) {
				asset = asset.Replace("vmesh", "vmesh_c");
			} else if(asset.EndsWith("vanim")) {
				asset = asset.Replace("vanim", "vanim_c");
			} else if(asset.EndsWith("vpost")) {
				asset = asset.Replace("vpost", "vpost_c");
			} else if(asset.EndsWith("vseq")) {
				asset = asset.Replace("vseq", "vseq_c");
			} else if(asset.EndsWith("vagrp")) {
				asset = asset.Replace("vagrp", "vagrp_c");
			}
			return asset;
		}

		public static string TrimAddonPath(string path) {
			// trim full path to .../addons/addonName/
			var addonsIndex = path.LastIndexOf("addons");
			var trim1 = path.Substring(0, addonsIndex + 7);
			var trim2 = path.Substring(addonsIndex + 7, path.Length - addonsIndex - 7);
			var addonName = trim2.Substring(0, trim2.IndexOf("\\"));
			return trim1 + addonName;
		}
	}

	// this should probably get reworked with asset type enums
	public class AssetFile {
		private string assetReference;
		public static AssetFile From(byte[] bytes) {
			AssetFile asset = new AssetFile();
			asset.assetReference = Encoding.Default.GetString(bytes);
			return asset;
		}

		public string[] SplitNull() {
			string[] arr = { "\0" }; // lol why doesn't this have an overload for strings
			return assetReference.Split(arr, StringSplitOptions.RemoveEmptyEntries);
		}

		public string TrimAssetList(string marker = "map_asset_references") {
			int start = assetReference.IndexOf(marker);
			int end = start + marker.Length + assetReference.Substring(start + marker.Length).IndexOf(marker);
			var output = assetReference[start..end];
			return output;
		}

// obsolete since we're doing prefab scans anyways
		public bool IsMapSkybox() {
			var splitStrings = this.SplitNull();

			for(var i = 0; i < splitStrings.Length; i++) {
				var item = splitStrings[i];
				if(item == "mapUsageType") {
					if(splitStrings[i + 1] == "skybox") { // skybox map type
						return true;
					}
				}
			}
			return false;
		}
	}
}
