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

			m_messageLabel.Text = ObtenerMensajeCarta();
		}

		public override void Update()
		{
			if (m_acceptButton.IsClicked)
			{
				var invasionSubsystem = m_player.Project.FindSubsystem<SubsystemBanditInvasion>(true);
				if (invasionSubsystem != null)
				{
					invasionSubsystem.AcceptWar();
				}

				m_player.ComponentGui.DisplaySmallMessage(
					"Has aceptado la guerra, pendejo. El Cartel te va a triturar al amanecer. Ya firmaste tu sentencia.",
					new Color(255, 50, 50), false, true);
				DialogsManager.HideDialog(this);
			}
			else if (m_rejectButton.IsClicked || Input.Cancel)
			{
				var invasionSubsystem = m_player.Project.FindSubsystem<SubsystemBanditInvasion>(true);
				if (invasionSubsystem != null)
				{
					invasionSubsystem.CancelWar();
				}

				m_player.ComponentGui.DisplaySmallMessage(
					"Rechazaste el desafio, cobarde. La carta seguira aqui, pero no creas que te olvidamos. El Cartel siempre vuelve.",
					new Color(200, 200, 200), false, true);
				DialogsManager.HideDialog(this);
			}
		}

		private string ObtenerMensajeCarta()
		{
			// SOLO \n simple, NINGÚN \n\n, líneas de máximo ~55 caracteres
			return
"De parte del Cartel de Los Bandidos:\n" +
"Tras terminar la noche verde nos demostraste que al menos\n" +
"pudiste sobrevivir a las noches verdes, desde las faciles\n" +
"hasta las mas dificiles.\n" +
"Sin embargo, queremos declararte la guerra como un nuevo\n" +
"reto. Queremos saber si eres capaz de otra vez sobrevivir.\n" +
"Si aceptas, manana al amanecer te vamos a dar en la madre.\n" +
"Pero si logras ganarnos la guerra, te dejaremos en paz\n" +
"para siempre. Ni un solo plomazo mas.\n" +
"Si rechazas, podras volver a leer la carta por ahora.\n" +
"Pero todos sabran que eres un pinche cobarde que se esconde\n" +
"antes de luchar de verdad.\n" +
"El Cartel no perdona. Elige con cuidado, por tu puta madre.\n" +
"Firma: El Consejo de Bandidos";
		}
	}
}
