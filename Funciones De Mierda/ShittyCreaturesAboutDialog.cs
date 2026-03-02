using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class ShittyCreaturesAboutDialog : Dialog
	{
		private ButtonWidget m_okButton;

		public ShittyCreaturesAboutDialog()
		{
			XElement node = ContentManager.Get<XElement>("Dialogs/ShittyCreaturesAboutDialog");
			this.LoadContents(this, node);
			m_okButton = this.Children.Find<ButtonWidget>("About.OkButton", true);

			// Aplicar traducciones
			ApplyTranslations();
		}

		private void ApplyTranslations()
		{
			// Título principal
			SetLabelText("About.Title", "ShittyCreaturesAbout", "Title");

			// Descripción larga
			SetLabelText("About.Description", "ShittyCreaturesAbout", "Description");

			// Subtítulo del creador
			SetLabelText("About.CreatorTitle", "ShittyCreaturesAbout", "CreatorTitle");

			// Nombre del creador y rol
			SetLabelText("About.CreatorName", "ShittyCreaturesAbout", "CreatorName");

			// Redes sociales - título
			SetLabelText("About.SocialTitle", "ShittyCreaturesAbout", "SocialTitle");

			// Etiquetas de redes sociales (los textos fijos)
			SetLabelText("About.YoutubeLabel", "ShittyCreaturesAbout", "YoutubeLabel");
			SetLabelText("About.TwitterLabel", "ShittyCreaturesAbout", "TwitterLabel");
			SetLabelText("About.DiscordLabel", "ShittyCreaturesAbout", "DiscordLabel");
			SetLabelText("About.BiliBiliLabel", "ShittyCreaturesAbout", "BiliBiliLabel");

			// Enlace a la página del mod - etiqueta
			SetLabelText("About.ModPageLabel", "ShittyCreaturesAbout", "ModPageLabel");

			// Mensaje final
			SetLabelText("About.FinalMessage", "ShittyCreaturesAbout", "FinalMessage");

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
