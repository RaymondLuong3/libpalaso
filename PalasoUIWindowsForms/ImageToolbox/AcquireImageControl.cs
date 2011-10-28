﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Palaso.UI.WindowsForms.ImageToolbox
{
	public partial class AcquireImageControl : UserControl, IImageToolboxControl
	{
		private PalasoImage _previousImage;
		private PalasoImage _currentImage;

		public event EventHandler ImageChanged;

		public AcquireImageControl()
		{
			InitializeComponent();
			#if MONO
			_scannerButton.Enabled =  _cameraButton.Enabled = false;
			#endif

			_galleryControl.ImageChanged += new EventHandler(_galleryControl_ImageChanged);
		}

		void _galleryControl_ImageChanged(object sender, EventArgs e)
		{
			//propogate that event to the outer toolbox
			if (ImageChanged != null)
				ImageChanged(sender, e);
		}

		private void OnGetFromFileSystemClick(object sender, EventArgs e)
		{
			SetMode(Modes.SingleImage);
			using (var dlg = new OpenFileDialog())
			{
				if (string.IsNullOrEmpty(ImageToolboxSettings.Default.LastImageFolder))
				{
					dlg.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
				}
				else
				{
					dlg.InitialDirectory = ImageToolboxSettings.Default.LastImageFolder;
				}

				dlg.Filter = "picture files|*.png;*.tif;*.tiff;*.jpg;*.jpeg;*.bmp;*.gif";
				dlg.Multiselect = false;
				dlg.AutoUpgradeEnabled = true;
				if (DialogResult.OK == dlg.ShowDialog())
				{
					_currentImage = PalasoImage.FromFile(dlg.FileName);
					_pictureBox.Image = _currentImage.Image;
					ImageToolboxSettings.Default.LastImageFolder = Path.GetDirectoryName(dlg.FileName);
					ImageToolboxSettings.Default.Save();
					if (ImageChanged != null)
						ImageChanged.Invoke(this, null);
				}
			}
		}

		public void SetImage(PalasoImage image)
		{
			_previousImage = image;
			_scannerButton.Checked = _cameraButton.Checked = false;
			_currentImage = image;
			if (image == null)
				_pictureBox.Image = null;
			else
				_pictureBox.Image = image.Image;
		}

		public PalasoImage GetImage()
		{
			if(_galleryControl.Visible)
			{
				return _galleryControl.GetImage();
			}
			if (_currentImage != null)
			//            if (_pictureBox.Image != null)
			{
				//return new PalasoImage() { Image = _pictureBox.Image, FileName = _fileName };
				return _currentImage;
			}
			return _previousImage;
		}


		private void OnScannerClick(object sender, EventArgs e)
		{
			#if !MONO
			_scannerButton.Checked = true;
			SetImage(null);
			SetMode(Modes.SingleImage);
			GetFromDevice(ImageAcquisitionService.DeviceKind.Scanner);
			#endif
		}
		private void OnCameraClick(object sender, EventArgs e)
		{
			#if !MONO
			SetMode(Modes.SingleImage);
			SetImage(null);
			_cameraButton.Checked = true;
			GetFromDevice(ImageAcquisitionService.DeviceKind.Camera);
			#endif
		}
	  #if !MONO
		private void GetFromDevice(ImageAcquisitionService.DeviceKind deviceKind)
		{
	  //_pictureBox.Image = SampleImages.sampleScan;
			try
			{
				var acquisitionService = new ImageAcquisitionService(deviceKind);

				var file = acquisitionService.Acquire();
				if (file == null)
					return;
				var temp = Path.GetTempFileName();
				File.Delete(temp);
				file.SaveFile(temp);
				temp = ConvertToPngIfNotAlready(temp);
				_pictureBox.Load(temp);
				File.Delete(temp);
				if (ImageChanged != null)
					ImageChanged.Invoke(this, null);
			}
			catch (ImageDeviceNotFoundException error)
			{
				_messageLabel.Text = error.Message + Environment.NewLine +Environment.NewLine+
									 "Note: this program works with devices that have a 'WIA' driver, not the old-style 'TWAIN' driver";
				_messageLabel.Visible = true;
			}
			catch (Exception error)
			{
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(error, "Problem Getting Image");
			}

		}
		#endif

		/// <summary>
		/// Will delete the incoming file if it needs to do a conversion
		/// </summary>
		private string ConvertToPngIfNotAlready(string incoming)
		{
			string outgoing = incoming;
			//important to dispose of these things, they lock down the file.
			using (var image = Image.FromFile(incoming))
			{
				if (!ImageFormat.Png.Equals(image.PixelFormat))
				{
					outgoing = Path.GetTempFileName();
					image.Save(outgoing, ImageFormat.Png);
				}
			}
			if (outgoing != incoming)
			{
				try
				{
					File.Delete(incoming);
				}
				catch (Exception e)
				{
					Debug.Fail(e.Message);
					//in release, just keep going
				}
			}
			return outgoing;
		}

		private enum Modes {Gallery, SingleImage}
		private void SetMode(Modes mode)
		{
			_messageLabel.Visible = false;
			switch (mode)
			{
				case Modes.Gallery:
					_pictureBox.Visible = false;
					_galleryControl.Visible= true;
					//_galleryButton.Select();
					_galleryButton.Checked = true;
					break;
				case Modes.SingleImage:
					_galleryButton.Checked = false;
					_pictureBox.Visible = true;
					_galleryControl.Visible = false;
					break;
				default:
					throw new ArgumentOutOfRangeException("mode");
			}
		}

		private void OnGalleryClick(object sender, EventArgs e)
		{
			SetMode(Modes.Gallery);
		}

		private void AcquireImageControl_Load(object sender, EventArgs e)
		{
			if(_galleryControl.HaveImageCollectionOnThisComputer)
			{
				SetMode(Modes.Gallery);
			}
			else
			{
				SetMode(Modes.SingleImage);
			}

		}
	}
}
