using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class ShittyCreaturesLogDialog : Dialog
	{
		private ButtonWidget m_okButton;

		public ShittyCreaturesLogDialog()
		{
			XElement node = ContentManager.Get<XElement>("Dialogs/ShittyCreaturesLogDialog");
			this.LoadContents(this, node);
			m_okButton = this.Children.Find<ButtonWidget>("ShittyLog.OkButton", true);

			// Aplicar traducciones en tiempo de ejecución
			ApplyTranslations();
		}

		private void ApplyTranslations()
		{
			// Título
			SetLabelText("ShittyLog.Title", "ShittyCreaturesLog", "Title");

			// Versiones
			string[] versions = { "1_0_5", "1_0_4", "1_0_3", "1_0_2", "1_0_1", "1_0_0" };
			foreach (string version in versions)
			{
				SetLabelText($"Version{version}", "ShittyCreaturesLog", $"Version{version}");
			}

			// Líneas 1.0.5 (9 líneas)
			for (int i = 1; i <= 9; i++)
				SetLabelText($"Line1_0_5_{i}", "ShittyCreaturesLog", $"Line1_0_5_{i}");

			// Líneas 1.0.4 (4 líneas)
			for (int i = 1; i <= 4; i++)
				SetLabelText($"Line1_0_4_{i}", "ShittyCreaturesLog", $"Line1_0_4_{i}");

			// Líneas 1.0.3 (2 líneas)
			for (int i = 1; i <= 2; i++)
				SetLabelText($"Line1_0_3_{i}", "ShittyCreaturesLog", $"Line1_0_3_{i}");

			// Líneas 1.0.2 (6 líneas)
			for (int i = 1; i <= 6; i++)
				SetLabelText($"Line1_0_2_{i}", "ShittyCreaturesLog", $"Line1_0_2_{i}");

			// Líneas 1.0.1 (4 líneas)
			for (int i = 1; i <= 4; i++)
				SetLabelText($"Line1_0_1_{i}", "ShittyCreaturesLog", $"Line1_0_1_{i}");

			// Líneas 1.0.0 (3 líneas)
			for (int i = 1; i <= 3; i++)
				SetLabelText($"Line1_0_0_{i}", "ShittyCreaturesLog", $"Line1_0_0_{i}");

			// Botón OK (usando traducción estándar)
			if (m_okButton != null)
				m_okButton.Text = LanguageControl.Ok;
		}

		private void SetLabelText(string widgetName, string category, string key)
		{
			LabelWidget label = this.Children.Find<LabelWidget>(widgetName, false);
			if (label != null)
			{
				string translation = LanguageControl.Get(new string[] { category, key });
				if (!string.IsNullOrEmpty(translation) && translation != $"{category}:{key}")
				{
					label.Text = translation;
				}
			}
		}

		public override void Update()
		{
			if (m_okButton.IsClicked || base.Input.Back || base.Input.Cancel)
			{
				DialogsManager.HideDialog(this);
			}
		}
	}
}
