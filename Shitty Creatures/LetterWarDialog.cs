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
				HandleAccept(invasionSubsystem);
			}
			else if (m_rejectButton.IsClicked || Input.Cancel)
			{
				HandleReject(invasionSubsystem);
			}
		}

		private void HandleAccept(SubsystemBanditInvasion invasion)
		{
			// CASO 1: Guerra completada → reiniciar (con verificación de Noche Verde)
			if (invasion.IsWarCompleted)
			{
				AcceptWithGreenNightCheck(invasion, () =>
				{
					// La guerra se reinicia y se acepta
					invasion.AcceptWar();
					m_player.ComponentGui.DisplaySmallMessage(
						LanguageControl.GetContentWidgets("LetterWarDialog", "AcceptMessage"),
						new Color(255, 50, 50), false, true);
					DialogsManager.HideDialog(this);
				});
				return;
			}

			// CASO 2: Guerra ya aceptada y en curso → solo cerrar
			if (invasion.IsWarAccepted)
			{
				DialogsManager.HideDialog(this);
				return;
			}

			// CASO 3: Primera vez aceptando la guerra
			AcceptWithGreenNightCheck(invasion, () =>
			{
				invasion.AcceptWar();
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("LetterWarDialog", "AcceptMessage"),
					new Color(255, 50, 50), false, true);
				DialogsManager.HideDialog(this);
			});
		}

		private void AcceptWithGreenNightCheck(SubsystemBanditInvasion invasion, Action onAccept)
		{
			SubsystemGreenNightSky greenNight = m_player.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			bool willBeGreenNight = false;
			if (greenNight != null && greenNight.GreenNightEnabled)
			{
				willBeGreenNight = greenNight.IsGreenNightActive ||
								   greenNight.DaysSinceLastGreenNight >= greenNight.GreenNightIntervalDays;
			}

			if (willBeGreenNight)
			{
				// Mostrar advertencia y ejecutar onAccept solo si el jugador confirma
				var warningDialog = new GreenNightWarConflictDialog(m_player, onAccept);
				DialogsManager.ShowDialog(m_player.GuiWidget, warningDialog);
			}
			else
			{
				// Sin Noche Verde, ejecutar directamente
				onAccept?.Invoke();
			}
		}

		private void HandleReject(SubsystemBanditInvasion invasion)
		{
			// Si ya se rechazó antes, solo cerrar
			if (invasion.IsWarRejected)
			{
				DialogsManager.HideDialog(this);
				return;
			}

			// Si la guerra está completada, solo cerrar (no tiene sentido rechazar algo terminado)
			if (invasion.IsWarCompleted)
			{
				DialogsManager.HideDialog(this);
				return;
			}

			// Si la guerra fue aceptada (activa o pendiente de noche), cancelarla
			if (invasion.IsWarAccepted)
			{
				invasion.CancelWar();
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("LetterWarDialog", "RejectMessage"),
					new Color(200, 200, 200), false, true);
				DialogsManager.HideDialog(this);
				return;
			}

			// Estado inicial: PRIMER rechazo → mostrar RejectMessage y marcar como rechazada
			invasion.CancelWar();
			m_player.ComponentGui.DisplaySmallMessage(
				LanguageControl.GetContentWidgets("LetterWarDialog", "RejectMessage"),
				new Color(200, 200, 200), false, true);
			DialogsManager.HideDialog(this);
		}
	}
}
