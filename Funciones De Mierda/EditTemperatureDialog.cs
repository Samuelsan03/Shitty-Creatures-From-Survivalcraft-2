using System;
using System.Xml.Linq;
using Game;

namespace Game
{
	public class EditTemperatureDialog : Dialog
	{
		public EditTemperatureDialog(int temperature, Action<int> handler)
		{
			XElement node = ContentManager.Get<XElement>("Dialogs/EditTemperatureDialog");
			this.LoadContents(this, node);

			this.m_okButton = this.Children.Find<ButtonWidget>("EditTemperatureDialog.OK", true);
			this.m_cancelButton = this.Children.Find<ButtonWidget>("EditTemperatureDialog.Cancel", true);
			this.m_temperatureSlider = this.Children.Find<SliderWidget>("EditTemperatureDialog.TemperatureSlider", true);

			this.m_okButton.Text = LanguageControl.Get("EditTemperatureDialog", "OK") ?? "OK";
			this.m_cancelButton.Text = LanguageControl.Get("EditTemperatureDialog", "Cancel") ?? "Cancel";

			this.m_handler = handler;
			this.m_temperature = temperature;
			this.UpdateControls();
		}

		public override void Update()
		{
			if (this.m_temperatureSlider.IsSliding)
			{
				this.m_temperature = (int)this.m_temperatureSlider.Value;
			}
			if (this.m_okButton.IsClicked)
			{
				this.Dismiss(new int?(this.m_temperature));
			}
			if (base.Input.Cancel || this.m_cancelButton.IsClicked)
			{
				this.Dismiss(null);
			}
			this.UpdateControls();
		}

		public void UpdateControls()
		{
			string statusText;
			if (this.m_temperature == 0)
			{
				statusText = LanguageControl.Get("EditTemperatureDialog", "Off") ?? "Off";
			}
			else
			{
				statusText = LanguageControl.Get("EditTemperatureDialog", "CoverageRadius") ?? "Coverage radius";
			}

			this.m_temperatureSlider.Text = string.Format("{0} ({1})",
				this.m_temperature,
				statusText);

			this.m_temperatureSlider.Value = (float)this.m_temperature;
		}

		public void Dismiss(int? result)
		{
			DialogsManager.HideDialog(this);
			if (this.m_handler != null && result != null)
			{
				this.m_handler(result.Value);
			}
		}

		public Action<int> m_handler;
		public ButtonWidget m_okButton;
		public ButtonWidget m_cancelButton;
		public SliderWidget m_temperatureSlider;
		public int m_temperature;
	}
}
