using System;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	public class LetterWarDialog : Dialog
	{
		private readonly ComponentPlayer m_player;
		private LabelWidget m_messageLabel;
		private ButtonWidget m_acceptButton;
		private ButtonWidget m_rejectButton;

		public LetterWarDialog(ComponentPlayer player)
		{
			m_player = player;

			XElement node = ContentManager.Get<XElement>("Dialogs/LetterWarDialog");
			LoadContents(this, node);

			m_messageLabel = Children.Find<LabelWidget>("LetterWarDialog.MessageLabel", true);
			m_acceptButton = Children.Find<ButtonWidget>("LetterWarDialog.AcceptButton", true);
			m_rejectButton = Children.Find<ButtonWidget>("LetterWarDialog.RejectButton", true);

			// El mensaje se carga desde el JSON (con el formato original de líneas)
			m_messageLabel.Text = LanguageControl.GetContentWidgets("LetterWarDialog", "Message");
		}

		public override void Update()
		{
			var invasionSubsystem = m_player.Project.FindSubsystem<SubsystemBanditInvasion>(true);
			if (invasionSubsystem == null) return;

			if (m_acceptButton.IsClicked)
			{
				// Si ya se aceptó (guerra activa o completada) solo cerrar sin mensaje
				if (invasionSubsystem.IsWarAccepted || invasionSubsystem.IsWarCompleted)
				{
					DialogsManager.HideDialog(this);
					return;
				}

				// En caso contrario, aceptar la guerra
				invasionSubsystem.AcceptWar();
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("LetterWarDialog", "AcceptMessage"),
					new Color(255, 50, 50), false, true);
				DialogsManager.HideDialog(this);
			}
			else if (m_rejectButton.IsClicked || Input.Cancel)
			{
				// Si ya se rechazó, solo cerrar sin mensaje
				if (invasionSubsystem.IsWarRejected)
				{
					DialogsManager.HideDialog(this);
					return;
				}

				// Si la guerra ya está completada, no se puede rechazar (solo cerrar)
				if (invasionSubsystem.IsWarCompleted)
				{
					DialogsManager.HideDialog(this);
					return;
				}

				// En caso contrario (guerra pendiente o activa), rechazar la guerra
				invasionSubsystem.CancelWar();
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("LetterWarDialog", "RejectMessage"),
					new Color(200, 200, 200), false, true);
				DialogsManager.HideDialog(this);
			}
		}
	}
}
