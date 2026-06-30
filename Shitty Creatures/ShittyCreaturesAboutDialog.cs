using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
    public class ShittyCreaturesAboutDialog : Dialog
    {
        public LabelWidget m_titleLabel;

        public LabelWidget m_contentLabel;

        public LabelWidget m_creatorTitleLabel;

        public LabelWidget m_creatorNameLabel;

        public LabelWidget m_socialTitleLabel;

        public LabelWidget m_youtubeLabel;

        public LabelWidget m_twitterLabel;

        public LabelWidget m_discordLabel;

        public LabelWidget m_biliBiliLabel;

        public LabelWidget m_modPageLabel;

        public LabelWidget m_finalMessageLabel;

        public ButtonWidget m_okButton;

        public LabelWidget m_buttonLabel;

        public ShittyCreaturesAboutDialog()
        {
            XElement node = ContentManager.Get<XElement>("Dialogs/ShittyCreaturesAboutDialog");
            this.LoadContents(this, node);
            this.m_okButton = this.Children.Find<ButtonWidget>("OkButton", true);
            this.m_buttonLabel = this.Children.Find<LabelWidget>("ButtonLabel", true);
            this.m_titleLabel = this.Children.Find<LabelWidget>("Title", true);
            this.m_contentLabel = this.Children.Find<LabelWidget>("Content", true);
            this.m_creatorTitleLabel = this.Children.Find<LabelWidget>("CreatorTitle", true);
            this.m_creatorNameLabel = this.Children.Find<LabelWidget>("CreatorName", true);
            this.m_socialTitleLabel = this.Children.Find<LabelWidget>("SocialTitle", true);
            this.m_youtubeLabel = this.Children.Find<LabelWidget>("YoutubeLabel", true);
            this.m_twitterLabel = this.Children.Find<LabelWidget>("TwitterLabel", true);
            this.m_discordLabel = this.Children.Find<LabelWidget>("DiscordLabel", true);
            this.m_biliBiliLabel = this.Children.Find<LabelWidget>("BiliBiliLabel", true);
            this.m_modPageLabel = this.Children.Find<LabelWidget>("ModPageLabel", true);
            this.m_finalMessageLabel = this.Children.Find<LabelWidget>("FinalMessage", true);
            this.m_buttonLabel.Text = LanguageControl.Ok;
            ApplyTranslations();
        }

        private void ApplyTranslations()
        {
            SetLabelText(m_titleLabel, "ShittyCreaturesAbout", "Title");
            SetLabelText(m_contentLabel, "ShittyCreaturesAbout", "Description");
            SetLabelText(m_creatorTitleLabel, "ShittyCreaturesAbout", "CreatorTitle");
            SetLabelText(m_creatorNameLabel, "ShittyCreaturesAbout", "CreatorName");
            SetLabelText(m_socialTitleLabel, "ShittyCreaturesAbout", "SocialTitle");
            SetLabelText(m_youtubeLabel, "ShittyCreaturesAbout", "YoutubeLabel");
            SetLabelText(m_twitterLabel, "ShittyCreaturesAbout", "TwitterLabel");
            SetLabelText(m_discordLabel, "ShittyCreaturesAbout", "DiscordLabel");
            SetLabelText(m_biliBiliLabel, "ShittyCreaturesAbout", "BiliBiliLabel");
            SetLabelText(m_modPageLabel, "ShittyCreaturesAbout", "ModPageLabel");
            SetLabelText(m_finalMessageLabel, "ShittyCreaturesAbout", "FinalMessage");
        }

        private void SetLabelText(LabelWidget label, string category, string key)
        {
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
            if (this.m_okButton.IsClicked)
            {
                DialogsManager.HideDialog(this);
            }
        }
    }
}
