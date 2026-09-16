using System;
using System.Xml.Linq;
using Engine;
using Engine.Input;
using Game;

namespace Game
{
	/// <summary>
	/// Respuesta del jugador al desafío de Infinite.
	/// </summary>
	public enum InfiniteChallengeResponse
	{
		Reject = 0,
		Accept = 1,
		Skip = 2
	}

	public class InfiniteChallengeWidget : CanvasWidget
	{
		public const string fName = "InfiniteChallengeWidget";

		private ComponentPlayer m_componentPlayer;
		private ButtonWidget m_acceptButton;
		private ButtonWidget m_rejectButton;
		private ButtonWidget m_skipButton;
		private Action<InfiniteChallengeResponse> m_onComplete;
		private bool m_completed;
		private bool m_skipAvailable;

		// Labels que necesitan actualización dinámica
		private LabelWidget m_titleLabel;
		private LabelWidget m_subtitleLabel;
		private LabelWidget m_line1Label;
		private LabelWidget m_line2Label;
		private LabelWidget m_line3Label;
		private LabelWidget m_line4Label;
		private LabelWidget m_questionLabel;
		private LabelWidget m_acceptLabel;
		private LabelWidget m_rejectLabel;
		private LabelWidget m_skipLabel;

		public InfiniteChallengeWidget(ComponentPlayer componentPlayer, bool skipAvailable, Action<InfiniteChallengeResponse> onComplete)
		{
			m_componentPlayer = componentPlayer;
			m_onComplete = onComplete;
			m_skipAvailable = skipAvailable;

			XElement node = ContentManager.Get<XElement>("Widgets/InfiniteChallengeWidget");
			LoadContents(this, node);

			// Obtener referencias a los widgets
			m_acceptButton = Children.Find<ButtonWidget>("AcceptButton", true);
			m_rejectButton = Children.Find<ButtonWidget>("RejectButton", true);
			m_skipButton = Children.Find<ButtonWidget>("SkipButton", false); // opcional
			m_titleLabel = Children.Find<LabelWidget>("TitleLabel", true);
			m_subtitleLabel = Children.Find<LabelWidget>("SubtitleLabel", true);
			m_line1Label = Children.Find<LabelWidget>("Line1Label", true);
			m_line2Label = Children.Find<LabelWidget>("Line2Label", true);
			m_line3Label = Children.Find<LabelWidget>("Line3Label", true);
			m_line4Label = Children.Find<LabelWidget>("Line4Label", true);
			m_questionLabel = Children.Find<LabelWidget>("QuestionLabel", true);
			m_acceptLabel = Children.Find<LabelWidget>("AcceptLabel", true);
			m_rejectLabel = Children.Find<LabelWidget>("RejectLabel", true);
			m_skipLabel = Children.Find<LabelWidget>("SkipLabel", false);

			// Si no existe el botón Skip en el XML, lo ocultamos silenciosamente
			if (m_skipButton != null)
				m_skipButton.IsVisible = skipAvailable;
			if (m_skipLabel != null && !skipAvailable)
				m_skipLabel.IsVisible = false;

			// Aplicar textos localizados
			UpdateTexts();
		}

		private void UpdateTexts()
		{
			m_titleLabel.Text = LanguageControl.Get(fName, 0);
			m_subtitleLabel.Text = LanguageControl.Get(fName, 1);
			m_line1Label.Text = LanguageControl.Get(fName, 2);
			m_line2Label.Text = LanguageControl.Get(fName, 3);
			m_line3Label.Text = LanguageControl.Get(fName, 4);
			m_line4Label.Text = LanguageControl.Get(fName, 5);
			m_acceptLabel.Text = LanguageControl.Get(fName, 7);
			m_rejectLabel.Text = LanguageControl.Get(fName, 8);

			if (m_skipAvailable)
			{
				// Texto alternativo: ya lo derrotaste antes, explicar y ofrecer saltar
				m_questionLabel.Text = LanguageControl.Get(fName, 9);
				if (m_skipLabel != null)
					m_skipLabel.Text = LanguageControl.Get(fName, 10);
			}
			else
			{
				// Texto original
				m_questionLabel.Text = LanguageControl.Get(fName, 6);
			}
		}

		public override void Update()
		{
			if (m_completed) return;

			if (Input.IsKeyDown(Key.Escape))
			{
				Close(InfiniteChallengeResponse.Reject);
				return;
			}

			if (m_acceptButton.IsClicked)
			{
				Close(InfiniteChallengeResponse.Accept);
			}
			else if (m_rejectButton.IsClicked)
			{
				Close(InfiniteChallengeResponse.Reject);
			}
			else if (m_skipAvailable && m_skipButton != null && m_skipButton.IsClicked)
			{
				Close(InfiniteChallengeResponse.Skip);
			}
		}

		private void Close(InfiniteChallengeResponse result)
		{
			if (m_completed) return;
			m_completed = true;

			Action<InfiniteChallengeResponse> callback = m_onComplete;
			m_onComplete = null;

			if (m_componentPlayer?.ComponentGui != null &&
				m_componentPlayer.ComponentGui.ModalPanelWidget == this)
			{
				m_componentPlayer.ComponentGui.ModalPanelWidget = null;
			}

			callback?.Invoke(result);
		}

		public static void Show(ComponentPlayer player, bool skipAvailable, Action<InfiniteChallengeResponse> onComplete)
		{
			if (player?.ComponentGui == null) return;

			if (player.ComponentGui.ModalPanelWidget is InfiniteChallengeWidget existing)
			{
				existing.m_completed = true;
				existing.m_onComplete = null;
			}

			player.ComponentGui.ModalPanelWidget = new InfiniteChallengeWidget(player, skipAvailable, onComplete);
		}
	}
}
