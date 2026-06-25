using System;
using Engine;
using Game;

namespace Game
{
	public class ExtremeCompletionDialog : Dialog
	{
		private SubsystemGreenNightSky m_greenNightSky;
		private ComponentPlayer m_player;
		private Action m_onAccept;
		private bool m_isClosing;
		private bool m_showRejectMessage;

		private BevelledButtonWidget m_acceptButton;
		private BevelledButtonWidget m_rejectButton;

		public ExtremeCompletionDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player, Action onAccept, bool showRejectMessage = true)
		{
			m_greenNightSky = greenNightSky;
			m_player = player;
			m_onAccept = onAccept;
			m_showRejectMessage = showRejectMessage;

			// Contenedor principal
			CanvasWidget mainContainer = new CanvasWidget
			{
				Size = new Vector2(660f, 520f),
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center
			};

			// Fondo oscuro con borde blanco (estilo DialogArea)
			BevelledRectangleWidget background = new BevelledRectangleWidget
			{
				Size = mainContainer.Size,
				BevelColor = Color.White,          // Borde blanco
				CenterColor = new Color(0, 0, 0, 230), // Fondo negro semi-transparente
				RoundingRadius = 14f,
				BevelSize = 3f,
				ShadowColor = new Color(0, 0, 0, 120),
				ShadowSize = 4f
			};
			mainContainer.Children.Add(background);

			// ScrollPanel para el contenido (deja espacio para los botones)
			ScrollPanelWidget scrollPanel = new ScrollPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				HorizontalAlignment = WidgetAlignment.Stretch,
				VerticalAlignment = WidgetAlignment.Stretch,
				MarginLeft = 20f,
				MarginTop = 20f,
				MarginRight = 20f,
				MarginBottom = 95f,
				ClampToBounds = true,
				ScrollSpeed = 0f
			};
			mainContainer.Children.Add(scrollPanel);

			// StackPanel vertical para los textos
			StackPanelWidget stackPanel = new StackPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Near,
				MarginLeft = 10f,
				MarginTop = 5f,
				MarginRight = 10f,
				MarginBottom = 5f
			};
			scrollPanel.Children.Add(stackPanel);

			// Título
			LabelWidget titleLabel = new LabelWidget
			{
				Text = "¡Felicidades, Superviviente!",
				FontScale = 1.4f,
				Color = new Color(255, 215, 0),
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 10f),
				DropShadow = true
			};
			stackPanel.Children.Add(titleLabel);

			// Subtítulo
			LabelWidget subTitleLabel = new LabelWidget
			{
				Text = "Has logrado superar la dificultad más alta conocida\n hasta ahora.",
				FontScale = 0.9f,
				Color = new Color(200, 200, 200),
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 8f),
				DropShadow = true
			};
			stackPanel.Children.Add(subTitleLabel);

			// Texto de felicitación
			LabelWidget congratsText = new LabelWidget
			{
				Text = "Has demostrado ser un superviviente excepcional.\nTu esfuerzo y perseverancia te han llevado hasta aquí.",
				FontScale = 0.85f,
				Color = Color.White,
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 15f),
				DropShadow = true
			};
			stackPanel.Children.Add(congratsText);

			// Línea separadora
			RectangleWidget separatorLine = new RectangleWidget
			{
				Size = new Vector2(500f, 2f),
				FillColor = new Color(255, 215, 0, 100),
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 15f)
			};
			stackPanel.Children.Add(separatorLine);

			// Título del nuevo desafío
			LabelWidget challengeTitle = new LabelWidget
			{
				Text = "Un Nuevo Desafío te Espera",
				FontScale = 1.1f,
				Color = new Color(255, 200, 50),
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 15f),
				DropShadow = true
			};
			stackPanel.Children.Add(challengeTitle);

			// Descripción del desafío (texto largo)
			LabelWidget challengeDesc = new LabelWidget
			{
				Text = "Has desbloqueado un reto que pondrá a prueba todo lo que has\n aprendido.\n" +
					   "Aquellos que te han visto sobrevivir te consideran una leyenda,\n" +
					   "pero hay quienes dicen que aún no has visto la verdadera oscuridad.\n\n" +
					   "Dicen que hay un nivel donde el más mínimo error tiene\n consecuencias\n" +
					   "definitivas. Donde cada decisión cuenta y cada movimiento\n puede ser el último.\n\n" +
					   "Si crees que estás preparado, acepta el desafío.\n" +
					   "Pero tenlo claro: no hay vuelta atrás.",
				FontScale = 0.78f,
				Color = new Color(220, 220, 220),
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 10f),
				DropShadow = true
			};
			stackPanel.Children.Add(challengeDesc);

			// Advertencia sobre reinicio de olas
			LabelWidget warningText = new LabelWidget
			{
				Text = "Al aceptar, las oleadas de la noche verde se reiniciarán desde el\n principio\n" +
					   "y deberás volver a demostrar tu valía.",
				FontScale = 0.8f,
				Color = new Color(255, 180, 80),
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 20f),
				DropShadow = true
			};
			stackPanel.Children.Add(warningText);

			// ---------- CONTENEDOR PARA LOS BOTONES (fuera del scroll) ----------
			StackPanelWidget buttonContainer = new StackPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Far,
				MarginBottom = 25f
			};
			mainContainer.Children.Add(buttonContainer);

			// Botón Aceptar
			m_acceptButton = new BevelledButtonWidget
			{
				Text = "Aceptar",
				Size = new Vector2(200f, 50f),
				BevelColor = new Color(0, 160, 0),
				CenterColor = new Color(0, 160, 0),
				FontScale = 0.85f,
				Color = Color.White,
				Margin = new Vector2(15f, 0f)
			};
			buttonContainer.Children.Add(m_acceptButton);

			// Botón Rechazar
			m_rejectButton = new BevelledButtonWidget
			{
				Text = "Rechazar",
				Size = new Vector2(200f, 50f),
				BevelColor = new Color(120, 40, 40),
				CenterColor = new Color(120, 40, 40),
				FontScale = 0.85f,
				Color = Color.White,
				Margin = new Vector2(15f, 0f)
			};
			buttonContainer.Children.Add(m_rejectButton);

			this.Children.Add(mainContainer);
		}

		public override void Update()
		{
			if (m_isClosing) return;

			if (m_acceptButton.IsClicked)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Accept();
			}
			else if (m_rejectButton.IsClicked)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Reject();
			}

			base.Update();
		}

		private void Accept()
		{
			if (m_isClosing) return;
			m_isClosing = true;

			m_onAccept?.Invoke();

			DialogsManager.HideDialog(this);
		}

		private void Reject()
		{
			if (m_isClosing) return;
			m_isClosing = true;

			if (m_showRejectMessage && m_player != null && m_player.ComponentGui != null)
			{
				m_player.ComponentGui.DisplaySmallMessage(
					"Has rechazado el desafío. El modo permanece bloqueado.",
					new Color(200, 200, 200),
					false,
					true
				);
			}

			DialogsManager.HideDialog(this);
		}
	}
}
