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
					invasion.AcceptWar();
					ShowAcceptanceMessage(invasion);
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
				ShowAcceptanceMessage(invasion);
				DialogsManager.HideDialog(this);
			});
		}

		private void ShowAcceptanceMessage(SubsystemBanditInvasion invasion)
		{
			// Verificar si la Noche Verde está activa o programada para esta noche
			SubsystemGreenNightSky greenNight = m_player.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			bool isGreenNightActive = greenNight != null && greenNight.IsGreenNightActive;
			bool isGreenNightTonight = greenNight != null && greenNight.GreenNightEnabled &&
									   greenNight.DaysSinceLastGreenNight >= greenNight.GreenNightIntervalDays;

			string messageKey;
			Color color;

			if (isGreenNightActive)
			{
				// Noche Verde activa ahora mismo → mensaje normal de aceptación
				messageKey = "AcceptMessage";
				color = new Color(255, 50, 50);
			}
			else if (isGreenNightTonight)
			{
				// Noche Verde programada para esta noche → NUEVO MENSAJE
				messageKey = "AcceptBeforeGreenNightMessage";
				color = new Color(255, 200, 0);
			}
			else
			{
				// Día normal sin Noche Verde próxima → mensaje normal
				messageKey = "AcceptMessage";
				color = new Color(255, 50, 50);
			}

			string message = LanguageControl.GetContentWidgets("LetterWarDialog", messageKey);
			if (string.IsNullOrEmpty(message))
			{
				// Fallback si no existe la clave
				message = LanguageControl.GetContentWidgets("LetterWarDialog", "AcceptMessage");
			}

			m_player.ComponentGui.DisplaySmallMessage(message, color, false, true);
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

			// Si la guerra está completada, solo cerrar
			if (invasion.IsWarCompleted)
			{
				DialogsManager.HideDialog(this);
				return;
			}

			// Si la guerra fue aceptada, cancelarla
			if (invasion.IsWarAccepted)
			{
				invasion.CancelWar();
				m_player.ComponentGui.DisplaySmallMessage(
					LanguageControl.GetContentWidgets("LetterWarDialog", "RejectMessage"),
					new Color(200, 200, 200), false, true);
				DialogsManager.HideDialog(this);
				return;
			}

			// Estado inicial: PRIMER rechazo
			invasion.CancelWar();
			m_player.ComponentGui.DisplaySmallMessage(
				LanguageControl.GetContentWidgets("LetterWarDialog", "RejectMessage"),
				new Color(200, 200, 200), false, true);
			DialogsManager.HideDialog(this);
		}
	}
}
