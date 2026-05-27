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

			m_messageLabel.Text = LanguageControl.GetContentWidgets("LetterWarDialog", "Message");
		}

		public override void Update()
		{
			var invasionSubsystem = m_player.Project.FindSubsystem<SubsystemBanditInvasion>(true);
			if (invasionSubsystem == null) return;

			if (m_acceptButton.IsClicked)
			{
				// CASO 1: Guerra completada → Reiniciar para nueva guerra
				if (invasionSubsystem.IsWarCompleted)
				{
					invasionSubsystem.AcceptWar(); // Esto reinicia el estado internamente
					m_player.ComponentGui.DisplaySmallMessage(
						LanguageControl.GetContentWidgets("LetterWarDialog", "AcceptMessage"),
						new Color(255, 50, 50), false, true);
					DialogsManager.HideDialog(this);
					return;
				}

				// CASO 2: Guerra ya aceptada y en curso → Solo cerrar sin mensaje
				if (invasionSubsystem.IsWarAccepted)
				{
					DialogsManager.HideDialog(this);
					return;
				}

				// CASO 3: Primera vez aceptando la guerra
				invasionSubsystem.AcceptWar();
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("LetterWarDialog", "AcceptMessage"),
					new Color(255, 50, 50), false, true);
				DialogsManager.HideDialog(this);
			}
			else if (m_rejectButton.IsClicked || Input.Cancel)
			{
				// Si ya se rechazó antes, solo cerrar sin mensaje
				if (invasionSubsystem.IsWarRejected)
				{
					DialogsManager.HideDialog(this);
					return;
				}

				// Si la guerra está completada, solo cerrar (no tiene sentido rechazar algo terminado)
				if (invasionSubsystem.IsWarCompleted)
				{
					DialogsManager.HideDialog(this);
					return;
				}

				// Si la guerra fue aceptada (activa o pendiente de noche), cancelarla
				if (invasionSubsystem.IsWarAccepted)
				{
					invasionSubsystem.CancelWar();
					m_player.ComponentGui.DisplaySmallMessage(
						LanguageControl.GetContentWidgets("LetterWarDialog", "RejectMessage"),
						new Color(200, 200, 200), false, true);
					DialogsManager.HideDialog(this);
					return;
				}

				// Si no hay guerra aceptada ni rechazada (estado inicial), solo cerrar
				DialogsManager.HideDialog(this);
			}
		}
	}
}
